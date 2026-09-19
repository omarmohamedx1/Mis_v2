import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Upload, Eye, CheckCircle2, Download, FileSpreadsheet } from 'lucide-react';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge } from '../../components/common/StatusBadge';
import { FileInput } from '../../components/forms/FileInput';
import { TextInput } from '../../components/forms/TextInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { useLocalization } from '../../context/LocalizationContext';
import { getApiErrorMessage } from '../../services/apiClient';
import { preferredHrImportSheetIndex } from '../../features/hr/preferredHrImportSheet';
import { socialInsuranceImportService, type SocialInsuranceImportHistory, type SocialInsuranceImportMapping, type SocialInsuranceImportPreview, type SocialInsuranceImportResult, type SocialInsuranceImportUpload } from '../../features/hr/services/socialInsuranceImportService';

const fields = [
  ['EmployeeNumber', 'Employee Number', 'رقم الموظف', /^(code|employee code|employee no\.?|employee number|رقم الموظف)$/i, false],
  ['EmployeeName', 'Employee Name', 'اسم الموظف', /^(name|employee name|اسم الموظف)$/i, false],
  ['NationalId', 'National ID', 'الرقم القومي', /^(national id|id number|الرقم القومي)$/i, false],
  ['EmployeeId', 'Employee ID (system UUID)', 'معرف الموظف بالنظام', /^(employee id|employee uuid)$/i, false],
  ['SocialInsuranceNumber', 'Social Insurance Number', 'الرقم التأميني', /^(insurance no\.?|social insurance no\.?|insurance number|social insurance number|الرقم التأميني)$/i, true],
  ['InsuranceStartDate', 'Insurance Start Date', 'تاريخ بداية التأمين', /^(start date|insurance start|insurance start date|تاريخ بداية التأمين)$/i, false],
  ['InsuranceEndDate', 'Insurance End Date', 'تاريخ نهاية التأمين', /^(end date|insurance end|insurance end date|تاريخ نهاية التأمين)$/i, false],
  ['InsurableSalary', 'Insurable Salary', 'الأجر التأميني', /^(insurance salary|insurable salary|الأجر التأميني)$/i, true],
  ['InsuranceStatus', 'Insurance Status', 'حالة التأمين', /^(status|insurance status|حالة التأمين)$/i, true],
  ['InsuranceOffice', 'Insurance Office', 'مكتب التأمينات', /^(office|insurance office|مكتب التأمينات)$/i, false],
  ['ReferenceNumber', 'Reference Number', 'رقم الاستمارة / المرجع', /^(reference(?:\s*\/\s*form number)?|reference number|form number|رقم الاستمارة(?:\s*\/\s*المرجع)?|المرجع)$/i, false],
  ['Notes', 'Notes', 'ملاحظات', /^(notes|ملاحظات)$/i, false],
] as const;

function mappingFor(upload: SocialInsuranceImportUpload, index: number): SocialInsuranceImportMapping {
  const sheet = upload.sheets[index];
  return { sheetName: sheet.sheetName, headerRow: sheet.suggestedHeaderRowNumber, firstDataRow: sheet.suggestedHeaderRowNumber + 1, dateFormat: null,
    columns: Object.fromEntries(fields.map(([key, , , pattern]) => [key, sheet.detectedColumns.find(column => pattern.test(column.trim())) ?? ''])) };
}

export function HrSocialInsuranceImportPage() {
  const { language } = useLocalization(); const ar = language === 'ar';
  const text = (en: string, arabic: string) => ar ? arabic : en;
  const [step, setStep] = useState(0); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const [file, setFile] = useState<File | null>(null); const [upload, setUpload] = useState<SocialInsuranceImportUpload | null>(null);
  const [sheetIndex, setSheetIndex] = useState(0); const [mapping, setMapping] = useState<SocialInsuranceImportMapping | null>(null);
  const [preview, setPreview] = useState<SocialInsuranceImportPreview | null>(null); const [result, setResult] = useState<SocialInsuranceImportResult | null>(null);
  const [history, setHistory] = useState<SocialInsuranceImportHistory[]>([]);
  const [downloadingTemplate, setDownloadingTemplate] = useState(false);
  useEffect(() => { void socialInsuranceImportService.history().then(setHistory).catch(() => setError(ar ? 'تعذر تحميل سجل الاستيراد.' : 'Unable to load import history.')); }, [result, upload, preview, ar]);
  async function run(action: () => Promise<void>) { setBusy(true); setError(''); try { await action(); } catch (reason) { setError(getApiErrorMessage(reason, text('Insurance import failed.', 'تعذر استيراد التأمينات.'))); } finally { setBusy(false); } }
  async function sendFile() {
    if (!file) return;
    if (!/\.(xlsx|xls|csv)$/i.test(file.name) || file.size > 20 * 1024 * 1024) { setError(text('Choose CSV, XLS, or XLSX, maximum 20 MB.', 'اختر CSV أو XLS أو XLSX بحد أقصى 20 ميجابايت.')); return; }
    await run(async () => {
      const data = await socialInsuranceImportService.upload(file);
      const index = preferredHrImportSheetIndex(data.sheets);
      setUpload(data);
      setSheetIndex(index);
      setMapping(mappingFor(data, index));
      setStep(1);
    });
  }
  async function downloadTemplate() {
    setDownloadingTemplate(true);
    setError('');
    try {
      await socialInsuranceImportService.downloadTemplate();
    } catch (reason) {
      setError(getApiErrorMessage(reason, text('Unable to download the insurance template.', 'تعذر تنزيل قالب التأمينات.')));
    } finally {
      setDownloadingTemplate(false);
    }
  }
  const labels = [text('Upload', 'رفع الملف'), text('Column Mapping', 'تعيين الأعمدة'), text('Preview & Validation', 'المعاينة والتحقق'), text('Import Complete', 'اكتمل الاستيراد')];
  const statusLabel = (status: string) => ({ Ready: text('Ready', 'جاهز'), Warning: text('Warning', 'تحذير'), Error: text('Error', 'خطأ'), Existing: text('Existing active record — skipped', 'سجل نشط موجود بالفعل — سيتم التجاوز') }[status] ?? status);
  const insuranceStatus = (status: string) => ({ Insured: text('Insured', 'مؤمن عليه'), NotInsured: text('Not Insured', 'غير مؤمن عليه'), Suspended: text('Suspended', 'موقوف'), Ended: text('Ended', 'منتهي') }[status] ?? status);
  const link = <Link className="inline-flex h-10 items-center rounded-xl border border-mis-border px-4 text-sm font-semibold text-mis-primary" to="/hr/social-insurance">{text('View Social Insurance', 'عرض التأمينات الاجتماعية')}</Link>;
  return <div className="space-y-5"><PageHeader title={text('Import Insurance Sheet', 'استيراد ملف التأمينات')} description={text('Upload, map, review, then confirm. Existing active insurance records are skipped.', 'ارفع الملف وعيّن الأعمدة وراجع المعاينة ثم أكد. يتم تجاوز سجلات التأمين النشطة.')} actions={<div className="flex flex-wrap gap-2"><Button fullWidth={false} isLoading={downloadingTemplate} leftIcon={<Download className="h-4 w-4" />} onClick={() => void downloadTemplate()} variant="outline">{text('Download Excel template', 'تنزيل قالب Excel')}</Button>{link}</div>} />
    <div className="grid grid-cols-2 overflow-hidden rounded-2xl border border-mis-border bg-white sm:grid-cols-4">{labels.map((label, index) => <div key={label} className={`p-4 text-center text-sm font-semibold ${step === index ? 'bg-mis-pale text-mis-primary' : 'text-slate-500'}`}>{index + 1}. {label}</div>)}</div>
    {error ? <div role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">{error}</div> : null}
    {step === 0 ? <div className="grid gap-5 lg:grid-cols-[1.2fr_0.8fr]"><Card padding="lg"><form className="mx-auto max-w-2xl space-y-5" onSubmit={event => { event.preventDefault(); void sendFile(); }}><FileInput accept=".xlsx,.xls,.csv" label={text('Insurance Excel / CSV file', 'ملف التأمينات Excel / CSV')} hint={text('Maximum 20 MB and 2,000 rows. Uploading does not save insurance records.', 'بحد أقصى 20 ميجابايت و2000 صف. رفع الملف لا يحفظ سجلات التأمين.')} onChange={event => setFile(event.target.files?.[0] ?? null)} required /><Button isLoading={busy} type="submit" leftIcon={<Upload className="h-4 w-4" />}>{text('Upload and Continue', 'رفع ومتابعة')}</Button></form></Card><Card className="border-sky-200 bg-sky-50/60" padding="lg"><FileSpreadsheet className="h-9 w-9 text-mis-primary" /><h2 className="mt-4 font-bold text-mis-navy">{text('Start with the official template', 'ابدأ بالقالب الرسمي')}</h2><p className="mt-2 text-sm leading-6 text-slate-600">{text('It includes the insurance columns and bilingual instructions so the file maps automatically after upload.', 'يحتوي على أعمدة التأمين وتعليمات بالعربية والإنجليزية حتى يتعرّف الملف تلقائيًا بعد الرفع.')}</p><Button className="mt-5" isLoading={downloadingTemplate} leftIcon={<Download className="h-4 w-4" />} onClick={() => void downloadTemplate()} variant="secondary">{text('Download template', 'تنزيل القالب')}</Button></Card></div> : null}
    {step === 1 && upload && mapping ? <Card padding="lg"><form className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3" onSubmit={event => { event.preventDefault(); void run(async () => { setPreview(await socialInsuranceImportService.preview(upload.id, mapping)); setStep(2); }); }}>
      <SelectInput label={text('Sheet', 'ورقة العمل')} value={sheetIndex} onChange={event => { const index = Number(event.target.value); setSheetIndex(index); setMapping(mappingFor(upload, index)); }}>{upload.sheets.map((sheet, index) => <option key={index} value={index}>{sheet.sheetName ?? text('CSV', 'CSV')}</option>)}</SelectInput>
      <TextInput type="number" min={1} max={1000} label={text('Header Row', 'صف العناوين')} value={mapping.headerRow} onChange={event => setMapping({ ...mapping, headerRow: Number(event.target.value) })} required />
      <TextInput type="number" min={mapping.headerRow + 1} max={2000} label={text('First Data Row', 'أول صف بيانات')} value={mapping.firstDataRow} onChange={event => setMapping({ ...mapping, firstDataRow: Number(event.target.value) })} required />
      {fields.map(([key, en, arabic, , required]) => <SelectInput key={key} required={required} label={text(en, arabic)} value={mapping.columns[key] ?? ''} onChange={event => setMapping({ ...mapping, columns: { ...mapping.columns, [key]: event.target.value } })}><option value="">{text('Not mapped', 'غير معيّن')}</option>{upload.sheets[sheetIndex].detectedColumns.map(column => <option key={column}>{column}</option>)}</SelectInput>)}
      <TextInput dir="ltr" maxLength={32} label={text('Date Format (optional)', 'تنسيق التاريخ (اختياري)')} placeholder="dd/MM/yyyy" value={mapping.dateFormat ?? ''} onChange={event => setMapping({ ...mapping, dateFormat: event.target.value || null })} />
      <p className="text-sm text-slate-500 sm:col-span-2 lg:col-span-3">{text('Match by Employee Number, National ID, or system Employee ID. Names are verification only. Status: Insured, NotInsured, Suspended, Ended. Ended requires an end date. Existing active records are skipped. Dates: yyyy-MM-dd or dd/MM/yyyy; salary without thousands separators.', 'المطابقة بمعرف موظف موجود فقط. الحالات: مؤمن، غير مؤمن، موقوف، منتهي. يتم تجاوز السجلات النشطة. التاريخ yyyy-MM-dd أو dd/MM/yyyy والأجر دون فواصل آلاف.')}</p>
      <div className="flex justify-end gap-3 sm:col-span-2 lg:col-span-3"><Button disabled={busy} fullWidth={false} type="button" variant="outline" onClick={() => setStep(0)}>{text('Back', 'رجوع')}</Button><Button fullWidth={false} isLoading={busy} type="submit" leftIcon={<Eye className="h-4 w-4" />}>{text('Build Preview', 'إنشاء المعاينة')}</Button></div>
    </form></Card> : null}
    {step === 2 && preview ? <><div className="grid grid-cols-2 gap-3 sm:grid-cols-4">{(['Ready', 'Warning', 'Error', 'Existing'] as const).map(status => <Card key={status} className="p-4"><p className="text-sm text-slate-500">{statusLabel(status)}</p><p className="mt-2 text-2xl font-bold">{preview.rows.filter(row => row.status === status).length}</p></Card>)}</div><Card padding="none"><div className="max-h-[70vh] overflow-auto"><table className="w-full min-w-[1300px] text-sm"><thead className="sticky top-0 z-10 bg-mis-surface"><tr>{[text('Employee Number', 'رقم الموظف'), text('Employee Name / Source Name', 'اسم الموظف / الاسم بالملف'), ...fields.slice(4, 9).map(([, en, arabic]) => text(en, arabic)), text('Validation', 'التحقق')].map(label => <th key={label} className="p-3 text-start">{label}</th>)}</tr></thead><tbody>{preview.rows.map(row => <tr key={row.row} className="border-t border-mis-border"><td className="p-3">{row.employeeNumber}</td><td className="p-3">{row.employeeName}<p className="text-xs text-slate-500">{row.sourceEmployeeName}</p></td>{[row.record.socialInsuranceNumber, row.record.insuranceStartDate, row.record.insuranceEndDate, row.record.insurableSalary, insuranceStatus(row.record.insuranceStatus)].map((value, index) => <td key={index} className="p-3"><bdi>{value ?? '—'}</bdi></td>)}<td className="p-3"><StatusBadge tone={row.status === 'Ready' ? 'success' : row.status === 'Error' ? 'danger' : 'warning'}>{statusLabel(row.status)}</StatusBadge>{row.errors.map((message, index) => <p key={index} className="mt-1 text-xs text-slate-600">{message}</p>)}</td></tr>)}</tbody></table></div></Card><div className="flex justify-end gap-3"><Button fullWidth={false} variant="outline" disabled={busy} onClick={() => setStep(1)}>{text('Edit Mapping', 'تعديل الأعمدة')}</Button><Button fullWidth={false} isLoading={busy} disabled={!preview.rows.some(row => row.status === 'Ready' || row.status === 'Warning')} onClick={() => void run(async () => { setResult(await socialInsuranceImportService.confirm(preview.id, preview.previewId)); setStep(3); })}>{text('Confirm Import', 'تأكيد الاستيراد')}</Button></div></> : null}
    {step === 3 && result ? <Card padding="lg"><div className="space-y-5 text-center"><CheckCircle2 className="mx-auto h-12 w-12 text-emerald-600" /><h2 className="text-xl font-bold">{labels[3]}</h2><p>{text('Imported', 'تم الاستيراد')}: {result.imported} · {text('Skipped', 'تم التجاوز')}: {result.skipped} · {text('Failed', 'فشل')}: {result.failed}</p><div className="flex justify-center gap-3">{link}<Button fullWidth={false} variant="outline" onClick={() => { setStep(0); setFile(null); setUpload(null); setPreview(null); }}>{text('Import Another File', 'استيراد ملف آخر')}</Button></div></div></Card> : null}
    <Card padding="lg"><h2 className="mb-4 text-lg font-bold">{text('Your Import History (latest 50)', 'سجل استيرادك (آخر 50)')}</h2><div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr>{[text('File', 'الملف'), text('Uploaded by', 'رفع بواسطة'), text('Uploaded at', 'تاريخ الرفع'), text('Rows', 'الصفوف'), text('Imported / Skipped / Failed', 'تم الاستيراد / التجاوز / الفشل')].map(label => <th key={label} className="p-3 text-start">{label}</th>)}</tr></thead><tbody>{history.map(item => <tr key={item.id} className="border-t border-mis-border"><td className="p-3">{item.fileName}</td><td className="p-3">{item.uploadedBy}</td><td className="p-3">{new Date(item.uploadedAt).toLocaleString(ar ? 'ar-EG' : 'en-GB')}</td><td className="p-3">{item.totalRows ?? '—'}</td><td className="p-3"><bdi>{item.result ? `${item.result.imported} / ${item.result.skipped} / ${item.result.failed}` : text('Awaiting confirmation', 'في انتظار التأكيد')}</bdi></td></tr>)}</tbody></table></div></Card>
  </div>;
}
