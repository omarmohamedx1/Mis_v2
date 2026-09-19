import { useState } from 'react';
import { Button } from '../../../components/common/Button';
import { Modal } from '../../../components/common/Modal';
import { DateInput } from '../../../components/forms/DateInput';
import { SelectInput } from '../../../components/forms/SelectInput';
import { TextInput } from '../../../components/forms/TextInput';
import { inferOperationalRole, keepArabicEmployeeName, keepEnglishEmployeeName, lookupLabel, organizationLabel } from '../employeeDisplay';
import type { DepartmentOption, EmployeeOrganizationAssignment } from '../types/employee';
import type { MasterDataLookup } from '../types/masterData';
import type { EmployeeImportEmployee, EmployeeImportRow } from '../services/employeeImportService';

const emptyGuid = '00000000-0000-0000-0000-000000000000';

function cleanId(value?: string | null) {
  return !value || value === emptyGuid ? '' : value;
}

export function EmployeeImportRowEditor({
  departments,
  language,
  onClose,
  onSave,
  organizations,
  positions,
  row,
  saving,
  text,
}: {
  departments: DepartmentOption[];
  language: 'ar' | 'en';
  onClose: () => void;
  onSave: (employee: EmployeeImportEmployee) => void;
  organizations: EmployeeOrganizationAssignment[];
  positions: MasterDataLookup[];
  row: EmployeeImportRow;
  saving: boolean;
  text: (en: string, arabic: string) => string;
}) {
  const [form, setForm] = useState(() => ({
    employeeNumber: row.employee.employeeNumber ?? '',
    fullNameArabic: row.employee.fullNameArabic ?? '',
    fullNameEnglish: row.employee.fullNameEnglish ?? '',
    nationalId: row.employee.nationalId ?? '',
    mobileNumber: row.employee.mobileNumber ?? '',
    departmentId: cleanId(row.employee.departmentId),
    positionId: cleanId(row.employee.positionId),
    organizationIds: row.employee.organizationIds ?? [],
    workStartDate: row.employee.workStartDate ?? '',
    dateOfBirth: row.employee.dateOfBirth ?? '',
    fingerprintEnrollmentDate: row.employee.fingerprintEnrollmentDate ?? '',
    workEndDate: row.employee.workEndDate ?? '',
    basicSalary: row.employee.basicSalary ?? null as number | null,
    allowances: row.employee.allowances ?? 0,
    workNumber: row.employee.workNumber ?? '',
    packageType: row.employee.packageType ?? '',
    gender: row.employee.gender === 'Female' || row.employee.gender === 'Male' ? row.employee.gender : '',
    status: row.employee.status ?? 'Active',
    address: row.employee.address ?? '',
  }));
  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) => setForm((current) => ({ ...current, [key]: value }));
  const toggleOrganization = (id: string) => set('organizationIds', form.organizationIds.includes(id) ? form.organizationIds.filter((item) => item !== id) : [...form.organizationIds, id]);
  const position = positions.find((item) => item.id === form.positionId);
  const arabic = form.fullNameArabic.trim();
  const english = form.fullNameEnglish.trim();

  function submit() {
    const localizedName = arabic || english || row.employee.fullName;
    onSave({
      ...row.employee,
      employeeNumber: form.employeeNumber.trim(),
      fullName: localizedName,
      fullNameArabic: arabic || null,
      fullNameEnglish: english || null,
      nationalId: form.nationalId.trim(),
      mobileNumber: form.mobileNumber.trim() || null,
      departmentId: form.departmentId || emptyGuid,
      positionId: form.positionId || null,
      organizationIds: form.organizationIds,
      operationalRole: inferOperationalRole(position),
      workStartDate: form.workStartDate || null,
      dateOfBirth: form.dateOfBirth || null,
      fingerprintEnrollmentDate: form.fingerprintEnrollmentDate || null,
      workEndDate: form.workEndDate || null,
      basicSalary: form.basicSalary,
      allowances: form.allowances ?? 0,
      workNumber: form.workNumber.trim() || null,
      packageType: form.packageType.trim() || null,
      gender: form.gender === 'Male' || form.gender === 'Female' ? form.gender : null,
      status: form.status || 'Active',
      isActive: form.status !== 'Inactive',
      address: form.address.trim() || null,
    });
  }

  return (
    <Modal
      footer={(
        <>
          <Button disabled={saving} fullWidth={false} onClick={onClose} variant="outline">{text('Cancel', 'إلغاء')}</Button>
          <Button fullWidth={false} isLoading={saving} onClick={submit}>{text('Save correction', 'حفظ التصحيح')}</Button>
        </>
      )}
      onClose={saving ? () => undefined : onClose}
      open
      size="xl"
      title={text(`Fix row ${row.row}`, `تعديل الصف ${row.row}`)}
    >
      {row.errors.length ? (
        <div className="mb-4 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
          {row.errors.map((message) => <p key={message}>{message}</p>)}
        </div>
      ) : null}
      <div className="grid gap-4 sm:grid-cols-2">
        <TextInput label={text('Employee Number', 'رقم الموظف')} onChange={(event) => set('employeeNumber', event.target.value)} required value={form.employeeNumber} />
        <TextInput dir="rtl" label={text('Arabic name', 'الاسم بالعربية')} lang="ar" onChange={(event) => set('fullNameArabic', keepArabicEmployeeName(event.target.value))} value={form.fullNameArabic} />
        <TextInput dir="ltr" label={text('English name', 'الاسم بالإنجليزية')} lang="en" onChange={(event) => set('fullNameEnglish', keepEnglishEmployeeName(event.target.value))} value={form.fullNameEnglish} />
        <TextInput dir="ltr" inputMode="numeric" label={text('National ID', 'الرقم القومي')} onChange={(event) => set('nationalId', event.target.value)} required value={form.nationalId} />
        <TextInput dir="ltr" label={text('Mobile Number', 'رقم الموبايل')} onChange={(event) => set('mobileNumber', event.target.value)} value={form.mobileNumber} />
        <SelectInput label={text('Department', 'القسم')} onChange={(event) => set('departmentId', event.target.value)} required value={form.departmentId}>
          <option value="">{text('Select department', 'اختر القسم')}</option>
          {departments.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </SelectInput>
        <SelectInput label={text('Position', 'المسمى الوظيفي')} onChange={(event) => set('positionId', event.target.value)} required value={form.positionId}>
          <option value="">{text('Select position', 'اختر المسمى')}</option>
          {positions.filter((item) => item.isActive || item.id === form.positionId).map((item) => <option key={item.id} value={item.id}>{lookupLabel(item, language)}</option>)}
        </SelectInput>
        <DateInput label={text('Employment Date', 'تاريخ التعيين')} onChange={(event) => set('workStartDate', event.target.value)} required value={form.workStartDate} />
        <DateInput label={text('Date of Birth', 'تاريخ الميلاد')} onChange={(event) => set('dateOfBirth', event.target.value)} value={form.dateOfBirth} />
        <TextInput inputMode="decimal" label={text('Basic Salary', 'الراتب الأساسي')} min={0} onChange={(event) => set('basicSalary', event.target.value === '' ? null : Number(event.target.value))} step="0.01" type="number" value={form.basicSalary ?? ''} />
        <TextInput inputMode="decimal" label={text('Allowances', 'البدلات')} min={0} onChange={(event) => set('allowances', event.target.value === '' ? 0 : Number(event.target.value))} step="0.01" type="number" value={form.allowances ?? 0} />
        <TextInput label={text('Work Number', 'رقم الشغل')} maxLength={50} onChange={(event) => set('workNumber', event.target.value)} value={form.workNumber} />
        <TextInput label={text('Package Type', 'نوع الباقة')} maxLength={80} onChange={(event) => set('packageType', event.target.value)} value={form.packageType} />
        <SelectInput label={text('Gender', 'النوع')} onChange={(event) => set('gender', event.target.value)} value={form.gender}>
          <option value="">{text('From National ID', 'من الرقم القومي')}</option>
          <option value="Male">{text('Male', 'ذكر')}</option>
          <option value="Female">{text('Female', 'أنثى')}</option>
        </SelectInput>
        <SelectInput label={text('Status', 'الحالة')} onChange={(event) => set('status', event.target.value)} value={form.status}>
          <option value="Active">{text('Active', 'نشط')}</option>
          <option value="Inactive">{text('Inactive', 'غير نشط')}</option>
          <option value="OnLeave">{text('On leave', 'في إجازة')}</option>
          <option value="Suspended">{text('Suspended', 'موقوف')}</option>
          <option value="Terminated">{text('Terminated', 'منتهي')}</option>
        </SelectInput>
        <fieldset className="rounded-xl border border-mis-border bg-slate-50/70 p-4 sm:col-span-2">
          <legend className="px-2 text-sm font-bold text-mis-navy">{text('Assigned bank / company', 'البنك / الشركة')}</legend>
          <div className="grid max-h-40 gap-2 overflow-y-auto sm:grid-cols-2">
            {organizations.map((item) => {
              const checked = form.organizationIds.includes(item.id);
              return (
                <label className={`flex cursor-pointer items-center gap-2 rounded-xl border px-3 py-2 text-sm ${checked ? 'border-mis-sky bg-white text-mis-primary' : 'border-transparent bg-white/70'}`} key={item.id}>
                  <input checked={checked} className="accent-mis-primary" onChange={() => toggleOrganization(item.id)} type="checkbox" />
                  <span>{organizationLabel(item, language)}</span>
                </label>
              );
            })}
          </div>
        </fieldset>
      </div>
    </Modal>
  );
}
