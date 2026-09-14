import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Upload, Eye, CheckCircle2, AlertTriangle, Loader2 } from 'lucide-react';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge } from '../../components/common/StatusBadge';
import { Pagination } from '../../components/common/Pagination';
import { FileInput } from '../../components/forms/FileInput';
import { TextInput } from '../../components/forms/TextInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { useLocalization } from '../../context/LocalizationContext';
import { getApiErrorMessage } from '../../services/apiClient';
import {
  autoMapEmployeeImportColumns,
  employeeImportFields,
  missingRequiredEmployeeImportFields,
} from '../../features/hr/employeeImportAutoMap';
import {
  employeeImportService,
  type EmployeeImportHistory,
  type EmployeeImportMapping,
  type EmployeeImportPreview,
  type EmployeeImportResult,
  type EmployeeImportUpload,
} from '../../features/hr/services/employeeImportService';

type WizardStep = 'upload' | 'missing' | 'advanced' | 'working' | 'preview' | 'done';

function buildMapping(upload: EmployeeImportUpload, index: number): EmployeeImportMapping {
  const sheet = upload.sheets[index];
  return {
    sheetName: sheet.sheetName,
    headerRow: sheet.suggestedHeaderRowNumber,
    firstDataRow: sheet.suggestedHeaderRowNumber + 1,
    dateFormat: null,
    columns: autoMapEmployeeImportColumns(sheet.detectedColumns),
  };
}

export function HrEmployeeImportPage() {
  const { language } = useLocalization();
  const ar = language === 'ar';
  const text = (en: string, arabic: string) => (ar ? arabic : en);

  const [step, setStep] = useState<WizardStep>('upload');
  const [busy, setBusy] = useState(false);
  const [phase, setPhase] = useState('');
  const [error, setError] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [upload, setUpload] = useState<EmployeeImportUpload | null>(null);
  const [sheetIndex, setSheetIndex] = useState(0);
  const [mapping, setMapping] = useState<EmployeeImportMapping | null>(null);
  const [missing, setMissing] = useState<string[]>([]);
  const [preview, setPreview] = useState<EmployeeImportPreview | null>(null);
  const [result, setResult] = useState<EmployeeImportResult | null>(null);
  const [history, setHistory] = useState<EmployeeImportHistory[]>([]);
  const [page, setPage] = useState(1);
  const [showErrorsOnly, setShowErrorsOnly] = useState(false);

  useEffect(() => {
    void employeeImportService.history().then(setHistory).catch(() => undefined);
  }, [result, upload, preview]);

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError('');
    try {
      await action();
    } catch (reason) {
      setError(getApiErrorMessage(reason, text('Employee import failed.', 'تعذر استيراد الموظفين.')));
      if (step === 'working') setStep(upload ? 'missing' : 'upload');
    } finally {
      setBusy(false);
      setPhase('');
    }
  }

  async function autoImportWith(data: EmployeeImportUpload, nextMapping: EmployeeImportMapping, index: number) {
    const gaps = missingRequiredEmployeeImportFields(nextMapping.columns);
    setUpload(data);
    setMapping(nextMapping);
    setSheetIndex(index);

    if (gaps.length) {
      setMissing(gaps.map((field) => text(field.en, field.ar)));
      setStep('missing');
      return;
    }

    setStep('working');
    setPhase(text('Reading file and validating rows…', 'جاري قراءة الملف والتحقق من البيانات…'));
    const built = await employeeImportService.preview(data.id, nextMapping);
    setPreview(built);
    setPage(1);

    const importable = built.rows.some((row) => row.status === 'Ready' || row.status === 'Warning');
    if (!importable) {
      setStep('preview');
      return;
    }

    setPhase(text('Importing employees…', 'جاري استيراد الموظفين…'));
    const confirmed = await employeeImportService.confirm(data.id, built.previewId);
    setResult(confirmed);
    setStep('done');
  }

  async function sendFile() {
    if (!file) return;
    if (!/\.(xlsx|xls|csv)$/i.test(file.name) || file.size > 20 * 1024 * 1024) {
      setError(text('Choose CSV, XLS, or XLSX, maximum 20 MB.', 'اختر CSV أو XLS أو XLSX بحد أقصى 20 ميجابايت.'));
      return;
    }
    await run(async () => {
      setStep('working');
      setPhase(text('Reading file…', 'جاري قراءة الملف…'));
      const data = await employeeImportService.upload(file);
      const next = buildMapping(data, 0);
      await autoImportWith(data, next, 0);
    });
  }

  const statusLabel = (status: string) =>
    ({
      Ready: text('Ready', 'جاهز'),
      Warning: text('Warning', 'تحذير'),
      Error: text('Error', 'خطأ'),
      Existing: text('Existing employee — skipped', 'موظف موجود بالفعل — سيتم التجاوز'),
    }[status] ?? status);

  const employeeStatus = (status: string | null) =>
    ({
      Active: text('Active', 'نشط'),
      Inactive: text('Inactive', 'غير نشط'),
      OnLeave: text('On leave', 'في إجازة'),
      Suspended: text('Suspended', 'موقوف'),
      Terminated: text('Terminated', 'منتهي'),
    }[status ?? 'Active'] ?? status);

  const link = (
    <Link className="inline-flex h-10 items-center rounded-xl border border-mis-border px-4 text-sm font-semibold text-mis-primary" to="/hr/employees">
      {text('View Employees', 'عرض الموظفين')}
    </Link>
  );

  const stepper = [
    text('Upload', 'رفع الملف'),
    text('Auto validate', 'تحقق تلقائي'),
    text('Import', 'الاستيراد'),
    text('Complete', 'اكتمل'),
  ];
  const activeStep = step === 'upload' || step === 'missing' || step === 'advanced' ? 0 : step === 'working' ? 1 : step === 'preview' ? 2 : 3;

  const previewRows = preview
    ? showErrorsOnly
      ? preview.rows.filter((row) => row.status === 'Error' || row.status === 'Existing')
      : preview.rows
    : [];

  return (
    <div className="mx-auto max-w-[1500px] space-y-5">
      <PageHeader
        title={text('Import Employees', 'استيراد موظفين')}
        description={text(
          'Upload an employee Excel file. Columns are detected and mapped automatically. Existing employees are never overwritten.',
          'ارفع ملف الموظفين Excel. يتم اكتشاف الأعمدة وربطها تلقائياً. لا يتم تعديل الموظفين الموجودين.',
        )}
        actions={link}
      />

      <div className="grid grid-cols-4 overflow-hidden rounded-2xl border border-mis-border bg-white">
        {stepper.map((label, index) => (
          <div key={label} className={`p-4 text-center text-sm font-semibold ${activeStep === index ? 'bg-mis-pale text-mis-primary' : 'text-slate-500'}`}>
            {index + 1}. {label}
          </div>
        ))}
      </div>

      {error ? (
        <div role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">
          {error}
        </div>
      ) : null}

      {step === 'upload' ? (
        <Card padding="lg">
          <form
            className="mx-auto max-w-2xl space-y-5"
            onSubmit={(event) => {
              event.preventDefault();
              void sendFile();
            }}
          >
            <FileInput
              accept=".xlsx,.xls,.csv"
              label={text('Employee Excel / CSV file', 'ملف الموظفين Excel / CSV')}
              hint={text('Maximum 20 MB and 2,000 rows. Recognized headers import automatically.', 'بحد أقصى 20 ميجابايت و2000 صف. الأعمدة المعروفة تُستورد تلقائياً.')}
              onChange={(event) => setFile(event.target.files?.[0] ?? null)}
              required
            />
            <Button isLoading={busy} type="submit" leftIcon={<Upload className="h-4 w-4" />}>
              {text('Upload employee file', 'رفع ملف الموظفين')}
            </Button>
          </form>
        </Card>
      ) : null}

      {step === 'working' ? (
        <Card padding="lg">
          <div className="flex flex-col items-center gap-3 py-10 text-center">
            <Loader2 className="h-10 w-10 animate-spin text-mis-primary" />
            <p className="text-lg font-semibold text-mis-navy">{phase || text('Working…', 'جاري العمل…')}</p>
          </div>
        </Card>
      ) : null}

      {step === 'missing' ? (
        <Card padding="lg">
          <div className="mx-auto max-w-2xl space-y-5">
            <div className="flex items-start gap-3 rounded-xl bg-amber-50 p-4 text-amber-900">
              <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" />
              <div>
                <p className="font-semibold">{text('Could not import the file because these columns were not found:', 'تعذر استيراد الملف لأن الأعمدة التالية غير موجودة:')}</p>
                <ul className="mt-2 list-disc ps-5 text-sm">
                  {missing.map((label) => (
                    <li key={label}>{label}</li>
                  ))}
                </ul>
              </div>
            </div>
            <div className="flex flex-wrap gap-3">
              <Button
                fullWidth={false}
                variant="outline"
                onClick={() => {
                  setStep('upload');
                  setFile(null);
                  setUpload(null);
                  setMissing([]);
                }}
              >
                {text('Back', 'رجوع')}
              </Button>
              <Button
                fullWidth={false}
                variant="outline"
                onClick={() => {
                  setStep('upload');
                  setFile(null);
                }}
              >
                {text('Upload another file', 'رفع ملف آخر')}
              </Button>
              <Button fullWidth={false} onClick={() => setStep('advanced')}>
                {text('Advanced Manual Mapping', 'تعيين يدوي متقدم')}
              </Button>
            </div>
          </div>
        </Card>
      ) : null}

      {step === 'advanced' && upload && mapping ? (
        <Card padding="lg">
          <form
            className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3"
            onSubmit={(event) => {
              event.preventDefault();
              void run(async () => {
                await autoImportWith(upload, mapping, sheetIndex);
              });
            }}
          >
            <p className="sm:col-span-2 lg:col-span-3 text-sm text-slate-600">
              {text('Fallback for unusual legacy files only. Normal imports skip this screen.', 'شاشة احتياطية للملفات القديمة غير القياسية. الاستيراد العادي يتجاوز هذه الشاشة.')}
            </p>
            <SelectInput
              label={text('Sheet', 'ورقة العمل')}
              value={sheetIndex}
              onChange={(event) => {
                const index = Number(event.target.value);
                setSheetIndex(index);
                setMapping(buildMapping(upload, index));
              }}
            >
              {upload.sheets.map((sheet, index) => (
                <option key={index} value={index}>
                  {sheet.sheetName ?? text('CSV', 'CSV')}
                </option>
              ))}
            </SelectInput>
            <TextInput
              type="number"
              min={1}
              max={1000}
              label={text('Header Row', 'صف العناوين')}
              value={mapping.headerRow}
              onChange={(event) => setMapping({ ...mapping, headerRow: Number(event.target.value) })}
              required
            />
            <TextInput
              type="number"
              min={mapping.headerRow + 1}
              max={2000}
              label={text('First Data Row', 'أول صف بيانات')}
              value={mapping.firstDataRow}
              onChange={(event) => setMapping({ ...mapping, firstDataRow: Number(event.target.value) })}
              required
            />
            {employeeImportFields.map((field) => (
              <SelectInput
                key={field.key}
                required={field.required}
                label={text(field.en, field.ar)}
                value={mapping.columns[field.key] ?? ''}
                onChange={(event) => {
                  const value = event.target.value;
                  const next = { ...mapping.columns, [field.key]: value };
                  if (value) {
                    for (const other of employeeImportFields) {
                      if (other.key !== field.key && next[other.key] === value) next[other.key] = '';
                    }
                  }
                  setMapping({ ...mapping, columns: next });
                }}
              >
                <option value="">{text('Not mapped', 'غير معيّن')}</option>
                {upload.sheets[sheetIndex].detectedColumns.map((column) => (
                  <option key={column}>{column}</option>
                ))}
              </SelectInput>
            ))}
            <TextInput
              dir="ltr"
              maxLength={32}
              label={text('Date Format (optional)', 'تنسيق التاريخ (اختياري)')}
              placeholder="dd/MM/yyyy"
              value={mapping.dateFormat ?? ''}
              onChange={(event) => setMapping({ ...mapping, dateFormat: event.target.value || null })}
            />
            <p className="text-sm text-slate-500 sm:col-span-2 lg:col-span-3">
              {text(
                'Department and position must match existing Basic Data. Employee role maps to COLLECTOR, SUPERVISOR, or ADMIN. National ID: 14 digits. The same Excel column cannot map to two fields.',
                'يجب أن يطابق القسم والمسمى الوظيفي البيانات الأساسية. الدور يُحوَّل إلى COLLECTOR أو SUPERVISOR أو ADMIN. الرقم القومي: 14 رقمًا. لا يمكن ربط نفس عمود Excel بحقلين.',
              )}
            </p>
            <div className="flex justify-end gap-3 sm:col-span-2 lg:col-span-3">
              <Button disabled={busy} fullWidth={false} type="button" variant="outline" onClick={() => setStep('missing')}>
                {text('Back', 'رجوع')}
              </Button>
              <Button fullWidth={false} isLoading={busy} type="submit" leftIcon={<Eye className="h-4 w-4" />}>
                {text('Validate and Import', 'تحقق واستورد')}
              </Button>
            </div>
          </form>
        </Card>
      ) : null}

      {step === 'preview' && preview ? (
        <>
          <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-amber-900">
            {text(
              'No new employees were imported. Review the validation results below, fix the file, or use Advanced Manual Mapping.',
              'لم يتم استيراد موظفين جدد. راجع نتائج التحقق أدناه، أو صحّح الملف، أو استخدم التعيين اليدوي المتقدم.',
            )}
          </div>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {(['Ready', 'Warning', 'Error', 'Existing'] as const).map((status) => (
              <Card key={status} className="p-4">
                <p className="text-sm text-slate-500">{statusLabel(status)}</p>
                <p className="mt-2 text-2xl font-bold">{preview.rows.filter((row) => row.status === status).length}</p>
              </Card>
            ))}
          </div>
          <div className="flex flex-wrap gap-3">
            <Button fullWidth={false} variant="outline" onClick={() => setShowErrorsOnly((value) => !value)}>
              {showErrorsOnly ? text('Show all rows', 'عرض كل الصفوف') : text('Show errors', 'عرض الأخطاء')}
            </Button>
            <Button fullWidth={false} variant="outline" onClick={() => setStep('advanced')}>
              {text('Advanced Manual Mapping', 'تعيين يدوي متقدم')}
            </Button>
            <Button
              fullWidth={false}
              variant="outline"
              onClick={() => {
                setStep('upload');
                setFile(null);
                setUpload(null);
                setPreview(null);
              }}
            >
              {text('Upload another file', 'رفع ملف آخر')}
            </Button>
          </div>
          <Card padding="none">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[1300px] text-sm">
                <thead className="bg-mis-surface">
                  <tr>
                    {[
                      text('Employee Number', 'رقم الموظف'),
                      text('Name', 'الاسم'),
                      text('Gender', 'النوع'),
                      text('Department', 'القسم'),
                      text('Position', 'المسمى الوظيفي'),
                      text('Mobile Number', 'رقم الموبايل'),
                      text('National ID', 'الرقم القومي'),
                      text('Employment Date', 'تاريخ التعيين'),
                      text('End Work Date', 'تاريخ انتهاء العمل'),
                      text('Status', 'الحالة'),
                      text('Validation', 'التحقق'),
                    ].map((label) => (
                      <th className="p-3 text-start" key={label}>
                        {label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-mis-border">
                  {previewRows.slice((page - 1) * 50, page * 50).map((row) => (
                    <tr key={row.row}>
                      <td className="p-3">
                        <bdi>{row.employee.employeeNumber}</bdi>
                      </td>
                      <td className="p-3">{row.employee.fullName}</td>
                      <td className="p-3">
                        {row.employee.gender === 'Male' ? text('Male', 'ذكر') : row.employee.gender === 'Female' ? text('Female', 'أنثى') : '—'}
                      </td>
                      <td className="p-3">{row.department}</td>
                      <td className="p-3">{row.position}</td>
                      <td className="p-3">
                        <bdi dir="ltr">{row.employee.mobileNumber}</bdi>
                      </td>
                      <td className="p-3">
                        <bdi>{row.employee.nationalId}</bdi>
                      </td>
                      <td className="p-3">
                        <bdi>{row.employee.workStartDate}</bdi>
                      </td>
                      <td className="p-3">
                        <bdi>{row.employee.workEndDate}</bdi>
                      </td>
                      <td className="p-3">{employeeStatus(row.employee.status)}</td>
                      <td className="p-3">
                        <StatusBadge tone={row.status === 'Ready' ? 'success' : row.status === 'Error' ? 'danger' : 'warning'}>
                          {statusLabel(row.status)}
                        </StatusBadge>
                        {row.errors.map((message, index) => (
                          <p className="mt-1 text-xs text-slate-600" key={index}>
                            {message}
                          </p>
                        ))}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={page} pageSize={50} totalCount={previewRows.length} totalPages={Math.max(1, Math.ceil(previewRows.length / 50))} onPageChange={setPage} />
          </Card>
        </>
      ) : null}

      {step === 'done' && result && preview ? (
        <Card padding="lg">
          <div className="space-y-5 text-center">
            <CheckCircle2 className="mx-auto h-12 w-12 text-emerald-600" />
            <h2 className="text-xl font-bold">{text('Import complete', 'اكتمل الاستيراد')}</h2>
            <p className="text-lg">
              {text(`Imported ${result.imported} employees successfully`, `تم استيراد ${result.imported} موظف بنجاح`)}
            </p>
            <p className="text-slate-600">
              {text('Failed / invalid rows', 'صفوف بها أخطاء')}: {result.failed}
              {' · '}
              {text('Already existed', 'موظف موجود بالفعل')}: {result.skipped}
            </p>
            <div className="flex flex-wrap justify-center gap-3">
              {link}
              <Button fullWidth={false} variant="outline" onClick={() => { setShowErrorsOnly(true); setStep('preview'); }}>
                {text('View errors', 'عرض الأخطاء')}
              </Button>
              <Button
                fullWidth={false}
                variant="outline"
                onClick={() => {
                  setStep('upload');
                  setFile(null);
                  setUpload(null);
                  setPreview(null);
                  setResult(null);
                }}
              >
                {text('Import another file', 'استيراد ملف آخر')}
              </Button>
            </div>
          </div>
        </Card>
      ) : null}

      <Card padding="lg">
        <h2 className="mb-4 text-lg font-bold">{text('Your Import History (latest 50)', 'سجل استيرادك (آخر 50)')}</h2>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr>
                {[text('File', 'الملف'), text('Uploaded by', 'رفع بواسطة'), text('Uploaded at', 'تاريخ الرفع'), text('Rows', 'الصفوف'), text('Imported / Skipped / Failed', 'تم الاستيراد / التجاوز / الفشل')].map((label) => (
                  <th key={label} className="p-3 text-start">
                    {label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {history.map((item) => (
                <tr key={item.id} className="border-t border-mis-border">
                  <td className="p-3">{item.fileName}</td>
                  <td className="p-3">{item.uploadedBy}</td>
                  <td className="p-3">{new Date(item.uploadedAt).toLocaleString(ar ? 'ar-EG' : 'en-GB')}</td>
                  <td className="p-3">{item.totalRows ?? '—'}</td>
                  <td className="p-3">
                    <bdi>{item.result ? `${item.result.imported} / ${item.result.skipped} / ${item.result.failed}` : text('Awaiting confirmation', 'في انتظار التأكيد')}</bdi>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  );
}
