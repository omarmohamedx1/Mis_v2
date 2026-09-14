import { Upload } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Button } from '../../components/common/Button';
import { Modal } from '../../components/common/Modal';
import { StatusBadge } from '../../components/common/StatusBadge';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { useToast } from '../../components/common/Toast';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankDistributionImportMapping, BankDistributionImportPreview, BankDistributionImportResult, BankDistributionImportUpload, DistributionCollector } from '../../features/collections/types/collections';
import { getApiErrorMessage } from '../../services/apiClient';

const fields = [
  ['CaseNumber', 'Case Number', 'رقم الحالة', /^(case.?number|case.?id|رقم.?الحالة)$/i],
  ['AccountReference', 'Account Reference', 'رقم الحساب', /^(account|account.?no|account.?reference|account.?number|رقم.?الحساب)$/i],
  ['ContractNumber', 'Contract Number', 'رقم العقد', /^(contract|contract.?no|contract.?number|رقم.?العقد)$/i],
  ['CustomerCode', 'Customer Code', 'كود العميل', /^(customer.?code|customer.?id|كود.?العميل)$/i],
  ['CustomerName', 'Customer Name', 'اسم العميل', /^(customer.?name|name|اسم.?العميل)$/i],
  ['CollectorEmployeeNumber', 'Collector Employee Number', 'رقم الموظف', /^(employee.?number|collector.?id|collector.?employee|رقم.?الموظف)$/i],
  ['CollectorUsername', 'Collector Username', 'اسم المستخدم', /^(username|user.?name|اسم.?المستخدم)$/i],
  ['CollectorEmail', 'Collector Email', 'البريد', /^(email|collector.?email|البريد)$/i],
  ['CollectorName', 'Collector Name', 'اسم المحصل', /^(collector.?name|collector|اسم.?المحصل|المحصل)$/i],
] as const;

type Props = { bankId: string; open: boolean; onClose: () => void; onCompleted: (result: BankDistributionImportResult) => void };

export function BankDistributionUploadModal({ bankId, open, onClose, onCompleted }: Props) {
  const { language, ct } = useCollectionsLocalization();
  const toast = useToast();
  const inputRef = useRef<HTMLInputElement>(null);
  const [step, setStep] = useState(0);
  const [busy, setBusy] = useState(false);
  const [mode, setMode] = useState<'FILE' | 'AUTO'>('FILE');
  const [upload, setUpload] = useState<BankDistributionImportUpload | null>(null);
  const [sheetIndex, setSheetIndex] = useState(0);
  const [mapping, setMapping] = useState<BankDistributionImportMapping | null>(null);
  const [collectors, setCollectors] = useState<DistributionCollector[]>([]);
  const [selectedCollectors, setSelectedCollectors] = useState<string[]>([]);
  const [reassignExisting, setReassignExisting] = useState(false);
  const [preview, setPreview] = useState<BankDistributionImportPreview | null>(null);
  const [result, setResult] = useState<BankDistributionImportResult | null>(null);
  const ar = language === 'ar';

  useEffect(() => {
    if (!open) {
      setStep(0); setMode('FILE'); setUpload(null); setMapping(null); setPreview(null); setResult(null);
      setSelectedCollectors([]); setReassignExisting(false); setSheetIndex(0);
      if (inputRef.current) inputRef.current.value = '';
      return;
    }
    void collectionsService.distributionImportCollectors(bankId).then(setCollectors).catch(() => setCollectors([]));
  }, [bankId, open]);

  function buildMapping(data: BankDistributionImportUpload, index: number): BankDistributionImportMapping {
    const sheet = data.sheets[index];
    return {
      sheetName: sheet.sheetName,
      headerRow: sheet.suggestedHeaderRowNumber,
      firstDataRow: sheet.suggestedHeaderRowNumber + 1,
      mode,
      reassignExisting,
      reason: 'Bulk distribution import',
      collectorIds: mode === 'AUTO' ? selectedCollectors : null,
      columns: Object.fromEntries(fields.map(([key, , , pattern]) => [key, sheet.detectedColumns.find((column) => pattern.test(column.trim())) ?? ''])),
    };
  }

  async function onUpload(file?: File) {
    if (!file) return;
    setBusy(true);
    try {
      const data = await collectionsService.uploadDistributionImport(bankId, file);
      setUpload(data); setSheetIndex(0); setMapping(buildMapping(data, 0)); setStep(1);
    } catch (error) {
      toast.error(getApiErrorMessage(error, ct('distributionActionError')));
    } finally {
      setBusy(false);
      if (inputRef.current) inputRef.current.value = '';
    }
  }

  async function runPreview() {
    if (!upload || !mapping) return;
    setBusy(true);
    try {
      const next = { ...mapping, mode, reassignExisting, collectorIds: mode === 'AUTO' ? selectedCollectors : null };
      setPreview(await collectionsService.previewDistributionImport(bankId, upload.id, next));
      setMapping(next);
      setStep(2);
    } catch (error) {
      toast.error(getApiErrorMessage(error, ct('distributionActionError')));
    } finally {
      setBusy(false);
    }
  }

  async function confirm() {
    if (!upload || !preview) return;
    setBusy(true);
    try {
      const confirmed = await collectionsService.confirmDistributionImport(bankId, upload.id, preview.previewId, reassignExisting);
      setResult(confirmed); setStep(3); onCompleted(confirmed);
    } catch (error) {
      toast.error(getApiErrorMessage(error, ct('distributionActionError')));
    } finally {
      setBusy(false);
    }
  }

  const tone = (status: string) => status === 'Ready' ? 'success' : status === 'AlreadyAssigned' ? 'warning' : 'danger';

  return (
    <Modal open={open} onClose={() => !busy && onClose()} size="xl" title={ct('uploadDistributionFile')}
      footer={step === 1 ? <>
        <Button disabled={busy} fullWidth={false} onClick={() => setStep(0)} variant="outline">{ct('back')}</Button>
        <Button disabled={busy || (mode === 'AUTO' && !selectedCollectors.length)} fullWidth={false} isLoading={busy} onClick={() => void runPreview()}>{ct('preview')}</Button>
      </> : step === 2 ? <>
        <Button disabled={busy} fullWidth={false} onClick={() => setStep(1)} variant="outline">{ct('back')}</Button>
        <Button disabled={busy} fullWidth={false} isLoading={busy} onClick={() => void confirm()}>{ct('confirmDistribution')}</Button>
      </> : step === 3 ? <Button fullWidth={false} onClick={onClose}>{ct('cancel')}</Button> : undefined}
    >
      {step === 0 ? (
        <div className="space-y-4">
          <fieldset className="space-y-2">
            <legend className="text-sm font-semibold">{ct('distributionMethod')}</legend>
            <label className="flex items-center gap-3 rounded-xl border border-mis-border p-3"><input checked={mode === 'FILE'} name="mode" onChange={() => setMode('FILE')} type="radio" /><span><strong className="block text-sm">{ct('assignFromFile')}</strong><small className="text-slate-500">{ct('assignFromFileHelp')}</small></span></label>
            <label className="flex items-center gap-3 rounded-xl border border-mis-border p-3"><input checked={mode === 'AUTO'} name="mode" onChange={() => setMode('AUTO')} type="radio" /><span><strong className="block text-sm">{ct('autoDistribute')}</strong><small className="text-slate-500">{ct('autoDistributeHelp')}</small></span></label>
          </fieldset>
          <p className="text-sm text-slate-500">{ct('supportedPortfolioFiles')}</p>
          <Button fullWidth={false} isLoading={busy} leftIcon={<Upload className="h-4 w-4" />} onClick={() => inputRef.current?.click()}>{ct('uploadDistributionFile')}</Button>
          <input ref={inputRef} accept=".xlsx,.xls,.csv" className="sr-only" onChange={(event) => void onUpload(event.target.files?.[0])} type="file" />
        </div>
      ) : null}

      {step === 1 && upload && mapping ? (
        <div className="space-y-4">
          {upload.sheets.length > 1 ? <label className="block text-sm font-semibold">{ct('sheet')}<ProfessionalSelect className="field mt-2" value={sheetIndex} onChange={(event) => { const index = Number(event.target.value); setSheetIndex(index); setMapping(buildMapping(upload, index)); }}>{upload.sheets.map((sheet, index) => <option key={sheet.sheetName ?? index} value={index}>{sheet.sheetName || `Sheet ${index + 1}`}</option>)}</ProfessionalSelect></label> : null}
          <div className="overflow-x-auto rounded-xl border border-mis-border">
            <table className="min-w-full text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr><th className="px-3 py-2 text-start">{ct('misField')}</th><th className="px-3 py-2 text-start">{ct('excelColumn')}</th></tr></thead>
              <tbody className="divide-y divide-mis-border">
                {fields.filter(([key]) => mode === 'FILE' || !key.startsWith('Collector')).map(([key, en, arLabel]) => (
                  <tr key={key}>
                    <td className="px-3 py-2 font-semibold">{ar ? arLabel : en}</td>
                    <td className="px-3 py-2">
                      <ProfessionalSelect className="field" value={mapping.columns[key] ?? ''} onChange={(event) => setMapping({ ...mapping, columns: { ...mapping.columns, [key]: event.target.value } })}>
                        <option value="">—</option>
                        {upload.sheets[sheetIndex].detectedColumns.map((column) => <option key={column} value={column}>{column}</option>)}
                      </ProfessionalSelect>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {mode === 'AUTO' ? (
            <fieldset>
              <legend className="mb-2 text-sm font-semibold">{ct('collectorsLabel')}</legend>
              <div className="grid gap-2 sm:grid-cols-2">
                {collectors.map((collector) => (
                  <label className="flex items-center gap-3 rounded-xl border border-mis-border p-3" key={collector.id}>
                    <input checked={selectedCollectors.includes(collector.id)} onChange={(event) => setSelectedCollectors((value) => event.target.checked ? [...value, collector.id] : value.filter((id) => id !== collector.id))} type="checkbox" />
                    <span className="text-sm font-semibold">{collector.name}</span>
                  </label>
                ))}
              </div>
            </fieldset>
          ) : null}
          <label className="flex items-center gap-2 text-sm font-semibold"><input checked={reassignExisting} onChange={(event) => setReassignExisting(event.target.checked)} type="checkbox" />{ct('reassignExistingOption')}</label>
        </div>
      ) : null}

      {step === 2 && preview ? (
        <div className="space-y-4">
          <div className="flex flex-wrap gap-3 text-sm">
            <StatusBadge tone="success">{ct('Ready')}: {preview.readyRows}</StatusBadge>
            <StatusBadge tone="warning">{ct('AlreadyAssigned')}: {preview.alreadyAssignedRows}</StatusBadge>
            <StatusBadge tone="danger">{ct('Invalid')}: {preview.invalidRows}</StatusBadge>
          </div>
          {preview.mode === 'AUTO' && preview.autoPlan.length ? (
            <div className="overflow-x-auto rounded-xl border border-mis-border">
              <table className="min-w-full text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr><th className="px-3 py-2 text-start">{ct('collector')}</th><th className="px-3 py-2 text-start">{ct('casesCount')}</th></tr></thead>
                <tbody className="divide-y divide-mis-border">{preview.autoPlan.map((item) => <tr key={item.collectorId}><td className="px-3 py-2">{item.collectorName}</td><td className="px-3 py-2">{item.caseCount}</td></tr>)}</tbody>
              </table>
            </div>
          ) : null}
          <div className="max-h-80 overflow-auto rounded-xl border border-mis-border">
            <table className="min-w-full text-sm">
              <thead className="sticky top-0 bg-slate-50 text-xs uppercase text-slate-500">
                <tr>{[ct('caseId'), ct('customerName'), ct('currentCollector'), ct('newCollector'), ct('status')].map((label) => <th className="px-3 py-2 text-start" key={label}>{label}</th>)}</tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {preview.rows.slice(0, 200).map((row) => (
                  <tr key={row.row}>
                    <td className="px-3 py-2" data-bidi="ltr">{row.caseNumber ?? row.accountReference ?? '—'}</td>
                    <td className="px-3 py-2">{row.customerName ?? '—'}</td>
                    <td className="px-3 py-2">{row.currentCollectorName ?? '—'}</td>
                    <td className="px-3 py-2">{row.newCollectorName ?? '—'}</td>
                    <td className="px-3 py-2"><StatusBadge tone={tone(row.status)}>{ct(row.status as never) || row.status}</StatusBadge></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}

      {step === 3 && result ? (
        <div className="grid gap-3 sm:grid-cols-4">
          <Summary label={ct('assignedCount')} value={result.assigned} />
          <Summary label={ct('reassignedCount')} value={result.reassigned} />
          <Summary label={ct('skippedCount')} value={result.skipped} />
          <Summary label={ct('failedCount')} value={result.failed} />
        </div>
      ) : null}
    </Modal>
  );
}

function Summary({ label, value }: { label: string; value: number }) {
  return <div className="rounded-xl border border-mis-border bg-slate-50 p-4"><p className="text-xs font-semibold uppercase text-slate-500">{label}</p><p className="mt-1 text-xl font-bold text-mis-navy">{value}</p></div>;
}
