import { ProfessionalSelect } from '../../../components/forms/ProfessionalSelect';
import { useState } from 'react';
import { Button } from '../../../components/common/Button';
import { Modal } from '../../../components/common/Modal';
import { useToast } from '../../../components/common/Toast';
import { ExcelDropzone, autoMapVisitImportColumns, pickBestImportSheetIndex, visitImportCatalog } from '../../import';
import { getApiErrorMessage } from '../../../services/apiClient';
import { useCollectionsLocalization } from '../localization/collectionsTranslations';
import { visitImportService, type VisitImportMapping, type VisitImportPreview, type VisitImportResult, type VisitImportUpload } from '../services/visitImportService';

type Props = { bankId: string; open: boolean; onClose: () => void; onCompleted: () => void };

const requiredKeys = ['CaseNumber', 'Collector', 'VisitDate', 'VisitTime'];

function mappingFor(upload: VisitImportUpload, index: number): VisitImportMapping {
  const sheet = upload.sheets[index];
  const headerRow = sheet?.suggestedHeaderRowNumber || 1;
  return {
    sheetName: sheet?.sheetName ?? null,
    headerRow,
    firstDataRow: headerRow + 1,
    columns: autoMapVisitImportColumns(visitImportCatalog, sheet?.detectedColumns ?? []),
    sheetNames: sheet?.sheetName ? [sheet.sheetName] : undefined,
  };
}

function mapped(mapping: VisitImportMapping | null): boolean {
  return requiredKeys.every((key) => mapping?.columns[key]?.trim());
}

export function VisitImportModal({ bankId, open, onClose, onCompleted }: Props) {
  const { language, ct } = useCollectionsLocalization();
  const arabic = language === 'ar';
  const toast = useToast();
  const text = (en: string, ar: string) => (arabic ? ar : en);
  const [file, setFile] = useState<File | null>(null);
  const [upload, setUpload] = useState<VisitImportUpload | null>(null);
  const [sheetIndex, setSheetIndex] = useState(0);
  const [mapping, setMapping] = useState<VisitImportMapping | null>(null);
  const [showMapping, setShowMapping] = useState(false);
  const [preview, setPreview] = useState<VisitImportPreview | null>(null);
  const [result, setResult] = useState<VisitImportResult | null>(null);
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

  async function previewUpload(nextUpload: VisitImportUpload, nextMapping: VisitImportMapping) {
    setBusy(true);
    setError('');
    try {
      setPreview(await visitImportService.preview(bankId, nextUpload.id, nextMapping));
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
      const uploaded = await visitImportService.upload(bankId, next);
      const index = pickBestImportSheetIndex(visitImportCatalog, uploaded.sheets);
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
      const imported = await visitImportService.confirm(bankId, upload.id, preview.previewId);
      setResult(imported);
      onCompleted();
      toast.success(ct('visitImportSaved'));
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

  return (
    <Modal
      open={open}
      size="xl"
      title={ct('importExcel')}
      description={text('Valid rows are created with the same rules as a manual visit.', 'الصفوف الصالحة تُنشأ بنفس قواعد الزيارة اليدوية.')}
      onClose={() => { if (!busy) { reset(); onClose(); } }}
      footer={
        <div className="flex flex-wrap justify-end gap-2">
          <Button fullWidth={false} variant="outline" disabled={busy} onClick={() => { reset(); onClose(); }} type="button">{text('Close', 'إغلاق')}</Button>
          {preview && !result ? <Button fullWidth={false} disabled={busy || ready === 0} isLoading={busy} onClick={() => void confirm()} type="button">{ct('confirmImport')}</Button> : null}
        </div>
      }
    >
      <div className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-sm text-slate-600">{ct('visitImportHelp')}</p>
          <Button fullWidth={false} variant="secondary" disabled={busy} onClick={() => void visitImportService.downloadTemplate(bankId).catch((reason) => toast.error(getApiErrorMessage(reason, text('The template could not be downloaded.', 'تعذر تنزيل النموذج.'))))} type="button">{ct('downloadExcelTemplate')}</Button>
        </div>
        <ExcelDropzone arabic={arabic} busy={busy} file={file} onFile={(next) => void onFile(next)} />
        {error ? <p className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">{error}</p> : null}
        {upload && upload.sheets.length > 1 ? (
          <label className="block text-sm font-semibold text-slate-700">
            {text('Sheet', 'الورقة')}
            <ProfessionalSelect className="mt-1 w-full rounded-xl border border-slate-300 px-3 py-2" value={sheetIndex} disabled={busy} onChange={(event) => {
              const index = Number(event.target.value);
              const nextMapping = mappingFor(upload, index);
              setSheetIndex(index);
              setMapping(nextMapping);
              setPreview(null);
              setResult(null);
              setShowMapping(!mapped(nextMapping));
              if (mapped(nextMapping)) void previewUpload(upload, nextMapping);
            }}>
              {upload.sheets.map((sheet, index) => <option key={`${sheet.sheetName}-${index}`} value={index}>{sheet.sheetName || index + 1}</option>)}
            </ProfessionalSelect>
          </label>
        ) : null}
        {mapping && (showMapping || !mapped(mapping)) ? (
          <div className="grid gap-3 sm:grid-cols-2">
            {visitImportCatalog.map((field) => (
              <label key={field.key} className="block text-sm font-semibold text-slate-700">
                <span>{arabic ? field.ar : field.en}{field.required ? ' *' : ''}</span>
                <span className="mt-0.5 block text-xs font-medium text-slate-500">{arabic ? field.en : field.ar}</span>
                <ProfessionalSelect className="mt-1 w-full rounded-xl border border-slate-300 px-3 py-2 font-normal" value={mapping.columns[field.key] ?? ''} disabled={busy} onChange={(event) => setMapping({ ...mapping, columns: { ...mapping.columns, [field.key]: event.target.value } })}>
                  <option value="">{text('Not mapped', 'غير مربوط')}</option>
                  {columns.map((column) => <option key={column} value={column}>{column}</option>)}
                </ProfessionalSelect>
              </label>
            ))}
            <div className="sm:col-span-2">
              <Button fullWidth={false} disabled={busy || !upload || !mapped(mapping)} isLoading={busy} onClick={() => upload && mapping && void previewUpload(upload, mapping)} type="button">{text('Validate rows', 'التحقق من الصفوف')}</Button>
            </div>
          </div>
        ) : null}
        {preview ? (
          <>
            <div className="grid gap-3 sm:grid-cols-3">
              <Counter label={ct('visitReadyCount')} value={ready} tone="ready" />
              <Counter label={ct('invalidRows')} value={invalid} tone="invalid" />
              <Counter label={ct('totalRows')} value={rows.length} tone="total" />
            </div>
            <div className="max-h-96 overflow-auto rounded-xl border border-slate-200">
              <table className="min-w-full text-sm">
                <thead className="sticky top-0 bg-slate-50 text-slate-700">
                  <tr>
                    {[ct('caseId'), ct('customerName'), ct('collector'), ct('visitDate'), ct('visitTime'), ct('address'), ct('status'), ct('validationError')].map((heading) => (
                      <th key={heading} className="px-3 py-2 text-start font-semibold">{heading}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={row.row} className="border-t border-slate-100">
                      <td className="px-3 py-2">{row.caseNumber || '—'}</td>
                      <td className="px-3 py-2">{row.customerName || '—'}</td>
                      <td className="px-3 py-2">{row.collectorName || '—'}</td>
                      <td className="px-3 py-2">{row.visitDate || '—'}</td>
                      <td className="px-3 py-2">{row.visitTime || '—'}</td>
                      <td className="px-3 py-2">{row.address || '—'}</td>
                      <td className="px-3 py-2">{row.status === 'Ready' ? ct('Ready') : ct('Error')}</td>
                      <td className="px-3 py-2 text-red-700">{row.errors.join(' ')}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        ) : null}
        {result ? <p className="rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-900">{text(`${result.imported} imported, ${result.skipped} skipped.`, `تم استيراد ${result.imported} وتجاوز ${result.skipped}.`)}</p> : null}
      </div>
    </Modal>
  );
}

function Counter({ label, value, tone }: { label: string; value: number; tone: 'ready' | 'invalid' | 'total' }) {
  const toneClass = tone === 'ready' ? 'border-emerald-200 bg-emerald-50 text-emerald-900' : tone === 'invalid' ? 'border-red-200 bg-red-50 text-red-900' : 'border-slate-200 bg-slate-50 text-slate-800';
  return <div className={`rounded-xl border px-3 py-2 ${toneClass}`}><div className="text-xs font-semibold">{label}</div><div className="text-2xl font-bold">{value}</div></div>;
}
