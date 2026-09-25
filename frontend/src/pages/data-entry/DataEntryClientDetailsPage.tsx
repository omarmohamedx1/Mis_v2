import { Pencil, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { useToast } from '../../components/common/Toast';
import { useAuth } from '../../context/AuthContext';
import { DataEntryDocumentsPanel } from '../../features/data-entry/DataEntryDocumentsPanel';
import { EditClientSheetModal } from '../../features/data-entry/EditClientSheetModal';
import { useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { readNationalIdCard } from '../../features/data-entry/nationalIdCard';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryClientDetails, DataEntryDocument } from '../../features/data-entry/types/dataEntry';
import { getApiErrorMessage } from '../../services/apiClient';

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
  const [editing, setEditing] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const canUpload = Boolean(user?.roles.some((role) => ['Admin', 'DataEntry'].includes(role)) || user?.permissions.includes('data_entry.manage') || user?.permissions.includes('data_entry.access'));

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
  const column = (...names: string[]) => {
    const fields = data.fields ?? {};
    for (const name of names) {
      const hit = Object.entries(fields).find(([key]) => key.trim().toLowerCase() === name.trim().toLowerCase());
      if (hit?.[1]?.trim()) return hit[1];
    }
    return '';
  };
  const rawPhones = column('Tell', 'Tel', 'Telephone', 'Mobile', 'Phone') || (data.phones ?? []).filter(Boolean).join(' / ');
  const phones = rawPhones.split(/[/|،,;\n\r]+/).map((phone) => phone.trim()).filter(Boolean);
  const shownPhones = phones.length
    ? phones
    : [data.mobileNumber, data.alternateMobile].filter((value): value is string => Boolean(value?.trim()));
  const known = new Set(['id', 'name', 'tell', 'tel', 'telephone', 'mobile', 'phone', 'all address', 'address', 'feedback', 'data', 'notes']);
  const extraFields = Object.entries(data.fields ?? {}).filter(([key, value]) => value?.trim() && !known.has(key.trim().toLowerCase()));

  return (
    <div className="space-y-5">
      <PageHeader
        breadcrumbs={
          <Link className="text-sm font-semibold text-mis-primary hover:underline" to="/data-entry/clients">
            {d.text('← العملاء', '← Clients')}
          </Link>
        }
        title={displayName}
        description={data.nationalId ? <span data-bidi="ltr">{data.nationalId}</span> : d.text('بيانات الحالة كما رُفعت', 'Case data as uploaded')}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button fullWidth={false} leftIcon={<Pencil className="h-4 w-4" />} variant="outline" onClick={() => setEditing(true)}>{d.text('تعديل', 'Edit')}</Button>
            <Button fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} variant="danger" onClick={() => setDeleteOpen(true)}>{d.text('حذف العميل', 'Delete client')}</Button>
          </div>
        }
      />

      <div className="space-y-4">
        <section className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
          <h2 className="text-base font-bold text-mis-navy">{d.text('التليفونات', 'Phones')}</h2>
          <div className="mt-3 flex flex-wrap gap-2">
            {shownPhones.map((number) => (
              <span className="rounded-full bg-slate-100 px-3 py-1 font-mono text-sm text-mis-navy" data-bidi="ltr" key={number}>{number}</span>
            ))}
            {!shownPhones.length ? <span className="text-sm text-slate-400">—</span> : null}
          </div>
        </section>
        <DetailSection
          title={d.text('بيانات الملف', 'File data')}
          rows={[
            [d.text('الرقم القومي', 'National ID'), column('ID', 'National ID') || data.nationalId || '—'],
            [d.text('الاسم', 'Name'), displayName],
            [d.text('العنوان', 'Address'), column('All Address', 'Address') || data.address || '—'],
            [d.text('فيدباك', 'Feedback'), column('FEEDBACK', 'Feedback') || data.feedback || '—'],
          ]}
        />
        <NationalIdSection arabic={d.ar} id={column('ID', 'National ID') || data.nationalId} text={d.text} />
        {extraFields.length ? (
          <DetailSection
            title={d.text('أعمدة إضافية', 'Extra columns')}
            rows={extraFields.map(([label, value]) => [label, value])}
          />
        ) : null}
        <DataEntryDocumentsPanel
          canUpload={canUpload}
          documents={documents}
          canDownload
          emptyHint={d.text('ارفع شهادة ميلاد أو أي ورق، واكتب الملف عبارة عن إيه. الملف يفتح هنا بعد الرفع.', 'Upload a birth certificate or any paper, and write what the file is. It opens here after upload.')}
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
              toast.success(d.text('تم رفع الملف ويمكن فتحه الآن.', 'File uploaded and ready to open.'));
            } finally {
              setUploading(false);
            }
          } : undefined}
        />
      </div>
      <ConfirmDialog
        confirmLabel={d.text('حذف', 'Delete')}
        isConfirming={deleting}
        message={d.text('حذف هذا العميل من القائمة؟', 'Remove this client from the list?')}
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
      {editing ? (
        <EditClientSheetModal
          client={{
            id: data.id,
            customerName: displayName,
            nationalId: column('ID', 'National ID') || data.nationalId,
            phones: rawPhones,
            address: column('All Address', 'Address') || data.address,
            feedback: column('FEEDBACK', 'Feedback') || data.feedback,
            data: column('Data', 'Notes') || data.notes,
            fields: data.fields,
          }}
          onClose={() => setEditing(false)}
          onSaved={() => {
            setEditing(false);
            dataEntryService.client(id).then(setData).catch(() => setError(true));
            toast.success(d.text('تم حفظ التعديل.', 'Changes saved.'));
          }}
        />
      ) : null}
    </div>
  );
}

function NationalIdSection({ id, arabic, text }: { id?: string | null; arabic: boolean; text: (arabic: string, english: string) => string }) {
  const card = readNationalIdCard(id);
  if (!card) return null;
  const birth = new Intl.DateTimeFormat(arabic ? 'ar-EG' : 'en-GB', { day: '2-digit', month: 'long', year: 'numeric' }).format(card.birthDate);
  return (
    <DetailSection
      title={text('من الرقم القومي', 'From the national ID')}
      rows={[
        [text('تاريخ الميلاد', 'Date of birth'), birth],
        [text('النوع', 'Gender'), card.gender === 'male' ? text('ذكر', 'Male') : text('أنثى', 'Female')],
        [text('السن', 'Age'), String(card.age)],
        [text('المحافظة', 'Governorate'), arabic ? card.governorateAr : card.governorateEn],
      ]}
    />
  );
}
