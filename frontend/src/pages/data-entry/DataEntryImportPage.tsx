import { Download } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { PageHeader } from '../../components/common/PageHeader';
import { useToast } from '../../components/common/Toast';
import { SelectInput } from '../../components/forms/SelectInput';
import { DataEntryDeskFields } from '../../features/data-entry/DataEntryDeskFields';
import { DataEntryRowStatus, useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import {
  DATA_ENTRY_IMPORT_FIELDS,
  type DataEntryImportField,
  type DataEntryImportMappingRequest,
  type DataEntryImportPreview,
  type DataEntryImportUpload,
  type DataEntryOrganization,
  type DataEntryPortfolio,
  type DataEntrySheetPreview,
} from '../../features/data-entry/types/dataEntry';
import {
  ExcelColumnReview,
  ExcelDropzone,
  ExcelPreviewToolbar,
  ExcelSheetPicker,
  autoMapImportColumns,
  dataEntryImportCatalog,
  defaultSelectedSheetIndexes,
  importCopy,
  rememberExtraColumns,
  sheetNamesFromIndexes,
} from '../../features/import';
import { getApiErrorMessage } from '../../services/apiClient';

const fieldMeta: Record<DataEntryImportField, { en: string; ar: string; required?: boolean }> = {
  CustomerCode: { en: 'Customer number', ar: 'رقم العميل' },
  CustomerName: { en: 'Customer name', ar: 'اسم العميل', required: true },
  NationalId: { en: 'National ID', ar: 'الرقم القومي' },
  MobileNumber: { en: 'Mobile number', ar: 'رقم الموبايل' },
  Address: { en: 'Address', ar: 'العنوان' },
  Feedback: { en: 'Feedback', ar: 'فيدباك' },
  Notes: { en: 'Notes', ar: 'ملاحظات' },
  AccountNumber: { en: 'Account number', ar: 'رقم الحساب', required: true },
  ContractNumber: { en: 'Contract number', ar: 'رقم العقد' },
  OutstandingAmount: { en: 'Outstanding amount', ar: 'المديونية' },
  OverdueAmount: { en: 'Overdue amount', ar: 'المتأخر' },
  DaysPastDue: { en: 'Days past due', ar: 'أيام التأخر' },
};

function mappingFor(
  upload: DataEntryImportUpload,
  indexes: number[],
  organizationId: string,
  portfolioId: string,
  primaryClassification: string,
  subClassification: string,
): DataEntryImportMappingRequest {
  const selected = indexes.length ? indexes : [0];
  const sheet = upload.sheets[selected[0]];
  const columns = [...new Set(selected.flatMap((index) => upload.sheets[index]?.detectedColumns ?? []))];
  return {
    organizationId,
    portfolioId: portfolioId || null,
    primaryClassification: primaryClassification || null,
    subClassification: subClassification || null,
    sheetName: sheet?.sheetName ?? '',
    headerRow: sheet?.suggestedHeaderRowNumber ?? 1,
    firstDataRow: (sheet?.suggestedHeaderRowNumber ?? 1) + 1,
    columns: Object.fromEntries(
      Object.entries(autoMapImportColumns(dataEntryImportCatalog, columns)).map(([key, value]) => [key, value || null]),
    ),
    sheetNames: sheetNamesFromIndexes(upload.sheets, selected),
  };
}

function downloadTemplate(arabic: boolean) {
  const headers = DATA_ENTRY_IMPORT_FIELDS.map((key) => (arabic ? fieldMeta[key].ar : fieldMeta[key].en));
  const blob = new Blob([`${headers.join(',')}\n`], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = arabic ? 'قالب-ادخال-البيانات.csv' : 'data-entry-template.csv';
  link.click();
  URL.revokeObjectURL(url);
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
  const [primaryClassification, setPrimaryClassification] = useState('');
  const [subClassification, setSubClassification] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [upload, setUpload] = useState<DataEntryImportUpload | null>(null);
  const [sheet, setSheet] = useState<DataEntrySheetPreview | null>(null);
  const [activeSheet, setActiveSheet] = useState('');
  const [selectedSheets, setSelectedSheets] = useState<number[]>([0]);
  const [mapping, setMapping] = useState<DataEntryImportMappingRequest | null>(null);
  const [preview, setPreview] = useState<DataEntryImportPreview | null>(null);
  const [onlyInvalid, setOnlyInvalid] = useState(false);
  const [extraColumns, setExtraColumns] = useState<string[]>([]);
  const [excludedRows, setExcludedRows] = useState<number[]>([]);
  const [selectedRows, setSelectedRows] = useState<number[]>([]);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);

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
      setPortfolioId('');
    }).catch(() => {
      setPortfolios([]);
      setPortfolioId('');
    });
  }, [organizationId]);

  useEffect(() => {
    const selected = portfolios.find((item) =>
      item.primaryClassification === primaryClassification && item.subClassification === subClassification);
    setPortfolioId(selected?.id ?? '');
  }, [primaryClassification, portfolios, subClassification]);

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError('');
    try {
      await action();
    } catch (reason) {
      setError(getApiErrorMessage(reason, d.text('تعذر إكمال العملية', 'Could not complete the action')));
    } finally {
      setBusy(false);
    }
  }

  async function receiveFile(next: File | null) {
    setFile(next);
    setUpload(null);
    setSheet(null);
    setPreview(null);
    setMapping(null);
    setActiveSheet('');
    setExcludedRows([]);
    setSelectedRows([]);
    setExtraColumns([]);
    if (!next) return;
    await run(async () => {
      const data = await dataEntryService.uploadImport(next);
      const indexes = defaultSelectedSheetIndexes(data.sheets);
      const first = data.sheets[indexes[0] ?? 0];
      setUpload(data);
      setSelectedSheets(indexes);
      setMapping(mappingFor(data, indexes, organizationId, portfolioId, primaryClassification, subClassification));
      setActiveSheet(first?.sheetName ?? '');
      setSheet(await dataEntryService.sheetPreview(data.uploadId, first?.sheetName));
      setShowAdvanced(false);
    });
  }

  const deskReady = Boolean(organizationId && primaryClassification && subClassification);
  const previewRows = useMemo(() => {
    if (!preview) return [];
    return onlyInvalid ? preview.rows.filter((row) => row.status !== 'READY' && row.status !== 'EXISTING_CUSTOMER') : preview.rows;
  }, [onlyInvalid, preview]);
  const missingRequired = mapping && (!mapping.columns.CustomerName || !mapping.columns.AccountNumber);
  const detectedColumns = upload ? [...new Set(selectedSheets.flatMap((index) => upload.sheets[index]?.detectedColumns ?? []))] : [];
  const readyCount = preview?.rows.filter((row) => (row.status === 'READY' || row.status === 'EXISTING_CUSTOMER') && !excludedRows.includes(row.rowNumber)).length ?? 0;

  return (
    <div className="space-y-5">
      <PageHeader
        title={d.text('رفع العملاء', 'Upload clients')}
        description={d.text('ارفع أي ملف Excel أو CSV. البيانات تظهر فورًا كما هي في الملف، وبعدين تقدر ترسل العملاء للتحصيل. مستندات كل عميل في صفحة منفصلة.', 'Upload any Excel or CSV file. The rows appear immediately as they are in the file, then you can send the clients to collections. Each client’s papers live on a separate page.')}
        actions={
          <Button fullWidth={false} leftIcon={<Download className="h-4 w-4" />} variant="outline" onClick={() => downloadTemplate(d.ar)}>
            {d.text('قالب اختياري', 'Optional template')}
          </Button>
        }
      />
      {error ? <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</div> : null}

      <Card className="space-y-4 p-6">
        <div>
          <p className="text-xs font-bold uppercase tracking-wide text-mis-primary">{d.text('١. ملف العملاء', '1. Client file')}</p>
          <h2 className="mt-1 text-lg font-bold text-mis-navy">{d.text('أي إكسل يتعرض هنا على طول', 'Any spreadsheet is shown here immediately')}</h2>
        </div>
        <ExcelDropzone
          arabic={d.ar}
          busy={busy}
          file={file}
          hint={d.text('الأعمدة مش لازم تطابق قالب معين. الملف يظهر كما رفعته.', 'Columns do not have to match a template. The file is shown as you uploaded it.')}
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
                    setSheet(await dataEntryService.sheetPreview(upload.uploadId, item.sheetName));
                  })}
                >
                  {name}
                </button>
              );
            })}
          </div>
        ) : null}
        {sheet ? <SheetGrid arabicCount={d.number} rowsLabel={d.text('صف', 'rows')} sheet={sheet} truncatedLabel={d.text('ظاهر أول جزء من الملف. باقي الصفوف محفوظة مع الرفع.', 'Showing the first part of the file. The remaining rows stay with the upload.')} /> : null}
      </Card>

      {upload && mapping ? (
        <Card className="space-y-4 p-6">
          <div>
            <p className="text-xs font-bold uppercase tracking-wide text-mis-primary">{d.text('٢. إرسال للتحصيل', '2. Send to collections')}</p>
            <h2 className="mt-1 text-lg font-bold text-mis-navy">{d.text('بعد ما تشوف البيانات، حدّد الجهة وابعت العملاء', 'After you see the data, choose the desk and send the clients')}</h2>
            <p className="mt-1 text-sm text-slate-500">{d.text('مستندات العميل — شهادة الميلاد وأي ورق — بتترفع من صفحة ملفات العملاء، مش من هنا.', 'Client papers — birth certificate and anything else — are uploaded from Client files, not here.')}</p>
          </div>
          <DataEntryDeskFields
            organizationId={organizationId}
            organizations={organizations}
            portfolios={portfolios}
            primaryClassification={primaryClassification}
            subClassification={subClassification}
            onOrganizationChange={(id) => {
              setOrganizationId(id);
              setPrimaryClassification('');
              setSubClassification('');
              setPortfolioId('');
              setPreview(null);
            }}
            onPrimaryChange={(value) => {
              setPrimaryClassification(value);
              setSubClassification('');
              setPreview(null);
            }}
            onSubChange={(value) => {
              setSubClassification(value);
              setPreview(null);
            }}
          />
          {missingRequired ? <p className="rounded-xl bg-amber-50 px-3 py-2 text-sm text-amber-800">{d.text('اربط اسم العميل ورقم الحساب حتى يستطيع التحصيل إنشاء الحالة بعد القبول.', 'Map customer name and account number so collections can create the case after acceptance.')}</p> : null}
          <ExcelSheetPicker
            arabic={d.ar}
            onChange={(indexes) => {
              const nextIndexes = indexes.length ? indexes : [0];
              setSelectedSheets(nextIndexes);
              setMapping(mappingFor(upload, nextIndexes, organizationId, portfolioId, primaryClassification, subClassification));
              setPreview(null);
            }}
            selected={selectedSheets}
            sheets={upload.sheets}
          />
          <ExcelColumnReview
            advanced={(
              <div className="grid gap-3 md:grid-cols-2">
                {DATA_ENTRY_IMPORT_FIELDS.map((key) => (
                  <SelectInput
                    key={key}
                    label={`${d.ar ? fieldMeta[key].ar : fieldMeta[key].en}${fieldMeta[key].required ? ' *' : ''}`}
                    value={mapping.columns[key] ?? ''}
                    onChange={(event) => {
                      setMapping({ ...mapping, columns: { ...mapping.columns, [key]: event.target.value || null } });
                      setPreview(null);
                    }}
                  >
                    <option value="">—</option>
                    {detectedColumns.map((column) => (
                      <option key={column} value={column}>{column}</option>
                    ))}
                  </SelectInput>
                ))}
              </div>
            )}
            arabic={d.ar}
            detectedColumns={detectedColumns}
            extraSelected={extraColumns}
            fields={dataEntryImportCatalog}
            mapping={mapping.columns}
            onExtraSelected={setExtraColumns}
            onToggleAdvanced={() => setShowAdvanced((value) => !value)}
            showAdvanced={showAdvanced}
          />
          <Button
            disabled={busy || !deskReady || Boolean(missingRequired)}
            fullWidth={false}
            isLoading={busy}
            onClick={() => void run(async () => {
              const payload: DataEntryImportMappingRequest = {
                ...mapping,
                organizationId,
                portfolioId: portfolioId || null,
                primaryClassification: primaryClassification || null,
                subClassification: subClassification || null,
              };
              setMapping(payload);
              setPreview(await dataEntryService.previewImport(upload.uploadId, payload));
              setOnlyInvalid(false);
              setExcludedRows([]);
              setSelectedRows([]);
            })}
          >
            {d.text('مراجعة الصفوف قبل الإرسال', 'Review rows before send')}
          </Button>
          {preview ? (
            <div className="space-y-4 border-t border-mis-border pt-4">
              <ExcelPreviewToolbar
                arabic={d.ar}
                excludedCount={excludedRows.length}
                onExclude={() => setExcludedRows((current) => [...new Set([...current, ...selectedRows])])}
                onRestore={() => { setExcludedRows((current) => current.filter((row) => !selectedRows.includes(row))); setSelectedRows([]); }}
                readyCount={readyCount}
                selectedCount={selectedRows.length}
              />
              <div className="grid gap-3 sm:grid-cols-4">
                <div className="rounded-xl bg-slate-50 p-3 text-sm"><p className="text-slate-500">{d.text('الإجمالي', 'Total')}</p><p className="font-bold text-mis-navy">{d.number(preview.totalRows)}</p></div>
                <div className="rounded-xl bg-emerald-50 p-3 text-sm"><p className="text-emerald-700">{d.text('جاهز للتحصيل', 'Ready for collections')}</p><p className="font-bold text-emerald-800">{d.number(preview.readyRows + preview.existingCustomerRows)}</p></div>
                <div className="rounded-xl bg-amber-50 p-3 text-sm"><p className="text-amber-700">{d.text('عميل موجود', 'Existing')}</p><p className="font-bold text-amber-800">{d.number(preview.existingCustomerRows)}</p></div>
                <div className="rounded-xl bg-rose-50 p-3 text-sm"><p className="text-rose-700">{d.text('غير صالح', 'Invalid')}</p><p className="font-bold text-rose-800">{d.number(preview.invalidRows)}</p></div>
              </div>
              <label className="inline-flex items-center gap-2 text-sm font-semibold text-slate-600">
                <input checked={onlyInvalid} type="checkbox" onChange={(event) => setOnlyInvalid(event.target.checked)} />
                {d.text('عرض غير الصالح فقط', 'Show invalid only')}
              </label>
              <div className="overflow-x-auto rounded-xl border border-mis-border">
                <table className="min-w-full text-sm">
                  <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                    <tr>
                      {[d.text('تحديد', 'Select'), d.text('رقم العميل', 'Customer number'), d.text('الاسم', 'Name'), d.text('الرقم القومي', 'National ID'), d.text('الموبايل', 'Mobile'), d.text('الحالة', 'Status')].map((label) => (
                        <th className="px-3 py-2 text-start" key={label}>{label}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-mis-border">
                    {previewRows.slice(0, 200).map((row) => (
                      <tr className={excludedRows.includes(row.rowNumber) ? 'bg-slate-100 opacity-60' : ''} key={row.rowNumber}>
                        <td className="px-3 py-2"><input checked={selectedRows.includes(row.rowNumber)} onChange={() => setSelectedRows((current) => current.includes(row.rowNumber) ? current.filter((item) => item !== row.rowNumber) : [...current, row.rowNumber])} type="checkbox" /></td>
                        <td className="px-3 py-2" data-bidi="ltr">{row.customerNumber || '—'}</td>
                        <td className="px-3 py-2">{row.customerName}</td>
                        <td className="px-3 py-2" data-bidi="ltr">{row.nationalId || '—'}</td>
                        <td className="px-3 py-2" data-bidi="ltr">{row.mobileNumber || '—'}</td>
                        <td className="px-3 py-2">
                          <DataEntryRowStatus value={row.status} />
                          {row.errorMessage ? <p className="mt-1 text-xs text-amber-700">{row.errorMessage}</p> : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Button
                disabled={busy || readyCount < 1}
                fullWidth={false}
                isLoading={busy}
                onClick={() => setConfirmOpen(true)}
              >
                {d.text(`إرسال ${readyCount} للتحصيل`, `Send ${readyCount} to collections`)}
              </Button>
            </div>
          ) : null}
        </Card>
      ) : null}

      <ConfirmDialog
        confirmLabel={importCopy.confirmAction(d.ar, readyCount)}
        confirmVariant="primary"
        isConfirming={busy}
        message={importCopy.confirmMessage(d.ar, readyCount)}
        onCancel={() => { if (!busy) setConfirmOpen(false); }}
        onConfirm={() => void run(async () => {
          if (!preview) return;
          rememberExtraColumns('data-entry', extraColumns);
          await dataEntryService.confirmImport({ uploadId: preview.uploadId, previewId: preview.previewId, excludedRowNumbers: excludedRows });
          setConfirmOpen(false);
          toast.success(d.text('تم إرسال الدفعة لمراجعة التحصيل', 'Batch sent for collections review'));
          navigate('/data-entry/files');
        })}
        open={confirmOpen}
        title={importCopy.confirmTitle(d.ar)}
      />
    </div>
  );
}

function SheetGrid({
  sheet,
  rowsLabel,
  truncatedLabel,
  arabicCount,
}: {
  sheet: DataEntrySheetPreview;
  rowsLabel: string;
  truncatedLabel: string;
  arabicCount: (value: number) => string;
}) {
  return (
    <div className="space-y-2">
      <p className="text-sm font-semibold text-slate-600">
        <span dir="ltr">{sheet.fileName}</span>
        {' · '}
        {arabicCount(sheet.totalRows)} {rowsLabel}
        {sheet.sheetName ? ` · ${sheet.sheetName}` : ''}
      </p>
      {sheet.truncated ? <p className="text-xs text-amber-700">{truncatedLabel}</p> : null}
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
        {!sheet.rows.length ? <p className="p-4 text-sm text-slate-500">—</p> : null}
      </div>
    </div>
  );
}
