import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { Archive, Eye, ListFilter, Pencil, Plus, RotateCcw, Search, Trash2, UsersRound, X } from 'lucide-react';
import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge, type StatusTone } from '../../components/common/StatusBadge';
import { useToast } from '../../components/common/Toast';
import { DateInput } from '../../components/forms/DateInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { TextInput } from '../../components/forms/TextInput';
import { useLocalization } from '../../context/LocalizationContext';
import { useAuth } from '../../context/AuthContext';
import { isSystemAdmin } from '../../features/modules/moduleAccess';
import { RemoveEmployeesModal } from '../../features/hr/components/RemoveEmployeesModal';
import { lookupLabel, inferOperationalRole, keepArabicEmployeeName, keepEnglishEmployeeName, isArabicEmployeeName, isEnglishEmployeeName, organizationLabel, employeeNameForLanguage, secondaryEmployeeName } from '../../features/hr/employeeDisplay';
import { normalizeEgyptianMobile, parseEgyptianNationalId, type NationalIdIssue } from '../../features/hr/egyptianIdentity';
import { hrEmployeeService, type EmployeeSearchField } from '../../features/hr/services/hrEmployeeService';
import { hrMasterDataService } from '../../features/hr/services/hrMasterDataService';
import type { DepartmentOption, EmployeeDetails, EmployeeListItem, EmployeeListStatus, EmployeeOrganizationAssignment, PagedEmployees, SaveEmployeeRequest } from '../../features/hr/types/employee';
import type { MasterDataLookup } from '../../features/hr/types/masterData';
import type { TranslationKey } from '../../localization/translations';
import { getApiErrorMessage } from '../../services/apiClient';

const emptyPage: PagedEmployees = { items: [], page: 1, pageSize: 0, totalCount: 0, totalPages: 0 };
const statusOptions: EmployeeListStatus[] = ['Active', 'Inactive', 'OnLeave', 'Suspended', 'Terminated'];
const statusLabels: Record<EmployeeListStatus, TranslationKey> = { Active: 'employeeStatusActive', Inactive: 'employeeStatusInactive', OnLeave: 'employeeStatusOnLeave', Suspended: 'employeeStatusSuspended', Terminated: 'employeeStatusTerminated' };
const statusTones: Record<EmployeeListStatus, StatusTone> = { Active: 'success', Inactive: 'neutral', OnLeave: 'info', Suspended: 'warning', Terminated: 'danger' };
const employeeSearchFields: EmployeeSearchField[] = ['identity', 'employeeNumber', 'name', 'nationalId', 'mobile', 'organization', 'position'];
function currentStatus(employee: EmployeeListItem): EmployeeListStatus { return employee.status ?? (employee.isActive ? 'Active' : 'Inactive'); }
const operationalDepartmentCodes = new Set(['HR', 'LEGAL', 'ADMIN', 'OFFICE', 'DATA_ENTRY', 'ACCOUNTING', 'COLLECTIONS']);
function directoryGroup(code?: string) {
  const normalized = (code ?? '').toUpperCase();
  if (operationalDepartmentCodes.has(normalized)) return normalized;
  if (normalized === 'COLLECTION_MANAGER_UNIT' || normalized === 'LOWER') return 'COLLECTIONS';
  if (normalized === 'OFFICE_GIRL_UNIT') return 'OFFICE';
  if (normalized === 'FIN_ADMIN_DIRECTOR_UNIT' || normalized === 'DIRECTOR_UNIT') return 'ADMIN';
  return null;
}
function resolveDirectoryDepartmentId(current: string, items: DepartmentOption[]) {
  if (!current || !items.length) return current;
  const selected = items.find((item) => item.id === current);
  if (selected && operationalDepartmentCodes.has((selected.code ?? '').toUpperCase())) return current;
  const group = directoryGroup(selected?.code) ?? (selected ? 'COLLECTIONS' : '');
  return group ? items.find((item) => (item.code ?? '').toUpperCase() === group)?.id ?? '' : '';
}
function directoryDepartments(items: DepartmentOption[]) {
  return items.filter((item) => operationalDepartmentCodes.has((item.code ?? '').toUpperCase()));
}

function positionsForDepartment(positions: MasterDataLookup[], departmentId: string, currentPositionId: string) {
  const activeOrCurrent = positions.filter((item) => item.isActive || item.id === currentPositionId);
  if (!departmentId) return activeOrCurrent;
  const linked = activeOrCurrent.filter((item) => !item.departmentId || item.departmentId === departmentId || item.id === currentPositionId);
  return linked.length ? linked : activeOrCurrent;
}

function FilterField({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid min-w-44 gap-1.5">
      <span className="text-[11px] font-bold uppercase tracking-wide text-slate-500">{label}</span>
      {children}
    </div>
  );
}

function OrganizationCell({ employee, language, notAssigned }: { employee: EmployeeListItem; language: 'ar' | 'en'; notAssigned: string }) {
  const orgs = employee.organizations ?? [];
  if (!orgs.length) return <span className="text-slate-400">{notAssigned}</span>;
  const names = orgs.map((org) => organizationLabel(org, language)).join(' · ');
  return <div className="flex flex-wrap gap-1.5" title={names}>{orgs.map((org) => <span className="inline-flex max-w-full rounded-lg bg-mis-pale px-2 py-1 text-xs font-bold leading-5 text-mis-primary" key={org.id}>{organizationLabel(org, language)}</span>)}</div>;
}

type EmployeeFormField = 'employeeNumber' | 'fullName' | 'fullNameArabic' | 'fullNameEnglish' | 'nationalId' | 'mobileNumber' | 'departmentId' | 'positionId' | 'workStartDate' | 'dateOfBirth' | 'workEndDate' | 'basicSalary' | 'allowances';
const nationalIdIssueKey: Record<NationalIdIssue, TranslationKey> = {
  required: 'nationalIdRequired',
  length: 'nationalIdInvalidLength',
  century: 'nationalIdInvalidCentury',
  birth: 'nationalIdInvalidBirth',
  future: 'nationalIdFutureBirth',
};

function EmployeeForm({ departments, organizations, positions, employee, onClose, onSaved }: { departments: DepartmentOption[]; organizations: EmployeeOrganizationAssignment[]; positions: MasterDataLookup[]; employee: EmployeeDetails | null; onClose: () => void; onSaved: () => void }) {
  const { language, t } = useLocalization();
  const toast = useToast();
  const today = new Date().toISOString().slice(0, 10);
  const dobAutoFilled = useRef(false);
  const [form, setForm] = useState<SaveEmployeeRequest>(() => employee ? {
    employeeNumber: employee.employeeNumber, fullName: employee.fullName, fullNameArabic: employee.fullNameArabic, fullNameEnglish: employee.fullNameEnglish,
    nationalId: employee.nationalId ?? '', mobileNumber: employee.mobileNumber ?? null, departmentId: employee.departmentId,
    organizationIds: (employee.organizations ?? []).map((item) => item.id), positionId: employee.positionId ?? '', operationalRole: employee.operationalRole ?? '',
    workStartDate: employee.workStartDate ?? '', fingerprintEnrollmentDate: employee.fingerprintEnrollmentDate, dateOfBirth: employee.dateOfBirth,
    address: employee.address, workEndDate: employee.workEndDate, isActive: employee.isActive, basicSalary: employee.basicSalary ?? null, allowances: employee.allowances ?? null,
    workNumber: employee.workNumber ?? null, packageType: employee.packageType ?? null,
  } : {
    employeeNumber: '', fullName: '', fullNameArabic: null, fullNameEnglish: null, nationalId: '', mobileNumber: null, departmentId: '',
    organizationIds: [], positionId: '', operationalRole: '', workStartDate: '', fingerprintEnrollmentDate: null, dateOfBirth: null, address: null, workEndDate: null, isActive: true, basicSalary: null, allowances: null, workNumber: null, packageType: null,
  });
  const [error, setError] = useState('');
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<EmployeeFormField, string>>>({});
  const [saving, setSaving] = useState(false);
  const formId = 'employee-form';
  const parsedNationalId = parseEgyptianNationalId(form.nationalId);
  const setFieldError = (key: EmployeeFormField, message?: string) => setFieldErrors((current) => {
    if (!message) {
      if (!current[key]) return current;
      const next = { ...current };
      delete next[key];
      return next;
    }
    return current[key] === message ? current : { ...current, [key]: message };
  });
  const set = <K extends keyof SaveEmployeeRequest>(key: K, value: SaveEmployeeRequest[K]) => setForm((current) => ({ ...current, [key]: value }));
  const toggleOrganization = (id: string) => set('organizationIds', form.organizationIds.includes(id) ? form.organizationIds.filter((item) => item !== id) : [...form.organizationIds, id]);
  const availablePositions = positionsForDepartment(positions, form.departmentId, form.positionId);
  function setDepartment(departmentId: string) {
    const allowed = positionsForDepartment(positions, departmentId, form.positionId);
    const nextPosition = allowed.some((item) => item.id === form.positionId) ? form.positionId : '';
    setFieldError('departmentId');
    if (nextPosition) setFieldError('positionId');
    setForm((current) => ({
      ...current,
      departmentId,
      positionId: nextPosition,
      operationalRole: nextPosition ? inferOperationalRole(positions.find((item) => item.id === nextPosition)) : '',
    }));
  }
  function setPosition(positionId: string) {
    setFieldError('positionId');
    setForm((current) => ({
      ...current,
      positionId,
      operationalRole: inferOperationalRole(positions.find((item) => item.id === positionId)),
    }));
  }
  function setNationalId(value: string) {
    const parsed = parseEgyptianNationalId(value);
    setForm((current) => {
      const next = { ...current, nationalId: value };
      if (parsed.ok && (!current.dateOfBirth || dobAutoFilled.current || current.dateOfBirth === parsed.value.dateOfBirth)) {
        dobAutoFilled.current = true;
        next.dateOfBirth = parsed.value.dateOfBirth;
        next.gender = parsed.value.gender;
      }
      return next;
    });
    if (!value.trim()) setFieldError('nationalId', t('nationalIdRequired'));
    else if (!parsed.ok) setFieldError('nationalId', t(nationalIdIssueKey[parsed.error]));
    else setFieldError('nationalId');
    if (parsed.ok) setFieldError('dateOfBirth');
  }
  function setDateOfBirth(value: string | null) {
    dobAutoFilled.current = false;
    set('dateOfBirth', value);
    const parsed = parseEgyptianNationalId(form.nationalId);
    if (!value) setFieldError('dateOfBirth', t('dateOfBirthRequired'));
    else if (value > today) setFieldError('dateOfBirth', t('invalidBirth'));
    else if (parsed.ok && value !== parsed.value.dateOfBirth) setFieldError('dateOfBirth', t('dateOfBirthMismatch'));
    else setFieldError('dateOfBirth');
  }

  useEffect(() => {
    const employeeNumber = form.employeeNumber.trim();
    const parsed = parseEgyptianNationalId(form.nationalId);
    const handle = window.setTimeout(() => {
      void hrEmployeeService.checkIdentityAvailability({
        employeeNumber: employeeNumber || undefined,
        nationalId: parsed.ok ? parsed.value.nationalId : undefined,
        excludingId: employee?.id,
      }).then((availability) => {
        setFieldErrors((current) => {
          const next = { ...current };
          if (!employeeNumber) {
            if (next.employeeNumber === t('employeeNumberDuplicate')) delete next.employeeNumber;
          } else if (availability.employeeNumberTaken) next.employeeNumber = t('employeeNumberDuplicate');
          else if (next.employeeNumber === t('employeeNumberDuplicate')) delete next.employeeNumber;
          if (parsed.ok) {
            if (availability.nationalIdTaken) next.nationalId = t('nationalIdDuplicate');
            else if (next.nationalId === t('nationalIdDuplicate')) delete next.nationalId;
          }
          return next;
        });
      }).catch(() => undefined);
    }, 400);
    return () => window.clearTimeout(handle);
  }, [employee?.id, form.employeeNumber, form.nationalId, t]);

  function validate(): { errors: Partial<Record<EmployeeFormField, string>>; parsedNationalId: ReturnType<typeof parseEgyptianNationalId>; mobile: ReturnType<typeof normalizeEgyptianMobile>; localizedName: string } {
    const next: Partial<Record<EmployeeFormField, string>> = {};
    const arabicName = form.fullNameArabic?.trim() || '';
    const englishName = form.fullNameEnglish?.trim() || '';
    const localizedName = (language === 'ar' ? arabicName : englishName) || arabicName || englishName;
    if (!form.employeeNumber.trim()) next.employeeNumber = t('employeeNumberRequired');
    if (localizedName.length < 2) next.fullName = t('employeeFormRequired');
    if (arabicName && !isArabicEmployeeName(arabicName)) next.fullNameArabic = t('fullNameArabicInvalid');
    if (englishName && !isEnglishEmployeeName(englishName)) next.fullNameEnglish = t('fullNameEnglishInvalid');
    const parsed = parseEgyptianNationalId(form.nationalId);
    if (!parsed.ok) next.nationalId = t(nationalIdIssueKey[parsed.error]);
    const mobile = normalizeEgyptianMobile(form.mobileNumber);
    if (!mobile.ok) next.mobileNumber = t('egyptianMobileInvalid');
    if (!form.departmentId) next.departmentId = t('departmentRequired');
    if (!form.positionId) next.positionId = t('employeeFormRequired');
    if (!form.workStartDate) next.workStartDate = t('employeeFormRequired');
    if (!form.dateOfBirth) next.dateOfBirth = t('dateOfBirthRequired');
    else if (form.dateOfBirth > today) next.dateOfBirth = t('invalidBirth');
    else if (parsed.ok && form.dateOfBirth !== parsed.value.dateOfBirth) next.dateOfBirth = t('dateOfBirthMismatch');
    if (form.workEndDate && form.workStartDate && form.workEndDate < form.workStartDate) next.workEndDate = t('invalidWorkEnd');
    if (form.workStartDate && parsed.ok && form.workStartDate < parsed.value.dateOfBirth) next.workStartDate = t('workStartBeforeBirth');
    if (form.basicSalary == null || Number.isNaN(form.basicSalary) || form.basicSalary < 0) next.basicSalary = t('salaryRequired');
    return { errors: next, parsedNationalId: parsed, mobile, localizedName };
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError('');
    const { errors: nextErrors, parsedNationalId: parsed, mobile, localizedName } = validate();
    setFieldErrors(nextErrors);
    if (Object.keys(nextErrors).length) {
      setError(t('employeeFormFixFields'));
      return;
    }
    if (!parsed.ok || !mobile.ok) return;
    setSaving(true);
    try {
      const availability = await hrEmployeeService.checkIdentityAvailability({
        employeeNumber: form.employeeNumber.trim(),
        nationalId: parsed.value.nationalId,
        excludingId: employee?.id,
      });
      const taken: Partial<Record<EmployeeFormField, string>> = {};
      if (availability.employeeNumberTaken) taken.employeeNumber = t('employeeNumberDuplicate');
      if (availability.nationalIdTaken) taken.nationalId = t('nationalIdDuplicate');
      if (Object.keys(taken).length) {
        setFieldErrors((current) => ({ ...current, ...taken }));
        setError(t('employeeFormFixFields'));
        setSaving(false);
        return;
      }
      const payload: SaveEmployeeRequest = {
        ...form,
        employeeNumber: form.employeeNumber.trim(),
        fullName: localizedName,
        fullNameArabic: form.fullNameArabic?.trim() || null,
        fullNameEnglish: form.fullNameEnglish?.trim() || null,
        nationalId: parsed.value.nationalId,
        mobileNumber: mobile.mobile,
        dateOfBirth: parsed.value.dateOfBirth,
        gender: parsed.value.gender,
        operationalRole: inferOperationalRole(positions.find((item) => item.id === form.positionId)),
        basicSalary: form.basicSalary,
        allowances: form.allowances ?? 0,
        workNumber: form.workNumber?.trim() || null,
        packageType: form.packageType?.trim() || null,
      };
      employee ? await hrEmployeeService.updateEmployee(employee.id, payload) : await hrEmployeeService.createEmployee(payload);
      toast.success(t(employee ? 'employeeUpdatedSuccess' : 'employeeCreatedSuccess'));
      onSaved();
    } catch (requestError) {
      const message = getApiErrorMessage(requestError, t('saveEmployeeError'));
      setError(message);
      toast.error(message);
    } finally { setSaving(false); }
  }

  const nationalIdHint = parsedNationalId.ok
    ? `${t('dateOfBirthFromNationalId')} · ${parsedNationalId.value.dateOfBirth} · ${t(parsedNationalId.value.gender === 'Female' ? 'female' : 'male')}`
    : t('nationalIdDigitsHint');

  return (
    <Modal closeOnBackdrop={!saving} closeOnEscape={!saving} footer={<><Button disabled={saving} fullWidth={false} onClick={onClose} size="md" variant="outline">{t('cancel')}</Button><Button form={formId} fullWidth={false} isLoading={saving} size="md" type="submit">{t('saveChanges')}</Button></>} hideCloseButton={saving} onClose={onClose} open size="xl" title={t(employee ? 'editEmployee' : 'addEmployee')}>
      <form className="grid gap-4 sm:grid-cols-2" id={formId} noValidate onSubmit={submit}>
        {error ? <div className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700 sm:col-span-2" role="alert">{error}</div> : null}
        <TextInput error={fieldErrors.employeeNumber} hint={fieldErrors.employeeNumber ? undefined : t('employeeNumberHint')} label={t('employeeId')} maxLength={50} name="employeeNumber" onChange={(e) => { set('employeeNumber', e.target.value); if (e.target.value.trim()) setFieldError('employeeNumber'); }} required value={form.employeeNumber} />
        <TextInput dir="rtl" error={fieldErrors.fullNameArabic ?? fieldErrors.fullName} lang="ar" label={t('fullNameArabic')} maxLength={160} name="fullNameArabic" onChange={(e) => { const value = keepArabicEmployeeName(e.target.value); set('fullNameArabic', value || null); if (value.trim() || form.fullNameEnglish?.trim()) setFieldError('fullName'); setFieldError('fullNameArabic'); }} required={!form.fullNameEnglish} value={form.fullNameArabic ?? ''} />
        <TextInput dir="ltr" error={fieldErrors.fullNameEnglish ?? fieldErrors.fullName} lang="en" label={t('fullNameEnglish')} maxLength={160} name="fullNameEnglish" onChange={(e) => { const value = keepEnglishEmployeeName(e.target.value); set('fullNameEnglish', value || null); if (value.trim() || form.fullNameArabic?.trim()) setFieldError('fullName'); setFieldError('fullNameEnglish'); }} required={!form.fullNameArabic} value={form.fullNameEnglish ?? ''} />
        <TextInput dir="ltr" error={fieldErrors.nationalId} hint={fieldErrors.nationalId ? undefined : nationalIdHint} inputMode="numeric" label={t('nationalId')} maxLength={20} name="nationalId" onChange={(e) => setNationalId(e.target.value)} required value={form.nationalId} />
        <DateInput error={fieldErrors.dateOfBirth} hint={parsedNationalId.ok && form.dateOfBirth === parsedNationalId.value.dateOfBirth ? t('dateOfBirthFromNationalId') : undefined} label={t('dateOfBirth')} max={today} name="dateOfBirth" onChange={(e) => setDateOfBirth(e.target.value || null)} required value={form.dateOfBirth ?? ''} />
        <TextInput autoComplete="tel" dir="ltr" error={fieldErrors.mobileNumber} hint={fieldErrors.mobileNumber ? undefined : t('egyptianMobileHint')} label={t('mobileNumber')} maxLength={32} name="mobileNumber" onChange={(e) => { const next = e.target.value || null; set('mobileNumber', next); const parsedMobile = normalizeEgyptianMobile(next); setFieldError('mobileNumber', parsedMobile.ok ? undefined : t('egyptianMobileInvalid')); }} type="tel" value={form.mobileNumber ?? ''} />
        <fieldset className="rounded-xl border border-mis-border bg-slate-50/70 p-4 sm:col-span-2">
          <legend className="px-2 text-sm font-bold text-mis-navy">{t('employmentDetails')}</legend>
          <div className="grid gap-4 sm:grid-cols-2">
            <SelectInput error={fieldErrors.departmentId} label={t('internalDepartment')} onChange={(e) => setDepartment(e.target.value)} required value={form.departmentId}>
              <option value="">{t('selectDepartment')}</option>
              {directoryDepartments(departments).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </SelectInput>
            <SelectInput error={fieldErrors.positionId} label={t('position')} onChange={(e) => setPosition(e.target.value)} required value={form.positionId}>
              <option value="">{t('selectPosition')}</option>
              {availablePositions.map((item) => <option key={item.id} value={item.id}>{lookupLabel(item, language)}</option>)}
            </SelectInput>
            <TextInput label={t('workNumber')} maxLength={50} name="workNumber" onChange={(e) => set('workNumber', e.target.value.trim() ? e.target.value : null)} value={form.workNumber ?? ''} />
            <TextInput label={t('packageType')} maxLength={80} name="packageType" onChange={(e) => set('packageType', e.target.value.trim() ? e.target.value : null)} value={form.packageType ?? ''} />
          </div>
        </fieldset>
        <DateInput error={fieldErrors.workStartDate} label={t('workStartDate')} onChange={(e) => { set('workStartDate', e.target.value); if (e.target.value) setFieldError('workStartDate'); }} required value={form.workStartDate} />
        <TextInput error={fieldErrors.basicSalary} inputMode="decimal" label={t('basicSalary')} min={0} name="basicSalary" onChange={(e) => { const value = e.target.value; const amount = value === '' ? null : Number(value); set('basicSalary', amount); setFieldError('basicSalary', amount == null || Number.isNaN(amount) ? t('salaryRequired') : amount < 0 ? t('salaryValidation') : undefined); }} required step="0.01" type="number" value={form.basicSalary ?? ''} />
        <TextInput error={fieldErrors.allowances} inputMode="decimal" label={t('allowances')} min={0} name="allowances" onChange={(e) => { const value = e.target.value; const amount = value === '' ? 0 : Number(value); set('allowances', Number.isNaN(amount) ? 0 : amount); setFieldError('allowances', Number.isNaN(amount) || amount < 0 ? t('salaryValidation') : undefined); }} step="0.01" type="number" value={form.allowances ?? 0} />
        <DateInput label={t('fingerprintEnrollmentDate')} onChange={(e) => set('fingerprintEnrollmentDate', e.target.value || null)} value={form.fingerprintEnrollmentDate ?? ''} />
        <DateInput error={fieldErrors.workEndDate} label={t('workEndDate')} min={form.workStartDate || undefined} onChange={(e) => { set('workEndDate', e.target.value || null); setFieldError('workEndDate', e.target.value && form.workStartDate && e.target.value < form.workStartDate ? t('invalidWorkEnd') : undefined); }} value={form.workEndDate ?? ''} />
        <SelectInput label={t('status')} onChange={(e) => set('isActive', e.target.value === 'active')} value={form.isActive ? 'active' : 'inactive'}>
          <option value="active">{t('active')}</option>
          <option value="inactive">{t('inactive')}</option>
        </SelectInput>
        <fieldset className="rounded-xl border border-mis-border bg-slate-50/70 p-4 sm:col-span-2">
          <legend className="px-2 text-sm font-bold text-mis-navy">{t('clientAssignment')}</legend>
          <div className="grid max-h-48 gap-2 overflow-y-auto pe-1 sm:grid-cols-2">
            {organizations.map((item) => {
              const checked = form.organizationIds.includes(item.id);
              return (
                <label className={`flex min-w-0 cursor-pointer items-center gap-3 rounded-xl border px-3 py-2.5 text-sm transition ${checked ? 'border-mis-sky bg-white text-mis-primary shadow-sm' : 'border-transparent bg-white/60 text-slate-700 hover:border-slate-200'}`} key={item.id}>
                  <input checked={checked} className="h-4 w-4 shrink-0 accent-mis-primary" onChange={() => toggleOrganization(item.id)} type="checkbox" />
                  <span className="min-w-0 break-words font-semibold">{organizationLabel(item, language)}</span>
                  <bdi className="ms-auto shrink-0 text-[11px] text-slate-400">{item.code}</bdi>
                </label>
              );
            })}
          </div>
        </fieldset>
        <TextAreaInput containerClassName="sm:col-span-2" label={t('address')} maxLength={500} onChange={(e) => set('address', e.target.value || null)} rows={3} value={form.address ?? ''} />
      </form>
    </Modal>
  );
}

export function HrEmployeesPage() {
  const { user } = useAuth();
  const canDelete = isSystemAdmin(user);
  const { language, t } = useLocalization();
  const toast = useToast();
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedStatus = searchParams.get('status')?.toLowerCase() ?? 'all';
  const initialStatus = ['all', 'active', 'inactive', 'onleave', 'suspended', 'terminated'].includes(requestedStatus) ? requestedStatus : 'all';
  const [data, setData] = useState(emptyPage);
  const [departments, setDepartments] = useState<DepartmentOption[]>([]);
  const [organizations, setOrganizations] = useState<EmployeeOrganizationAssignment[]>([]);
  const [positions, setPositions] = useState<MasterDataLookup[]>([]);
  const [searchInput, setSearchInput] = useState(() => searchParams.get('search') ?? '');
  const [search, setSearch] = useState(() => searchParams.get('search')?.trim() ?? '');
  const [searchField, setSearchField] = useState<EmployeeSearchField>(() => {
    const requested = searchParams.get('searchField') as EmployeeSearchField | null;
    return requested && employeeSearchFields.includes(requested) ? requested : 'identity';
  });
  const [departmentId, setDepartmentId] = useState(() => searchParams.get('departmentId') ?? '');
  const [organizationId, setOrganizationId] = useState(() => searchParams.get('organizationId') ?? '');
  const [positionId, setPositionId] = useState(() => searchParams.get('positionId') ?? '');
  const requestedGender = searchParams.get('gender');
  const initialGender = requestedGender === 'Male' || requestedGender === 'Female' ? requestedGender : '';
  const [gender, setGender] = useState(initialGender);
  const [status, setStatus] = useState(initialStatus);
  const [archived, setArchived] = useState(() => searchParams.get('archived') === 'true');
  const [loading, setLoading] = useState(true);
  const [catalogReady, setCatalogReady] = useState(false);
  const [error, setError] = useState('');
  const [formEmployee, setFormEmployee] = useState<EmployeeDetails | null | undefined>(undefined);
  const [archiveTarget, setArchiveTarget] = useState<EmployeeListItem | null>(null);
  const [archiveReason, setArchiveReason] = useState('');
  const [restoreTarget, setRestoreTarget] = useState<EmployeeListItem | null>(null);
  const [restoring, setRestoring] = useState(false);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [removeIds, setRemoveIds] = useState<string[]>([]);
  const [removing, setRemoving] = useState(false);

  useEffect(() => {
    const timer = window.setTimeout(() => setSearch(searchInput.trim()), 350);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    const next = new URLSearchParams();
    if (search) next.set('search', search);
    if (searchField !== 'identity') next.set('searchField', searchField);
    if (departmentId) next.set('departmentId', departmentId);
    if (organizationId) next.set('organizationId', organizationId);
    if (positionId) next.set('positionId', positionId);
    if (gender) next.set('gender', gender);
    if (status !== 'all') next.set('status', status);
    if (archived) next.set('archived', 'true');
    setSearchParams(next, { replace: true });
  }, [archived, departmentId, gender, organizationId, positionId, search, searchField, setSearchParams, status]);

  useEffect(() => {
    void Promise.all([hrEmployeeService.getDepartments(), hrEmployeeService.getOrganizations(), hrMasterDataService.getLookup('positions', true)])
      .then(([departmentItems, organizationItems, positionItems]) => {
        setDepartments(departmentItems);
        setOrganizations(organizationItems);
        setPositions(positionItems);
        setDepartmentId((current) => resolveDirectoryDepartmentId(current, departmentItems));
        setCatalogReady(true);
      })
      .catch((reason) => {
        setError(getApiErrorMessage(reason, t('loadDepartmentsError')));
        setCatalogReady(true);
      });
  }, [t]);

  const load = useCallback(async () => {
    if (!catalogReady) return;
    setLoading(true);
    setError('');
    try {
      const page = await hrEmployeeService.getEmployees({ all: true, archived, departmentId, gender, organizationId, page: 1, pageSize: 100, positionId, search, searchField, status });
      setData(page);
      const visible = new Set(page.items.map((item) => item.id));
      setSelectedIds((current) => current.filter((id) => visible.has(id)));
    } catch (reason) {
      setError(getApiErrorMessage(reason, t('loadEmployeesError')));
    } finally {
      setLoading(false);
    }
  }, [archived, catalogReady, departmentId, gender, organizationId, positionId, search, searchField, status, t]);
  useEffect(() => { void load(); }, [load]);

  async function edit(employee: EmployeeListItem) {
    try { setFormEmployee(await hrEmployeeService.getEmployee(employee.id)); }
    catch (reason) { toast.error(getApiErrorMessage(reason, t('loadEmployeeError'))); }
  }
  async function archiveEmployee() {
    if (!archiveTarget || archiveReason.trim().length < 2) return;
    try {
      await hrEmployeeService.archiveEmployee(archiveTarget.id, archiveReason.trim());
      toast.success(t('archiveEmployee')); setArchiveTarget(null); setArchiveReason(''); void load();
    } catch (reason) { toast.error(getApiErrorMessage(reason, t('saveEmployeeError'))); }
  }
  async function restoreEmployee() {
    if (!restoreTarget) return;
    setRestoring(true);
    try {
      await hrEmployeeService.restoreEmployee(restoreTarget.id);
      toast.success(t('restoreEmployee')); setRestoreTarget(null); void load();
    } catch (reason) { toast.error(getApiErrorMessage(reason, t('saveEmployeeError'))); }
    finally { setRestoring(false); }
  }
  async function restoreSelected() {
    const targets = data.items.filter((item) => selectedIds.includes(item.id) && item.isArchived);
    if (!targets.length) return;
    setRestoring(true);
    try {
      await Promise.all(targets.map((item) => hrEmployeeService.restoreEmployee(item.id)));
      toast.success(t('restoreEmployee'));
      setSelectedIds([]);
      void load();
    } catch (reason) { toast.error(getApiErrorMessage(reason, t('saveEmployeeError'))); }
    finally { setRestoring(false); }
  }
  async function removeEmployees(keepData: boolean, reason: string) {
    if (!removeIds.length) return;
    setRemoving(true);
    try {
      const result = await hrEmployeeService.removeEmployees(removeIds, keepData, keepData ? reason : undefined);
      if (result.kept && !result.deleted) toast.success(t('removeEmployeesSuccessKept', { count: result.kept }));
      else if (result.deleted && !result.kept) toast.success(t('removeEmployeesSuccessDeleted', { count: result.deleted }));
      else toast.success(t('removeEmployeesPartial', { kept: result.kept, deleted: result.deleted, skipped: result.skipped }));
      setRemoveIds([]);
      setSelectedIds([]);
      void load();
    } catch (reason) { toast.error(getApiErrorMessage(reason, t('saveEmployeeError'))); }
    finally { setRemoving(false); }
  }
  function toggleSelected(id: string) {
    setSelectedIds((current) => current.includes(id) ? current.filter((item) => item !== id) : [...current, id]);
  }
  function clearFilters() {
    setSearchInput(''); setSearch(''); setSearchField('identity'); setDepartmentId(''); setOrganizationId(''); setPositionId(''); setGender(''); setStatus('all'); setArchived(false);
  }
  function applyPreset(nextStatus: string, showArchived = false) {
    setStatus(nextStatus);
    setArchived(showArchived);
  }

  const visibleIds = data.items.map((item) => item.id);
  const allChecked = visibleIds.length > 0 && visibleIds.every((id) => selectedIds.includes(id));
  const someChecked = selectedIds.length > 0 && !allChecked;
  const removeTargets = data.items.filter((item) => removeIds.includes(item.id));
  const activeFilterCount = Number(Boolean(search)) + Number(searchField !== 'identity') + Number(Boolean(departmentId)) + Number(Boolean(organizationId)) + Number(Boolean(positionId)) + Number(Boolean(gender)) + Number(status !== 'all') + Number(archived);
  const searchFields: Array<{ value: EmployeeSearchField; label: string }> = [
    { value: 'identity', label: t('searchIdentity') }, { value: 'employeeNumber', label: t('searchEmployeeNumber') }, { value: 'name', label: t('searchEmployeeName') },
    { value: 'nationalId', label: t('searchNationalId') }, { value: 'mobile', label: t('searchMobile') }, { value: 'organization', label: t('searchOrganization') }, { value: 'position', label: t('searchPosition') },
  ];
  const selectedSearchField = searchFields.find((item) => item.value === searchField)?.label ?? t('searchIdentity');
  const quickFilters = [
    { id: 'all', label: t('all'), active: status === 'all' && !archived, action: () => applyPreset('all') },
    { id: 'active', label: t('employeeStatusActive'), active: status === 'active' && !archived, action: () => applyPreset('active') },
    { id: 'inactive', label: t('employeeStatusInactive'), active: status === 'inactive' && !archived, action: () => applyPreset('inactive') },
    { id: 'onleave', label: t('employeeStatusOnLeave'), active: status === 'onleave' && !archived, action: () => applyPreset('onleave') },
    { id: 'archived', label: t('archivedEmployees'), active: archived, action: () => applyPreset('all', true) },
  ];
  const employeeActions = (employee: EmployeeListItem) => (
    <div className="flex items-center gap-1">
      <Link aria-label={`${t('view')} ${employeeNameForLanguage(employee, language)}`} className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-mis-primary hover:bg-mis-pale" title={t('view')} to={`/hr/employees/${employee.id}`}><Eye className="h-4 w-4" /></Link>
      <button aria-label={`${t('edit')} ${employeeNameForLanguage(employee, language)}`} className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-mis-primary hover:bg-mis-pale" onClick={() => void edit(employee)} title={t('edit')} type="button"><Pencil className="h-4 w-4" /></button>
      {employee.isArchived
        ? <button aria-label={`${t('restoreEmployee')} ${employeeNameForLanguage(employee, language)}`} className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-emerald-700 hover:bg-emerald-50" onClick={() => setRestoreTarget(employee)} title={t('restoreEmployee')} type="button"><RotateCcw className="h-4 w-4" /></button>
        : <button aria-label={`${t('archiveEmployee')} ${employeeNameForLanguage(employee, language)}`} className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-amber-700 hover:bg-amber-50" onClick={() => setArchiveTarget(employee)} title={t('archiveEmployee')} type="button"><Archive className="h-4 w-4" /></button>}
      {!employee.isArchived || canDelete ? <button aria-label={`${t('delete')} ${employeeNameForLanguage(employee, language)}`} className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-red-600 hover:bg-red-50" onClick={() => setRemoveIds([employee.id])} title={t('delete')} type="button"><Trash2 className="h-4 w-4" /></button> : null}
    </div>
  );

  return (
    <div>
      <PageHeader
        actions={<div className="flex flex-wrap gap-3"><Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} onClick={() => setFormEmployee(null)} size="md">{t('addEmployee')}</Button><Link className="inline-flex h-10 items-center justify-center rounded-xl border border-mis-border bg-white px-4 text-sm font-semibold text-mis-primary hover:bg-mis-pale" to="/hr/employees/import">{t('importFromExcel')}</Link></div>}
        description={t('employeesSubtitle')}
        eyebrow={t('hrDepartment')}
        title={t('employees')}
      />

      <section aria-label={t('employeeDirectory')} className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="border-b border-mis-border bg-slate-50/60 p-4 sm:p-5">
          <div className="flex flex-col gap-3">
            <div className="grid min-w-0 gap-3 sm:grid-cols-[minmax(190px,260px)_minmax(0,1fr)]">
              <ProfessionalSelect aria-label={t('searchIn')} className="h-11" onChange={(event) => setSearchField(event.target.value as EmployeeSearchField)} value={searchField}>{searchFields.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</ProfessionalSelect>
              <label className="relative min-w-0">
                <span className="sr-only">{t('searchValuePlaceholder')}</span>
                <Search aria-hidden="true" className="pointer-events-none absolute start-3 top-3 h-5 w-5 text-slate-400" />
                <input autoComplete="off" className="h-11 w-full rounded-xl border border-mis-border bg-white pe-10 ps-10 text-sm outline-none transition focus:border-mis-blue focus:shadow-input" maxLength={160} onChange={(event) => setSearchInput(event.target.value)} placeholder={`${selectedSearchField}: ${t('searchValuePlaceholder')}`} type="search" value={searchInput} />
                {searchInput ? <button aria-label={t('clearSearch')} className="absolute end-2 top-1.5 flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700" onClick={() => setSearchInput('')} type="button"><X className="h-4 w-4" /></button> : null}
              </label>
            </div>
            <div className="module-filter-grid">
              <FilterField label={t('internalDepartment')}>
                <ProfessionalSelect aria-label={t('internalDepartment')} className="h-11 min-w-44 rounded-xl border border-mis-border bg-white px-3 text-sm" onChange={(event) => setDepartmentId(event.target.value)} value={departmentId}><option value="">{t('allDepartments')}</option>{directoryDepartments(departments).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</ProfessionalSelect>
              </FilterField>
              <FilterField label={t('position')}>
                <ProfessionalSelect aria-label={t('position')} className="h-11 min-w-44 rounded-xl border border-mis-border bg-white px-3 text-sm" onChange={(event) => setPositionId(event.target.value)} value={positionId}><option value="">{t('allPositions')}</option>{positionsForDepartment(positions, departmentId, positionId).map((item) => <option key={item.id} value={item.id}>{lookupLabel(item, language)}</option>)}</ProfessionalSelect>
              </FilterField>
              <FilterField label={t('clientAssignment')}>
                <ProfessionalSelect aria-label={t('clientAssignment')} className="h-11 min-w-44 rounded-xl border border-mis-border bg-white px-3 text-sm" onChange={(event) => setOrganizationId(event.target.value)} value={organizationId}><option value="">{t('allOrganizations')}</option>{organizations.map((item) => <option key={item.id} value={item.id}>{organizationLabel(item, language)}</option>)}</ProfessionalSelect>
              </FilterField>
              <FilterField label={t('gender')}>
                <ProfessionalSelect aria-label={t('gender')} className="h-11 min-w-40 rounded-xl border border-mis-border bg-white px-3 text-sm" onChange={(event) => setGender(event.target.value)} value={gender}><option value="">{t('allGenders')}</option><option value="Male">{t('male')}</option><option value="Female">{t('female')}</option></ProfessionalSelect>
              </FilterField>
              <FilterField label={t('status')}>
                <ProfessionalSelect aria-label={t('status')} className="h-11 min-w-40 rounded-xl border border-mis-border bg-white px-3 text-sm" onChange={(event) => { setStatus(event.target.value); setArchived(false); }} value={status}><option value="all">{t('allStatuses')}</option>{statusOptions.map((item) => <option key={item} value={item.toLowerCase()}>{t(statusLabels[item])}</option>)}</ProfessionalSelect>
              </FilterField>
              <FilterField label={t('recordType')}>
                <ProfessionalSelect aria-label={t('recordType')} className="h-11 min-w-44 rounded-xl border border-mis-border bg-white px-3 text-sm" onChange={(event) => { setArchived(event.target.value === 'true'); if (event.target.value === 'true') setStatus('all'); }} value={String(archived)}><option value="false">{t('currentEmployees')}</option><option value="true">{t('archivedEmployees')}</option></ProfessionalSelect>
              </FilterField>
            </div>
          </div>
          <div className="mt-4 flex flex-col gap-3 border-t border-slate-200 pt-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex flex-wrap items-center gap-2"><span className="me-1 inline-flex items-center gap-2 text-xs font-bold text-slate-500"><ListFilter className="h-4 w-4" />{t('quickFilters')}</span>{quickFilters.map((filter) => <button aria-pressed={filter.active} className={`rounded-full border px-3 py-1.5 text-xs font-bold transition ${filter.active ? 'border-mis-primary bg-mis-primary text-white' : 'border-slate-200 bg-white text-slate-600 hover:border-mis-primary hover:text-mis-primary'}`} key={filter.id} onClick={filter.action} type="button">{filter.label}</button>)}</div>
            {activeFilterCount ? <button className="inline-flex items-center gap-2 self-start text-xs font-bold text-slate-600 hover:text-red-600 sm:self-auto" onClick={clearFilters} type="button"><X className="h-4 w-4" />{t('clearFilters')} · {t('activeFiltersCount', { count: activeFilterCount })}</button> : null}
          </div>
        </div>

        <div aria-live="polite" className="flex min-h-14 items-center justify-between gap-3 border-b border-mis-border px-4 py-3 sm:px-5">
          <div><p className="text-sm font-bold text-mis-navy">{t('employeeDirectory')}</p><p className="mt-0.5 text-xs text-slate-500">{loading ? t('updatingResults') : t('showingAllEmployees', { count: data.totalCount })}</p></div>
          {!loading && data.totalCount > 0 ? <span className="rounded-full bg-mis-pale px-3 py-1 text-sm font-black tabular-nums text-mis-primary">{data.totalCount}</span> : null}
        </div>
        {selectedIds.length > 0 ? (
          <div className="sticky top-3 z-10 mx-4 my-3 flex flex-wrap items-center gap-3 rounded-xl border border-mis-sky bg-mis-pale px-4 py-3 shadow-sm sm:mx-5">
            <strong className="text-sm text-mis-navy">{t('employeesSelected', { count: selectedIds.length })}</strong>
            {archived
              ? <Button fullWidth={false} isLoading={restoring} leftIcon={<RotateCcw className="h-4 w-4" />} onClick={() => void restoreSelected()} size="sm">{t('restoreSelectedEmployees')}</Button>
              : <Button fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} onClick={() => setRemoveIds(selectedIds)} size="sm" variant="danger">{t('removeSelectedEmployees')}</Button>}
            {archived && canDelete ? <Button fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} onClick={() => setRemoveIds(selectedIds)} size="sm" variant="danger">{t('removeSelectedEmployees')}</Button> : null}
            <Button fullWidth={false} onClick={() => setSelectedIds([])} size="sm" variant="ghost">{t('clearSelection')}</Button>
          </div>
        ) : null}

        {error ? <div className="p-5"><ErrorState message={error} onRetry={() => void load()} title={t('loadEmployeesError')} /></div> : loading ? <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div> : !data.items.length ? <EmptyState description={activeFilterCount ? t('adjustFilters') : t('employeesSubtitle')} icon={<UsersRound />} title={t('noEmployeesFound')} /> : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[1080px] text-start text-sm">
              <thead className="bg-mis-surface text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="w-12 px-4 py-3">
                    <input
                      aria-label={t('selectVisibleEmployees')}
                      checked={allChecked}
                      className="h-4 w-4 accent-mis-primary"
                      onChange={(event) => setSelectedIds(event.target.checked ? visibleIds : [])}
                      ref={(element) => { if (element) element.indeterminate = someChecked; }}
                      type="checkbox"
                    />
                  </th>
                  <th className="px-5 py-3">{t('employeeId')}</th>
                  <th className="px-5 py-3">{t('employeeName')}</th>
                  <th className="px-5 py-3">{t('department')}</th>
                  <th className="px-5 py-3">{t('assignedBankCompany')}</th>
                  <th className="px-5 py-3">{t('position')}</th>
                  <th className="px-5 py-3">{t('status')}</th>
                  <th className="px-5 py-3">{t('action')}</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((employee) => {
                  const employeeStatus = currentStatus(employee);
                  const otherName = secondaryEmployeeName(employee, language);
                  return (
                    <tr className="border-t border-mis-border align-top hover:bg-slate-50/80" key={employee.id}>
                      <td className="px-4 py-4">
                        <input aria-label={employeeNameForLanguage(employee, language)} checked={selectedIds.includes(employee.id)} className="h-4 w-4 accent-mis-primary" onChange={() => toggleSelected(employee.id)} type="checkbox" />
                      </td>
                      <td className="px-5 py-4 font-semibold text-slate-600"><bdi>{employee.employeeNumber}</bdi></td>
                      <td className="px-5 py-4">
                        <Link className="break-words font-semibold text-mis-navy hover:text-mis-primary" to={`/hr/employees/${employee.id}`}>{employeeNameForLanguage(employee, language)}</Link>
                        {otherName ? <p className="mt-0.5 text-xs text-slate-400" dir="auto">{otherName}</p> : null}
                      </td>
                      <td className="px-5 py-4 text-slate-700">{employee.departmentName}</td>
                      <td className="px-5 py-4"><OrganizationCell employee={employee} language={language} notAssigned={t('noBankCompanyAssigned')} /></td>
                      <td className="px-5 py-4 text-slate-700">{employee.positionName ?? t('noPositionAssigned')}</td>
                      <td className="px-5 py-4">{employee.isArchived ? <StatusBadge tone="neutral">{t('archivedEmployees')}</StatusBadge> : <StatusBadge dot tone={statusTones[employeeStatus]}>{t(statusLabels[employeeStatus])}</StatusBadge>}</td>
                      <td className="px-5 py-4">{employeeActions(employee)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {formEmployee !== undefined ? <EmployeeForm departments={departments} employee={formEmployee} onClose={() => setFormEmployee(undefined)} onSaved={() => { setFormEmployee(undefined); void load(); }} organizations={organizations} positions={positions} /> : null}
      {archiveTarget ? <Modal footer={<><Button fullWidth={false} onClick={() => { setArchiveTarget(null); setArchiveReason(''); }} variant="outline">{t('cancel')}</Button><Button disabled={archiveReason.trim().length < 2} fullWidth={false} onClick={() => void archiveEmployee()} variant="danger">{t('archiveEmployee')}</Button></>} onClose={() => { setArchiveTarget(null); setArchiveReason(''); }} open title={t('archiveEmployeePrompt')}><p className="mb-4 font-semibold text-mis-navy">{employeeNameForLanguage(archiveTarget, language)}</p><TextAreaInput label={t('archiveReason')} maxLength={500} onChange={(event) => setArchiveReason(event.target.value)} required rows={3} value={archiveReason} /></Modal> : null}
      <ConfirmDialog confirmLabel={t('restoreEmployee')} isConfirming={restoring} message={t('restoreEmployeeConfirm', { name: restoreTarget ? employeeNameForLanguage(restoreTarget, language) : '' })} onCancel={() => setRestoreTarget(null)} onConfirm={() => void restoreEmployee()} open={Boolean(restoreTarget)} title={t('restoreEmployeePrompt')} />
      <RemoveEmployeesModal busy={removing} canDelete={canDelete} canKeep={removeTargets.some((item) => !item.isArchived)} count={removeIds.length} names={removeTargets.map((item) => employeeNameForLanguage(item, language))} onClose={() => setRemoveIds([])} onConfirm={(keepData, reason) => void removeEmployees(keepData, reason)} open={removeIds.length > 0} />
    </div>
  );
}
