import { HrExcusesPage } from './HrExcusesPage';
import { HrSocialInsurancePage } from './HrSocialInsurancePage';
import { useAuth } from '../../context/AuthContext';
import {
  ArrowLeft, BadgeDollarSign, BriefcaseBusiness, CalendarCheck2, CalendarDays,
  ContactRound, ExternalLink, FileSignature, Files, HeartHandshake, History,
  LayoutDashboard, ScrollText, Search, ShieldAlert, UserRound, UserRoundX,
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { Pagination } from '../../components/common/Pagination';
import { Section } from '../../components/common/Section';
import { StatusBadge, type StatusTone } from '../../components/common/StatusBadge';
import { Tabs } from '../../components/common/Tabs';
import { useToast } from '../../components/common/Toast';
import { DateInput } from '../../components/forms/DateInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { TextInput } from '../../components/forms/TextInput';
import { useLocalization } from '../../context/LocalizationContext';
import { hrAuditService } from '../../features/hr/services/hrAuditService';
import { hrEmployeeProfileService } from '../../features/hr/services/hrEmployeeProfileService';
import { hrEmployeeService } from '../../features/hr/services/hrEmployeeService';
import { hrMasterDataService } from '../../features/hr/services/hrMasterDataService';
import type { AuditLogItem, PagedAuditLogs } from '../../features/hr/types/audit';
import {
  contractStatuses,
  employeeStatuses,
  type ChangeEmployeeStatusRequest,
  type ContractStatus,
  type EmployeeContactInformation,
  type EmployeePersonalInformation,
  type EmployeeProfile,
  type EmployeeReportingLine,
  type EmployeeStatus,
  type UpdateEmployeeCompensationRequest,
  type UpdateEmployeeContractRequest,
  type UpdateEmployeeEmergencyContactRequest,
  type UpdateEmployeeEmploymentRequest,
} from '../../features/hr/types/employeeProfile';
import type { EmployeeListItem } from '../../features/hr/types/employee';
import type { MasterDataLookup } from '../../features/hr/types/masterData';
import type { TranslationKey } from '../../localization/translations';
import { getApiErrorMessage } from '../../services/apiClient';

type ProfileTab = 'overview' | 'personal' | 'employment' | 'contract' | 'compensation' | 'emergency' | 'documents' | 'attendance' | 'leaves' | 'absences' | 'delegations' | 'socialInsurance' | 'excusesMissions' | 'audit';

interface ProfileLookups {
  contractTypes: MasterDataLookup[];
  departments: MasterDataLookup[];
  employmentTypes: MasterDataLookup[];
  positions: MasterDataLookup[];
}

const profileTabs: ProfileTab[] = ['overview', 'personal', 'employment', 'contract', 'compensation', 'emergency', 'documents', 'attendance', 'leaves', 'absences', 'delegations', 'socialInsurance', 'excusesMissions', 'audit'];
const tabLabels: Record<ProfileTab, TranslationKey> = {
  overview: 'profileOverview',
  personal: 'profilePersonal',
  employment: 'profileEmployment',
  contract: 'profileContract',
  compensation: 'profileCompensation',
  emergency: 'profileEmergency',
  documents: 'employeeDocuments',
  attendance: 'attendance',
  leaves: 'leaves',
  absences: 'companyAbsences',
  delegations: 'delegations',
  socialInsurance: 'socialInsurance',
  excusesMissions: 'excusesMissions',
  audit: 'profileAudit',
};

const tabIcons: Record<ProfileTab, ReactNode> = {
  overview: <LayoutDashboard aria-hidden="true" className="h-4 w-4" />,
  personal: <ContactRound aria-hidden="true" className="h-4 w-4" />,
  employment: <BriefcaseBusiness aria-hidden="true" className="h-4 w-4" />,
  contract: <FileSignature aria-hidden="true" className="h-4 w-4" />,
  compensation: <BadgeDollarSign aria-hidden="true" className="h-4 w-4" />,
  emergency: <HeartHandshake aria-hidden="true" className="h-4 w-4" />,
  documents: <Files aria-hidden="true" className="h-4 w-4" />,
  attendance: <CalendarCheck2 aria-hidden="true" className="h-4 w-4" />,
  leaves: <CalendarDays aria-hidden="true" className="h-4 w-4" />,
  absences: <UserRoundX aria-hidden="true" className="h-4 w-4" />,
  delegations: <ScrollText aria-hidden="true" className="h-4 w-4" />,
  excusesMissions: <ShieldAlert aria-hidden="true" className="h-4 w-4" />,
  socialInsurance: <ShieldAlert aria-hidden="true" className="h-4 w-4" />,
  audit: <History aria-hidden="true" className="h-4 w-4" />,
};

const statusLabels: Record<EmployeeStatus, TranslationKey> = {
  Active: 'employeeStatusActive',
  Inactive: 'employeeStatusInactive',
  OnLeave: 'employeeStatusOnLeave',
  Suspended: 'employeeStatusSuspended',
  Terminated: 'employeeStatusTerminated',
};

const contractStatusLabels: Record<ContractStatus, TranslationKey> = {
  Draft: 'contractStatusDraft',
  Active: 'contractStatusActive',
  Expired: 'contractStatusExpired',
  Terminated: 'contractStatusTerminated',
};

function statusTone(status: EmployeeStatus): StatusTone {
  if (status === 'Active') return 'success';
  if (status === 'OnLeave') return 'info';
  if (status === 'Suspended') return 'warning';
  if (status === 'Terminated') return 'danger';
  return 'neutral';
}

function nullable(value: string): string | null {
  const trimmed = value.trim();
  return trimmed || null;
}

function lookupLabel(item: MasterDataLookup, language: 'ar' | 'en'): string {
  return language === 'ar' && item.nameArabic ? item.nameArabic : item.nameEnglish;
}

function formatDate(value: string | null, language: 'ar' | 'en'): string {
  if (!value) return '—';
  const date = value.length === 10 ? new Date(`${value}T00:00:00`) : new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { dateStyle: 'medium' }).format(date);
}

function formatDateTime(value: string, language: 'ar' | 'en'): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Africa/Cairo' }).format(date);
}

function InfoItem({ label, value }: { label: ReactNode; value: ReactNode }) {
  return <div><dt className="text-xs font-semibold uppercase tracking-wide text-slate-400">{label}</dt><dd className="mt-1.5 break-words text-sm font-semibold text-mis-navy" dir="auto">{value || '—'}</dd></div>;
}

function FormError({ message }: { message: string }) {
  return message ? <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">{message}</div> : null;
}

function SaveActions({ saving }: { saving: boolean }) {
  const { t } = useLocalization();
  return <div className="flex justify-end border-t border-mis-border pt-5"><Button fullWidth={false} isLoading={saving} size="md" type="submit">{saving ? t('saving') : t('saveChanges')}</Button></div>;
}

function OverviewTab({ profile, reportingLine }: { profile: EmployeeProfile; reportingLine: EmployeeReportingLine }) {
  const { language, t } = useLocalization();
  const counters = [
    { label: t('employeeDocuments'), value: profile.counters.documents },
    { label: t('attendanceRecords'), value: profile.counters.attendanceRecords },
    { label: t('leaveRequests'), value: profile.counters.leaveRequests },
    { label: t('absences'), value: profile.counters.absences },
    { label: t('delegations'), value: profile.counters.delegations },
  ];
  const labels = language === 'ar'
    ? { birth: 'تاريخ الميلاد', address: 'العنوان', role: 'الدور الوظيفي', start: 'تاريخ بدء العمل', end: 'تاريخ انتهاء العمل', fingerprint: 'تاريخ تسجيل البصمة', attendance: 'معلومات الحضور' }
    : { birth: 'Date of Birth', address: 'Address', role: 'Employee Role', start: 'Work Start Date', end: 'Work End Date', fingerprint: 'Fingerprint Enrollment Date', attendance: 'Attendance Information' };
  const role = profile.employment.operationalRole ? ({ COLLECTOR: language === 'ar' ? 'محصل' : 'Collector', ADMIN: language === 'ar' ? 'إداري' : 'Admin', SUPERVISOR: language === 'ar' ? 'مشرف' : 'Supervisor' } as const)[profile.employment.operationalRole] : null;

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {counters.map((counter) => <Card key={counter.label as string}><p className="text-sm font-semibold text-slate-500">{counter.label}</p><p className="mt-3 text-3xl font-bold text-mis-navy">{counter.value}</p></Card>)}
      </div>
      <div className="grid gap-6 xl:grid-cols-2">
        <Section title={t('employeeSummary')}>
          <dl className="grid gap-5 sm:grid-cols-2">
            <InfoItem label={t('employeeId')} value={profile.employeeNumber} />
            <InfoItem label={t('status')} value={<StatusBadge tone={statusTone(profile.status)}>{t(statusLabels[profile.status])}</StatusBadge>} />
            <InfoItem label={t('department')} value={profile.employment.departmentName} />
            <InfoItem label={t('position')} value={profile.employment.positionName} />
            <InfoItem label={labels.role} value={role} />
            <InfoItem label={labels.start} value={formatDate(profile.employment.hireDate, language)} />
            <InfoItem label={labels.end} value={formatDate(profile.employment.terminationDate, language)} />
            <InfoItem label={labels.birth} value={formatDate(profile.personal.dateOfBirth, language)} />
            <InfoItem label={t('mobileNumber')} value={profile.contact.mobileNumber ? <bdi dir="ltr">{profile.contact.mobileNumber}</bdi> : null} />
            <InfoItem label={labels.address} value={profile.contact.address} />
          </dl>
        </Section>
        <Section title={t('reportingLine')}>
          <div className="rounded-xl bg-mis-surface p-4">
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">{t('directManager')}</p>
            {reportingLine.directManagerId ? <Link className="mt-2 inline-flex font-semibold text-mis-primary hover:text-mis-deep" to={`/hr/employees/${reportingLine.directManagerId}`}>{reportingLine.directManagerName}</Link> : <p className="mt-2 text-sm text-slate-500">{t('notAssigned')}</p>}
          </div>
          <div className="mt-5">
            <p className="text-sm font-bold text-mis-navy">{t('directReports')} ({reportingLine.directReports.length})</p>
            {reportingLine.directReports.length ? (
              <div className="mt-3 divide-y divide-mis-border overflow-hidden rounded-xl border border-mis-border">
                {reportingLine.directReports.map((employee) => <Link className="flex items-center justify-between gap-4 p-3 hover:bg-mis-pale/40" key={employee.id} to={`/hr/employees/${employee.id}`}><span><span className="block text-sm font-semibold text-mis-navy">{employee.fullName}</span><span className="text-xs text-slate-500">{employee.employeeNumber}</span></span><StatusBadge tone={statusTone(employee.status)}>{t(statusLabels[employee.status])}</StatusBadge></Link>)}
              </div>
            ) : <p className="mt-3 text-sm text-slate-500">{t('noDirectReports')}</p>}
          </div>
        </Section>
      </div>
      <Section title={labels.attendance}><dl><InfoItem label={labels.fingerprint} value={formatDate(profile.employment.fingerprintEnrollmentDate, language)} /></dl></Section>
    </div>
  );
}

type LinkedRecordsTabName = 'documents' | 'attendance' | 'leaves' | 'absences' | 'delegations';

function LinkedRecordsTab({ profile, tab }: { profile: EmployeeProfile; tab: LinkedRecordsTabName }) {
  const { t } = useLocalization();
  const config: Record<LinkedRecordsTabName, { count: number; label: TranslationKey; route: string }> = {
    documents: { count: profile.counters.documents, label: 'employeeDocuments', route: '/hr/employee-documents' },
    attendance: { count: profile.counters.attendanceRecords, label: 'attendanceRecords', route: '/hr/attendance' },
    leaves: { count: profile.counters.leaveRequests, label: 'leaveRequests', route: '/hr/leaves' },
    absences: { count: profile.counters.absences, label: 'companyAbsences', route: '/hr/absences' },
    delegations: { count: profile.counters.delegations, label: 'delegations', route: '/hr/delegations' },
  };
  const item = config[tab];
  const query = new URLSearchParams({ employee: profile.employeeNumber, employeeId: profile.id }).toString();

  return (
    <Card padding="lg">
      <div className="flex flex-col items-start justify-between gap-5 sm:flex-row sm:items-center">
        <div>
          <p className="text-sm font-semibold text-slate-500">{t(item.label)}</p>
          <p className="mt-2 text-4xl font-bold text-mis-navy">{item.count}</p>
          <p className="mt-3 max-w-2xl text-sm leading-6 text-slate-500">{t('profileLinkedRecordsHelp')}</p>
        </div>
        <Link className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-mis-primary px-5 text-sm font-semibold text-white transition hover:bg-mis-deep" to={`${item.route}?${query}`}>
          {t('openFilteredRecords')}<ExternalLink className="h-4 w-4" aria-hidden="true" />
        </Link>
      </div>
    </Card>
  );
}

function PersonalTab({ onUpdated, profile }: { onUpdated: (profile: EmployeeProfile) => void; profile: EmployeeProfile }) {
  const { t } = useLocalization();
  const toast = useToast();
  const [personal, setPersonal] = useState<EmployeePersonalInformation>(profile.personal);
  const [contact, setContact] = useState<EmployeeContactInformation>(profile.contact);
  const [personalError, setPersonalError] = useState('');
  const [contactError, setContactError] = useState('');
  const [savingPersonal, setSavingPersonal] = useState(false);
  const [savingContact, setSavingContact] = useState(false);

  useEffect(() => { setPersonal(profile.personal); setContact(profile.contact); }, [profile]);

  async function savePersonal(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPersonalError('');
    const nationalIdDigits = personal.nationalId?.replace(/[\s()-]/g, '') ?? '';
    if (nationalIdDigits && !/^[0-9٠-٩]{14}$/.test(nationalIdDigits)) {
      setPersonalError(t('nationalIdValidation'));
      return;
    }
    setSavingPersonal(true);
    try {
      const updated = await hrEmployeeProfileService.updatePersonal(profile.id, {
        dateOfBirth: personal.dateOfBirth || null,
        fullNameArabic: nullable(personal.fullNameArabic ?? ''),
        fullNameEnglish: nullable(personal.fullNameEnglish ?? ''),
        gender: nullable(personal.gender ?? ''),
        maritalStatus: nullable(personal.maritalStatus ?? ''),
        nationalId: nullable(personal.nationalId ?? ''),
      });
      onUpdated(updated);
      toast.success(t('personalSavedSuccess'));
    } catch (requestError) {
      const message = getApiErrorMessage(requestError, t('savePersonalError'));
      setPersonalError(message);
      toast.error(message);
    } finally { setSavingPersonal(false); }
  }

  async function saveContact(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setContactError('');
    setSavingContact(true);
    try {
      const updated = await hrEmployeeProfileService.updateContact(profile.id, {
        address: nullable(contact.address ?? ''),
        alternativeMobileNumber: nullable(contact.alternativeMobileNumber ?? ''),
        city: nullable(contact.city ?? ''),
        email: nullable(contact.email ?? ''),
        mobileNumber: nullable(contact.mobileNumber ?? ''),
      });
      onUpdated(updated);
      toast.success(t('contactSavedSuccess'));
    } catch (requestError) {
      const message = getApiErrorMessage(requestError, t('saveContactError'));
      setContactError(message);
      toast.error(message);
    } finally { setSavingContact(false); }
  }

  const today = new Date().toISOString().slice(0, 10);
  return (
    <div className="grid gap-6 xl:grid-cols-2">
      <Section title={t('personalInformation')}>
        <form className="space-y-5" noValidate onSubmit={savePersonal}>
          <FormError message={personalError} />
          <TextInput label={t('fullNameArabic')} maxLength={160} name="fullNameArabic" onChange={(event) => setPersonal((current) => ({ ...current, fullNameArabic: event.target.value }))} value={personal.fullNameArabic ?? ''} />
          <TextInput label={t('fullNameEnglish')} maxLength={160} name="fullNameEnglish" onChange={(event) => setPersonal((current) => ({ ...current, fullNameEnglish: event.target.value }))} value={personal.fullNameEnglish ?? ''} />
          <TextInput inputMode="numeric" label={t('nationalId')} maxLength={18} name="nationalId" onChange={(event) => setPersonal((current) => ({ ...current, nationalId: event.target.value }))} value={personal.nationalId ?? ''} />
          <DateInput label={t('dateOfBirth')} max={today} name="dateOfBirth" onChange={(event) => setPersonal((current) => ({ ...current, dateOfBirth: event.target.value || null }))} value={personal.dateOfBirth ?? ''} />
          <SelectInput label={t('gender')} name="gender" onChange={(event) => setPersonal((current) => ({ ...current, gender: event.target.value || null }))} value={personal.gender ?? ''}>
            <option value="">{t('selectValue')}</option><option value="Male">{t('male')}</option><option value="Female">{t('female')}</option><option value="Other">{t('other')}</option>
          </SelectInput>
          <SelectInput label={t('maritalStatus')} name="maritalStatus" onChange={(event) => setPersonal((current) => ({ ...current, maritalStatus: event.target.value || null }))} value={personal.maritalStatus ?? ''}>
            <option value="">{t('selectValue')}</option><option value="Single">{t('single')}</option><option value="Married">{t('married')}</option><option value="Divorced">{t('divorced')}</option><option value="Widowed">{t('widowed')}</option><option value="Other">{t('other')}</option>
          </SelectInput>
          <SaveActions saving={savingPersonal} />
        </form>
      </Section>
      <Section title={t('contactInformation')}>
        <form className="space-y-5" noValidate onSubmit={saveContact}>
          <FormError message={contactError} />
          <TextInput dir="ltr" label={t('mobileNumber')} maxLength={32} name="mobileNumber" onChange={(event) => setContact((current) => ({ ...current, mobileNumber: event.target.value }))} type="tel" value={contact.mobileNumber ?? ''} />
          <TextInput label={t('alternativeMobile')} maxLength={32} name="alternativeMobile" onChange={(event) => setContact((current) => ({ ...current, alternativeMobileNumber: event.target.value }))} type="tel" value={contact.alternativeMobileNumber ?? ''} />
          <TextInput label={t('email')} maxLength={256} name="email" onChange={(event) => setContact((current) => ({ ...current, email: event.target.value }))} type="email" value={contact.email ?? ''} />
          <TextInput label={t('city')} maxLength={100} name="city" onChange={(event) => setContact((current) => ({ ...current, city: event.target.value }))} value={contact.city ?? ''} />
          <TextAreaInput label={t('address')} maxLength={500} name="address" onChange={(event) => setContact((current) => ({ ...current, address: event.target.value }))} value={contact.address ?? ''} />
          <SaveActions saving={savingContact} />
        </form>
      </Section>
    </div>
  );
}

function EmploymentTab({ lookups, onUpdated, profile }: { lookups: ProfileLookups; onUpdated: (profile: EmployeeProfile) => void; profile: EmployeeProfile }) {
  const { language, t } = useLocalization();
  const toast = useToast();
  const [form, setForm] = useState<UpdateEmployeeEmploymentRequest>({
    branchId: profile.employment.branchId,
    departmentId: profile.employment.departmentId,
    directManagerId: profile.employment.directManagerId,
    employmentTypeId: profile.employment.employmentTypeId,
    hireDate: profile.employment.hireDate,
    positionId: profile.employment.positionId,
  });
  const [managerSearch, setManagerSearch] = useState('');
  const [managers, setManagers] = useState<EmployeeListItem[]>([]);
  const [managerLoading, setManagerLoading] = useState(false);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setForm({ branchId: profile.employment.branchId, departmentId: profile.employment.departmentId, directManagerId: profile.employment.directManagerId, employmentTypeId: profile.employment.employmentTypeId, hireDate: profile.employment.hireDate, positionId: profile.employment.positionId });
  }, [profile]);

  useEffect(() => {
    const timer = window.setTimeout(async () => {
      setManagerLoading(true);
      try {
        const result = await hrEmployeeService.getEmployees({ departmentId: '', page: 1, pageSize: 20, search: managerSearch.trim(), status: 'active' });
        setManagers(result.items.filter((item) => item.id !== profile.id));
      } catch { setManagers([]); }
      finally { setManagerLoading(false); }
    }, 300);
    return () => window.clearTimeout(timer);
  }, [managerSearch, profile.id]);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    if (!form.departmentId) { setError(t('departmentRequired')); return; }
    setSaving(true);
    try {
      const updated = await hrEmployeeProfileService.updateEmployment(profile.id, form);
      onUpdated(updated);
      toast.success(t('employmentSavedSuccess'));
    } catch (requestError) {
      const message = getApiErrorMessage(requestError, t('saveEmploymentError'));
      setError(message); toast.error(message);
    } finally { setSaving(false); }
  }

  const currentManagerMissing = form.directManagerId && !managers.some((item) => item.id === form.directManagerId);
  return (
    <Section description={t('employmentInformationHelp')} title={t('employmentInformation')}>
      <form className="grid gap-5 sm:grid-cols-2" noValidate onSubmit={submit}>
        {error ? <div className="sm:col-span-2"><FormError message={error} /></div> : null}
        <SelectInput label={t('department')} name="departmentId" onChange={(event) => setForm((current) => ({ ...current, departmentId: event.target.value }))} required value={form.departmentId}>
          <option value="">{t('selectDepartment')}</option>{lookups.departments.map((item) => <option disabled={!item.isActive && item.id !== form.departmentId} key={item.id} value={item.id}>{lookupLabel(item, language)} ({item.code}){item.isActive ? '' : ` — ${t('inactive')}`}</option>)}
        </SelectInput>
        <SelectInput label={t('position')} name="positionId" onChange={(event) => setForm((current) => ({ ...current, positionId: event.target.value || null }))} value={form.positionId ?? ''}>
          <option value="">{t('notAssigned')}</option>{lookups.positions.map((item) => <option disabled={!item.isActive && item.id !== form.positionId} key={item.id} value={item.id}>{lookupLabel(item, language)}{item.isActive ? '' : ` — ${t('inactive')}`}</option>)}
        </SelectInput>
        <SelectInput label={t('employmentType')} name="employmentTypeId" onChange={(event) => setForm((current) => ({ ...current, employmentTypeId: event.target.value || null }))} value={form.employmentTypeId ?? ''}>
          <option value="">{t('notAssigned')}</option>{lookups.employmentTypes.map((item) => <option disabled={!item.isActive && item.id !== form.employmentTypeId} key={item.id} value={item.id}>{lookupLabel(item, language)}{item.isActive ? '' : ` — ${t('inactive')}`}</option>)}
        </SelectInput>
        <DateInput label={t('hireDate')} name="hireDate" onChange={(event) => setForm((current) => ({ ...current, hireDate: event.target.value || null }))} value={form.hireDate ?? ''} />
        <div className="space-y-3">
          <TextInput label={t('searchManager')} name="managerSearch" onChange={(event) => setManagerSearch(event.target.value)} placeholder={t('employeeIdOrName')} value={managerSearch} />
          <SelectInput disabled={managerLoading} label={t('directManager')} name="directManagerId" onChange={(event) => setForm((current) => ({ ...current, directManagerId: event.target.value || null }))} value={form.directManagerId ?? ''}>
            <option value="">{t('notAssigned')}</option>
            {currentManagerMissing ? <option value={form.directManagerId ?? ''}>{profile.employment.directManagerName}</option> : null}
            {managers.map((manager) => <option key={manager.id} value={manager.id}>{manager.employeeNumber} — {manager.fullName}</option>)}
          </SelectInput>
        </div>
        <div className="sm:col-span-2"><SaveActions saving={saving} /></div>
      </form>
    </Section>
  );
}

function ContractTab({ contractTypes, onUpdated, profile }: { contractTypes: MasterDataLookup[]; onUpdated: (profile: EmployeeProfile) => void; profile: EmployeeProfile }) {
  const { language, t } = useLocalization();
  const toast = useToast();
  const [form, setForm] = useState<UpdateEmployeeContractRequest>({
    contractTypeId: profile.contract?.contractTypeId ?? null,
    endDate: profile.contract?.endDate ?? null,
    notes: profile.contract?.notes ?? null,
    probationEndDate: profile.contract?.probationEndDate ?? null,
    probationStartDate: profile.contract?.probationStartDate ?? null,
    startDate: profile.contract?.startDate ?? null,
    status: profile.contract?.status ?? 'Draft',
  });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => setForm({ contractTypeId: profile.contract?.contractTypeId ?? null, endDate: profile.contract?.endDate ?? null, notes: profile.contract?.notes ?? null, probationEndDate: profile.contract?.probationEndDate ?? null, probationStartDate: profile.contract?.probationStartDate ?? null, startDate: profile.contract?.startDate ?? null, status: profile.contract?.status ?? 'Draft' }), [profile]);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError('');
    if (!form.contractTypeId || !form.startDate) { setError(t('contractRequiredFields')); return; }
    if (form.endDate && form.endDate < form.startDate) { setError(t('contractDateValidation')); return; }
    if (form.probationStartDate && form.probationEndDate && form.probationEndDate < form.probationStartDate) { setError(t('probationDateValidation')); return; }
    setSaving(true);
    try {
      const updated = await hrEmployeeProfileService.updateContract(profile.id, { ...form, notes: nullable(form.notes ?? '') });
      onUpdated(updated); toast.success(t('contractSavedSuccess'));
    } catch (requestError) {
      const message = getApiErrorMessage(requestError, t('saveContractError')); setError(message); toast.error(message);
    } finally { setSaving(false); }
  }

  return (
    <Section description={t('contractInformationHelp')} title={t('contractInformation')}>
      <form className="grid gap-5 sm:grid-cols-2" noValidate onSubmit={submit}>
        {error ? <div className="sm:col-span-2"><FormError message={error} /></div> : null}
        <SelectInput label={t('contractType')} name="contractTypeId" onChange={(event) => setForm((current) => ({ ...current, contractTypeId: event.target.value || null }))} required value={form.contractTypeId ?? ''}>
          <option value="">{t('selectContractType')}</option>{contractTypes.map((item) => <option disabled={!item.isActive && item.id !== form.contractTypeId} key={item.id} value={item.id}>{lookupLabel(item, language)}{item.isActive ? '' : ` — ${t('inactive')}`}</option>)}
        </SelectInput>
        <SelectInput label={t('contractStatus')} name="contractStatus" onChange={(event) => setForm((current) => ({ ...current, status: event.target.value as ContractStatus }))} value={form.status}>
          {contractStatuses.map((status) => <option key={status} value={status}>{t(contractStatusLabels[status])}</option>)}
        </SelectInput>
        <DateInput label={t('contractStartDate')} name="contractStartDate" onChange={(event) => setForm((current) => ({ ...current, startDate: event.target.value || null }))} required value={form.startDate ?? ''} />
        <DateInput label={t('contractEndDate')} min={form.startDate ?? undefined} name="contractEndDate" onChange={(event) => setForm((current) => ({ ...current, endDate: event.target.value || null }))} value={form.endDate ?? ''} />
        <DateInput label={t('probationStartDate')} name="probationStartDate" onChange={(event) => setForm((current) => ({ ...current, probationStartDate: event.target.value || null }))} value={form.probationStartDate ?? ''} />
        <DateInput label={t('probationEndDate')} min={form.probationStartDate ?? undefined} name="probationEndDate" onChange={(event) => setForm((current) => ({ ...current, probationEndDate: event.target.value || null }))} value={form.probationEndDate ?? ''} />
        <TextAreaInput containerClassName="sm:col-span-2" label={t('notes')} maxLength={2000} name="contractNotes" onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))} value={form.notes ?? ''} />
        <div className="sm:col-span-2"><SaveActions saving={saving} /></div>
      </form>
    </Section>
  );
}

function CompensationTab({ onUpdated, profile }: { onUpdated: (profile: EmployeeProfile) => void; profile: EmployeeProfile }) {
  const { language, t } = useLocalization();
  const toast = useToast();
  const today = new Date().toISOString().slice(0, 10);
  const [form, setForm] = useState<UpdateEmployeeCompensationRequest>({ allowances: profile.compensation?.allowances ?? 0, bankAccount: profile.compensation?.bankAccount ?? null, bankName: profile.compensation?.bankName ?? null, basicSalary: profile.compensation?.basicSalary ?? 0, effectiveFrom: profile.compensation?.effectiveFrom ?? today, iban: profile.compensation?.iban ?? null, notes: profile.compensation?.notes ?? null });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => setForm({ allowances: profile.compensation?.allowances ?? 0, bankAccount: profile.compensation?.bankAccount ?? null, bankName: profile.compensation?.bankName ?? null, basicSalary: profile.compensation?.basicSalary ?? 0, effectiveFrom: profile.compensation?.effectiveFrom ?? today, iban: profile.compensation?.iban ?? null, notes: profile.compensation?.notes ?? null }), [profile, today]);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError('');
    if (form.basicSalary < 0 || form.allowances < 0 || !form.effectiveFrom) { setError(t('salaryValidation')); return; }
    setSaving(true);
    try {
      const updated = await hrEmployeeProfileService.updateCompensation(profile.id, { ...form, bankAccount: nullable(form.bankAccount ?? ''), bankName: nullable(form.bankName ?? ''), iban: nullable(form.iban ?? ''), notes: nullable(form.notes ?? '') });
      onUpdated(updated); toast.success(t('compensationSavedSuccess'));
    } catch (requestError) {
      const message = getApiErrorMessage(requestError, t('saveCompensationError')); setError(message); toast.error(message);
    } finally { setSaving(false); }
  }

  const total = form.basicSalary + form.allowances;
  const formattedTotal = new Intl.NumberFormat(language === 'ar' ? 'ar-EG' : 'en-EG', { maximumFractionDigits: 2, minimumFractionDigits: 2 }).format(total);
  return (
    <div className="space-y-5">
      <div className="flex items-start gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-amber-800" role="note"><ShieldAlert className="mt-0.5 h-5 w-5 flex-none" aria-hidden="true" /><div><p className="font-bold">{t('restrictedInformation')}</p><p className="mt-1 text-sm leading-5">{t('compensationWarning')}</p></div></div>
      <Section title={t('salaryInformation')}>
        <form className="grid gap-5 sm:grid-cols-2" noValidate onSubmit={submit}>
          {error ? <div className="sm:col-span-2"><FormError message={error} /></div> : null}
          <TextInput label={t('basicSalary')} min={0} name="basicSalary" onChange={(event) => setForm((current) => ({ ...current, basicSalary: Number(event.target.value) || 0 }))} step="0.01" type="number" value={form.basicSalary} />
          <TextInput label={t('allowances')} min={0} name="allowances" onChange={(event) => setForm((current) => ({ ...current, allowances: Number(event.target.value) || 0 }))} step="0.01" type="number" value={form.allowances} />
          <DateInput label={t('effectiveFrom')} max={today} min={profile.employment.hireDate ?? undefined} name="effectiveFrom" onChange={(event) => setForm((current) => ({ ...current, effectiveFrom: event.target.value }))} required value={form.effectiveFrom} />
          <div className="rounded-xl bg-mis-surface p-4 sm:col-span-2"><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">{t('totalSalary')}</p><p className="mt-2 text-2xl font-bold text-mis-navy">{formattedTotal}</p></div>
          <TextInput label={t('bankName')} maxLength={160} name="bankName" onChange={(event) => setForm((current) => ({ ...current, bankName: event.target.value }))} value={form.bankName ?? ''} />
          <TextInput label={t('bankAccount')} maxLength={100} name="bankAccount" onChange={(event) => setForm((current) => ({ ...current, bankAccount: event.target.value }))} value={form.bankAccount ?? ''} />
          <TextInput label={t('iban')} maxLength={64} name="iban" onChange={(event) => setForm((current) => ({ ...current, iban: event.target.value }))} value={form.iban ?? ''} />
          <TextAreaInput containerClassName="sm:col-span-2" label={t('notes')} maxLength={2000} name="compensationNotes" onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))} value={form.notes ?? ''} />
          <div className="sm:col-span-2"><SaveActions saving={saving} /></div>
        </form>
      </Section>
    </div>
  );
}

function EmergencyTab({ onUpdated, profile }: { onUpdated: (profile: EmployeeProfile) => void; profile: EmployeeProfile }) {
  const { t } = useLocalization();
  const toast = useToast();
  const [form, setForm] = useState<UpdateEmployeeEmergencyContactRequest>({ alternativeNumber: profile.emergencyContact?.alternativeNumber ?? null, contactName: profile.emergencyContact?.contactName ?? '', mobileNumber: profile.emergencyContact?.mobileNumber ?? '', notes: profile.emergencyContact?.notes ?? null, relationship: profile.emergencyContact?.relationship ?? '' });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  useEffect(() => setForm({ alternativeNumber: profile.emergencyContact?.alternativeNumber ?? null, contactName: profile.emergencyContact?.contactName ?? '', mobileNumber: profile.emergencyContact?.mobileNumber ?? '', notes: profile.emergencyContact?.notes ?? null, relationship: profile.emergencyContact?.relationship ?? '' }), [profile]);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError('');
    if (form.contactName.trim().length < 2 || form.relationship.trim().length < 2 || !form.mobileNumber.trim()) { setError(t('emergencyRequiredFields')); return; }
    setSaving(true);
    try {
      const updated = await hrEmployeeProfileService.updateEmergencyContact(profile.id, { alternativeNumber: nullable(form.alternativeNumber ?? ''), contactName: form.contactName.trim(), mobileNumber: form.mobileNumber.trim(), notes: nullable(form.notes ?? ''), relationship: form.relationship.trim() });
      onUpdated(updated); toast.success(t('emergencySavedSuccess'));
    } catch (requestError) {
      const message = getApiErrorMessage(requestError, t('saveEmergencyError')); setError(message); toast.error(message);
    } finally { setSaving(false); }
  }

  return (
    <Section description={t('emergencyContactHelp')} title={t('emergencyContact')}>
      <form className="grid gap-5 sm:grid-cols-2" noValidate onSubmit={submit}>
        {error ? <div className="sm:col-span-2"><FormError message={error} /></div> : null}
        <TextInput label={t('contactName')} maxLength={160} name="contactName" onChange={(event) => setForm((current) => ({ ...current, contactName: event.target.value }))} required value={form.contactName} />
        <TextInput label={t('relationship')} maxLength={80} name="relationship" onChange={(event) => setForm((current) => ({ ...current, relationship: event.target.value }))} required value={form.relationship} />
        <TextInput label={t('mobileNumber')} maxLength={32} name="emergencyMobile" onChange={(event) => setForm((current) => ({ ...current, mobileNumber: event.target.value }))} required type="tel" value={form.mobileNumber} />
        <TextInput label={t('alternativeNumber')} maxLength={32} name="emergencyAlternative" onChange={(event) => setForm((current) => ({ ...current, alternativeNumber: event.target.value }))} type="tel" value={form.alternativeNumber ?? ''} />
        <TextAreaInput containerClassName="sm:col-span-2" label={t('notes')} maxLength={1000} name="emergencyNotes" onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))} value={form.notes ?? ''} />
        <div className="sm:col-span-2"><SaveActions saving={saving} /></div>
      </form>
    </Section>
  );
}

const auditActionLabels: Record<string, TranslationKey> = {
  EmployeeCreated: 'auditEmployeeCreated', EmployeeUpdated: 'auditEmployeeUpdated', EmployeePersonalUpdated: 'auditPersonalUpdated', EmployeeContactUpdated: 'auditContactUpdated', EmployeeEmploymentUpdated: 'auditEmploymentUpdated', EmployeeContractUpdated: 'auditContractUpdated', EmployeeCompensationUpdated: 'auditCompensationUpdated', EmployeeEmergencyContactUpdated: 'auditEmergencyUpdated', EmployeeStatusChanged: 'auditStatusChanged',
};

const auditFieldLabels: Record<string, TranslationKey> = {
  FullNameArabic: 'fullNameArabic', FullNameEnglish: 'fullNameEnglish', NationalId: 'nationalId', DateOfBirth: 'dateOfBirth', Gender: 'gender', MaritalStatus: 'maritalStatus', MobileNumber: 'mobileNumber', AlternativeMobile: 'alternativeMobile', AlternativeMobileNumber: 'alternativeMobile', Email: 'email', Address: 'address', City: 'city', DepartmentId: 'department', DepartmentName: 'department', PositionId: 'position', PositionName: 'position', BranchId: 'branch', BranchName: 'branch', EmploymentTypeId: 'employmentType', EmploymentTypeName: 'employmentType', DirectManagerId: 'directManager', DirectManagerName: 'directManager', HireDate: 'hireDate', ContractTypeId: 'contractType', ContractTypeName: 'contractType', StartDate: 'contractStartDate', EndDate: 'contractEndDate', ProbationStartDate: 'probationStartDate', ProbationEndDate: 'probationEndDate', Status: 'status', Notes: 'notes', BasicSalary: 'basicSalary', Allowances: 'allowances', TotalSalary: 'totalSalary', BankName: 'bankName', BankAccount: 'bankAccount', Iban: 'iban', ContactName: 'contactName', Relationship: 'relationship', AlternativeNumber: 'alternativeNumber', Reason: 'reason', IsActive: 'activeRecord',
};

const arabicAuditValues: Record<string, string> = {
  Active: 'نشط', Inactive: 'غير نشط', OnLeave: 'في إجازة', Suspended: 'موقوف', Terminated: 'منتهي الخدمة', Draft: 'مسودة', Expired: 'منتهي', Cancelled: 'ملغي', Pending: 'قيد الانتظار', Approved: 'مقبول', Rejected: 'مرفوض', Male: 'ذكر', Female: 'أنثى', Other: 'أخرى', Single: 'أعزب', Married: 'متزوج', Divorced: 'مطلق', Widowed: 'أرمل',
};

function AuditValue({ value }: { value: string | null }) {
  const { language, t } = useLocalization();
  if (value === null || value === '') return <span className="text-slate-400">—</span>;
  if (value === 'Yes' || value === 'true') return <>{t('yes')}</>;
  if (value === 'No' || value === 'false') return <>{t('no')}</>;
  if (language === 'ar' && arabicAuditValues[value]) return <>{arabicAuditValues[value]}</>;
  if (value.startsWith('{') || value.startsWith('[')) {
    try {
      const parsed = JSON.parse(value) as unknown;
      return <StructuredAuditValue value={parsed} />;
    } catch { return <>{value}</>; }
  }
  return <>{value}</>;
}

function AuditFieldName({ field }: { field: string }) {
  const { language, t } = useLocalization();
  const pascalCase = field ? `${field[0].toUpperCase()}${field.slice(1)}` : field;
  const key = auditFieldLabels[field] ?? auditFieldLabels[pascalCase];
  return <>{key ? t(key) : language === 'ar' ? t('field') : field.replace(/([a-z])([A-Z])/g, '$1 $2')}</>;
}

function StructuredAuditValue({ value }: { value: unknown }): ReactNode {
  const { language, t } = useLocalization();
  if (value === null || value === undefined || value === '') return <span className="text-slate-400">—</span>;
  if (typeof value === 'boolean') return value ? t('yes') : t('no');
  if (typeof value === 'string') return language === 'ar' && arabicAuditValues[value] ? arabicAuditValues[value] : value;
  if (typeof value === 'number') return String(value);
  if (Array.isArray(value)) return <span className="space-y-1">{value.map((item, index) => <span className="block" key={index}><StructuredAuditValue value={item} /></span>)}</span>;
  if (typeof value === 'object') {
    return (
      <span className="space-y-1">
        {Object.entries(value as Record<string, unknown>).map(([key, item]) => (
          <span className="block" key={key}><span className="font-semibold"><AuditFieldName field={key} />:</span> <StructuredAuditValue value={item} /></span>
        ))}
      </span>
    );
  }
  return String(value);
}

function AuditTab({ employeeId }: { employeeId: string }) {
  const { language, t } = useLocalization();
  const [data, setData] = useState<PagedAuditLogs>({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 });
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => { const timer = window.setTimeout(() => { setSearch(searchInput.trim()); setPage(1); }, 350); return () => window.clearTimeout(timer); }, [searchInput]);
  const load = useCallback(async () => {
    setLoading(true); setError('');
    try { setData(await hrAuditService.getPaged({ employeeId, page, pageSize: 10, search })); }
    catch (requestError) { setError(getApiErrorMessage(requestError, t('loadAuditError'))); }
    finally { setLoading(false); }
  }, [employeeId, page, search, t]);
  useEffect(() => { void load(); }, [load]);

  return (
    <Section bodyClassName="p-0" description={t('auditHistoryHelp')} title={t('auditHistory')}>
      <div className="border-b border-mis-border p-4">
        <label className="relative block"><span className="sr-only">{t('searchAudit')}</span><Search className="absolute start-3 top-3 h-5 w-5 text-slate-400" aria-hidden="true" /><input className="h-11 w-full rounded-xl border border-mis-border pe-3 ps-10 text-sm outline-none focus:border-mis-blue focus:shadow-input" onChange={(event) => setSearchInput(event.target.value)} placeholder={t('searchAudit')} value={searchInput} /></label>
      </div>
      {loading ? <div className="flex min-h-64 items-center justify-center"><LoadingSpinner /></div> : error ? <ErrorState compact message={error} onRetry={() => void load()} title={t('auditUnavailable')} /> : data.items.length === 0 ? <EmptyState compact description={t('noAuditHelp')} icon={<History className="h-6 w-6" aria-hidden="true" />} title={t('noAuditRecords')} /> : (
        <>
          <div className="divide-y divide-mis-border">
            {data.items.map((item: AuditLogItem) => (
              <article className="p-5 sm:p-6" key={item.id}>
                <div className="flex flex-col justify-between gap-2 sm:flex-row sm:items-start"><div><h3 className="font-bold text-mis-navy">{auditActionLabels[item.action] ? t(auditActionLabels[item.action]) : language === 'ar' ? t('auditEmployeeUpdated') : item.action.replace(/([a-z0-9])([A-Z])/g, '$1 $2')}</h3>{item.description && (language === 'en' || /[\u0600-\u06ff]/.test(item.description)) ? <p className="mt-1 text-sm text-slate-500">{item.description}</p> : null}</div><time className="text-xs font-semibold text-slate-400" dateTime={item.timestamp}>{formatDateTime(item.timestamp, language)}</time></div>
                <p className="mt-3 text-xs text-slate-500">{t('changedBy', { user: item.username })}</p>
                {item.changes.length ? <div className="mt-4 overflow-x-auto rounded-xl border border-mis-border"><div className="grid min-w-[620px] grid-cols-[minmax(110px,0.8fr)_minmax(120px,1fr)_minmax(120px,1fr)] bg-mis-surface px-4 py-2 text-xs font-bold uppercase tracking-wide text-slate-500"><span>{t('field')}</span><span>{t('from')}</span><span>{t('to')}</span></div>{item.changes.map((change) => <div className="grid min-w-[620px] grid-cols-[minmax(110px,0.8fr)_minmax(120px,1fr)_minmax(120px,1fr)] gap-3 border-t border-mis-border px-4 py-3 text-sm" key={change.field}><span className="font-semibold text-mis-navy"><AuditFieldName field={change.field} /></span><span className="break-words text-slate-500"><AuditValue value={change.oldValue} /></span><span className="break-words text-slate-700"><AuditValue value={change.newValue} /></span></div>)}</div> : null}
              </article>
            ))}
          </div>
          <Pagination labels={{ nextPage: t('nextPage'), pageOf: (current, total) => t('pageOf', { page: current, total }), previousPage: t('previousPage'), showing: (from, to, total) => t('showing', { from, to, total }) }} onPageChange={setPage} page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} totalPages={data.totalPages} />
        </>
      )}
    </Section>
  );
}

function StatusChangeModal({ onClose, onUpdated, profile }: { onClose: () => void; onUpdated: (profile: EmployeeProfile) => void; profile: EmployeeProfile }) {
  const { t } = useLocalization();
  const toast = useToast();
  const [request, setRequest] = useState<ChangeEmployeeStatusRequest>({ reason: null, status: profile.status, terminationDate: profile.employment.terminationDate });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  async function save() {
    setError('');
    if (request.status === 'Terminated' && (!request.reason?.trim() || !request.terminationDate)) { setError(t('terminationReasonRequired')); return; }
    setSaving(true);
    try {
      const updated = await hrEmployeeProfileService.changeStatus(profile.id, { reason: nullable(request.reason ?? ''), status: request.status, terminationDate: request.status === 'Terminated' ? request.terminationDate : null });
      onUpdated(updated); toast.success(t('statusChangedSuccess')); onClose();
    } catch (requestError) {
      const message = getApiErrorMessage(requestError, t('changeEmployeeStatusError')); setError(message); toast.error(message);
    } finally { setSaving(false); }
  }

  return (
    <Modal closeLabel={t('close')} closeOnBackdrop={!saving} closeOnEscape={!saving} footer={<><Button disabled={saving} fullWidth={false} onClick={onClose} size="md" type="button" variant="outline">{t('cancel')}</Button><Button disabled={request.status === profile.status} fullWidth={false} isLoading={saving} onClick={() => void save()} size="md" type="button">{t('confirmStatusChange')}</Button></>} hideCloseButton={saving} onClose={onClose} open size="sm" title={t('changeEmployeeStatus')}>
      <div className="space-y-5"><FormError message={error} /><SelectInput label={t('employeeStatus')} name="employeeStatus" onChange={(event) => setRequest((current) => ({ ...current, status: event.target.value as EmployeeStatus, terminationDate: event.target.value === 'Terminated' ? current.terminationDate : null }))} value={request.status}>{employeeStatuses.map((status) => <option key={status} value={status}>{t(statusLabels[status])}</option>)}</SelectInput>{request.status === 'Terminated' ? <DateInput label={t('terminationDate')} max={new Date().toISOString().slice(0, 10)} min={profile.employment.hireDate ?? undefined} name="terminationDate" onChange={(event) => setRequest((current) => ({ ...current, terminationDate: event.target.value || null }))} required value={request.terminationDate ?? ''} /> : null}<TextAreaInput hint={request.status === 'Terminated' ? t('terminationWarning') : t('statusReasonHelp')} label={t('reason')} maxLength={500} name="statusReason" onChange={(event) => setRequest((current) => ({ ...current, reason: event.target.value }))} value={request.reason ?? ''} /></div>
    </Modal>
  );
}

export function HrEmployeeProfilePage() {
  const { user } = useAuth();
  const canViewExcuses = !!(user?.roles.some(r => ['HrManager', 'HrOfficer'].includes(r)) || user?.permissions.some(p => ['hr.excuses.view', 'hr.excuses.manage', 'hr.excuses.approve'].includes(p)));
  const canViewInsurance = !!(user?.roles.some(r => ['HrManager', 'HrOfficer'].includes(r)) || user?.permissions.some(p => ['hr.social_insurance.view', 'hr.social_insurance.manage'].includes(p)));
  const { id = '' } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const { t } = useLocalization();
  const [profile, setProfile] = useState<EmployeeProfile | null>(null);
  const [reportingLine, setReportingLine] = useState<EmployeeReportingLine | null>(null);
  const [lookups, setLookups] = useState<ProfileLookups | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [statusOpen, setStatusOpen] = useState(false);

  const requestedTab = searchParams.get('tab');
  const requestedTabAllowed = (requestedTab !== 'compensation' || profile?.canManageCompensation === true) && (requestedTab !== 'socialInsurance' || canViewInsurance) && (requestedTab !== 'excusesMissions' || canViewExcuses);
  const activeTab: ProfileTab = profileTabs.includes(requestedTab as ProfileTab) && requestedTabAllowed ? requestedTab as ProfileTab : 'overview';

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true); setError('');
    try {
      const [loadedProfile, line, departments, positions, employmentTypes, contractTypes] = await Promise.all([
        hrEmployeeProfileService.getProfile(id), hrEmployeeProfileService.getReportingLine(id), hrMasterDataService.getLookup('departments', true), hrMasterDataService.getLookup('positions', true), hrMasterDataService.getLookup('employment-types', true), hrMasterDataService.getLookup('contract-types', true),
      ]);
      setProfile(loadedProfile); setReportingLine(line); setLookups({ contractTypes, departments, employmentTypes, positions });
    } catch (requestError) { setError(getApiErrorMessage(requestError, t('loadEmployeeProfileError'))); }
    finally { setLoading(false); }
  }, [id, t]);
  useEffect(() => { void load(); }, [load]);

  async function updateProfile(updated: EmployeeProfile) {
    setProfile(updated);
    try { setReportingLine(await hrEmployeeProfileService.getReportingLine(updated.id)); } catch { /* The saved profile remains usable if the secondary summary refresh fails. */ }
  }

  const tabs = useMemo(
    () => profileTabs
      .filter((tab) => tab !== 'socialInsurance' || canViewInsurance)
      .filter((tab) => tab !== 'excusesMissions' || canViewExcuses)
      .filter((tab) => tab !== 'compensation' || profile?.canManageCompensation)
      .map((tab) => ({ icon: tabIcons[tab], id: tab, label: t(tabLabels[tab]) })),
    [profile?.canManageCompensation, canViewInsurance, canViewExcuses, t],
  );
  function changeTab(tab: string) { setSearchParams(tab === 'overview' ? {} : { tab }, { replace: true }); }

  if (loading) return <div className="flex min-h-[480px] items-center justify-center"><LoadingSpinner /></div>;
  if (!profile || !reportingLine || !lookups) return <ErrorState message={error} onRetry={() => void load()} title={t('employeeProfileUnavailable')} />;

  return (
    <div className="mx-auto max-w-7xl">
      <Link className="mb-5 inline-flex items-center gap-2 text-sm font-semibold text-mis-primary hover:text-mis-deep" to="/hr/employees"><ArrowLeft className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />{t('backToEmployees')}</Link>
      <PageHeader
        actions={<Button fullWidth={false} onClick={() => setStatusOpen(true)} size="md" variant="outline">{t('changeEmployeeStatus')}</Button>}
        description={<span className="flex flex-wrap items-center gap-3"><span>{t('employeeId')}: {profile.employeeNumber}</span><StatusBadge dot tone={statusTone(profile.status)}>{t(statusLabels[profile.status])}</StatusBadge></span>}
        eyebrow={t('employeeProfile')}
        title={profile.displayName}
      />

      <div className="mb-6 flex items-center gap-4 rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
        <div className="flex h-16 w-16 flex-none items-center justify-center rounded-2xl bg-mis-pale text-2xl font-bold text-mis-primary">{profile.displayName.trim().charAt(0).toUpperCase() || <UserRound />}</div>
        <div className="min-w-0"><p className="truncate text-lg font-bold text-mis-navy">{profile.displayName}</p><p className="mt-1 truncate text-sm text-slate-500">{profile.employment.positionName || t('notAssigned')} · {profile.employment.departmentName}</p></div>
      </div>

      <div className="mb-6 rounded-2xl border border-slate-200 bg-white shadow-sm"><Tabs ariaLabel={t('employeeProfileTabs')} items={tabs} onChange={changeTab} value={activeTab} wrap /></div>

      {activeTab === 'overview' ? <OverviewTab profile={profile} reportingLine={reportingLine} /> : null}
      {activeTab === 'personal' ? <PersonalTab onUpdated={(updated) => void updateProfile(updated)} profile={profile} /> : null}
      {activeTab === 'employment' ? <EmploymentTab lookups={lookups} onUpdated={(updated) => void updateProfile(updated)} profile={profile} /> : null}
      {activeTab === 'contract' ? <ContractTab contractTypes={lookups.contractTypes} onUpdated={(updated) => void updateProfile(updated)} profile={profile} /> : null}
      {activeTab === 'compensation' && profile.canManageCompensation ? <CompensationTab onUpdated={(updated) => void updateProfile(updated)} profile={profile} /> : null}
      {activeTab === 'emergency' ? <EmergencyTab onUpdated={(updated) => void updateProfile(updated)} profile={profile} /> : null}
      {activeTab === 'documents' ? <LinkedRecordsTab profile={profile} tab="documents" /> : null}
      {activeTab === 'attendance' ? <LinkedRecordsTab profile={profile} tab="attendance" /> : null}
      {activeTab === 'leaves' ? <LinkedRecordsTab profile={profile} tab="leaves" /> : null}
      {activeTab === 'absences' ? <LinkedRecordsTab profile={profile} tab="absences" /> : null}
      {activeTab === 'delegations' ? <LinkedRecordsTab profile={profile} tab="delegations" /> : null}
      {activeTab === 'excusesMissions' ? <HrExcusesPage employeeId={profile.id} /> : null}
      {activeTab === 'socialInsurance' ? <HrSocialInsurancePage employeeId={profile.id} /> : null}
      {activeTab === 'audit' ? <AuditTab employeeId={profile.id} /> : null}

      {statusOpen ? <StatusChangeModal onClose={() => setStatusOpen(false)} onUpdated={(updated) => void updateProfile(updated)} profile={profile} /> : null}
    </div>
  );
}
