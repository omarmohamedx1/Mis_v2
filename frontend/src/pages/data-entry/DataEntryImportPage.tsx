import { Download, FileUp } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { PageHeader } from '../../components/common/PageHeader';
import { useToast } from '../../components/common/Toast';
import { SelectInput } from '../../components/forms/SelectInput';
import { DataEntryDeskFields } from '../../features/data-entry/DataEntryDeskFields';
import { DataEntryPipeline, DataEntryRowStatus, useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import {
  DATA_ENTRY_IMPORT_FIELDS,
  type DataEntryImportField,
  type DataEntryImportMappingRequest,
  type DataEntryImportPreview,
  type DataEntryImportUpload,
  type DataEntryOrganization,
  type DataEntryPortfolio,
} from '../../features/data-entry/types/dataEntry';
import { getApiErrorMessage } from '../../services/apiClient';

const fieldMeta: Record<DataEntryImportField, { en: string; ar: string; required?: boolean; pattern: RegExp }> = {
  CustomerCode: { en: 'Customer number', ar: 'رقم العميل', pattern: /^(customer.?code|customer.?id|customer.?number|كود.?العميل|رقم.?العميل)$/i },
  CustomerName: { en: 'Customer name', ar: 'اسم العميل', required: true, pattern: /^(name|customer.?name|client.?name|اسم.?العميل|الاسم)$/i },
  NationalId: { en: 'National ID', ar: 'الرقم القومي', pattern: /^(national.?id|id.?number|nid|الرقم.?القومي)$/i },
  MobileNumber: { en: 'Mobile number', ar: 'رقم الموبايل', pattern: /^(mobile|phone|phone.?number|رقم.?الموبايل|رقم.?الهاتف)$/i },
  Address: { en: 'Address', ar: 'العنوان', pattern: /^(address|العنوان)$/i },
  Feedback: { en: 'Feedback', ar: 'فيدباك', pattern: /^(feedback|فيدباك|تعليق)$/i },
  Notes: { en: 'Notes', ar: 'ملاحظات', pattern: /^(notes|ملاحظات|ملاحظة)$/i },
  AccountNumber: { en: 'Account number', ar: 'رقم الحساب', required: true, pattern: /^(account|account.?no|account.?number|رقم.?الحساب)$/i },
  ContractNumber: { en: 'Contract number', ar: 'رقم العقد', pattern: /^(contract|contract.?no|contract.?number|رقم.?العقد)$/i },
  OutstandingAmount: { en: 'Outstanding amount', ar: 'المديونية', pattern: /^(outstanding|outstanding.?amount|المديونية|المبلغ.?المستحق)$/i },
  OverdueAmount: { en: 'Overdue amount', ar: 'المتأخر', pattern: /^(overdue|overdue.?amount|المتأخر)$/i },
  DaysPastDue: { en: 'Days past due', ar: 'أيام التأخر', pattern: /^(dpd|days.?past.?due|أيام.?التأخر)$/i },
};

function mappingFor(
  upload: DataEntryImportUpload,
  sheetIndex: number,
  organizationId: string,
  portfolioId: string,
  primaryClassification: string,
  subClassification: string,
): DataEntryImportMappingRequest {
  const sheet = upload.sheets[sheetIndex];
  return {
    organizationId,
    portfolioId: portfolioId || null,
    primaryClassification: primaryClassification || null,
    subClassification: subClassification || null,
    sheetName: sheet.sheetName,
    headerRow: sheet.suggestedHeaderRowNumber,
    firstDataRow: sheet.suggestedHeaderRowNumber + 1,
    columns: Object.fromEntries(
      DATA_ENTRY_IMPORT_FIELDS.map((key) => [
        key,
        sheet.detectedColumns.find((column) => fieldMeta[key].pattern.test(column.trim())) ?? null,
      ]),
    ),
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
  const [step, setStep] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [organizations, setOrganizations] = useState<DataEntryOrganization[]>([]);
  const [portfolios, setPortfolios] = useState<DataEntryPortfolio[]>([]);
  const [organizationId, setOrganizationId] = useState('');
  const [portfolioId, setPortfolioId] = useState('');
  const [primaryClassification, setPrimaryClassification] = useState('');
  const [subClassification, setSubClassification] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const [upload, setUpload] = useState<DataEntryImportUpload | null>(null);
  const [sheetIndex, setSheetIndex] = useState(0);
  const [mapping, setMapping] = useState<DataEntryImportMappingRequest | null>(null);
  const [preview, setPreview] = useState<DataEntryImportPreview | null>(null);
  const [onlyInvalid, setOnlyInvalid] = useState(false);

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

  function takeFile(next?: File | null) {
    if (!next) return;
    setFile(next);
  }

  async function sendFile() {
    if (!file || !organizationId || !primaryClassification || !subClassification) return;
    if (!/\.(xlsx|xls|csv)$/i.test(file.name) || file.size > 20 * 1024 * 1024) {
      setError(d.text('الملف غير مدعوم أو أكبر من 20 ميجابايت', 'Unsupported file or larger than 20MB'));
      return;
    }
    await run(async () => {
      const data = await dataEntryService.uploadImport(file);
      setUpload(data);
      setMapping(mappingFor(data, 0, organizationId, portfolioId, primaryClassification, subClassification));
      setSheetIndex(0);
      setStep(1);
    });
  }

  const labels = [
    d.text('الجهة والملف', 'Organization & file'),
    d.text('ربط الأعمدة', 'Column mapping'),
    d.text('المعاينة والإرسال', 'Preview & send'),
  ];
  const deskReady = Boolean(organizationId && primaryClassification && subClassification);
  const previewRows = useMemo(() => {
    if (!preview) return [];
    return onlyInvalid ? preview.rows.filter((row) => row.status !== 'READY' && row.status !== 'EXISTING_CUSTOMER') : preview.rows;
  }, [onlyInvalid, preview]);
  const missingRequired = mapping && (!mapping.columns.CustomerName || !mapping.columns.AccountNumber);

  return (
    <div className="space-y-5">
      <PageHeader
        title={d.text('رفع ملف للتحصيل', 'Upload a file to collections')}
        description={d.text('ارفع ملف العملاء، راجع الربط، ثم أرسل الدفعة لمراجعة التحصيل حتى تُنشأ الحالات.', 'Upload the client file, review mapping, then send the batch for collections review so cases can be created.')}
        actions={
          <Button fullWidth={false} leftIcon={<Download className="h-4 w-4" />} variant="outline" onClick={() => downloadTemplate(d.ar)}>
            {d.text('تنزيل القالب', 'Download template')}
          </Button>
        }
      />
      <DataEntryPipeline current="enter" />
      <div className="flex flex-wrap gap-2">
        {labels.map((label, index) => (
          <span className={`rounded-full px-3 py-1 text-xs font-bold ${step === index ? 'bg-mis-primary text-white' : 'bg-slate-100 text-slate-500'}`} key={label}>
            {index + 1}. {label}
          </span>
        ))}
      </div>
      {error ? <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</div> : null}

      {step === 0 ? (
        <Card className="space-y-4 p-6">
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
            }}
            onPrimaryChange={(value) => {
              setPrimaryClassification(value);
              setSubClassification('');
            }}
            onSubChange={setSubClassification}
          />
          <label
            className={`flex min-h-40 cursor-pointer flex-col items-center justify-center rounded-2xl border-2 border-dashed px-4 text-center ${dragOver ? 'border-mis-primary bg-mis-pale' : 'border-mis-border bg-slate-50'}`}
            onDragOver={(event) => { event.preventDefault(); setDragOver(true); }}
            onDragLeave={() => setDragOver(false)}
            onDrop={(event) => {
              event.preventDefault();
              setDragOver(false);
              takeFile(event.dataTransfer.files?.[0] ?? null);
            }}
          >
            <FileUp className="mb-2 h-8 w-8 text-mis-primary" />
            <p className="font-bold text-mis-navy">{file ? file.name : d.text('اسحب الملف هنا أو اضغط للاختيار', 'Drop the file here or click to choose')}</p>
            <p className="mt-1 text-sm text-slate-500">{d.text('Excel أو CSV حتى 20 ميجابايت', 'Excel or CSV up to 20MB')}</p>
            <input accept=".csv,.xlsx,.xls" className="hidden" type="file" onChange={(event) => takeFile(event.target.files?.[0] ?? null)} />
          </label>
          <Button disabled={busy || !file || !deskReady} fullWidth={false} isLoading={busy} onClick={() => void sendFile()}>
            {d.text('رفع ومتابعة الربط', 'Upload and map columns')}
          </Button>
        </Card>
      ) : null}

      {step === 1 && upload && mapping ? (
        <Card className="space-y-4 p-6">
          {missingRequired ? <p className="rounded-xl bg-amber-50 px-3 py-2 text-sm text-amber-800">{d.text('اربط اسم العميل ورقم الحساب حتى يستطيع التحصيل إنشاء الحالة بعد القبول.', 'Map customer name and account number so collections can create the case after acceptance.')}</p> : null}
          {upload.sheets.length > 1 ? (
            <SelectInput
              label={d.text('الورقة', 'Sheet')}
              value={String(sheetIndex)}
              onChange={(event) => {
                const index = Number(event.target.value);
                setSheetIndex(index);
                setMapping(mappingFor(upload, index, organizationId, portfolioId, primaryClassification, subClassification));
              }}
            >
              {upload.sheets.map((sheet, index) => (
                <option key={sheet.sheetName ?? index} value={index}>{sheet.sheetName ?? `Sheet ${index + 1}`}</option>
              ))}
            </SelectInput>
          ) : null}
          <div className="grid gap-3 md:grid-cols-2">
            {DATA_ENTRY_IMPORT_FIELDS.map((key) => (
              <SelectInput
                key={key}
                label={`${d.ar ? fieldMeta[key].ar : fieldMeta[key].en}${fieldMeta[key].required ? ` *` : ''}`}
                value={mapping.columns[key] ?? ''}
                onChange={(event) => setMapping({ ...mapping, columns: { ...mapping.columns, [key]: event.target.value || null } })}
              >
                <option value="">—</option>
                {(upload.sheets[sheetIndex]?.detectedColumns ?? []).map((column) => (
                  <option key={column} value={column}>{column}</option>
                ))}
              </SelectInput>
            ))}
          </div>
          <div className="flex gap-2">
            <Button disabled={busy} fullWidth={false} variant="outline" onClick={() => setStep(0)}>{d.text('رجوع', 'Back')}</Button>
            <Button
              disabled={busy || Boolean(missingRequired)}
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
                setPreview(await dataEntryService.previewImport(upload.uploadId, payload));
                setOnlyInvalid(false);
                setStep(2);
              })}
            >
              {d.text('معاينة قبل الإرسال', 'Preview before send')}
            </Button>
          </div>
        </Card>
      ) : null}

      {step === 2 && preview && upload ? (
        <Card className="space-y-4 p-6">
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
                  {[d.text('رقم العميل', 'Customer number'), d.text('الاسم', 'Name'), d.text('الرقم القومي', 'National ID'), d.text('الموبايل', 'Mobile'), d.text('الحالة', 'Status')].map((label) => (
                    <th className="px-3 py-2 text-start" key={label}>{label}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {previewRows.slice(0, 200).map((row) => (
                  <tr key={row.rowNumber}>
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
          <p className="text-sm text-slate-500">
            {d.text('التأكيد يرسل الصفوف الصالحة لمراجعة التحصيل فورًا. الصفوف غير الصالحة تبقى في السجل للمراجعة.', 'Confirm sends valid rows to collections review immediately. Invalid rows stay in the record for review.')}
          </p>
          <div className="flex gap-2">
            <Button disabled={busy} fullWidth={false} variant="outline" onClick={() => setStep(1)}>{d.text('رجوع', 'Back')}</Button>
            <Button
              disabled={busy || preview.readyRows + preview.existingCustomerRows < 1}
              fullWidth={false}
              isLoading={busy}
              onClick={() => void run(async () => {
                await dataEntryService.confirmImport({ uploadId: preview.uploadId, previewId: preview.previewId });
                toast.success(d.text('تم إرسال الدفعة لمراجعة التحصيل', 'Batch sent for collections review'));
                navigate('/data-entry/history?status=SUBMITTED');
              })}
            >
              {d.text(`إرسال ${preview.readyRows + preview.existingCustomerRows} للتحصيل`, `Send ${preview.readyRows + preview.existingCustomerRows} to collections`)}
            </Button>
          </div>
        </Card>
      ) : null}
    </div>
  );
}
