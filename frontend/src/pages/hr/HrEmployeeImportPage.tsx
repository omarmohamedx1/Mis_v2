import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Upload, Eye, CheckCircle2, AlertTriangle, Download, FileSpreadsheet, Loader2, Pencil, Trash2 } from 'lucide-react';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge } from '../../components/common/StatusBadge';
import { FileInput } from '../../components/forms/FileInput';
import { TextInput } from '../../components/forms/TextInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { useLocalization } from '../../context/LocalizationContext';
import { getApiErrorMessage } from '../../services/apiClient';
import { EmployeeImportRowEditor } from '../../features/hr/components/EmployeeImportRowEditor';
import {
  autoMapEmployeeImportColumns,
  employeeImportFields,
  missingRequiredEmployeeImportFields,
  pickEmployeeImportSheetIndex,
} from '../../features/hr/employeeImportAutoMap';
import { hrEmployeeService } from '../../features/hr/services/hrEmployeeService';
import { hrMasterDataService } from '../../features/hr/services/hrMasterDataService';
import type { DepartmentOption, EmployeeOrganizationAssignment } from '../../features/hr/types/employee';
import type { MasterDataLookup } from '../../features/hr/types/masterData';
import {
  employeeImportService,
  type EmployeeImportEmployee,
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
  const [historyError, setHistoryError] = useState('');
  const [showErrorsOnly, setShowErrorsOnly] = useState(false);
  const [downloadingTemplate, setDownloadingTemplate] = useState(false);
  const [dragging, setDragging] = useState(false);
  const [editRow, setEditRow] = useState<number | null>(null);
  const [departments, setDepartments] = useState<DepartmentOption[]>([]);
  const [organizations, setOrganizations] = useState<EmployeeOrganizationAssignment[]>([]);
  const [positions, setPositions] = useState<MasterDataLookup[]>([]);
  const [historyToDelete, setHistoryToDelete] = useState<EmployeeImportHistory | null>(null);
  const [deletingHistory, setDeletingHistory] = useState(false);

  async function loadHistory() {
    try {
      setHistory(await employeeImportService.history());
      setHistoryError('');
    } catch (reason) {
      setHistoryError(getApiErrorMessage(reason, text('Unable to load import history.', 'تعذر تحميل سجل الاستيراد.')));
    }
  }

  useEffect(() => {
    void Promise.all([hrEmployeeService.getDepartments(), hrEmployeeService.getOrganizations(), hrMasterDataService.getLookup('positions', true)])
      .then(([departmentItems, organizationItems, positionItems]) => {
        setDepartments(departmentItems);
        setOrganizations(organizationItems);
        setPositions(positionItems);
      })
      .catch(() => undefined);
  }, []);

  useEffect(() => {
    void loadHistory();
  }, [result, upload, preview]);

  async function deleteHistoryRecord() {
    if (!historyToDelete) return;
    setDeletingHistory(true);
    setHistoryError('');
    try {
      await employeeImportService.deleteHistory(historyToDelete.id);
      setHistory((items) => items.filter((item) => item.id !== historyToDelete.id));
      setHistoryToDelete(null);
    } catch (reason) {
      setHistoryError(getApiErrorMessage(reason, text('Unable to delete the import record.', 'تعذر مسح سجل الاستيراد.')));
    } finally {
      setDeletingHistory(false);
    }
  }

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError('');
    try {
      await action();
    } catch (reason) {
      setError(getApiErrorMessage(reason, text('Employee import failed.', 'تعذر استيراد الموظفين.')));
      setStep((current) => (current === 'working' ? 'upload' : current));
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
    setMissing(gaps.map((field) => text(field.en, field.ar)));
    setStep('working');
    setPhase(text('Reading file and validating rows…', 'جاري قراءة الملف والتحقق من البيانات…'));
    const built = await employeeImportService.preview(data.id, nextMapping);
    setPreview(built);
    setShowErrorsOnly(built.rows.some((row) => row.status === 'Error'));
    setStep('preview');
  }

  async function sendFile(selected?: File | null) {
    const nextFile = selected ?? file;
    if (!nextFile) return;
    if (!/\.(xlsx|xls|csv)$/i.test(nextFile.name) || nextFile.size > 20 * 1024 * 1024) {
      setError(text('Choose CSV, XLS, or XLSX, maximum 20 MB.', 'اختر CSV أو XLS أو XLSX بحد أقصى 20 ميجابايت.'));
      return;
    }
    setFile(nextFile);
    await run(async () => {
      setStep('working');
      setPhase(text('Reading file…', 'جاري قراءة الملف…'));
      const data = await employeeImportService.upload(nextFile);
      const index = pickEmployeeImportSheetIndex(data.sheets);
      const next = buildMapping(data, index);
      await autoImportWith(data, next, index);
    });
  }

  async function saveCorrection(rowNumber: number, employee: EmployeeImportEmployee) {
    if (!upload || !preview) return;
    await run(async () => {
      const next = await employeeImportService.revise(upload.id, preview.previewId, [{ row: rowNumber, employee }]);
      setPreview(next);
      setEditRow(null);
    });
  }

  async function downloadTemplate() {
    setDownloadingTemplate(true);
    setError('');
    try {
      await employeeImportService.downloadTemplate();
    } catch (reason) {
      setError(getApiErrorMessage(reason, text('Unable to download the employee template.', 'تعذر تنزيل قالب الموظفين.')));
    } finally {
      setDownloadingTemplate(false);
    }
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
  const importableCount = preview?.rows.filter((row) => row.status === 'Ready' || row.status === 'Warning').length ?? 0;

  return (
    <div className="space-y-5">
      <PageHeader
        title={text('Import Employees', 'استيراد موظفين')}
        description={text(
          'Choose an employee Excel file and it uploads immediately. Fix any row errors here, then confirm. Existing employees are never overwritten.',
          'اختار ملف الموظفين وهيترفع ويتراجع لوحده. صحّح الأخطاء من هنا بعدين أكّد. الموظفين الموجودين مش هيتعدلوا.',
        )}
        actions={<div className="flex flex-wrap gap-2"><Button fullWidth={false} isLoading={downloadingTemplate} leftIcon={<Download className="h-4 w-4" />} onClick={() => void downloadTemplate()} variant="outline">{text('Download Excel template', 'تنزيل قالب Excel')}</Button>{link}</div>}
      />

      <div className="grid grid-cols-2 overflow-hidden rounded-2xl border border-mis-border bg-white sm:grid-cols-4">
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
        <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_320px]">
          <Card
            className={dragging ? 'border-mis-primary bg-mis-pale/40' : undefined}
            padding="lg"
            onDragOver={(event) => { event.preventDefault(); setDragging(true); }}
            onDragLeave={() => setDragging(false)}
            onDrop={(event) => {
              event.preventDefault();
              setDragging(false);
              const next = event.dataTransfer.files?.[0] ?? null;
              if (next) void sendFile(next);
            }}
          >
            <div className="mx-auto max-w-2xl space-y-5">
            <div className="flex items-start gap-3">
              <Upload className="mt-1 h-6 w-6 text-mis-primary" />
              <div>
                <h2 className="font-bold text-mis-navy">{text('Choose the file and it starts immediately', 'اختار الملف وهيشتغل لوحده')}</h2>
                <p className="mt-1 text-sm text-slate-600">{text('No extra upload click. Review the preview, fix errors in place, then confirm.', 'من غير زرار رفع إضافي. راجع المعاينة، صحّح الأخطاء هنا، وبعدين أكّد.')}</p>
              </div>
            </div>
            <FileInput
              accept=".xlsx,.xls,.csv"
              label={text('Employee Excel / CSV file', 'ملف الموظفين Excel / CSV')}
              hint={text('The file starts processing as soon as you choose it. Maximum 20 MB and 2,000 rows.', 'الملف بيشتغل أول ما تختاره. بحد أقصى 20 ميجابايت و2000 صف.')}
              onChange={(event) => {
                const next = event.target.files?.[0] ?? null;
                void sendFile(next);
              }}
              required
            />
            <p className="text-sm text-slate-500">{text('You can also drop the file on this card.', 'تقدر كمان تسحب الملف وتفلته هنا.')}</p>
            </div>
          </Card>
          <Card className="border-sky-200 bg-sky-50/60" padding="lg">
            <FileSpreadsheet className="h-9 w-9 text-mis-primary" />
            <h2 className="mt-4 font-bold text-mis-navy">{text('Start with the official template', 'ابدأ بالقالب الرسمي')}</h2>
            <p className="mt-2 text-sm leading-6 text-slate-600">{text('It includes the required columns, bilingual instructions, and the current departments and positions from the system.', 'يحتوي على الأعمدة المطلوبة وتعليمات بالعربية والإنجليزية، بالإضافة إلى الأقسام والمسميات الحالية من النظام.')}</p>
            <Button className="mt-5" isLoading={downloadingTemplate} leftIcon={<Download className="h-4 w-4" />} onClick={() => void downloadTemplate()} variant="secondary">{text('Download template', 'تنزيل القالب')}</Button>
          </Card>
        </div>
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
                required={field.requiredInFile}
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
                'Department is the internal HR unit. Banks and finance companies map to Assigned Bank / Company, and those employees are placed in Collections.',
                'القسم هو الوحدة الداخلية. البنوك وشركات التمويل تُعيَّن في البنك / الشركة، ويُنقل موظفوها إلى قسم التحصيل.',
              )}
            </p>
            <div className="flex justify-end gap-3 sm:col-span-2 lg:col-span-3">
              <Button disabled={busy} fullWidth={false} type="button" variant="outline" onClick={() => setStep(preview ? 'preview' : 'upload')}>
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
          {missing.length ? (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-amber-950">
              <p className="font-semibold">{text('Some columns were not recognized. You can still fix those values in the table.', 'بعض الأعمدة مش متعرفة. تقدر تعدّل القيم دي في الجدول.')}</p>
              <p className="mt-1 text-sm">{missing.join(' · ')}</p>
            </div>
          ) : null}
          <div className={`rounded-xl border p-4 ${importableCount > 0 ? 'border-blue-200 bg-blue-50 text-blue-900' : 'border-amber-200 bg-amber-50 text-amber-900'}`}>
            {importableCount > 0
              ? text(
                  `${importableCount} row(s) are ready. Fix remaining errors here, then confirm. Nothing has been saved yet.`,
                  `${importableCount} صف جاهز. صحّح الباقي من هنا بعدين أكّد. لسه مفيش حاجة اتحفظت.`,
                )
              : text(
                  'No valid employees are ready yet. Open a row with an error and correct it here, without going back to Excel.',
                  'مفيش صفوف جاهزة لسه. افتح الصف اللي فيه الغلط وصحّحه من هنا من غير ما ترجع للإكسل.',
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
            <Button
              disabled={busy || importableCount === 0}
              fullWidth={false}
              isLoading={busy}
              onClick={() => void run(async () => {
                const confirmed = await employeeImportService.confirm(preview.id, preview.previewId);
                setResult(confirmed);
                setStep('done');
              })}
            >
              {text(`Confirm import (${importableCount})`, `تأكيد استيراد (${importableCount})`)}
            </Button>
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
              <table className="w-full min-w-[1750px] text-sm">
                <thead className="bg-mis-surface">
                  <tr>
                    {[
                      text('Action', 'الإجراء'),
                      text('Employee Number', 'رقم الموظف'),
                      text('Arabic name', 'الاسم بالعربية'),
                      text('English name', 'الاسم بالإنجليزية'),
                      text('Gender', 'النوع'),
                      text('Department', 'القسم'),
                      text('Assigned bank / company', 'البنك / الشركة'),
                      text('Work Number', 'رقم الشغل'),
                      text('Package Type', 'نوع الباقة'),
                      text('Position', 'المسمى الوظيفي'),
                      text('Mobile Number', 'رقم الموبايل'),
                      text('National ID', 'الرقم القومي'),
                      text('Date of Birth', 'تاريخ الميلاد'),
                      text('Employment Date', 'تاريخ التعيين'),
                      text('Fingerprint Date', 'تاريخ البصمة'),
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
                  {previewRows.map((row) => (
                    <tr className={`border-t border-mis-border ${row.status === 'Error' ? 'bg-red-50/40' : row.status === 'Warning' ? 'bg-amber-50/30' : ''}`} key={row.row}>
                      <td className="p-3">
                        {row.status === 'Existing' ? null : (
                          <button className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-bold text-mis-primary hover:bg-mis-pale" onClick={() => setEditRow(row.row)} type="button">
                            <Pencil className="h-3.5 w-3.5" />
                            {text('Edit', 'تعديل')}
                          </button>
                        )}
                      </td>
                      <td className="p-3">
                        <bdi>{row.employee.employeeNumber}</bdi>
                      </td>
                      <td className="p-3" dir="rtl">{row.employee.fullNameArabic || '—'}</td>
                      <td className="p-3" dir="ltr">{row.employee.fullNameEnglish || '—'}</td>
                      <td className="p-3">
                        {row.employee.gender === 'Male' ? text('Male', 'ذكر') : row.employee.gender === 'Female' ? text('Female', 'أنثى') : '—'}
                      </td>
                      <td className="p-3">
                        <span>{row.department}</span>
                        {row.sourceDepartment && row.sourceDepartment !== row.department ? (
                          <p className="mt-1 text-xs text-amber-700">{text('File value', 'قيمة الملف')}: {row.sourceDepartment}</p>
                        ) : null}
                      </td>
                      <td className="p-3">{row.organization || '—'}</td>
                      <td className="p-3">{row.employee.workNumber || '—'}</td>
                      <td className="p-3">{row.employee.packageType || '—'}</td>
                      <td className="p-3">
                        <span>{row.position}</span>
                        {row.sourcePosition && row.sourcePosition !== row.position ? (
                          <p className="mt-1 text-xs text-amber-700">{text('File value', 'قيمة الملف')}: {row.sourcePosition}</p>
                        ) : null}
                      </td>
                      <td className="p-3">
                        <bdi dir="ltr">{row.employee.mobileNumber}</bdi>
                      </td>
                      <td className="p-3">
                        <bdi>{row.employee.nationalId}</bdi>
                      </td>
                      <td className="p-3">
                        <bdi>{row.employee.dateOfBirth ?? '—'}</bdi>
                      </td>
                      <td className="p-3">
                        <bdi>{row.employee.workStartDate}</bdi>
                      </td>
                      <td className="p-3">
                        <bdi>{row.employee.fingerprintEnrollmentDate ?? '—'}</bdi>
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
          </Card>
          {editRow != null && preview.rows.find((row) => row.row === editRow) ? (
            <EmployeeImportRowEditor
              departments={departments}
              language={language}
              onClose={() => setEditRow(null)}
              onSave={(employee) => void saveCorrection(editRow, employee)}
              organizations={organizations}
              positions={positions}
              row={preview.rows.find((row) => row.row === editRow)!}
              saving={busy}
              text={text}
            />
          ) : null}
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
        {historyError ? <p className="mb-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">{historyError}</p> : null}
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr>
                {[text('File', 'الملف'), text('Uploaded by', 'رفع بواسطة'), text('Uploaded at', 'تاريخ الرفع'), text('Rows', 'الصفوف'), text('Imported / Skipped / Failed', 'تم الاستيراد / التجاوز / الفشل'), text('Action', 'الإجراء')].map((label) => (
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
                  <td className="p-3">
                    <Button
                      disabled={deletingHistory}
                      fullWidth={false}
                      leftIcon={<Trash2 className="h-4 w-4" />}
                      onClick={() => setHistoryToDelete(item)}
                      size="sm"
                      type="button"
                      variant="outline"
                    >
                      {text('Delete', 'مسح')}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
      <ConfirmDialog
        confirmLabel={text('Delete', 'مسح')}
        isConfirming={deletingHistory}
        message={text(
          `Remove ${historyToDelete?.fileName ?? 'this import'} from your history? Employees already imported stay in the system.`,
          `مسح سجل ${historyToDelete?.fileName ?? 'الاستيراد'}؟ الموظفين اللي اتشافوا هيفضلوا في السيستم.`,
        )}
        onCancel={() => { if (!deletingHistory) setHistoryToDelete(null); }}
        onConfirm={() => void deleteHistoryRecord()}
        open={historyToDelete != null}
        title={text('Delete import record', 'مسح سجل الاستيراد')}
      />
    </div>
  );
}
