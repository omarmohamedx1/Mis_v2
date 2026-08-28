import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { DateControl } from '../../components/forms/DateControl';
import { Activity, CalendarRange, ChevronRight, Filter, Search, UserRound } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Card } from '../../components/common/Card';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { Pagination } from '../../components/common/Pagination';
import { StatusBadge } from '../../components/common/StatusBadge';
import { useLocalization } from '../../context/LocalizationContext';
import { EmployeeSearchSelect } from '../../features/hr/components/EmployeeSearchSelect';
import { hrAuditService } from '../../features/hr/services/hrAuditService';
import { hrEmployeeService } from '../../features/hr/services/hrEmployeeService';
import type { AuditLogItem, PagedAuditLogs } from '../../features/hr/types/audit';
import type { EmployeeListItem } from '../../features/hr/types/employee';
import { getApiErrorMessage } from '../../services/apiClient';

const emptyPage: PagedAuditLogs = { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 };
const copy = {
  en: { title: 'HR Audit Log', subtitle: 'A searchable history of important HR changes and the users who performed them', search: 'Search action, employee, user, or description', employee: 'Employee', from: 'Date From', to: 'Date To', allActions: 'All actions', allEntities: 'All entities', noRows: 'No audit records found', noRowsHelp: 'Important HR changes will appear here automatically.', loadError: 'Unable to load the HR audit log.', by: 'by {user}', employeeLabel: 'Employee', changes: '{count} field changes', noChanges: 'No field-level changes', field: 'Field', old: 'From', next: 'To', system: 'System' },
  ar: { title: 'سجل تدقيق الموارد البشرية', subtitle: 'سجل قابل للبحث للتغييرات المهمة والمستخدمين الذين نفذوها', search: 'ابحث بالإجراء أو الموظف أو المستخدم أو الوصف', employee: 'الموظف', from: 'من تاريخ', to: 'إلى تاريخ', allActions: 'كل الإجراءات', allEntities: 'كل أنواع السجلات', noRows: 'لا توجد سجلات تدقيق', noRowsHelp: 'ستظهر هنا تغييرات الموارد البشرية المهمة تلقائيًا.', loadError: 'تعذر تحميل سجل التدقيق.', by: 'بواسطة {user}', employeeLabel: 'الموظف', changes: '{count} تغيير في الحقول', noChanges: 'لا توجد تغييرات تفصيلية', field: 'الحقل', old: 'من', next: 'إلى', system: 'النظام' },
} as const;

const actionOptions = [
  'EmployeeCreated', 'EmployeeUpdated', 'EmployeeStatusChanged', 'EmployeePersonalUpdated', 'EmployeeContactUpdated', 'EmployeeEmploymentUpdated', 'EmployeeContractUpdated', 'EmployeeCompensationUpdated', 'EmployeeEmergencyContactUpdated',
  'AttendanceAdded', 'AttendanceUpdated', 'AttendanceDeleted', 'AttendanceImportUploaded', 'AttendanceImportPreviewed', 'AttendanceImported', 'AttendanceImportCancelled', 'AttendanceDayProcessed',
  'LeaveCreated', 'LeaveUpdated', 'LeaveApproved', 'LeaveRejected', 'LeaveCancelled', 'LeaveEntitlementCreated', 'LeaveEntitlementUpdated',
  'AbsenceCreated', 'AbsenceUpdated', 'AbsenceDeleted', 'AbsencePayrollDeductionApproved', 'AbsencePayrollDeductionExcluded', 'DocumentUploaded', 'DocumentUpdated', 'DocumentReplaced', 'DocumentDeleted',
  'DelegationCreated', 'DelegationUpdated', 'DelegationCancelled', 'MasterDataCreated', 'MasterDataUpdated',
  'WorkingCalendarUpdated', 'CalendarExceptionCreated', 'CalendarExceptionUpdated', 'CalendarExceptionStatusChanged', 'CalendarExceptionDeleted',
] as const;

const entityOptions = [
  'Employee', 'EmployeeContract', 'EmployeeCompensation', 'EmployeeEmergencyContact', 'AttendanceRecord', 'AttendanceImportBatch', 'AttendanceDay', 'LeaveRequest', 'EmployeeLeaveEntitlement', 'EmployeeAbsence', 'EmployeeDocument', 'EmployeeDelegation', 'Department', 'Position', 'Branch', 'EmploymentType', 'ContractType', 'LeaveType', 'DocumentType', 'DelegationType', 'WorkingCalendar', 'CalendarException',
] as const;

const arabicActions: Record<string, string> = {
  EmployeeCreated: 'إنشاء موظف', EmployeeUpdated: 'تحديث الموظف', EmployeeStatusChanged: 'تغيير حالة الموظف', EmployeePersonalUpdated: 'تحديث البيانات الشخصية', EmployeeContactUpdated: 'تحديث بيانات التواصل', EmployeeEmploymentUpdated: 'تحديث بيانات العمل', EmployeeContractUpdated: 'تحديث عقد الموظف', EmployeeCompensationUpdated: 'تحديث بيانات الراتب', EmployeeEmergencyContactUpdated: 'تحديث جهة اتصال الطوارئ',
  AttendanceAdded: 'إضافة حضور', AttendanceUpdated: 'تحديث الحضور', AttendanceDeleted: 'حذف الحضور', AttendanceImportUploaded: 'رفع ملف حضور', AttendanceImportPreviewed: 'معاينة استيراد الحضور', AttendanceImported: 'استيراد الحضور', AttendanceImportCancelled: 'إلغاء استيراد الحضور', AttendanceDayProcessed: 'معالجة حضور اليوم',
  LeaveCreated: 'إنشاء طلب إجازة', LeaveUpdated: 'تحديث طلب الإجازة', LeaveApproved: 'قبول الإجازة', LeaveRejected: 'رفض الإجازة', LeaveCancelled: 'إلغاء الإجازة', LeaveEntitlementCreated: 'إنشاء استحقاق إجازة', LeaveEntitlementUpdated: 'تحديث استحقاق الإجازة',
  AbsenceCreated: 'تسجيل غياب', AbsenceUpdated: 'تحديث الغياب', AbsenceDeleted: 'حذف الغياب', AbsencePayrollDeductionApproved: 'اعتماد خصم الغياب من المرتب', AbsencePayrollDeductionExcluded: 'استبعاد الغياب من خصومات المرتب', DocumentUploaded: 'رفع مستند', DocumentUpdated: 'تحديث المستند', DocumentReplaced: 'استبدال المستند', DocumentDeleted: 'حذف المستند',
  DelegationCreated: 'إنشاء تفويض', DelegationUpdated: 'تحديث التفويض', DelegationCancelled: 'إلغاء التفويض', MasterDataCreated: 'إنشاء بيانات أساسية', MasterDataUpdated: 'تحديث البيانات الأساسية',
  WorkingCalendarUpdated: 'تحديث تقويم العمل', CalendarExceptionCreated: 'إنشاء استثناء تقويم', CalendarExceptionUpdated: 'تحديث استثناء التقويم', CalendarExceptionStatusChanged: 'تغيير حالة استثناء التقويم', CalendarExceptionDeleted: 'حذف استثناء التقويم',
};

const arabicEntities: Record<string, string> = {
  Employee: 'الموظف', EmployeeContract: 'عقد الموظف', EmployeeCompensation: 'راتب الموظف', EmployeeEmergencyContact: 'جهة اتصال الطوارئ', AttendanceRecord: 'سجل الحضور', AttendanceImportBatch: 'دفعة استيراد الحضور', AttendanceDay: 'يوم الحضور', LeaveRequest: 'طلب الإجازة', EmployeeLeaveEntitlement: 'استحقاق الإجازة', EmployeeAbsence: 'غياب الموظف', EmployeeDocument: 'مستند الموظف', EmployeeDelegation: 'تفويض الموظف', Department: 'القسم', Position: 'المسمى الوظيفي', Branch: 'الفرع', EmploymentType: 'نوع التوظيف', ContractType: 'نوع العقد', LeaveType: 'نوع الإجازة', DocumentType: 'نوع المستند', DelegationType: 'نوع التفويض', WorkingCalendar: 'تقويم العمل', CalendarException: 'استثناء التقويم',
};

const arabicFields: Record<string, string> = {
  Id: 'المعرّف', EmployeeId: 'معرّف الموظف', EmployeeNumber: 'رقم الموظف', FullName: 'الاسم الكامل', FullNameArabic: 'الاسم الكامل بالعربية', FullNameEnglish: 'الاسم الكامل بالإنجليزية', NationalId: 'الرقم القومي', DateOfBirth: 'تاريخ الميلاد', Gender: 'النوع', MaritalStatus: 'الحالة الاجتماعية', ProfilePhotoPath: 'الصورة الشخصية', EmployeeStatus: 'حالة الموظف', MobileNumber: 'رقم الهاتف', AlternativeMobile: 'رقم الهاتف البديل', Email: 'البريد الإلكتروني', Address: 'العنوان', City: 'المدينة', DepartmentId: 'القسم', DepartmentName: 'اسم القسم', PositionId: 'المسمى الوظيفي', PositionName: 'اسم المسمى الوظيفي', BranchId: 'الفرع', BranchName: 'اسم الفرع', DirectManagerId: 'المدير المباشر', DirectManagerName: 'اسم المدير المباشر', HireDate: 'تاريخ التعيين', EmploymentTypeId: 'نوع التوظيف', EmploymentTypeName: 'اسم نوع التوظيف', TerminationDate: 'تاريخ إنهاء الخدمة', TerminationReason: 'سبب إنهاء الخدمة', Status: 'الحالة', Notes: 'الملاحظات', StartDate: 'تاريخ البداية', EndDate: 'تاريخ النهاية', ContractTypeId: 'نوع العقد', ContractTypeName: 'اسم نوع العقد', ProbationStartDate: 'بداية فترة الاختبار', ProbationEndDate: 'نهاية فترة الاختبار', BasicSalary: 'الراتب الأساسي', Allowances: 'البدلات', TotalSalary: 'إجمالي الراتب', BankName: 'اسم البنك', BankAccount: 'الحساب البنكي', Iban: 'رقم IBAN', EffectiveFrom: 'ساري من', EffectiveTo: 'ساري إلى', ContactName: 'اسم جهة الاتصال', Relationship: 'صلة القرابة', AlternativeNumber: 'الرقم البديل', AttendanceDate: 'تاريخ الحضور', CheckIn: 'وقت الحضور', CheckOut: 'وقت الانصراف', WorkingHours: 'ساعات العمل', LateMinutes: 'دقائق التأخير', EarlyLeaveMinutes: 'دقائق الانصراف المبكر', OvertimeMinutes: 'دقائق الوقت الإضافي', Source: 'المصدر', Reason: 'السبب', SuggestedDeductionAmount: 'الخصم المقترح', ApprovedDeductionAmount: 'الخصم المعتمد', PayrollImpactStatus: 'التأثير على المرتب', PayrollNotes: 'ملاحظات مراجعة المرتب', PayrollReviewedByUsername: 'مراجع الخصم', PayrollReviewedAt: 'تاريخ مراجعة الخصم', LeaveTypeId: 'نوع الإجازة', NumberOfDays: 'عدد الأيام', RequestDate: 'تاريخ الطلب', DecisionNotes: 'ملاحظات القرار', DecisionAt: 'تاريخ القرار', Entitled: 'المستحق', Used: 'المستخدم', Pending: 'قيد الانتظار', Remaining: 'المتبقي', FileName: 'اسم الملف', FileSize: 'حجم الملف', MimeType: 'نوع الملف', Sha256Hash: 'بصمة الملف', IssueDate: 'تاريخ الإصدار', ExpiryDate: 'تاريخ الانتهاء', DelegationNumber: 'رقم التفويض', Subject: 'موضوع التفويض', AuthorizedEntity: 'جهة التفويض', Purpose: 'الغرض', CancellationReason: 'سبب الإلغاء', CancelledAt: 'تاريخ الإلغاء', IsActive: 'نشط', Name: 'الاسم', NameEnglish: 'الاسم بالإنجليزية', NameArabic: 'الاسم بالعربية', Code: 'الكود', Description: 'الوصف', Date: 'التاريخ', Type: 'النوع', TimeZoneId: 'المنطقة الزمنية', CreatedAt: 'تاريخ الإنشاء', UpdatedAt: 'تاريخ التحديث', CreatedBy: 'أنشأ بواسطة', UpdatedBy: 'حدّث بواسطة',
};

const arabicValues: Record<string, string> = {
  Yes: 'نعم', No: 'لا', true: 'نعم', false: 'لا', Active: 'نشط', Inactive: 'غير نشط', OnLeave: 'في إجازة', Suspended: 'موقوف', Terminated: 'منتهي الخدمة', Draft: 'مسودة', Expired: 'منتهي', Cancelled: 'ملغي', Pending: 'قيد الانتظار', Approved: 'مقبول', Rejected: 'مرفوض', Excused: 'بعذر', Unexcused: 'بدون عذر', Present: 'حاضر', Absent: 'غائب', Late: 'متأخر', Leave: 'إجازة', Holiday: 'عطلة', Weekend: 'إجازة أسبوعية', Manual: 'يدوي', ExcelImport: 'استيراد Excel', DeviceIntegration: 'ربط جهاز البصمة', SystemProcessing: 'معالجة النظام', Male: 'ذكر', Female: 'أنثى', Other: 'أخرى', Single: 'أعزب', Married: 'متزوج', Divorced: 'مطلق', Widowed: 'أرمل', OfficialHoliday: 'عطلة رسمية', CompanyHoliday: 'عطلة الشركة', SpecialWorkingDay: 'يوم عمل خاص', NonWorking: 'يوم غير عامل', CustomWorkingHours: 'ساعات عمل مخصصة',
};

function splitIdentifier(value: string): string {
  return value.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/[._]/g, ' · ');
}

function labelAction(value: string, language: 'en' | 'ar'): string {
  return language === 'ar' ? arabicActions[value] ?? 'إجراء موارد بشرية' : splitIdentifier(value);
}

function labelEntity(value: string, language: 'en' | 'ar'): string {
  return language === 'ar' ? arabicEntities[value] ?? 'سجل موارد بشرية' : splitIdentifier(value);
}

function labelField(value: string, language: 'en' | 'ar'): string {
  if (language === 'en') return splitIdentifier(value);
  const leaf = value.split(/[._]/).at(-1) ?? value;
  return arabicFields[value] ?? arabicFields[leaf] ?? 'حقل بيانات';
}

function formatValue(value: string | null, language: 'en' | 'ar', locale: string): string {
  if (value === null || value === '') return '—';
  if (language === 'ar' && arabicValues[value]) return arabicValues[value];
  if (language === 'en' && (value === 'true' || value === 'false')) return value === 'true' ? 'Yes' : 'No';
  if (/^\d{4}-\d{2}-\d{2}(?:T[\d:.+-]+Z?)?$/.test(value)) {
    const parsed = new Date(value.includes('T') ? value : `${value}T12:00:00`);
    if (!Number.isNaN(parsed.getTime())) return new Intl.DateTimeFormat(locale, value.includes('T') ? { dateStyle: 'medium', timeStyle: 'short' } : { dateStyle: 'medium' }).format(parsed);
  }
  return value;
}

function localizedDescription(item: AuditLogItem, language: 'en' | 'ar'): string {
  if (!item.description) return labelAction(item.action, language);
  if (language === 'en' || /[\u0600-\u06ff]/.test(item.description)) return item.description;
  return labelAction(item.action, language);
}

function toStart(value: string) { return value ? new Date(`${value}T00:00:00`).toISOString() : undefined; }
function toEnd(value: string) { return value ? new Date(`${value}T23:59:59.999`).toISOString() : undefined; }

function AuditRecord({ item, language, locale }: { item: AuditLogItem; language: 'en' | 'ar'; locale: string }) {
  const text = copy[language];
  return (
    <article className="p-5 sm:p-6">
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
        <div className="flex min-w-0 gap-3">
          <div className="mt-0.5 flex h-10 w-10 flex-none items-center justify-center rounded-xl bg-mis-pale text-mis-primary"><Activity className="h-5 w-5" /></div>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2"><h2 className="font-bold text-mis-navy">{labelAction(item.action, language)}</h2><StatusBadge tone="neutral">{labelEntity(item.entityType, language)}</StatusBadge></div>
            <p className="mt-1 text-sm leading-5 text-slate-600">{localizedDescription(item, language)}</p>
            <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500">
              <span>{text.by.replace('{user}', item.username || text.system)}</span>
              <time dateTime={item.timestamp}>{new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(item.timestamp))}</time>
              {item.employeeName && item.employeeId ? <Link className="font-semibold text-mis-primary" to={`/hr/employees/${item.employeeId}`}>{text.employeeLabel}: {item.employeeName}</Link> : null}
            </div>
          </div>
        </div>
      </div>
      {item.changes.length ? (
        <details className="group mt-4 rounded-xl border border-mis-border">
          <summary className="flex cursor-pointer list-none items-center justify-between px-4 py-3 text-sm font-semibold text-mis-navy"><span>{text.changes.replace('{count}', String(item.changes.length))}</span><ChevronRight className="h-4 w-4 transition group-open:rotate-90 rtl:group-open:-rotate-90" /></summary>
          <div className="overflow-x-auto border-t border-mis-border">
            <table className="w-full min-w-[620px]">
              <thead className="bg-mis-surface text-xs uppercase text-slate-500"><tr><th className="px-4 py-2 text-start">{text.field}</th><th className="px-4 py-2 text-start">{text.old}</th><th className="px-4 py-2 text-start">{text.next}</th></tr></thead>
              <tbody className="divide-y divide-mis-border">{item.changes.map((change) => <tr key={change.field}><td className="px-4 py-3 text-sm font-semibold text-slate-700">{labelField(change.field, language)}</td><td dir="auto" className="max-w-sm whitespace-pre-wrap break-words px-4 py-3 text-sm text-slate-500">{formatValue(change.oldValue, language, locale)}</td><td dir="auto" className="max-w-sm whitespace-pre-wrap break-words px-4 py-3 text-sm text-mis-navy">{formatValue(change.newValue, language, locale)}</td></tr>)}</tbody>
            </table>
          </div>
        </details>
      ) : <p className="mt-3 text-xs text-slate-400">{text.noChanges}</p>}
    </article>
  );
}

export function HrAuditPage() {
  const { language, t } = useLocalization();
  const text = copy[language];
  const locale = language === 'ar' ? 'ar-EG' : 'en-GB';
  const [searchParams] = useSearchParams();
  const initialEmployeeId = searchParams.get('employeeId') ?? '';
  const [query, setQuery] = useState({ page: 1, pageSize: 20, search: '', action: '', entityType: '', employeeId: initialEmployeeId, from: '', to: '' });
  const [search, setSearch] = useState('');
  const [data, setData] = useState(emptyPage);
  const [employee, setEmployee] = useState<EmployeeListItem | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    const timer = window.setTimeout(() => setQuery((current) => ({ ...current, search: search.trim(), page: 1 })), 300);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    if (!initialEmployeeId) return;
    void hrEmployeeService.getEmployee(initialEmployeeId).then(setEmployee).catch(() => undefined);
  }, [initialEmployeeId]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setData(await hrAuditService.getPaged({ page: query.page, pageSize: query.pageSize, search: query.search || undefined, action: query.action || undefined, entityType: query.entityType || undefined, employeeId: query.employeeId || undefined, from: toStart(query.from), to: toEnd(query.to) }));
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, text.loadError));
    } finally {
      setLoading(false);
    }
  }, [language, query, text.loadError]);

  useEffect(() => { void load(); }, [load]);

  return (
    <div className="mx-auto max-w-[1400px]">
      <PageHeader description={text.subtitle} eyebrow={t('hrDepartment')} title={text.title} />
      <Card padding="none">
        <div className="grid gap-4 border-b border-slate-200 bg-slate-50/60 p-4 md:grid-cols-2 lg:p-5">
          <label className="relative self-end"><span className="mb-2 block text-sm font-bold text-slate-700">{language === 'ar' ? 'البحث' : 'Search'}</span><Search className="absolute start-3 bottom-3 h-5 w-5 text-mis-primary" /><input aria-label={text.search} className="h-11 w-full rounded-xl border border-slate-300 bg-white pe-3 ps-10 text-sm shadow-sm" onChange={(event) => setSearch(event.target.value)} placeholder={text.search} value={search} /></label>
          <EmployeeSearchSelect initialSelection={employee} label={text.employee} onChange={(id, item) => { setEmployee(item); setQuery((current) => ({ ...current, employeeId: id, page: 1 })); }} value={query.employeeId} />
          <div className="grid gap-3 md:col-span-2 sm:grid-cols-2 xl:grid-cols-4">
            <label><span className="mb-2 flex items-center gap-2 text-sm font-bold text-slate-700"><Filter className="h-4 w-4 text-mis-primary" />{language === 'ar' ? 'الإجراء' : 'Action'}</span><ProfessionalSelect aria-label={text.allActions} className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3 text-sm shadow-sm" onChange={(event) => setQuery((current) => ({ ...current, action: event.target.value, page: 1 }))} value={query.action}><option value="">{text.allActions}</option>{actionOptions.map((action) => <option key={action} value={action}>{labelAction(action, language)}</option>)}</ProfessionalSelect></label>
            <label><span className="mb-2 flex items-center gap-2 text-sm font-bold text-slate-700"><Filter className="h-4 w-4 text-mis-primary" />{language === 'ar' ? 'نوع السجل' : 'Record type'}</span><ProfessionalSelect aria-label={text.allEntities} className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3 text-sm shadow-sm" onChange={(event) => setQuery((current) => ({ ...current, entityType: event.target.value, page: 1 }))} value={query.entityType}><option value="">{text.allEntities}</option>{entityOptions.map((entity) => <option key={entity} value={entity}>{labelEntity(entity, language)}</option>)}</ProfessionalSelect></label>
            <label><span className="mb-2 flex items-center gap-2 text-sm font-bold text-slate-700"><CalendarRange className="h-4 w-4 text-mis-primary" />{text.from}</span><DateControl aria-label={text.from} className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3 text-sm shadow-sm" onChange={(event) => setQuery((current) => ({ ...current, from: event.target.value, page: 1 }))}  value={query.from} /></label>
            <label><span className="mb-2 flex items-center gap-2 text-sm font-bold text-slate-700"><CalendarRange className="h-4 w-4 text-mis-primary" />{text.to}</span><DateControl aria-label={text.to} className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3 text-sm shadow-sm" min={query.from || undefined} onChange={(event) => setQuery((current) => ({ ...current, to: event.target.value, page: 1 }))}  value={query.to} /></label>
          </div>
        </div>
        {error ? <div className="p-5"><ErrorState compact message={error} onRetry={() => void load()} title={text.loadError} /></div> : loading ? <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div> : !data.items.length ? <EmptyState description={text.noRowsHelp} icon={<UserRound />} title={text.noRows} /> : <><div className="divide-y divide-mis-border">{data.items.map((item) => <AuditRecord item={item} key={item.id} language={language} locale={locale} />)}</div><Pagination onPageChange={(page) => setQuery((current) => ({ ...current, page }))} page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} totalPages={data.totalPages} /></>}
      </Card>
    </div>
  );
}
