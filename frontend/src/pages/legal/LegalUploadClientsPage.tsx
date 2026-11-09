import { AlertCircle, FileUp, Upload } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { useToast } from '../../components/common/Toast';
import { PageHeader } from '../../components/common/PageHeader';
import { useAuth } from '../../context/AuthContext';
import { useLegalText } from '../../features/legal/legalUi';
import { legalService } from '../../features/legal/services/legalService';
import { getApiErrorMessage } from '../../services/apiClient';
import type { LegalClientImportPreview, LegalClientImportResult, LegalOrganization } from '../../features/legal/types/legal';

export function LegalUploadClientsPage() {
  const l = useLegalText();
  const toast = useToast();
  const { user } = useAuth();
  const [preview, setPreview] = useState<LegalClientImportPreview>();
  const [file, setFile] = useState<File>();
  const [loading, setLoading] = useState(false);
  const [importing, setImporting] = useState(false);
  const [result, setResult] = useState<LegalClientImportResult>();
  const [organizations, setOrganizations] = useState<LegalOrganization[]>([]);
  const [organizationId, setOrganizationId] = useState('');
  const [organizationsLoading, setOrganizationsLoading] = useState(true);
  const [organizationsError, setOrganizationsError] = useState(false);
  const canManage = Boolean(user?.roles.some((role) => ['Admin', 'LegalOfficer'].includes(role))
    || user?.permissions.includes('legal.case.manage') || user?.permissions.includes('*'));
  const originalHeaders = Object.keys(preview?.rows.find((row) => row.source.originalFields)?.source.originalFields ?? {});

  useEffect(() => {
    let active = true;
    setOrganizationsLoading(true);
    setOrganizationsError(false);
    legalService.organizations()
      .then((items) => { if (active) setOrganizations(items); })
      .catch(() => {
        if (!active) return;
        setOrganizationsError(true);
        toast.error(l.text('تعذر تحميل جهات النظام. أعد المحاولة قبل رفع الملف.', 'Could not load MIS organizations. Retry before uploading.'));
      })
      .finally(() => { if (active) setOrganizationsLoading(false); });
    return () => { active = false; };
  }, [l.ar]);

  async function selectFile(nextFile?: File) {
    setFile(nextFile);
    setPreview(undefined);
    setResult(undefined);
    if (!nextFile) return;
    if (!organizationId) {
      toast.error(l.text('اختر جهة النظام المرتبط بها سجل العميل قبل معاينة الملف.', 'Select the MIS organization for the customer record before previewing.'));
      return;
    }
    setLoading(true);
    try {
      setPreview(await legalService.previewClientImport(nextFile, organizationId));
    } catch (error) {
      toast.error(getApiErrorMessage(
        error,
        l.text('تعذر قراءة الملف. تحقق من نوع الملف وعناوين الأعمدة.', 'Could not read the file. Check its format and column headers.'),
      ));
    } finally {
      setLoading(false);
    }
  }

  async function importClients() {
    if (!file || !preview) return;
    setImporting(true);
    try {
      const imported = await legalService.importClients(preview.fileName, organizationId, preview.rows.map((row) => row.source));
      setResult(imported);
      if (imported.successfulRows > 0) {
        toast.success(l.text('تم حفظ العملاء المستوردين بنجاح.', 'Imported clients were saved successfully.'));
      } else {
        toast.error(l.text('لم يتم حفظ أي صف في قاعدة البيانات.', 'No rows were saved to the database.'));
      }
    } catch {
      toast.error(l.text('تعذر استيراد العملاء. أعد معاينة الملف وحاول مرة أخرى.', 'Could not import clients. Preview the file again and retry.'));
    } finally {
      setImporting(false);
    }
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title={l.text('رفع العملاء', 'Upload Clients')}
        description={l.text('ارفع ملف Excel لمعاينته والتحقق من بياناته قبل إضافته إلى الشؤون القانونية.', 'Upload an Excel workbook to preview and validate client data before adding it to Legal.')}
      />
      {!canManage ? (
        <EmptyState
          icon={<AlertCircle className="h-5 w-5" />}
          title={l.text('لا تملك صلاحية الاستيراد', 'Import permission required')}
          description={l.text('تواصل مع مسؤول النظام لمنحك صلاحية إدارة العملاء القانونيين.', 'Contact an administrator to request Legal client management access.')}
        />
      ) : (
        <>
          <section className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
            <label className="mb-4 block max-w-xl text-sm font-semibold text-slate-700">
              <span className="mb-1.5 block">{l.text('جهة سجل العميل في النظام', 'MIS organization for the customer record')}</span>
              <select
                className="h-11 w-full rounded-xl border border-mis-control bg-white px-3"
                disabled={organizationsLoading || organizationsError}
                onChange={(event) => {
                  setOrganizationId(event.target.value);
                  setPreview(undefined);
                  setResult(undefined);
                }}
                value={organizationId}
              >
                <option value="">{l.text('اختر جهة النظام', 'Select an MIS organization')}</option>
                {organizations.map((organization) => (
                  <option key={organization.id} value={organization.id}>{organization.name} ({organization.code})</option>
                ))}
              </select>
              <span className="mt-1.5 block font-normal text-slate-500">
                {l.text(
                  'هذا الاختيار مطلوب لربط سجل العميل الأصلي. لن تُستخدم الفروع أو الوكالة لتحديد البنك أو الجهة.',
                  'This selection links the original customer record. Branches and Agency workbook values are not used to infer an organization.',
                )}
              </span>
            </label>
            <label className="flex min-h-40 cursor-pointer flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed border-slate-300 bg-slate-50 px-4 text-center hover:border-mis-primary hover:bg-mis-pale/30">
              <FileUp className="h-8 w-8 text-mis-primary" />
              <span className="font-semibold text-mis-navy">{l.text('اختر ملف العملاء', 'Choose a client workbook')}</span>
              <span className="text-sm text-slate-500">{l.text('الصيغ المدعومة: XLSX و XLS و CSV', 'Supported formats: XLSX, XLS, and CSV')}</span>
              <input accept=".xlsx,.xls,.csv" className="sr-only" disabled={!organizationId || organizationsLoading || organizationsError} onChange={(event) => { void selectFile(event.target.files?.[0]); }} type="file" />
            </label>
            {organizationsError ? <p className="mt-3 text-sm text-rose-700">{l.text('تعذر تحميل جهات النظام. أعد تحميل الصفحة للمحاولة مجددًا.', 'MIS organizations could not be loaded. Reload the page to retry.')}</p> : null}
            {!organizationsLoading && !organizationsError && organizations.length === 0 ? <p className="mt-3 text-sm text-rose-700">{l.text('لا توجد جهة نشطة في النظام لربط سجل العميل بها.', 'No active MIS organization is available to link the customer record.')}</p> : null}
            {file ? <p className="mt-3 text-sm text-slate-600">{file.name}</p> : null}
            {loading ? <p className="mt-4 text-sm text-slate-500">{l.text('جارٍ تحليل الملف...', 'Reading workbook...')}</p> : null}
          </section>

          {preview ? (
            <section className="space-y-4 rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
                <Summary label={l.text('إجمالي الصفوف', 'Total rows')} value={l.number(preview.totalRows)} />
                <Summary label={l.text('صالحة للاستيراد', 'Ready to import')} value={l.number(preview.validRows)} tone="success" />
                <Summary label={l.text('صفوف غير صالحة', 'Invalid rows')} value={l.number(preview.invalidRows)} tone="warning" />
                <Summary label={l.text('عملاء مطابقون', 'Existing customer matches')} value={l.number(preview.existingCustomersMatched)} />
                <Summary label={l.text('صفوف مكررة', 'Duplicate rows')} value={l.number(preview.duplicateRows)} tone="warning" />
              </div>
              <div className="max-h-[36rem] overflow-auto rounded-xl border border-mis-border">
                <table className={`w-full text-sm ${originalHeaders.length ? 'min-w-[75rem]' : 'min-w-[60rem]'}`}>
                  <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                    <tr>
                      {originalHeaders.length ? originalHeaders.map((header) => (
                        <th className="px-3 py-3 text-start" key={header}>{originalHeaderLabel(header, l.text)}</th>
                      )) : (
                        <>
                          <th className="px-3 py-3 text-start">{l.text('الصف', 'Row')}</th>
                          <th className="px-3 py-3 text-start">{l.text('اسم العميل', 'Client Name')}</th>
                          <th className="px-3 py-3 text-start">{l.text('الرقم القومي', 'National ID')}</th>
                          <th className="px-3 py-3 text-start">{l.text('رقم الهاتف', 'Mobile Number')}</th>
                          <th className="px-3 py-3 text-start">{l.text('البنك', 'Bank Name')}</th>
                          <th className="px-3 py-3 text-start">{l.text('العنوان', 'Address')}</th>
                          <th className="px-3 py-3 text-start">{l.text('ملاحظات', 'Notes')}</th>
                        </>
                      )}
                      <th className="px-3 py-3 text-start">{l.text('نتيجة التحقق', 'Validation')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-mis-border">
                    {preview.rows.map((row) => (
                      <tr key={row.source.rowNumber}>
                        {originalHeaders.length ? originalHeaders.map((header) => (
                          <td className="max-w-56 truncate px-3 py-3" key={header} data-bidi={isIdentifierHeader(header) ? 'ltr' : undefined}>
                            {row.source.originalFields?.[header] || '—'}
                          </td>
                        )) : (
                          <>
                            <td className="px-3 py-3">{l.number(row.source.rowNumber)}</td>
                            <td className="px-3 py-3">{l.ar ? row.source.nameArabic || row.source.name : row.source.nameEnglish || row.source.nameArabic || row.source.name || '—'}</td>
                            <td className="px-3 py-3" data-bidi="ltr">{row.source.nationalId || '—'}</td>
                            <td className="px-3 py-3" data-bidi="ltr">{row.source.mobileNumber || '—'}</td>
                            <td className="px-3 py-3">{row.source.bankName || '—'}</td>
                            <td className="max-w-48 truncate px-3 py-3">{(l.ar ? row.source.addressArabic || row.source.address : row.source.addressEnglish || row.source.addressArabic || row.source.address) || '—'}</td>
                            <td className="max-w-48 truncate px-3 py-3">{row.source.notes || '—'}</td>
                          </>
                        )}
                        <td className="px-3 py-3">
                          <span className={`font-semibold ${row.status === 'VALID' || row.status === 'EXISTING' ? 'text-emerald-700' : 'text-rose-700'}`}>
                            {statusLabel(row.status, l.text)}
                          </span>
                          {row.errors.length ? <p className="mt-1 text-xs text-rose-700">{row.errors.map((error) => localizedError(error, l.text)).join(' ')}</p> : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {result ? (
                <div className="space-y-3">
                  <p className={`rounded-xl px-4 py-3 text-sm font-semibold ${result.successfulRows > 0 ? 'bg-emerald-50 text-emerald-800' : 'bg-rose-50 text-rose-800'}`}>
                    {l.text('تم حفظ {success} صفًا في قاعدة البيانات، وتعذر حفظ {failed} صف.', 'Saved {success} rows to the database; {failed} rows could not be saved.')
                    .replace('{success}', l.number(result.successfulRows))
                    .replace('{failed}', l.number(result.failedRows))}
                  </p>
                  {result.rows.some((row) => row.errors.length) ? (
                    <div className="overflow-x-auto rounded-xl border border-mis-border">
                      <table className="w-full min-w-[36rem] text-sm">
                        <thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr>
                          <th className="px-3 py-3 text-start">{l.text('الصف', 'Row')}</th>
                          <th className="px-3 py-3 text-start">{l.text('النتيجة', 'Result')}</th>
                          <th className="px-3 py-3 text-start">{l.text('الأخطاء', 'Errors')}</th>
                        </tr></thead>
                        <tbody className="divide-y divide-mis-border">
                          {result.rows.filter((row) => row.errors.length).map((row) => (
                            <tr key={row.rowNumber}><td className="px-3 py-3">{l.number(row.rowNumber)}</td><td className="px-3 py-3">{statusLabel(row.status, l.text)}</td><td className="px-3 py-3 text-rose-700">{row.errors.map((error) => localizedError(error, l.text)).join(' ')}</td></tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ) : null}
                </div>
              ) : null}
              <div className="flex justify-end">
                <Button disabled={!preview.validRows} fullWidth={false} isLoading={importing} leftIcon={<Upload />} onClick={importClients} type="button">
                  {l.text('استيراد الصفوف الصالحة', 'Import Valid Clients')}
                </Button>
              </div>
            </section>
          ) : (
            <EmptyState compact icon={<FileUp className="h-5 w-5" />} title={l.text('المعاينة قبل الاستيراد', 'Preview before importing')} description={l.text('سيتم اكتشاف الأعمدة والتحقق من البيانات والتكرار قبل حفظ أي عميل.', 'Columns, required data, and duplicates are checked before any client is saved.')} />
          )}
        </>
      )}
    </div>
  );
}

function originalHeaderLabel(header: string, text: (arabic: string, english: string) => string) {
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
  const label = labels[header];
  return label ? text(label[0], label[1]) : header;
}

function isIdentifierHeader(header: string) {
  return ['#', 'ID Number', 'Installment Unique ID', 'serial', 'رقم المحضر'].includes(header);
}

function Summary({ label, value, tone = 'default' }: { label: string; value: string; tone?: 'default' | 'success' | 'warning' }) {
  const color = tone === 'success' ? 'text-emerald-700' : tone === 'warning' ? 'text-amber-700' : 'text-mis-navy';
  return <article className="rounded-xl border border-mis-border bg-slate-50 p-4"><p className="text-xs font-semibold text-slate-500">{label}</p><p className={`mt-2 text-xl font-bold ${color}`} data-bidi="ltr">{value}</p></article>;
}

function statusLabel(status: string, text: (arabic: string, english: string) => string) {
  return ({
    VALID: text('جاهز', 'Ready'),
    EXISTING: text('عميل موجود - سيتم ربطه', 'Existing customer - will be linked'),
    INVALID: text('غير صالح', 'Invalid'),
    DUPLICATE: text('مكرر', 'Duplicate'),
  }[status] ?? status);
}

function localizedError(error: string, text: (arabic: string, english: string) => string) {
  if (error === 'Select the MIS organization for the imported clients.') return text('اختر جهة النظام المرتبط بها سجل العميل.', 'Select the MIS organization for the customer record.');
  if (error === 'The selected MIS organization is not active.') return text('جهة النظام المختارة غير نشطة.', 'The selected MIS organization is not active.');
  if (error === 'National ID matches more than one customer.') return text('الرقم القومي مرتبط بأكثر من سجل عميل؛ يلزم التحقق يدويًا.', 'National ID matches multiple customer records; manual review is required.');
  if (error === 'National ID does not match the customer name.') return text('الرقم القومي مطابق لسجل باسم مختلف؛ لم يتم ربط العميل.', 'The National ID matches a different customer name; the record was not linked.');
  if (error === 'National ID and installment ID refer to different customers.') return text('الرقم القومي ورقم القسط يشيران إلى عميلين مختلفين.', 'National ID and installment ID point to different customers.');
  if (error === 'Client name is required.') return text('اسم العميل مطلوب.', 'Client name is required.');
  if (error === 'National ID or mobile number is required.' || error === 'National ID, mobile number, or installment unique ID is required.') return text('الرقم القومي أو الهاتف أو الرقم الفريد للقسط مطلوب.', 'National ID, mobile number, or installment unique ID is required.');
  if (error === 'This client appears more than once in the uploaded file.') return text('هذا العميل مكرر داخل الملف.', 'This client appears more than once in the file.');
  if (error === 'This customer is already assigned to Legal.') return text('هذا العميل مسجل بالفعل لدى الشؤون القانونية.', 'This client is already assigned to Legal.');
  if (error === 'Installment Unique ID matches more than one customer.') return text('الرقم الفريد للقسط مرتبط بأكثر من عميل.', 'Installment Unique ID matches more than one customer.');
  if (error === 'Installment Unique ID does not match the customer name.') return text('الرقم الفريد للقسط لا يطابق اسم العميل.', 'Installment Unique ID does not match the customer name.');
  if (error === 'Installment Unique ID cannot exceed 128 characters.') return text('الرقم الفريد للقسط لا يمكن أن يتجاوز 128 حرفًا.', 'Installment Unique ID cannot exceed 128 characters.');
  if (error === 'National ID does not match the identified customer.') return text('الرقم القومي لا يطابق سجل العميل المحدد.', 'National ID does not match the identified customer.');
  if (error === 'Original workbook fields contain an invalid header or value.') return text('يحتوي الملف على عنوان عمود أو قيمة غير صالحة.', 'The workbook contains an invalid column header or value.');
  if (error === 'Original workbook fields exceed the supported size.') return text('تتجاوز بيانات الملف الحجم المدعوم.', 'Workbook data exceeds the supported size.');
  if (error === 'Client name cannot exceed 200 characters.') return text('اسم العميل يتجاوز 200 حرف.', 'Client name cannot exceed 200 characters.');
  if (error === 'National ID cannot exceed 32 characters.') return text('الرقم القومي يتجاوز 32 حرفًا.', 'National ID cannot exceed 32 characters.');
  if (error === 'Mobile number cannot exceed 32 characters.') return text('رقم الهاتف يتجاوز 32 حرفًا.', 'Mobile number cannot exceed 32 characters.');
  if (error === 'Address cannot exceed 600 characters.') return text('العنوان يتجاوز 600 حرف.', 'Address cannot exceed 600 characters.');
  if (error === 'Notes cannot exceed 2,000 characters.') return text('الملاحظات تتجاوز 2,000 حرف.', 'Notes cannot exceed 2,000 characters.');
  if (error === 'Bank name cannot exceed 200 characters.') return text('اسم البنك يتجاوز 200 حرف.', 'Bank name cannot exceed 200 characters.');
  return error;
}
