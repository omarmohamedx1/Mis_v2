import { ProfessionalSelect } from '../../../components/forms/ProfessionalSelect';
import { useState } from 'react';
import { Button } from '../../../components/common/Button';
import { Modal } from '../../../components/common/Modal';
import { useToast } from '../../../components/common/Toast';
import { ExcelDropzone, autoMapExcuseImportColumns, excuseImportCatalog, isExcuseRejectedHeader, pickBestImportSheetIndex } from '../../import';
import { useCollectionsLocalization } from '../../collections/localization/collectionsTranslations';
import { getApiErrorMessage } from '../../../services/apiClient';
import { excuseImportService, type ExcuseImportMapping, type ExcuseImportPreview, type ExcuseImportResult, type ExcuseImportUpload } from '../services/excuseImportService';

type Props = {
  onClose: () => void;
  onCompleted: () => void;
  open: boolean;
};

const requiredKeys = ['EmployeeNumber', 'ExcuseType', 'Date'];

function mappingFor(upload: ExcuseImportUpload, index: number): ExcuseImportMapping {
  const sheet = upload.sheets[index];
  const headerRow = sheet?.suggestedHeaderRowNumber || 1;
  return {
    sheetName: sheet?.sheetName ?? null,
    headerRow,
    firstDataRow: headerRow + 1,
    dateFormat: null,
    columns: autoMapExcuseImportColumns(excuseImportCatalog, sheet?.detectedColumns ?? []),
    sheetNames: sheet?.sheetName ? [sheet.sheetName] : undefined,
  };
}

function mapped(mapping: ExcuseImportMapping | null): boolean {
  return requiredKeys.every((key) => mapping?.columns[key]?.trim());
}

export function ExcuseImportModal({ onClose, onCompleted, open }: Props) {
  const { language } = useCollectionsLocalization();
  const arabic = language === 'ar';
  const toast = useToast();
  const text = (en: string, ar: string) => (arabic ? ar : en);
  const [file, setFile] = useState<File | null>(null);
  const [upload, setUpload] = useState<ExcuseImportUpload | null>(null);
  const [sheetIndex, setSheetIndex] = useState(0);
  const [mapping, setMapping] = useState<ExcuseImportMapping | null>(null);
  const [showMapping, setShowMapping] = useState(false);
  const [preview, setPreview] = useState<ExcuseImportPreview | null>(null);
  const [result, setResult] = useState<ExcuseImportResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  function reset() {
    setFile(null);
    setUpload(null);
    setMapping(null);
    setPreview(null);
    setResult(null);
    setShowMapping(false);
    setError('');
  }

  async function previewUpload(nextUpload: ExcuseImportUpload, nextMapping: ExcuseImportMapping) {
    setBusy(true);
    setError('');
    try {
      setPreview(await excuseImportService.preview(nextUpload.id, nextMapping));
      setResult(null);
    } catch (reason) {
      setPreview(null);
      setError(getApiErrorMessage(reason, text('The file could not be validated.', 'تعذر التحقق من الملف.')));
    } finally {
      setBusy(false);
    }
  }

  async function onFile(next: File | null) {
    setFile(next);
    setUpload(null);
    setMapping(null);
    setPreview(null);
    setResult(null);
    setError('');
    if (!next) return;
    setBusy(true);
    try {
      const uploaded = await excuseImportService.upload(next);
      const index = pickBestImportSheetIndex(excuseImportCatalog, uploaded.sheets);
      const nextMapping = mappingFor(uploaded, index);
      setUpload(uploaded);
      setSheetIndex(index);
      setMapping(nextMapping);
      setShowMapping(!mapped(nextMapping));
      if (mapped(nextMapping)) await previewUpload(uploaded, nextMapping);
    } catch (reason) {
      setError(getApiErrorMessage(reason, text('The file could not be read.', 'تعذر قراءة الملف.')));
    } finally {
      setBusy(false);
    }
  }

  async function confirm() {
    if (!upload || !preview) return;
    setBusy(true);
    setError('');
    try {
      const imported = await excuseImportService.confirm(upload.id, preview.previewId);
      setResult(imported);
      onCompleted();
      toast.success(text(`${imported.imported} excuse requests were saved and are pending approval.`, `تم حفظ ${imported.imported} طلب عذر وبقيت بانتظار الموافقة.`));
    } catch (reason) {
      setError(getApiErrorMessage(reason, text('The import could not be saved.', 'تعذر حفظ الاستيراد.')));
    } finally {
      setBusy(false);
    }
  }

  const rows = preview?.rows ?? [];
  const ready = rows.filter((row) => row.status === 'Ready').length;
  const invalid = rows.length - ready;
  const columns = upload?.sheets[sheetIndex]?.detectedColumns ?? [];
  const leaveFile = columns.some((column) => isExcuseRejectedHeader(column));

  return (
    <Modal
      open={open}
      size="xl"
      title={text('Upload Excel', 'رفع ملف Excel')}
      description={text('Valid rows are saved as pending approval, the same as a manual excuse.', 'الصفوف الصالحة تُحفظ بانتظار الموافقة، مثل العذر اليدوي.')}
      onClose={() => { if (!busy) { reset(); onClose(); } }}
      footer={
        <div className="flex flex-wrap justify-end gap-2">
          <Button fullWidth={false} variant="outline" disabled={busy} onClick={() => { reset(); onClose(); }} type="button">
            {text('Close', 'إغلاق')}
          </Button>
          {preview && !result ? (
            <Button fullWidth={false} disabled={busy || ready === 0} isLoading={busy} onClick={() => void confirm()} type="button">
              {text('Confirm import', 'تأكيد الاستيراد')}
            </Button>
          ) : null}
        </div>
      }
    >
      <div className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-sm text-slate-600">{text('Use Employee Number, Excuse Type, Date, From Time, To Time, and Notes. An unknown employee or excuse type is rejected.', 'استخدم رقم الموظف ونوع العذر والتاريخ ومن الساعة وإلى الساعة والملاحظات. رقم الموظف أو نوع العذر غير الموجود يُرفض.')}</p>
          <Button fullWidth={false} variant="secondary" disabled={busy} onClick={() => void excuseImportService.downloadTemplate().catch((reason) => toast.error(getApiErrorMessage(reason, text('The template could not be downloaded.', 'تعذر تنزيل النموذج.'))))} type="button">
            {text('Download Excel template', 'تحميل نموذج Excel')}
          </Button>
        </div>
        <ExcelDropzone arabic={arabic} busy={busy} file={file} onFile={(next) => void onFile(next)} />
        {error ? <p className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">{error}</p> : null}
        {leaveFile ? <p className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-950">{text('This file uses the leave columns (Leave Type, Start Date, End Date, Reason). Download the Excuses template. Its columns are Employee Number, Excuse Type, Date, From Time, To Time, and Notes.', 'هذا ملف إجازات ويستخدم Leave Type وStart Date وEnd Date وReason. حمّل نموذج الأعذار. أعمدته هي Employee Number وExcuse Type وDate وFrom Time وTo Time وNotes.')}</p> : null}
        {upload && upload.sheets.length > 1 ? (
          <label className="block text-sm font-semibold text-slate-700">
            {text('Sheet', 'الورقة')}
            <ProfessionalSelect
              className="mt-1 w-full rounded-xl border border-slate-300 px-3 py-2"
              value={sheetIndex}
              disabled={busy}
              onChange={(event) => {
                const index = Number(event.target.value);
                const nextMapping = mappingFor(upload, index);
                setSheetIndex(index);
                setMapping(nextMapping);
                setPreview(null);
                setResult(null);
                setShowMapping(!mapped(nextMapping));
                if (mapped(nextMapping)) void previewUpload(upload, nextMapping);
              }}
            >
              {upload.sheets.map((sheet, index) => <option key={`${sheet.sheetName}-${index}`} value={index}>{sheet.sheetName || index + 1}</option>)}
            </ProfessionalSelect>
          </label>
        ) : null}
        {mapping && (showMapping || !mapped(mapping)) ? (
          <div className="grid gap-3 sm:grid-cols-2">
            {excuseImportCatalog.map((field) => (
              <label key={field.key} className="block text-sm font-semibold text-slate-700">
                <span>{field.ar}{field.required ? ' *' : ''}</span>
                <span className="mt-0.5 block text-xs font-medium text-slate-500">{field.en}</span>
                <ProfessionalSelect
                  className="mt-1 w-full rounded-xl border border-slate-300 px-3 py-2 font-normal"
                  value={mapping.columns[field.key] ?? ''}
                  disabled={busy}
                  onChange={(event) => setMapping({ ...mapping, columns: { ...mapping.columns, [field.key]: event.target.value } })}
                >
                  <option value="">{text('Not mapped', 'غير مربوط')}</option>
                  {columns.filter((column) => !isExcuseRejectedHeader(column)).map((column) => <option key={column} value={column}>{column}</option>)}
                </ProfessionalSelect>
              </label>
            ))}
            <div className="sm:col-span-2">
              <Button fullWidth={false} disabled={busy || !upload || !mapped(mapping)} isLoading={busy} onClick={() => upload && mapping && void previewUpload(upload, mapping)} type="button">
                {text('Validate rows', 'التحقق من الصفوف')}
              </Button>
            </div>
          </div>
        ) : null}
        {preview ? (
          <>
            <div className="grid gap-3 sm:grid-cols-3">
              <Counter label={text('Ready to import', 'جاهز للاستيراد')} value={ready} tone="ready" />
              <Counter label={text('Invalid', 'غير صالح')} value={invalid} tone="invalid" />
              <Counter label={text('Total rows', 'إجمالي الصفوف')} value={rows.length} tone="total" />
            </div>
            <div className="max-h-96 overflow-auto rounded-xl border border-slate-200">
              <table className="min-w-full text-sm">
                <thead className="sticky top-0 bg-slate-50 text-slate-700">
                  <tr>
                    {[text('Employee', 'الموظف'), text('Employee Number', 'رقم الموظف'), text('Excuse Type', 'نوع العذر'), text('Date', 'التاريخ'), text('From Time', 'من الساعة'), text('To Time', 'إلى الساعة'), text('Status', 'الحالة'), text('Validation Error', 'خطأ التحقق')].map((heading) => (
                      <th key={heading} className="px-3 py-2 text-start font-semibold">{heading}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => {
                    const fullDay = row.status === 'Ready' && !row.fromTime && !row.toTime;
                    return (
                      <tr key={row.row} className="border-t border-slate-100">
                        <td className="px-3 py-2">{row.employeeName || '—'}</td>
                        <td className="px-3 py-2">{row.employeeNumber || '—'}</td>
                        <td className="px-3 py-2">{row.excuseType || '—'}</td>
                        <td className="px-3 py-2">{row.date || '—'}</td>
                        <td className="px-3 py-2">{fullDay ? text('Full day', 'يوم كامل') : row.fromTime || '—'}</td>
                        <td className="px-3 py-2">{fullDay ? text('Full day', 'يوم كامل') : row.toTime || '—'}</td>
                        <td className="px-3 py-2">{row.status === 'Ready' ? text('Ready', 'جاهز') : text('Invalid', 'غير صالح')}</td>
                        <td className="px-3 py-2 text-red-700">{row.errors.join(' ')}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </>
        ) : null}
        {result ? (
          <p className="rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-900">
            {text(`${result.imported} imported, ${result.skipped} skipped, ${result.failed} failed. Imported requests are pending approval.`, `تم استيراد ${result.imported}، وتجاوز ${result.skipped}، وفشل ${result.failed}. الطلبات المستوردة بانتظار الموافقة.`)}
          </p>
        ) : null}
      </div>
    </Modal>
  );
}

function Counter({ label, value, tone }: { label: string; value: number; tone: 'ready' | 'invalid' | 'total' }) {
  const toneClass = tone === 'ready' ? 'border-emerald-200 bg-emerald-50 text-emerald-900' : tone === 'invalid' ? 'border-red-200 bg-red-50 text-red-900' : 'border-slate-200 bg-slate-50 text-slate-800';
  return (
    <div className={`rounded-xl border px-3 py-2 ${toneClass}`}>
      <div className="text-xs font-semibold">{label}</div>
      <div className="text-2xl font-bold">{value}</div>
    </div>
  );
}
