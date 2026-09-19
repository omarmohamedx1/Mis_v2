import { ArrowUpRight, History, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { useToast } from '../../components/common/Toast';
import { useAuth } from '../../context/AuthContext';
import { DataEntryDeskLabel } from '../../features/data-entry/DataEntryDeskFields';
import { DataEntryDocumentsPanel } from '../../features/data-entry/DataEntryDocumentsPanel';
import { DataEntryBatchStatus, DataEntryPipeline, useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryClientDetails, DataEntryDocument } from '../../features/data-entry/types/dataEntry';
import { canAccessModule } from '../../features/modules/moduleAccess';
import { getApiErrorMessage } from '../../services/apiClient';

function hasValue(value: unknown) {
  if (value === null || value === undefined) return false;
  if (typeof value === 'string') return value.trim().length > 0;
  return true;
}

function DetailSection({ title, rows }: { title: string; rows: [string, string][] }) {
  const visible = rows.filter(([, value]) => value && value !== '—');
  if (!visible.length) return null;
  return (
    <section className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
      <h2 className="text-base font-bold text-mis-navy">{title}</h2>
      <dl className="mt-4 grid gap-3 sm:grid-cols-2">
        {visible.map(([label, value]) => (
          <div className="rounded-xl bg-slate-50 p-3" key={label}>
            <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</dt>
            <dd className="mt-1 break-words text-sm font-semibold text-mis-navy">{value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

export function DataEntryClientDetailsPage() {
  const { id = '' } = useParams();
  const d = useDataEntryText();
  const navigate = useNavigate();
  const { user } = useAuth();
  const toast = useToast();
  const [data, setData] = useState<DataEntryClientDetails>();
  const [documents, setDocuments] = useState<DataEntryDocument[]>([]);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const canCollections = user ? canAccessModule(user, 'collections') : false;
  const canUpload = Boolean(user?.roles.some((role) => ['Admin', 'DataEntry'].includes(role)) || user?.permissions.includes('data_entry.manage'));
  const canReview = Boolean(user?.roles.some((role) => ['Admin', 'CollectionsSupervisor', 'CollectionsOperationsManager'].includes(role)) || user?.permissions.includes('collections.data_batch.review'));

  useEffect(() => {
    if (!id) return;
    setError(false);
    Promise.all([dataEntryService.client(id), dataEntryService.clientDocuments(id).catch(() => [] as DataEntryDocument[])])
      .then(([client, files]) => { setData(client); setDocuments(files); })
      .catch(() => setError(true));
  }, [id]);

  if (error) {
    return <ErrorState title={d.text('تعذر تحميل بيانات العميل', 'Could not load client details')} onRetry={() => location.reload()} />;
  }
  if (!data) {
    return <div className="grid min-h-[440px] place-items-center"><LoadingSpinner /></div>;
  }

  const displayName =
    (d.ar ? data.customerNameArabic : data.customerNameEnglish) ||
    data.customerNameArabic ||
    data.customerNameEnglish ||
    data.customerNumber;
  const moneyOrDash = (value?: number | null) => (hasValue(value) ? d.money(Number(value)) : '—');
  const inCollections = Boolean(data.caseId || data.caseNumber);

  return (
    <div className="space-y-5">
      <PageHeader
        breadcrumbs={
          <Link className="text-sm font-semibold text-mis-primary hover:underline" to="/data-entry/clients">
            {d.text('← العملاء', '← Clients')}
          </Link>
        }
        title={displayName}
        description={
          <span data-bidi="ltr">
            {d.text('رقم العميل', 'Customer number')}: {data.customerNumber}
          </span>
        }
        actions={
          <div className="flex flex-wrap gap-2">
            {inCollections && canCollections && data.caseId ? (
              <Button fullWidth={false} leftIcon={<ArrowUpRight className="h-4 w-4" />} onClick={() => navigate(`/collections/cases/${data.caseId}`)}>{d.text('فتح الحالة في التحصيل', 'Open case in collections')}</Button>
            ) : data.batchId ? (
              <Button fullWidth={false} leftIcon={<History className="h-4 w-4" />} variant="outline" onClick={() => navigate('/data-entry/history')}>{d.text('متابعة الدفعة', 'Follow the batch')}</Button>
            ) : null}
            {!inCollections ? (
              <Button fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} variant="danger" onClick={() => setDeleteOpen(true)}>{d.text('حذف العميل', 'Delete client')}</Button>
            ) : null}
          </div>
        }
      />

      <DataEntryPipeline current={data.batchStatus} />

      <section className={`rounded-2xl border px-5 py-4 text-sm ${data.batchStatus === 'REJECTED' ? 'border-rose-200 bg-rose-50 text-rose-800' : inCollections ? 'border-emerald-200 bg-emerald-50 text-emerald-800' : 'border-amber-200 bg-amber-50 text-amber-800'}`}>
        {data.batchStatus === 'REJECTED'
          ? d.text('الدفعة مرفوضة من التحصيل. راجع السجل ثم أعد الإدخال أو الرفع.', 'Collections rejected this batch. Check history, then enter or upload again.')
          : inCollections
            ? d.text(`العميل متاح في التحصيل${data.caseNumber ? ` — الحالة ${data.caseNumber}` : '.'}`, `This client is available in collections${data.caseNumber ? ` — case ${data.caseNumber}` : '.'}`)
            : d.text('العميل أُرسل للتحصيل وما زال بانتظار قبول المشرف لإنشاء الحالة.', 'The client was sent to collections and is waiting for supervisor acceptance to create the case.')}
      </section>

      <div className="space-y-4">
        <DetailSection
          title={d.text('أساسي', 'Basic')}
          rows={[
            [d.text('رقم العميل', 'Customer number'), data.customerNumber],
            [d.text('الاسم بالعربية', 'Arabic name'), data.customerNameArabic || '—'],
            [d.text('الاسم بالإنجليزية', 'English name'), data.customerNameEnglish || '—'],
            [d.text('الرقم القومي', 'National ID'), data.nationalId || '—'],
            [d.text('المصدر', 'Source'), data.source ? d.source(data.source) : '—'],
            [d.text('أنشئ بواسطة', 'Created by'), data.createdBy || '—'],
            [d.text('تاريخ الإنشاء', 'Created at'), d.dateTime(data.createdAt)],
          ]}
        />
        <DetailSection
          title={d.text('الاتصال', 'Contact')}
          rows={[
            [d.text('الموبايل', 'Mobile'), data.mobileNumber || '—'],
            [d.text('موبايل بديل', 'Alternate mobile'), data.alternateMobile || '—'],
          ]}
        />
        <DetailSection title={d.text('العنوان', 'Address')} rows={[[d.text('العنوان', 'Address'), data.address || '—']]} />
        <DetailSection title={d.text('فيدباك', 'Feedback')} rows={[[d.text('فيدباك', 'Feedback'), data.feedback || '—']]} />
        <DetailSection title={d.text('ملاحظات', 'Notes')} rows={[[d.text('ملاحظات', 'Notes'), data.notes || '—']]} />
        <section className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
          <h2 className="text-base font-bold text-mis-navy">{d.text('الجهة', 'Organization')}</h2>
          <dl className="mt-4 grid gap-3 sm:grid-cols-2">
            {[
              [d.text('الجهة', 'Organization'), data.organizationName],
              [d.text('نوع الجهة', 'Organization type'), data.organizationType || '—'],
              [d.text('كود المحفظة', 'Portfolio code'), data.portfolioCode || '—'],
              [d.text('المحفظة', 'Portfolio'), data.portfolioName || '—'],
            ].filter(([, value]) => value && value !== '—').map(([label, value]) => (
              <div className="rounded-xl bg-slate-50 p-3" key={label}>
                <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</dt>
                <dd className="mt-1 break-words text-sm font-semibold text-mis-navy">{value}</dd>
              </div>
            ))}
            <div className="rounded-xl bg-slate-50 p-3 sm:col-span-2">
              <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{d.text('مكتب التحصيل', 'Collections desk')}</dt>
              <dd className="mt-1 break-words text-sm font-semibold text-mis-navy">
                <DataEntryDeskLabel primary={data.primaryClassification} sub={data.subClassification} />
              </dd>
            </div>
          </dl>
        </section>
        <DetailSection
          title={d.text('الحساب / الحالة', 'Account / case')}
          rows={[
            [d.text('رقم الحالة', 'Case number'), data.caseNumber || '—'],
            [d.text('حالة الحالة', 'Case status'), data.caseStatus ? d.caseStatus(data.caseStatus) : '—'],
            [d.text('رقم الحساب', 'Account number'), data.accountNumber || '—'],
            [d.text('رقم العقد', 'Contract number'), data.contractNumber || '—'],
          ]}
        />
        {(hasValue(data.outstandingBalance) || hasValue(data.overdueBalance) || hasValue(data.daysPastDue)) && (
          <DetailSection
            title={d.text('مالي', 'Financial')}
            rows={[
              [d.text('المديونية', 'Outstanding'), moneyOrDash(data.outstandingBalance)],
              [d.text('المتأخر', 'Overdue'), moneyOrDash(data.overdueBalance)],
              [d.text('أيام التأخر', 'Days past due'), hasValue(data.daysPastDue) ? d.number(Number(data.daysPastDue)) : '—'],
            ]}
          />
        )}
        {(hasValue(data.batchNumber) || hasValue(data.batchStatus)) && (
          <section className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
            <h2 className="text-base font-bold text-mis-navy">{d.text('الدفعة المرسلة للتحصيل', 'Batch sent to collections')}</h2>
            <dl className="mt-4 grid gap-3 sm:grid-cols-2">
              {hasValue(data.batchNumber) ? (
                <div className="rounded-xl bg-slate-50 p-3">
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{d.text('رقم الدفعة', 'Batch number')}</dt>
                  <dd className="mt-1 text-sm font-semibold text-mis-navy" data-bidi="ltr">{data.batchNumber}</dd>
                </div>
              ) : null}
              {hasValue(data.batchStatus) ? (
                <div className="rounded-xl bg-slate-50 p-3">
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{d.text('حالة الدفعة', 'Batch status')}</dt>
                  <dd className="mt-1"><DataEntryBatchStatus value={data.batchStatus!} /></dd>
                </div>
              ) : null}
            </dl>
          </section>
        )}
        <DataEntryDocumentsPanel
          canDownload={canReview}
          canUpload={canUpload}
          documents={documents}
          emptyHint={d.text('ارفع عقد، كشف، أو ملف بيانات إضافي ليراجعه المشرف مع الحالة.', 'Upload a contract, statement, or extra data file for the supervisor to review with the case.')}
          uploading={uploading}
          onDelete={canUpload ? async (documentId) => {
            await dataEntryService.deleteDocument(documentId);
            setDocuments((current) => current.filter((item) => item.id !== documentId));
            toast.success(d.text('تم حذف الملف.', 'File removed.'));
          } : undefined}
          onUpload={canUpload ? async (file, note) => {
            setUploading(true);
            try {
              const created = await dataEntryService.uploadClientDocument(id, file, note);
              setDocuments((current) => [created, ...current]);
              toast.success(d.text('تم رفع الملف للمشرف.', 'File sent for supervisor review.'));
            } finally {
              setUploading(false);
            }
          } : undefined}
        />
      </div>
      <ConfirmDialog
        confirmLabel={d.text('حذف', 'Delete')}
        isConfirming={deleting}
        message={d.text('حذف هذا العميل نهائيًا؟ الحذف متاح فقط قبل إنشاء حالة التحصيل.', 'Permanently delete this client? Allowed only before a collections case is created.')}
        onCancel={() => setDeleteOpen(false)}
        onConfirm={() => {
          setDeleting(true);
          dataEntryService.deleteClient(id)
            .then(() => {
              toast.success(d.text('تم حذف العميل.', 'Client deleted.'));
              navigate('/data-entry/clients');
            })
            .catch((reason) => {
              toast.error(getApiErrorMessage(reason, d.text('تعذر حذف العميل.', 'Could not delete the client.')));
              setDeleting(false);
            });
        }}
        open={deleteOpen}
        title={d.text('حذف العميل', 'Delete client')}
      />
    </div>
  );
}
