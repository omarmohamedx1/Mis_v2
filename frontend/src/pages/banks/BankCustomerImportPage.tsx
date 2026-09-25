import { useEffect, useState } from 'react';
import { Link, useOutletContext } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge } from '../../components/common/StatusBadge';
import { SelectInput } from '../../components/forms/SelectInput';
import { ExcelColumnReview, ExcelDropzone, ExcelPreviewToolbar, ExcelSheetPicker, autoMapImportColumns, bankCustomerImportCatalog, defaultSelectedSheetIndexes, importCopy, rememberExtraColumns, sheetNamesFromIndexes } from '../../features/import';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankCustomerImportMapping, BankCustomerImportPreview, BankCustomerImportResult, BankCustomerImportUpload, PortfolioLookup } from '../../features/collections/types/collections';
import { getApiErrorMessage } from '../../services/apiClient';
import type { BankWorkspaceContext } from './BankWorkspaceLayout';

const fields = [
  ['CustomerCode', 'Customer Code', 'كود العميل', /^(customer.?code|customer.?id|كود.?العميل)$/i],
  ['CustomerName', 'Customer Name', 'اسم العميل', /^(name|customer.?name|client.?name|اسم.?العميل)$/i],
  ['MobileNumber', 'Mobile Number', 'رقم الموبايل', /^(mobile|phone|phone.?number|رقم.?الموبايل|رقم.?الهاتف)$/i],
  ['NationalId', 'National ID', 'الرقم القومي', /^(national.?id|id.?number|الرقم.?القومي)$/i],
  ['Address', 'Address', 'العنوان', /^(address|العنوان)$/i],
  ['AccountNumber', 'Account Number', 'رقم الحساب', /^(account|account.?no|account.?number|رقم.?الحساب)$/i],
  ['ContractNumber', 'Contract Number', 'رقم العقد', /^(contract|contract.?no|contract.?number|رقم.?العقد)$/i],
  ['ProductType', 'Product Type', 'نوع المنتج', /^(product|product.?type|نوع.?المنتج)$/i],
  ['OutstandingAmount', 'Outstanding Amount', 'المديونية', /^(outstanding|outstanding.?amount|المديونية|المبلغ.?المستحق)$/i],
  ['PaidAmount', 'Paid Amount', 'المسدد', /^(paid|paid.?amount|المسدد)$/i],
  ['RemainingAmount', 'Remaining Amount', 'المتبقي', /^(balance|remaining|remaining.?amount|المتبقي)$/i],
  ['OverdueAmount', 'Overdue Amount', 'المتأخر', /^(overdue|overdue.?amount|المتأخر)$/i],
  ['DaysPastDue', 'Days Past Due', 'أيام التأخر', /^(dpd|days.?past.?due|أيام.?التأخر)$/i],
] as const;

function mappingFor(upload: BankCustomerImportUpload, indexes: number[], portfolioId: string): BankCustomerImportMapping {
  const selected = indexes.length ? indexes : [0];
  const sheet = upload.sheets[selected[0]];
  const columns = [...new Set(selected.flatMap((index) => upload.sheets[index]?.detectedColumns ?? []))];
  return {
    sheetName: sheet.sheetName,
    headerRow: sheet.suggestedHeaderRowNumber,
    firstDataRow: sheet.suggestedHeaderRowNumber + 1,
    portfolioId: portfolioId || null,
    columns: autoMapImportColumns(bankCustomerImportCatalog, columns),
    sheetNames: sheetNamesFromIndexes(upload.sheets, selected),
  };
}

export function BankCustomerImportPage() {
  const { bank, workspaceBase } = useOutletContext<BankWorkspaceContext>();
  const { language, ct } = useCollectionsLocalization();
  const ar = language === 'ar';
  const [step, setStep] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [portfolios, setPortfolios] = useState<PortfolioLookup[]>([]);
  const [portfolioId, setPortfolioId] = useState('');
  const [upload, setUpload] = useState<BankCustomerImportUpload | null>(null);
  const [selectedSheets, setSelectedSheets] = useState<number[]>([0]);
  const [mapping, setMapping] = useState<BankCustomerImportMapping | null>(null);
  const [preview, setPreview] = useState<BankCustomerImportPreview | null>(null);
  const [result, setResult] = useState<BankCustomerImportResult | null>(null);
  const [extraColumns, setExtraColumns] = useState<string[]>([]);
  const [excludedRows, setExcludedRows] = useState<number[]>([]);
  const [selectedRows, setSelectedRows] = useState<number[]>([]);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);

  useEffect(() => {
    void collectionsService.bankCustomerImportPortfolios(bank.id).then((items) => {
      setPortfolios(items);
      if (items[0]) setPortfolioId(items[0].id);
    }).catch(() => setPortfolios([]));
  }, [bank.id]);

  async function run(action: () => Promise<void>) {
    setBusy(true); setError('');
    try { await action(); } catch (reason) { setError(getApiErrorMessage(reason, ct('saveError'))); } finally { setBusy(false); }
  }

  async function sendFile() {
    if (!file) return;
    await run(async () => {
      const data = await collectionsService.uploadBankCustomerImport(bank.id, file);
      const indexes = defaultSelectedSheetIndexes(data.sheets);
      const next = mappingFor(data, indexes, portfolioId);
      setUpload(data);
      setMapping(next);
      setSelectedSheets(indexes);
      setExcludedRows([]);
      setSelectedRows([]);
      setExtraColumns([]);
      setShowAdvanced(false);
      if (next.columns.CustomerName) {
        setPreview(await collectionsService.previewBankCustomerImport(bank.id, data.id, { ...next, portfolioId: portfolioId || null }));
        setStep(2);
        return;
      }
      setStep(1);
    });
  }

  const labels = [ct('uploadFile'), ct('columnMapping'), ct('previewValidation'), ct('importComplete')];
  const detectedColumns = upload ? [...new Set(selectedSheets.flatMap((index) => upload.sheets[index]?.detectedColumns ?? []))] : [];
  const readyCount = preview?.rows.filter((row) => row.status === 'Ready' && !excludedRows.includes(row.row)).length ?? 0;

  return (
    <div className="space-y-5">
      <PageHeader
        actions={<Link className="rounded-xl border border-mis-border px-4 py-2 text-sm font-bold text-mis-primary" to={`${workspaceBase}/customers`}>{ct('backToCustomers')}</Link>}
        description={ct('importCustomersSubtitle')}
        title={ct('importCustomersTitle')}
      />
      <div className="flex flex-wrap gap-2">{labels.map((label, index) => <span className={`rounded-full px-3 py-1 text-xs font-bold ${step === index ? 'bg-mis-primary text-white' : 'bg-slate-100 text-slate-500'}`} key={label}>{index + 1}. {label}</span>)}</div>
      {error ? <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</div> : null}

      {step === 0 ? (
        <Card className="space-y-4 p-6">
          <SelectInput label={ct('selectPortfolio')} onChange={(event) => setPortfolioId(event.target.value)} value={portfolioId}>
            {portfolios.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </SelectInput>
          <ExcelDropzone arabic={ar} busy={busy} file={file} label={ct('file')} onFile={setFile} />
          <Button disabled={busy || !file} fullWidth={false} isLoading={busy} onClick={() => void sendFile()}>{ct('uploadPreview')}</Button>
        </Card>
      ) : null}

      {step === 1 && upload && mapping ? (
        <Card className="space-y-4 p-6">
          <ExcelSheetPicker
            arabic={ar}
            onChange={(indexes) => {
              const nextIndexes = indexes.length ? indexes : [0];
              setSelectedSheets(nextIndexes);
              setMapping(mappingFor(upload, nextIndexes, portfolioId));
            }}
            selected={selectedSheets}
            sheets={upload.sheets}
          />
          <ExcelColumnReview
            arabic={ar}
            detectedColumns={detectedColumns}
            extraSelected={extraColumns}
            fields={bankCustomerImportCatalog}
            mapping={mapping.columns}
            onExtraSelected={setExtraColumns}
            onToggleAdvanced={() => setShowAdvanced((value) => !value)}
            showAdvanced={showAdvanced}
          />
          {showAdvanced ? (
          <div className="grid gap-3 md:grid-cols-2">
            {fields.map(([key, enLabel, arLabel]) => (
              <SelectInput
                key={key}
                label={ar ? arLabel : enLabel}
                onChange={(event) => setMapping({ ...mapping, columns: { ...mapping.columns, [key]: event.target.value } })}
                value={mapping.columns[key] ?? ''}
              >
                <option value="">—</option>
                {detectedColumns.map((column) => <option key={column} value={column}>{column}</option>)}
              </SelectInput>
            ))}
          </div>
          ) : null}
          <div className="flex gap-2">
            <Button disabled={busy} fullWidth={false} onClick={() => setStep(0)} variant="outline">{ct('back')}</Button>
            <Button disabled={busy} fullWidth={false} isLoading={busy} onClick={() => void run(async () => {
              setPreview(await collectionsService.previewBankCustomerImport(bank.id, upload.id, { ...mapping, portfolioId: portfolioId || null }));
              setStep(2);
            })}>{ct('preview')}</Button>
          </div>
        </Card>
      ) : null}

      {step === 2 && preview && upload ? (
        <Card className="space-y-4 p-6">
          <ExcelPreviewToolbar
            arabic={ar}
            excludedCount={excludedRows.length}
            onExclude={() => setExcludedRows((current) => [...new Set([...current, ...selectedRows])])}
            onRestore={() => { setExcludedRows((current) => current.filter((row) => !selectedRows.includes(row))); setSelectedRows([]); }}
            readyCount={readyCount}
            selectedCount={selectedRows.length}
          />
          <div className="overflow-x-auto rounded-xl border border-mis-border">
            <table className="min-w-full text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>{[ar ? 'تحديد' : 'Select', ct('customerName'), ct('mobile'), ct('nationalId'), ct('accountContract'), ct('outstandingAmount'), ct('status')].map((label) => <th className="px-3 py-2 text-start" key={label}>{label}</th>)}</tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {preview.rows.slice(0, 100).map((row) => (
                  <tr className={excludedRows.includes(row.row) ? 'bg-slate-100 opacity-60' : ''} key={row.row}>
                    <td className="px-3 py-2"><input checked={selectedRows.includes(row.row)} onChange={() => setSelectedRows((current) => current.includes(row.row) ? current.filter((item) => item !== row.row) : [...current, row.row])} type="checkbox" /></td>
                    <td className="px-3 py-2">{row.customerName}</td>
                    <td className="px-3 py-2" data-bidi="ltr">{row.mobile ?? '—'}</td>
                    <td className="px-3 py-2" data-bidi="ltr">{row.nationalId ?? '—'}</td>
                    <td className="px-3 py-2" data-bidi="ltr">{row.accountReference ?? row.contractReference ?? '—'}</td>
                    <td className="px-3 py-2" data-bidi="ltr">{row.outstanding ?? '—'}</td>
                    <td className="px-3 py-2"><StatusBadge tone={row.status === 'Ready' ? 'success' : row.status === 'Existing' ? 'warning' : 'danger'}>{ct(row.status as never) || row.status}</StatusBadge>{row.errors[0] ? <p className="mt-1 text-xs text-amber-700">{row.errors[0]}</p> : null}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex gap-2">
            <Button disabled={busy} fullWidth={false} onClick={() => setStep(1)} variant="outline">{ct('back')}</Button>
            <Button disabled={busy || readyCount === 0} fullWidth={false} isLoading={busy} onClick={() => setConfirmOpen(true)}>{ct('confirmCustomerImport')}</Button>
          </div>
        </Card>
      ) : null}

      {step === 3 && result ? (
        <Card className="grid gap-4 p-6 sm:grid-cols-3">
          <div><p className="text-sm text-slate-500">{ct('importedCount')}</p><p className="text-3xl font-bold text-mis-navy">{result.imported}</p></div>
          <div><p className="text-sm text-slate-500">{ct('skippedCount')}</p><p className="text-3xl font-bold text-mis-navy">{result.skipped}</p></div>
          <div><p className="text-sm text-slate-500">{ct('failedCount')}</p><p className="text-3xl font-bold text-mis-navy">{result.failed}</p></div>
          <div className="sm:col-span-3"><Link className="inline-flex rounded-xl bg-mis-primary px-4 py-2.5 text-sm font-bold text-white" to={`${workspaceBase}/customers`}>{ct('backToCustomers')}</Link></div>
        </Card>
      ) : null}
      <ConfirmDialog
        confirmLabel={importCopy.confirmAction(ar, readyCount)}
        confirmVariant="primary"
        isConfirming={busy}
        message={importCopy.confirmMessage(ar, readyCount)}
        onCancel={() => { if (!busy) setConfirmOpen(false); }}
        onConfirm={() => void run(async () => {
          if (!preview || !upload) return;
          rememberExtraColumns('bank-customers', extraColumns);
          setResult(await collectionsService.confirmBankCustomerImport(bank.id, upload.id, preview.previewId, excludedRows));
          setConfirmOpen(false);
          setStep(3);
        })}
        open={confirmOpen}
        title={importCopy.confirmTitle(ar)}
      />
    </div>
  );
}
