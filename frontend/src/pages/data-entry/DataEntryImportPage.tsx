import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { PageHeader } from '../../components/common/PageHeader';
import { FileInput } from '../../components/forms/FileInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextInput } from '../../components/forms/TextInput';
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
} from '../../features/data-entry/types/dataEntry';
import { getApiErrorMessage } from '../../services/apiClient';

const fieldMeta: Record<DataEntryImportField, { en: string; ar: string; pattern: RegExp }> = {
  CustomerCode: { en: 'Customer Number', ar: 'رقم العميل', pattern: /^(customer.?code|customer.?id|customer.?number|كود.?العميل|رقم.?العميل)$/i },
  CustomerName: { en: 'Customer Name', ar: 'اسم العميل', pattern: /^(name|customer.?name|client.?name|اسم.?العميل|الاسم)$/i },
  NationalId: { en: 'National ID', ar: 'الرقم القومي', pattern: /^(national.?id|id.?number|nid|الرقم.?القومي)$/i },
  MobileNumber: { en: 'Mobile Number', ar: 'رقم الموبايل', pattern: /^(mobile|phone|phone.?number|رقم.?الموبايل|رقم.?الهاتف)$/i },
  Address: { en: 'Address', ar: 'العنوان', pattern: /^(address|العنوان)$/i },
  Feedback: { en: 'Feedback', ar: 'فيدباك', pattern: /^(feedback|فيدباك|تعليق)$/i },
  Notes: { en: 'Notes', ar: 'ملاحظات', pattern: /^(notes|ملاحظات|ملاحظة)$/i },
  AccountNumber: { en: 'Account Number', ar: 'رقم الحساب', pattern: /^(account|account.?no|account.?number|رقم.?الحساب)$/i },
  ContractNumber: { en: 'Contract Number', ar: 'رقم العقد', pattern: /^(contract|contract.?no|contract.?number|رقم.?العقد)$/i },
  OutstandingAmount: { en: 'Outstanding Amount', ar: 'المديونية', pattern: /^(outstanding|outstanding.?amount|المديونية|المبلغ.?المستحق)$/i },
  OverdueAmount: { en: 'Overdue Amount', ar: 'المتأخر', pattern: /^(overdue|overdue.?amount|المتأخر)$/i },
  DaysPastDue: { en: 'Days Past Due', ar: 'أيام التأخر', pattern: /^(dpd|days.?past.?due|أيام.?التأخر)$/i },
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

export function DataEntryImportPage() {
  const d = useDataEntryText();
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
  const [upload, setUpload] = useState<DataEntryImportUpload | null>(null);
  const [sheetIndex, setSheetIndex] = useState(0);
  const [mapping, setMapping] = useState<DataEntryImportMappingRequest | null>(null);
  const [preview, setPreview] = useState<DataEntryImportPreview | null>(null);

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
      setPrimaryClassification(items[0]?.primaryClassification ?? '');
      setSubClassification(items[0]?.subClassification ?? '');
    }).catch(() => {
      setPortfolios([]);
      setPortfolioId('');
    });
  }, [organizationId]);

  useEffect(() => {
    const selected = portfolios.find((item) => item.id === portfolioId);
    if (selected) {
      setPrimaryClassification(selected.primaryClassification ?? '');
      setSubClassification(selected.subClassification ?? '');
    }
  }, [portfolioId, portfolios]);

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

  async function sendFile() {
    if (!file || !organizationId) return;
    if (!/\.(xlsx|xls|csv)$/i.test(file.name) || file.size > 20 * 1024 * 1024) {
      setError(d.text('الملف غير مدعوم أو أكبر من 20 ميجابايت', 'Unsupported file or larger than 20MB'));
      return;
    }
    await run(async () => {
      const data = await dataEntryService.uploadImport(file);
      setUpload(data);
      setMapping(mappingFor(data, 0, organizationId, portfolioId, primaryClassification, subClassification));
      setSheetIndex(0);
      setStep(2);
    });
  }

  const labels = [
    d.text('الجهة', 'Organization'),
    d.text('رفع الملف', 'Upload file'),
    d.text('ربط الأعمدة', 'Column mapping'),
    d.text('المعاينة', 'Preview'),
  ];

  const orgLabel = (org: DataEntryOrganization) => (d.ar ? org.nameArabic : org.nameEnglish) || org.code;
  const portfolioLabel = (item: DataEntryPortfolio) => (d.ar ? item.nameArabic : item.nameEnglish) || item.code;
  const needsClassification = portfolios.length === 0;

  return (
    <div className="space-y-5">
      <PageHeader
        title={d.text('رفع البيانات', 'Import Data')}
        description={d.text('رفع ملف عملاء ثم ربط الأعمدة والمعاينة قبل الإرسال.', 'Upload a client file, map columns, preview, then confirm.')}
      />
      <div className="flex flex-wrap gap-2">
        {labels.map((label, index) => (
          <span
            className={`rounded-full px-3 py-1 text-xs font-bold ${step === index ? 'bg-mis-primary text-white' : 'bg-slate-100 text-slate-500'}`}
            key={label}
          >
            {index + 1}. {label}
          </span>
        ))}
      </div>
      {error ? <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</div> : null}

      {step === 0 ? (
        <Card className="space-y-4 p-6">
          <SelectInput
            label={d.text('الجهة', 'Organization')}
            required
            value={organizationId}
            onChange={(event) => setOrganizationId(event.target.value)}
          >
            {organizations.map((org) => (
              <option key={org.id} value={org.id}>
                {orgLabel(org)}
              </option>
            ))}
          </SelectInput>
          {portfolios.length > 0 ? (
            <SelectInput
              label={d.text('المحفظة', 'Portfolio')}
              value={portfolioId}
              onChange={(event) => setPortfolioId(event.target.value)}
            >
              <option value="">—</option>
              {portfolios.map((item) => (
                <option key={item.id} value={item.id}>
                  {portfolioLabel(item)}
                </option>
              ))}
            </SelectInput>
          ) : (
            <div className="grid gap-3 md:grid-cols-2">
              <TextInput
                label={d.text('التصنيف الرئيسي', 'Primary classification')}
                value={primaryClassification}
                onChange={(event) => setPrimaryClassification(event.target.value)}
              />
              <TextInput
                label={d.text('التصنيف الفرعي', 'Sub classification')}
                value={subClassification}
                onChange={(event) => setSubClassification(event.target.value)}
              />
            </div>
          )}
          <Button disabled={!organizationId || (needsClassification && !primaryClassification.trim())} fullWidth={false} onClick={() => setStep(1)}>
            {d.text('التالي', 'Next')}
          </Button>
        </Card>
      ) : null}

      {step === 1 ? (
        <Card className="space-y-4 p-6">
          <FileInput accept=".csv,.xlsx,.xls" label={d.text('الملف', 'File')} onChange={(event) => setFile(event.target.files?.[0] ?? null)} />
          <div className="flex gap-2">
            <Button disabled={busy} fullWidth={false} variant="outline" onClick={() => setStep(0)}>
              {d.text('رجوع', 'Back')}
            </Button>
            <Button disabled={busy || !file} fullWidth={false} isLoading={busy} onClick={() => void sendFile()}>
              {d.text('رفع ومعاينة', 'Upload & continue')}
            </Button>
          </div>
        </Card>
      ) : null}

      {step === 2 && upload && mapping ? (
        <Card className="space-y-4 p-6">
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
                <option key={sheet.sheetName ?? index} value={index}>
                  {sheet.sheetName ?? `Sheet ${index + 1}`}
                </option>
              ))}
            </SelectInput>
          ) : null}
          <div className="grid gap-3 md:grid-cols-2">
            {DATA_ENTRY_IMPORT_FIELDS.map((key) => (
              <SelectInput
                key={key}
                label={d.ar ? fieldMeta[key].ar : fieldMeta[key].en}
                value={mapping.columns[key] ?? ''}
                onChange={(event) =>
                  setMapping({
                    ...mapping,
                    columns: { ...mapping.columns, [key]: event.target.value || null },
                  })
                }
              >
                <option value="">—</option>
                {(upload.sheets[sheetIndex]?.detectedColumns ?? []).map((column) => (
                  <option key={column} value={column}>
                    {column}
                  </option>
                ))}
              </SelectInput>
            ))}
          </div>
          <div className="flex gap-2">
            <Button disabled={busy} fullWidth={false} variant="outline" onClick={() => setStep(1)}>
              {d.text('رجوع', 'Back')}
            </Button>
            <Button
              disabled={busy}
              fullWidth={false}
              isLoading={busy}
              onClick={() =>
                void run(async () => {
                  const payload: DataEntryImportMappingRequest = {
                    ...mapping,
                    organizationId,
                    portfolioId: portfolioId || null,
                    primaryClassification: needsClassification ? primaryClassification || null : mapping.primaryClassification,
                    subClassification: needsClassification ? subClassification || null : mapping.subClassification,
                  };
                  setPreview(await dataEntryService.previewImport(upload.uploadId, payload));
                  setStep(3);
                })
              }
            >
              {d.text('معاينة', 'Preview')}
            </Button>
          </div>
        </Card>
      ) : null}

      {step === 3 && preview && upload ? (
        <Card className="space-y-4 p-6">
          <div className="flex flex-wrap gap-3 text-sm text-slate-600">
            <span>{d.text('الإجمالي', 'Total')}: {d.number(preview.totalRows)}</span>
            <span>{d.text('جاهز', 'Ready')}: {d.number(preview.readyRows)}</span>
            <span>{d.text('موجود', 'Existing')}: {d.number(preview.existingCustomerRows)}</span>
            <span>{d.text('غير صالح', 'Invalid')}: {d.number(preview.invalidRows)}</span>
          </div>
          <div className="overflow-x-auto rounded-xl border border-mis-border">
            <table className="min-w-full text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  {[
                    d.text('رقم العميل', 'Customer Number'),
                    d.text('الاسم', 'Name'),
                    d.text('الرقم القومي', 'National ID'),
                    d.text('الموبايل', 'Mobile'),
                    d.text('الحالة', 'Status'),
                  ].map((label) => (
                    <th className="px-3 py-2 text-start" key={label}>
                      {label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {preview.rows.slice(0, 100).map((row) => (
                  <tr key={row.rowNumber}>
                    <td className="px-3 py-2" data-bidi="ltr">
                      {row.customerNumber || '—'}
                    </td>
                    <td className="px-3 py-2">{row.customerName}</td>
                    <td className="px-3 py-2" data-bidi="ltr">
                      {row.nationalId || '—'}
                    </td>
                    <td className="px-3 py-2" data-bidi="ltr">
                      {row.mobileNumber || '—'}
                    </td>
                    <td className="px-3 py-2">
                      <DataEntryRowStatus value={row.status} />
                      {row.errorMessage ? <p className="mt-1 text-xs text-amber-700">{row.errorMessage}</p> : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex gap-2">
            <Button disabled={busy} fullWidth={false} variant="outline" onClick={() => setStep(2)}>
              {d.text('رجوع', 'Back')}
            </Button>
            <Button
              disabled={busy || preview.readyRows + preview.existingCustomerRows < 1}
              fullWidth={false}
              isLoading={busy}
              onClick={() =>
                void run(async () => {
                  await dataEntryService.confirmImport({ uploadId: preview.uploadId, previewId: preview.previewId });
                  navigate('/data-entry/history');
                })
              }
            >
              {d.text('تأكيد الإرسال', 'Confirm')}
            </Button>
          </div>
        </Card>
      ) : null}
    </div>
  );
}
