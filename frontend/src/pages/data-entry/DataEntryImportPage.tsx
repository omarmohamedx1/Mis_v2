import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { PageHeader } from '../../components/common/PageHeader';
import { useToast } from '../../components/common/Toast';
import { SelectInput } from '../../components/forms/SelectInput';
import { useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type {
  DataEntryImportMappingRequest,
  DataEntryImportPreview,
  DataEntryImportUpload,
  DataEntryOrganization,
  DataEntryPortfolio,
  DataEntrySheetPreview,
} from '../../features/data-entry/types/dataEntry';
import {
  ExcelDropzone,
  autoMapImportColumns,
  dataEntryImportCatalog,
  defaultSelectedSheetIndexes,
  importCopy,
  sheetNamesFromIndexes,
} from '../../features/import';
import { getApiErrorMessage } from '../../services/apiClient';

function mappingFor(
  upload: DataEntryImportUpload,
  indexes: number[],
  organizationId: string,
  portfolio: DataEntryPortfolio | undefined,
): DataEntryImportMappingRequest {
  const selected = indexes.length ? indexes : [0];
  const sheet = upload.sheets[selected[0]];
  const columns = [...new Set(selected.flatMap((index) => upload.sheets[index]?.detectedColumns ?? []))];
  return {
    organizationId,
    portfolioId: portfolio?.id ?? null,
    primaryClassification: portfolio?.primaryClassification ?? null,
    subClassification: portfolio?.subClassification ?? null,
    sheetName: sheet?.sheetName ?? '',
    headerRow: sheet?.suggestedHeaderRowNumber ?? 1,
    firstDataRow: (sheet?.suggestedHeaderRowNumber ?? 1) + 1,
    columns: Object.fromEntries(
      Object.entries(autoMapImportColumns(dataEntryImportCatalog, columns)).map(([key, value]) => [key, value || null]),
    ),
    sheetNames: sheetNamesFromIndexes(upload.sheets, selected),
  };
}

export function DataEntryImportPage() {
  const d = useDataEntryText();
  const toast = useToast();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [organizations, setOrganizations] = useState<DataEntryOrganization[]>([]);
  const [portfolios, setPortfolios] = useState<DataEntryPortfolio[]>([]);
  const [organizationId, setOrganizationId] = useState('');
  const [portfolioId, setPortfolioId] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [upload, setUpload] = useState<DataEntryImportUpload | null>(null);
  const [sheet, setSheet] = useState<DataEntrySheetPreview | null>(null);
  const [activeSheet, setActiveSheet] = useState('');
  const [mapping, setMapping] = useState<DataEntryImportMappingRequest | null>(null);
  const [preview, setPreview] = useState<DataEntryImportPreview | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);

  useEffect(() => {
    dataEntryService.organizations().then((items) => {
      setOrganizations(items);
      if (items[0]) setOrganizationId(items[0].id);
    }).catch(() => setOrganizations([]));
  }, []);

  useEffect(() => {
    if (!organizationId) {
      setPortfolios([]);
      setPortfolioId('');
      return;
    }
    dataEntryService.portfolios(organizationId).then((items) => {
      setPortfolios(items);
      setPortfolioId(items[0]?.id ?? '');
    }).catch(() => {
      setPortfolios([]);
      setPortfolioId('');
    });
  }, [organizationId]);

  const portfolio = portfolios.find((item) => item.id === portfolioId) ?? portfolios[0];

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError('');
    try {
      await action();
    } catch (reason) {
      setError(getApiErrorMessage(reason, d.text('تعذر قراءة الملف', 'Could not read the file')));
    } finally {
      setBusy(false);
    }
  }

  async function receiveFile(next: File | null) {
    setFile(next);
    setUpload(null);
    setSheet(null);
    setMapping(null);
    setPreview(null);
    setActiveSheet('');
    if (!next) return;
    await run(async () => {
      const data = await dataEntryService.uploadImport(next);
      const indexes = defaultSelectedSheetIndexes(data.sheets);
      const first = data.sheets[indexes[0] ?? 0];
      setUpload(data);
      setMapping(mappingFor(data, indexes, organizationId, portfolio));
      setActiveSheet(first?.sheetName ?? '');
      setSheet(await dataEntryService.sheetPreview(data.uploadId, first?.sheetName));
    });
  }

  const detectedColumns = upload ? [...new Set(upload.sheets.flatMap((item) => item.detectedColumns ?? []))] : [];
  const nameMissing = Boolean(mapping && !mapping.columns.CustomerName);
  const readyCount = preview?.rows.filter((row) => row.status === 'READY' || row.status === 'EXISTING_CUSTOMER').length ?? 0;
  const portfolioLabel = (item: DataEntryPortfolio) => (d.ar ? item.nameArabic : item.nameEnglish) || item.code;
  const orgLabel = (item: DataEntryOrganization) => (d.ar ? item.nameArabic : item.nameEnglish) || item.code;

  return (
    <div className="space-y-5">
      <PageHeader
        title={d.text('رفع العملاء', 'Upload clients')}
        description={d.text('اختار ملف الإكسل. البيانات تظهر فورًا، والأعمدة بتتعرّف لوحدها.', 'Choose the spreadsheet. The rows appear at once, and the columns are recognized on their own.')}
      />
      {error ? <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</div> : null}

      <Card className="space-y-4 p-6">
        {organizations.length > 1 ? (
          <SelectInput label={d.text('الجهة', 'Organization')} value={organizationId} onChange={(event) => setOrganizationId(event.target.value)}>
            {organizations.map((item) => <option key={item.id} value={item.id}>{orgLabel(item)}</option>)}
          </SelectInput>
        ) : null}
        {portfolios.length > 1 ? (
          <SelectInput label={d.text('المحفظة', 'Portfolio')} value={portfolioId} onChange={(event) => setPortfolioId(event.target.value)}>
            {portfolios.map((item) => <option key={item.id} value={item.id}>{portfolioLabel(item)}</option>)}
          </SelectInput>
        ) : null}
        <ExcelDropzone
          arabic={d.ar}
          busy={busy}
          file={file}
          label={d.text('ملف العملاء', 'Client file')}
          onFile={(next) => void receiveFile(next)}
        />
        {upload && upload.sheets.length > 1 ? (
          <div className="flex flex-wrap gap-2">
            {upload.sheets.map((item) => {
              const name = item.sheetName || 'Sheet1';
              const active = activeSheet === item.sheetName;
              return (
                <button
                  className={`rounded-full px-3 py-1 text-xs font-bold ${active ? 'bg-mis-primary text-white' : 'bg-slate-100 text-slate-600'}`}
                  key={name}
                  type="button"
                  onClick={() => void run(async () => {
                    setActiveSheet(item.sheetName);
                    const index = upload.sheets.findIndex((sheetItem) => sheetItem.sheetName === item.sheetName);
                    setMapping(mappingFor(upload, [index < 0 ? 0 : index], organizationId, portfolio));
                    setSheet(await dataEntryService.sheetPreview(upload.uploadId, item.sheetName));
                  })}
                >
                  {name}
                </button>
              );
            })}
          </div>
        ) : null}
        {sheet ? <SheetGrid count={d.number} rowsLabel={d.text('صف', 'rows')} sheet={sheet} /> : null}
        {nameMissing && mapping ? (
          <SelectInput
            label={d.text('عمود اسم العميل', 'Customer name column')}
            value={mapping.columns.CustomerName ?? ''}
            onChange={(event) => setMapping({ ...mapping, columns: { ...mapping.columns, CustomerName: event.target.value || null } })}
          >
            <option value="">{d.text('اختار العمود', 'Choose the column')}</option>
            {detectedColumns.map((column) => <option key={column} value={column}>{column}</option>)}
          </SelectInput>
        ) : null}
        {upload && mapping ? (
          <Button
            disabled={busy || !organizationId || !portfolio || nameMissing}
            fullWidth={false}
            isLoading={busy}
            onClick={() => void run(async () => {
              const payload: DataEntryImportMappingRequest = {
                ...mapping,
                organizationId,
                portfolioId: portfolio?.id ?? null,
                primaryClassification: portfolio?.primaryClassification ?? null,
                subClassification: portfolio?.subClassification ?? null,
              };
              const next = await dataEntryService.previewImport(upload.uploadId, payload);
              setMapping(payload);
              setPreview(next);
              const count = next.rows.filter((row) => row.status === 'READY' || row.status === 'EXISTING_CUSTOMER').length;
              if (count < 1) {
                setError(d.text('الملف اتقرا، ومفيش صف جاهز للحفظ. تأكد إن فيه عمود اسم.', 'The file was read, and no row is ready to save. Check that a name column exists.'));
                return;
              }
              setConfirmOpen(true);
            })}
          >
            {d.text('حفظ العملاء', 'Save clients')}
          </Button>
        ) : null}
      </Card>

      <ConfirmDialog
        confirmLabel={importCopy.confirmAction(d.ar, readyCount)}
        confirmVariant="primary"
        isConfirming={busy}
        message={d.text(`هيتحفظ ${readyCount} عميل من الملف كما هو.`, `${readyCount} clients from the file will be saved as they are.`)}
        onCancel={() => { if (!busy) setConfirmOpen(false); }}
        onConfirm={() => void run(async () => {
          if (!preview) return;
          await dataEntryService.confirmImport({ uploadId: preview.uploadId, previewId: preview.previewId, excludedRowNumbers: [] });
          setConfirmOpen(false);
          toast.success(d.text('تم حفظ العملاء', 'Clients saved'));
          navigate('/data-entry/files');
        })}
        open={confirmOpen}
        title={d.text('حفظ العملاء', 'Save clients')}
      />
    </div>
  );
}

function SheetGrid({ sheet, rowsLabel, count }: { sheet: DataEntrySheetPreview; rowsLabel: string; count: (value: number) => string }) {
  return (
    <div className="space-y-2">
      <p className="text-sm font-semibold text-slate-600">
        <span dir="ltr">{sheet.fileName}</span>
        {' · '}
        {count(sheet.totalRows)} {rowsLabel}
      </p>
      <div className="max-h-[32rem] overflow-auto rounded-xl border border-mis-border">
        <table className="min-w-full text-sm">
          <thead className="sticky top-0 z-10 bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-3 py-2 text-start">#</th>
              {sheet.columns.map((column, index) => (
                <th className="whitespace-nowrap px-3 py-2 text-start" key={`${column}-${index}`}>{column || '—'}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-mis-border bg-white">
            {sheet.rows.map((row, rowIndex) => (
              <tr key={rowIndex}>
                <td className="px-3 py-2 text-slate-400">{rowIndex + 1}</td>
                {sheet.columns.map((_, columnIndex) => (
                  <td className="whitespace-nowrap px-3 py-2 text-mis-navy" key={columnIndex}>{row[columnIndex] || '—'}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
