import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Eye, CheckCircle2, Download, FileSpreadsheet } from 'lucide-react';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge } from '../../components/common/StatusBadge';
import { TextInput } from '../../components/forms/TextInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { useLocalization } from '../../context/LocalizationContext';
import { getApiErrorMessage } from '../../services/apiClient';
import {
  ExcelColumnReview,
  ExcelDropzone,
  ExcelPreviewToolbar,
  ExcelSheetPicker,
  autoMapImportColumns,
  defaultSelectedSheetIndexes,
  importCopy,
  missingRequiredImportFields,
  rememberExtraColumns,
  sheetNamesFromIndexes,
  socialInsuranceImportCatalog,
} from '../../features/import';
import {
  socialInsuranceImportService,
  type SocialInsuranceImportHistory,
  type SocialInsuranceImportMapping,
  type SocialInsuranceImportPreview,
  type SocialInsuranceImportResult,
  type SocialInsuranceImportUpload,
} from '../../features/hr/services/socialInsuranceImportService';

function mappingFor(upload: SocialInsuranceImportUpload, indexes: number[]): SocialInsuranceImportMapping {
  const selected = indexes.length ? indexes : [0];
  const sheet = upload.sheets[selected[0]];
  const columns = [...new Set(selected.flatMap((index) => upload.sheets[index]?.detectedColumns ?? []))];
  return {
    sheetName: sheet.sheetName,
    headerRow: sheet.suggestedHeaderRowNumber,
    firstDataRow: sheet.suggestedHeaderRowNumber + 1,
    dateFormat: null,
    columns: autoMapImportColumns(socialInsuranceImportCatalog, columns),
    sheetNames: sheetNamesFromIndexes(upload.sheets, selected),
  };
}

export function HrSocialInsuranceImportPage() {
  const { language } = useLocalization();
  const ar = language === 'ar';
  const text = (en: string, arabic: string) => (ar ? arabic : en);
  const [step, setStep] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [upload, setUpload] = useState<SocialInsuranceImportUpload | null>(null);
  const [selectedSheets, setSelectedSheets] = useState<number[]>([0]);
  const [mapping, setMapping] = useState<SocialInsuranceImportMapping | null>(null);
  const [preview, setPreview] = useState<SocialInsuranceImportPreview | null>(null);
  const [result, setResult] = useState<SocialInsuranceImportResult | null>(null);
  const [history, setHistory] = useState<SocialInsuranceImportHistory[]>([]);
  const [downloadingTemplate, setDownloadingTemplate] = useState(false);
  const [extraColumns, setExtraColumns] = useState<string[]>([]);
  const [excludedRows, setExcludedRows] = useState<number[]>([]);
  const [selectedRows, setSelectedRows] = useState<number[]>([]);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);

  useEffect(() => {
    void socialInsuranceImportService.history().then(setHistory).catch(() => setError(ar ? 'تعذر تحميل سجل الاستيراد.' : 'Unable to load import history.'));
  }, [result, upload, preview, ar]);

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError('');
    try {
      await action();
    } catch (reason) {
      setError(getApiErrorMessage(reason, text('Insurance import failed.', 'تعذر استيراد التأمينات.')));
    } finally {
      setBusy(false);
    }
  }

  async function sendFile(selected?: File | null) {
    const target = selected ?? file;
    if (!target) return;
    setFile(target);
    await run(async () => {
      const data = await socialInsuranceImportService.upload(target);
      const indexes = defaultSelectedSheetIndexes(data.sheets);
      const next = mappingFor(data, indexes);
      setUpload(data);
      setSelectedSheets(indexes);
      setExcludedRows([]);
      setSelectedRows([]);
      setExtraColumns([]);
      setShowAdvanced(false);
      setMapping(next);
      if (missingRequiredImportFields(socialInsuranceImportCatalog, next.columns).length === 0) {
        setPreview(await socialInsuranceImportService.preview(data.id, next));
        setStep(2);
      } else {
        setStep(1);
      }
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
  const statusLabel = (status: string) =>
    ({ Ready: text('Ready', 'جاهز'), Warning: text('Warning', 'تحذير'), Error: text('Error', 'خطأ'), Existing: text('Existing active record — skipped', 'سجل نشط موجود بالفعل — سيتم التجاوز') })[status] ?? status;
  const insuranceStatus = (status: string) =>
    ({ Insured: text('Insured', 'مؤمن عليه'), NotInsured: text('Not Insured', 'غير مؤمن عليه'), Suspended: text('Suspended', 'موقوف'), Ended: text('Ended', 'منتهي') })[status] ?? status;
  const readyCount = preview?.rows.filter((row) => (row.status === 'Ready' || row.status === 'Warning') && !excludedRows.includes(row.row)).length ?? 0;
  const detectedColumns = upload ? [...new Set(selectedSheets.flatMap((index) => upload.sheets[index]?.detectedColumns ?? []))] : [];
  const link = <Link className="inline-flex h-10 items-center rounded-xl border border-mis-border px-4 text-sm font-semibold text-mis-primary" to="/hr/social-insurance">{text('View Social Insurance', 'عرض التأمينات الاجتماعية')}</Link>;

  return (
    <div className="space-y-5">
      <PageHeader
        title={text('Import Insurance Sheet', 'استيراد ملف التأمينات')}
        description={text('Upload, map, review, then confirm. Existing active insurance records are skipped.', 'ارفع الملف وعيّن الأعمدة وراجع المعاينة ثم أكد. يتم تجاوز سجلات التأمين النشطة.')}
        actions={<div className="flex flex-wrap gap-2"><Button fullWidth={false} isLoading={downloadingTemplate} leftIcon={<Download className="h-4 w-4" />} onClick={() => void downloadTemplate()} variant="outline">{text('Download Excel template', 'تنزيل قالب Excel')}</Button>{link}</div>}
      />
      <div className="grid grid-cols-2 overflow-hidden rounded-2xl border border-mis-border bg-white sm:grid-cols-4">
        {labels.map((label, index) => (
          <div key={label} className={`p-4 text-center text-sm font-semibold ${step === index ? 'bg-mis-pale text-mis-primary' : 'text-slate-500'}`}>{index + 1}. {label}</div>
        ))}
      </div>
      {error ? <div role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">{error}</div> : null}

      {step === 0 ? (
        <div className="grid gap-5 lg:grid-cols-[1.2fr_0.8fr]">
          <Card padding="lg">
            <form className="mx-auto max-w-2xl space-y-5" onSubmit={(event) => { event.preventDefault(); void sendFile(); }}>
              <ExcelDropzone arabic={ar} busy={busy} file={file} hint={text('The file is read as soon as you choose it.', 'الملف يتقرا أول ما تختاره.')} label={text('Insurance Excel / CSV file', 'ملف التأمينات Excel / CSV')} onFile={(next) => { if (next) void sendFile(next); else setFile(null); }} />
            </form>
          </Card>
          <Card className="border-sky-200 bg-sky-50/60" padding="lg">
            <FileSpreadsheet className="h-9 w-9 text-mis-primary" />
            <h2 className="mt-4 font-bold text-mis-navy">{text('Start with the official template', 'ابدأ بالقالب الرسمي')}</h2>
            <p className="mt-2 text-sm leading-6 text-slate-600">{text('It includes the insurance columns and bilingual instructions so the file maps automatically after upload.', 'يحتوي على أعمدة التأمين وتعليمات بالعربية والإنجليزية حتى يتعرّف الملف تلقائيًا بعد الرفع.')}</p>
            <Button className="mt-5" isLoading={downloadingTemplate} leftIcon={<Download className="h-4 w-4" />} onClick={() => void downloadTemplate()} variant="secondary">{text('Download template', 'تنزيل القالب')}</Button>
          </Card>
        </div>
      ) : null}

      {step === 1 && upload && mapping ? (
        <Card padding="lg">
          <div className="mb-5 space-y-5">
            <ExcelSheetPicker
              arabic={ar}
              onChange={(indexes) => {
                const nextIndexes = indexes.length ? indexes : [0];
                setSelectedSheets(nextIndexes);
                setMapping(mappingFor(upload, nextIndexes));
              }}
              selected={selectedSheets}
              sheets={upload.sheets}
            />
            <ExcelColumnReview
              arabic={ar}
              detectedColumns={detectedColumns}
              extraSelected={extraColumns}
              fields={socialInsuranceImportCatalog}
              mapping={mapping.columns}
              onExtraSelected={setExtraColumns}
              onToggleAdvanced={() => setShowAdvanced((value) => !value)}
              showAdvanced={showAdvanced}
            />
          </div>
          <form
            className={`grid gap-5 sm:grid-cols-2 lg:grid-cols-3 ${showAdvanced ? '' : 'hidden'}`}
            onSubmit={(event) => {
              event.preventDefault();
              void run(async () => {
                setPreview(await socialInsuranceImportService.preview(upload.id, mapping));
                setExcludedRows([]);
                setSelectedRows([]);
                setStep(2);
              });
            }}
          >
            <TextInput type="number" min={1} max={1000} label={text('Header Row', 'صف العناوين')} value={mapping.headerRow} onChange={(event) => setMapping({ ...mapping, headerRow: Number(event.target.value) })} required />
            <TextInput type="number" min={mapping.headerRow + 1} max={2000} label={text('First Data Row', 'أول صف بيانات')} value={mapping.firstDataRow} onChange={(event) => setMapping({ ...mapping, firstDataRow: Number(event.target.value) })} required />
            {socialInsuranceImportCatalog.map((field) => (
              <SelectInput
                key={field.key}
                required={field.required}
                label={text(field.en, field.ar)}
                value={mapping.columns[field.key] ?? ''}
                onChange={(event) => setMapping({ ...mapping, columns: { ...mapping.columns, [field.key]: event.target.value } })}
              >
                <option value="">{text('Not mapped', 'غير معيّن')}</option>
                {detectedColumns.map((column) => <option key={column}>{column}</option>)}
              </SelectInput>
            ))}
            <TextInput dir="ltr" maxLength={32} label={text('Date Format (optional)', 'تنسيق التاريخ (اختياري)')} placeholder="dd/MM/yyyy" value={mapping.dateFormat ?? ''} onChange={(event) => setMapping({ ...mapping, dateFormat: event.target.value || null })} />
            <p className="text-sm text-slate-500 sm:col-span-2 lg:col-span-3">
              {text('Match by Employee Number, National ID, or system Employee ID. Names are verification only. Status: Insured, NotInsured, Suspended, Ended.', 'المطابقة بمعرف موظف موجود فقط. الحالات: مؤمن، غير مؤمن، موقوف، منتهي.')}
            </p>
            <div className="flex justify-end gap-3 sm:col-span-2 lg:col-span-3">
              <Button disabled={busy} fullWidth={false} type="button" variant="outline" onClick={() => setStep(0)}>{text('Back', 'رجوع')}</Button>
              <Button fullWidth={false} isLoading={busy} type="submit" leftIcon={<Eye className="h-4 w-4" />}>{text('Build Preview', 'إنشاء المعاينة')}</Button>
            </div>
          </form>
          {!showAdvanced ? (
            <div className="mt-5 flex justify-end gap-3">
              <Button disabled={busy} fullWidth={false} type="button" variant="outline" onClick={() => setStep(0)}>{text('Back', 'رجوع')}</Button>
              <Button
                fullWidth={false}
                isLoading={busy}
                leftIcon={<Eye className="h-4 w-4" />}
                onClick={() => void run(async () => {
                  setPreview(await socialInsuranceImportService.preview(upload.id, mapping));
                  setExcludedRows([]);
                  setSelectedRows([]);
                  setStep(2);
                })}
              >
                {text('Build Preview', 'إنشاء المعاينة')}
              </Button>
            </div>
          ) : null}
        </Card>
      ) : null}

      {step === 2 && preview ? (
        <>
          <ExcelPreviewToolbar
            arabic={ar}
            excludedCount={excludedRows.length}
            onExclude={() => setExcludedRows((current) => [...new Set([...current, ...selectedRows])])}
            onRestore={() => { setExcludedRows((current) => current.filter((row) => !selectedRows.includes(row))); setSelectedRows([]); }}
            readyCount={readyCount}
            selectedCount={selectedRows.length}
          />
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {(['Ready', 'Warning', 'Error', 'Existing'] as const).map((status) => (
              <Card key={status} className="p-4">
                <p className="text-sm text-slate-500">{statusLabel(status)}</p>
                <p className="mt-2 text-2xl font-bold">{preview.rows.filter((row) => row.status === status).length}</p>
              </Card>
            ))}
          </div>
          <Card padding="none">
            <div className="max-h-[70vh] overflow-auto">
              <table className="w-full min-w-[1300px] text-sm">
                <thead className="sticky top-0 z-10 bg-mis-surface">
                  <tr>
                    {[text('Select', 'تحديد'), text('Employee Number', 'رقم الموظف'), text('Employee Name / Source Name', 'اسم الموظف / الاسم بالملف'), text('Social Insurance Number', 'الرقم التأميني'), text('Insurance Start Date', 'تاريخ بداية التأمين'), text('Insurance End Date', 'تاريخ نهاية التأمين'), text('Insurable Salary', 'الأجر التأميني'), text('Insurance Status', 'حالة التأمين'), text('Validation', 'التحقق')].map((label) => (
                      <th key={label} className="p-3 text-start">{label}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {preview.rows.map((row) => (
                    <tr key={row.row} className={`border-t border-mis-border ${excludedRows.includes(row.row) ? 'bg-slate-100 opacity-60' : ''}`}>
                      <td className="p-3"><input checked={selectedRows.includes(row.row)} onChange={() => setSelectedRows((current) => current.includes(row.row) ? current.filter((item) => item !== row.row) : [...current, row.row])} type="checkbox" /></td>
                      <td className="p-3">{row.employeeNumber}</td>
                      <td className="p-3">{row.employeeName}<p className="text-xs text-slate-500">{row.sourceEmployeeName}</p></td>
                      {[row.record.socialInsuranceNumber, row.record.insuranceStartDate, row.record.insuranceEndDate, row.record.insurableSalary, insuranceStatus(row.record.insuranceStatus)].map((value, index) => (
                        <td key={index} className="p-3"><bdi>{value ?? '—'}</bdi></td>
                      ))}
                      <td className="p-3">
                        <StatusBadge tone={row.status === 'Ready' ? 'success' : row.status === 'Error' ? 'danger' : 'warning'}>{statusLabel(row.status)}</StatusBadge>
                        {excludedRows.includes(row.row) ? <p className="mt-1 text-xs text-slate-500">{importCopy.excludedBadge(ar)}</p> : null}
                        {row.errors.map((message, index) => <p key={index} className="mt-1 text-xs text-slate-600">{message}</p>)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
          <div className="flex justify-end gap-3">
            <Button fullWidth={false} variant="outline" disabled={busy} onClick={() => setStep(1)}>{text('Edit Mapping', 'تعديل الأعمدة')}</Button>
            <Button fullWidth={false} isLoading={busy} disabled={readyCount === 0} onClick={() => setConfirmOpen(true)}>{text('Confirm Import', 'تأكيد الاستيراد')}</Button>
          </div>
        </>
      ) : null}

      {step === 3 && result ? (
        <Card padding="lg">
          <div className="space-y-5 text-center">
            <CheckCircle2 className="mx-auto h-12 w-12 text-emerald-600" />
            <h2 className="text-xl font-bold">{labels[3]}</h2>
            <p>{text('Imported', 'تم الاستيراد')}: {result.imported} · {text('Skipped', 'تم التجاوز')}: {result.skipped} · {text('Failed', 'فشل')}: {result.failed}</p>
            <div className="flex justify-center gap-3">
              {link}
              <Button fullWidth={false} variant="outline" onClick={() => { setStep(0); setFile(null); setUpload(null); setPreview(null); }}>{text('Import Another File', 'استيراد ملف آخر')}</Button>
            </div>
          </div>
        </Card>
      ) : null}

      <Card padding="lg">
        <h2 className="mb-4 text-lg font-bold">{text('Your Import History (latest 50)', 'سجل استيرادك (آخر 50)')}</h2>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr>{[text('File', 'الملف'), text('Uploaded by', 'رفع بواسطة'), text('Uploaded at', 'تاريخ الرفع'), text('Rows', 'الصفوف'), text('Imported / Skipped / Failed', 'تم الاستيراد / التجاوز / الفشل')].map((label) => <th key={label} className="p-3 text-start">{label}</th>)}</tr>
            </thead>
            <tbody>
              {history.map((item) => (
                <tr key={item.id} className="border-t border-mis-border">
                  <td className="p-3">{item.fileName}</td>
                  <td className="p-3">{item.uploadedBy}</td>
                  <td className="p-3">{new Date(item.uploadedAt).toLocaleString(ar ? 'ar-EG' : 'en-GB')}</td>
                  <td className="p-3">{item.totalRows ?? '—'}</td>
                  <td className="p-3"><bdi>{item.result ? `${item.result.imported} / ${item.result.skipped} / ${item.result.failed}` : text('Awaiting confirmation', 'في انتظار التأكيد')}</bdi></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <ConfirmDialog
        confirmLabel={importCopy.confirmAction(ar, readyCount)}
        confirmVariant="primary"
        isConfirming={busy}
        message={importCopy.confirmMessage(ar, readyCount)}
        onCancel={() => { if (!busy) setConfirmOpen(false); }}
        onConfirm={() => void run(async () => {
          if (!preview) return;
          rememberExtraColumns('social-insurance', extraColumns);
          setResult(await socialInsuranceImportService.confirm(preview.id, preview.previewId, excludedRows));
          setConfirmOpen(false);
          setStep(3);
        })}
        open={confirmOpen}
        title={importCopy.confirmTitle(ar)}
      />
    </div>
  );
}
