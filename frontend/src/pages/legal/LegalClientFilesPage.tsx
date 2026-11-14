import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { ArrowLeft, Download, FileText, Search, Trash2, Upload, UserRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { useToast } from '../../components/common/Toast';
import { useLegalText } from '../../features/legal/legalUi';
import { legalService } from '../../features/legal/services/legalService';
import type { LegalClientDetails, LegalClientListItem } from '../../features/legal/types/legal';

export function LegalClientFilesPage() {
  const l = useLegalText();
  const toast = useToast();
  const { customerId } = useParams();
  const [search, setSearch] = useState('');
  const [clients, setClients] = useState<LegalClientListItem[]>([]);
  const [details, setDetails] = useState<LegalClientDetails>();
  const [notes, setNotes] = useState('');
  const [status, setStatus] = useState('ACTIVE');
  const [loading, setLoading] = useState(true);
  const [detailError, setDetailError] = useState(false);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [deletingId, setDeletingId] = useState<string>();
  const [deleting, setDeleting] = useState(false);
  const [detailReload, setDetailReload] = useState(0);
  const [clientsLoading, setClientsLoading] = useState(true);

  useEffect(() => {
    let active = true;
    setClientsLoading(true);
    legalService.clients({ search: search.trim() || undefined, page: 1, pageSize: 100 })
      .then((result) => { if (active) setClients(result.items); })
      .catch(() => { if (active) toast.error(l.text('تعذر تحميل قائمة العملاء.', 'Could not load the client list.')); })
      .finally(() => { if (active) setClientsLoading(false); });
    return () => { active = false; };
  }, [search, l.ar]);

  useEffect(() => {
    if (!customerId) {
      setDetails(undefined);
      setLoading(false);
      return;
    }
    let active = true;
    setLoading(true);
    setDetailError(false);
    legalService.client(customerId)
      .then((client) => {
        if (!active) return;
        setDetails(client);
        setNotes(client.notes ?? '');
        setStatus(client.client.status);
      })
      .catch(() => { if (active) setDetailError(true); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [customerId, detailReload]);

  async function saveClient() {
    if (!customerId) return;
    setSaving(true);
    try {
      const updated = await legalService.updateClient(customerId, { status, notes });
      setDetails(updated);
      setNotes(updated.notes ?? '');
      setStatus(updated.client.status);
      toast.success(l.text('تم حفظ بيانات الملف.', 'Client file saved.'));
    } catch {
      toast.error(l.text('تعذر حفظ بيانات الملف.', 'Could not save the client file.'));
    } finally {
      setSaving(false);
    }
  }

  async function uploadDocument(file?: File) {
    if (!customerId || !file) return;
    setUploading(true);
    try {
      const document = await legalService.uploadClientDocument(customerId, file);
      setDetails((current) => current ? { ...current, documents: [document, ...current.documents] } : current);
      toast.success(l.text('تم رفع المستند.', 'Document uploaded.'));
    } catch {
      toast.error(l.text('تعذر رفع المستند. الصيغ المدعومة PDF وJPG وPNG.', 'Could not upload the document. Supported formats are PDF, JPG, and PNG.'));
    } finally {
      setUploading(false);
    }
  }

  if (!customerId) {
    return (
      <div className="space-y-5">
        <PageHeader title={l.text('ملفات العملاء', 'Client Files')} description={l.text('اختر عميلًا لعرض ملفه ومعلوماته ومستنداته.', 'Choose a client to view their profile and documents.')} />
        <div className="relative rounded-2xl border border-mis-border bg-white p-3 shadow-sm">
          <Search className="pointer-events-none absolute top-6 h-4 w-4 text-mis-muted" style={{ insetInlineStart: '1.35rem' }} />
          <input className="h-11 w-full rounded-xl border border-mis-control bg-white pe-4 ps-10 text-sm" onChange={(event) => setSearch(event.target.value)} placeholder={l.text('ابحث باسم العميل أو رقمه القومي', 'Search by client name or National ID')} value={search} />
        </div>
        {clientsLoading ? <div className="grid min-h-32 place-items-center"><LoadingSpinner /></div> : clients.length ? (
          <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[46rem] text-sm">
                <thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr>
                  <th className="px-4 py-3 text-start">{l.text('اسم العميل', 'Client Name')}</th>
                  <th className="px-4 py-3 text-start">{l.text('الرقم القومي', 'National ID')}</th>
                  <th className="px-4 py-3 text-start">{l.text('البنك', 'Bank Name')}</th>
                  <th className="px-4 py-3 text-end">{l.text('التفاصيل', 'Details')}</th>
                </tr></thead>
                <tbody className="divide-y divide-mis-border">{clients.map((client) => (
                  <tr key={client.customerId}>
                    <td className="px-4 py-3 font-semibold text-mis-navy">{client.name}</td>
                    <td className="px-4 py-3" data-bidi="ltr">{client.nationalId || '—'}</td>
                    <td className="px-4 py-3">{client.bankName}</td>
                    <td className="px-4 py-3 text-end"><Link className="font-semibold text-mis-primary hover:underline" to={`/legal/client-files/${client.customerId}`}>{l.text('فتح الملف', 'Open File')}</Link></td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          </div>
        ) : <EmptyState icon={<UserRound className="h-5 w-5" />} title={l.text('لا يوجد عملاء', 'No clients found')} description={l.text('سيظهر العملاء المحولون من التحصيل هنا تلقائيًا.', 'Clients transferred from Collections will appear here automatically.')} />}
      </div>
    );
  }

  if (loading) return <div className="grid min-h-[360px] place-items-center"><LoadingSpinner /></div>;
  if (detailError || !details) {
    return <ErrorState title={l.text('تعذر تحميل ملف العميل', 'Could not load the client file')} onRetry={() => setDetailReload((value) => value + 1)} />;
  }

  const client = details.client;
  return (
    <div className="space-y-5">
      <PageHeader
        breadcrumbs={<Link className="inline-flex items-center gap-2 text-sm font-semibold text-mis-primary hover:underline" to="/legal/client-files"><ArrowLeft className="h-4 w-4 rtl:rotate-180" />{l.text('ملفات العملاء', 'Client Files')}</Link>}
        title={client.name}
        description={l.text('ملف العميل مرتبط بسجل العميل الأصلي في النظام.', 'This profile remains linked to the original MIS customer record.')}
      />

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        <Info label={l.text('الرقم القومي', 'National ID')} value={client.nationalId} ltr />
        <Info label={l.text('رقم الهاتف', 'Mobile Number')} value={client.mobileNumber} ltr />
        <Info label={l.text('البنك', 'Bank Name')} value={client.bankName} />
        <Info label={l.text('العنوان', 'Address')} value={client.address} />
        <Info label={l.text('المحافظة', 'Governorate')} value={details.governorate} />
        <Info label={l.text('المنطقة / المدينة', 'Area / City')} value={[details.area, details.city].filter(Boolean).join(' / ')} />
        <Info label={l.text('هاتف بديل', 'Alternate Phone')} value={details.alternatePhone} ltr />
        <Info label={l.text('هاتف إضافي', 'Additional Phone')} value={details.tertiaryPhone} ltr />
        <Info label={l.text('جهة العمل', 'Employer')} value={details.employer} />
        <Info label={l.text('المسمى الوظيفي', 'Job Title')} value={details.jobTitle} />
        <Info label={l.text('كود العميل', 'Customer Code')} value={details.customerCode} ltr />
        <Info label={l.text('مصدر سجل العميل', 'Customer Record Source')} value={details.dataEntrySource} />
        <Info label={l.text('أُضيف بواسطة', 'Created By')} value={details.createdBy} />
        <Info label={l.text('أُضيف في', 'Added On')} value={l.date(details.createdAt)} />
      </section>

      {details.collectionCaseNumbers.length ? (
        <section className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
          <h2 className="font-bold text-mis-navy">{l.text('حالات التحصيل المرتبطة', 'Linked Collection Cases')}</h2>
          <div className="mt-3 flex flex-wrap gap-2">{details.collectionCaseNumbers.map((caseNumber) => <span className="rounded-lg bg-slate-100 px-3 py-1.5 font-mono text-sm" data-bidi="ltr" key={caseNumber}>{caseNumber}</span>)}</div>
        </section>
      ) : null}

      {details.importedRecords?.length ? (
        <section className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
          <h2 className="font-bold text-mis-navy">{l.text('بيانات الملف القانوني المستورد', 'Imported Legal File Data')}</h2>
          <div className="mt-4 space-y-4">
            {details.importedRecords.map((record, recordIndex) => (
              <div className="rounded-xl border border-mis-border p-4" key={recordIndex}>
                <h3 className="text-sm font-semibold text-slate-600">
                  {l.text('سجل {number}', 'Record {number}').replace('{number}', l.number(recordIndex + 1))}
                </h3>
                <dl className="mt-3 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                  {Object.entries(record).map(([field, value]) => (
                    <div className="rounded-xl bg-slate-50 p-3" key={`${recordIndex}:${field}`}>
                      <dt className="text-xs font-semibold text-slate-500">{importedFieldLabel(field, l.text)}</dt>
                      <dd className="mt-1 whitespace-pre-wrap break-words text-sm text-slate-800" data-bidi={isImportedIdentifier(field) ? 'ltr' : undefined}>{value || '—'}</dd>
                    </div>
                  ))}
                </dl>
              </div>
            ))}
          </div>
        </section>
      ) : null}

      <section className="space-y-4 rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div><h2 className="font-bold text-mis-navy">{l.text('ملف العميل', 'Client File')}</h2><p className="mt-1 text-sm text-slate-500">{l.text('الحالة وملاحظات الشؤون القانونية.', 'Legal status and notes.')}</p></div>
          <span className="rounded-full bg-slate-100 px-3 py-1 text-sm font-semibold">{l.clientStatus(client.status)}</span>
        </div>
        {details.canManage ? (
          <>
            <label className="block max-w-sm text-sm font-semibold text-slate-700">
              <span className="mb-1.5 block">{l.text('حالة العميل', 'Client Status')}</span>
              <ProfessionalSelect className="h-11 w-full rounded-xl border border-mis-control bg-white px-3" onChange={(event) => setStatus(event.target.value)} value={status}>
                <option value="ACTIVE">{l.clientStatus('ACTIVE')}</option>
                <option value="LEGAL">{l.clientStatus('LEGAL')}</option>
                <option value="ON_HOLD">{l.clientStatus('ON_HOLD')}</option>
                <option value="CLOSED">{l.clientStatus('CLOSED')}</option>
              </ProfessionalSelect>
            </label>
            <TextAreaInput label={l.text('ملاحظات الشؤون القانونية', 'Legal Notes')} onChange={(event) => setNotes(event.target.value)} value={notes} />
            <div className="flex justify-end"><Button fullWidth={false} isLoading={saving} onClick={saveClient} type="button">{l.text('حفظ الملف', 'Save Client File')}</Button></div>
            <div className="grid gap-3 md:grid-cols-2">
              <Info label={l.text('ملاحظات سجل العميل', 'Customer Record Notes')} value={details.customerNotes} />
              <Info label={l.text('ملاحظات سابقة', 'Existing Customer Feedback')} value={details.customerFeedback} />
              <Info label={l.text('العنوان الإضافي', 'Secondary Address')} value={details.secondaryAddress} />
            </div>
          </>
        ) : (
          <>
            <p className="whitespace-pre-wrap text-sm leading-6 text-slate-700">{details.notes || '—'}</p>
            <div className="grid gap-3 md:grid-cols-2">
              <Info label={l.text('ملاحظات سجل العميل', 'Customer Record Notes')} value={details.customerNotes} />
              <Info label={l.text('ملاحظات سابقة', 'Existing Customer Feedback')} value={details.customerFeedback} />
              <Info label={l.text('العنوان الإضافي', 'Secondary Address')} value={details.secondaryAddress} />
            </div>
          </>
        )}
      </section>

      <section className="space-y-4 rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div><h2 className="font-bold text-mis-navy">{l.text('المستندات', 'Documents')}</h2><p className="mt-1 text-sm text-slate-500">{l.text('المستندات مرتبطة بسجل العميل ويمكن تنزيلها أو إدارتها.', 'Documents are connected to the customer record and can be downloaded or managed.')}</p></div>
          {details.canManage ? (
            <label className="inline-flex min-h-10 cursor-pointer items-center justify-center gap-2 rounded-xl bg-mis-primary px-4 py-2 text-sm font-semibold text-white hover:bg-mis-deep">
              <Upload className="h-4 w-4" /> {uploading ? l.text('جارٍ الرفع...', 'Uploading...') : l.text('رفع مستند', 'Upload Document')}
              <input accept=".pdf,.jpg,.jpeg,.png" className="sr-only" disabled={uploading} onChange={(event) => { void uploadDocument(event.target.files?.[0]); event.currentTarget.value = ''; }} type="file" />
            </label>
          ) : null}
        </div>
        {details.documents.length ? (
          <ul className="divide-y divide-mis-border rounded-xl border border-mis-border">
            {details.documents.map((document) => (
              <li className="flex flex-col gap-3 px-4 py-3 sm:flex-row sm:items-center sm:justify-between" key={document.id}>
                <div className="flex min-w-0 items-center gap-3">
                  <FileText className="h-5 w-5 shrink-0 text-mis-primary" />
                  <div className="min-w-0"><p className="truncate font-semibold text-mis-navy">{document.originalFileName}</p><p className="text-xs text-slate-500">{l.dateTime(document.uploadedAt)} · {l.text('رفع بواسطة', 'Uploaded by')} {document.uploadedBy}</p></div>
                </div>
                <div className="flex shrink-0 gap-2">
                  <Button fullWidth={false} leftIcon={<Download />} onClick={() => legalService.downloadClientDocument(document.id).catch(() => toast.error(l.text('تعذر تنزيل المستند.', 'Could not download the document.')))} size="sm" type="button" variant="outline">{l.text('تنزيل', 'Download')}</Button>
                  {details.canManage ? <Button fullWidth={false} leftIcon={<Trash2 />} onClick={() => setDeletingId(document.id)} size="sm" type="button" variant="danger">{l.text('حذف', 'Delete')}</Button> : null}
                </div>
              </li>
            ))}
          </ul>
        ) : <EmptyState compact icon={<FileText className="h-5 w-5" />} title={l.text('لا توجد مستندات', 'No documents')} description={l.text('ارفع مستندًا لربطه بملف هذا العميل.', 'Upload a document to attach it to this client file.')} />}
      </section>

      <ConfirmDialog
        confirmLabel={l.text('حذف المستند', 'Delete Document')}
        isConfirming={deleting}
        message={l.text('سيُحذف هذا المستند من ملف العميل.', 'This document will be removed from the client file.')}
        onCancel={() => setDeletingId(undefined)}
        onConfirm={() => {
          if (!deletingId) return;
          setDeleting(true);
          void legalService.deleteClientDocument(deletingId).then(() => {
            setDetails((current) => current ? { ...current, documents: current.documents.filter((item) => item.id !== deletingId) } : current);
            setDeletingId(undefined);
            toast.success(l.text('تم حذف المستند.', 'Document deleted.'));
          }).catch(() => {
            toast.error(l.text('تعذر حذف المستند.', 'Could not delete the document.'));
          }).finally(() => setDeleting(false));
        }}
        open={Boolean(deletingId)}
        title={l.text('تأكيد الحذف', 'Confirm deletion')}
      />
    </div>
  );
}

function importedFieldLabel(field: string, text: (arabic: string, english: string) => string) {
  const labels: Record<string, [string, string]> = {
    '#': ['#', '#'],
    Branches: ['الفروع', 'Branches'],
    'ID Number': ['الرقم القومي', 'National ID'],
    'Installment Unique ID': ['الرقم الفريد للقسط', 'Installment Unique ID'],
    'Cust Full Name': ['اسم العميل الكامل', 'Customer Full Name'],
    serial: ['المسلسل', 'Serial'],
    'تاريخ المحضر': ['تاريخ المحضر', 'Report Date'],
    'رقم المحضر': ['رقم المحضر', 'Report Number'],
    'النيابة': ['النيابة', 'Prosecution'],
    'تاريخ الحكم': ['تاريخ الحكم', 'Judgment Date'],
    'الحكم': ['الحكم', 'Judgment'],
    Agency: ['الجهة', 'Agency'],
  };
  const label = labels[field];
  return label ? text(label[0], label[1]) : field;
}

function isImportedIdentifier(field: string) {
  return ['#', 'ID Number', 'Installment Unique ID', 'serial', 'رقم المحضر'].includes(field);
}

function Info({ label, value, ltr = false }: { label: string; value?: string | null; ltr?: boolean }) {
  return <article className="min-w-0 rounded-xl border border-mis-border bg-white px-4 py-3 shadow-sm"><p className="text-xs font-semibold text-slate-500">{label}</p><p className="mt-1 break-words text-sm font-medium text-mis-navy" dir={ltr ? 'ltr' : undefined}>{value || '—'}</p></article>;
}
