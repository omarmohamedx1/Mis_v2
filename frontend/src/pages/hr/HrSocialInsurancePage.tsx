import { Plus, Upload } from 'lucide-react';
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { Pagination } from '../../components/common/Pagination';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { useToast } from '../../components/common/Toast';
import { DateInput } from '../../components/forms/DateInput';
import { TextInput } from '../../components/forms/TextInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { useLocalization } from '../../context/LocalizationContext';
import { EmployeeSearchSelect } from '../../features/hr/components/EmployeeSearchSelect';
import { hrEmployeeService } from '../../features/hr/services/hrEmployeeService';
import { socialInsuranceService as service, type InsuranceEmployee, type InsurancePage, type InsuranceRecord, type InsuranceStatus, type SaveInsurance } from '../../features/hr/services/socialInsuranceService';
import type { DepartmentOption } from '../../features/hr/types/employee';
import { getApiErrorMessage } from '../../services/apiClient';

const words = {
  en: { title: 'Social Insurance', number: 'Social Insurance Number', start: 'Insurance Start Date', end: 'Insurance End Date', salary: 'Insurable Salary', status: 'Insurance Status', actions: 'Actions', employee: 'Employee', employeeNumber: 'Employee Number', name: 'Employee Name', department: 'Department', position: 'Position', national: 'National ID', add: 'Add Insurance Record', view: 'View', edit: 'Edit', endAction: 'End Insurance', save: 'Save', cancel: 'Cancel', office: 'Insurance Office / Branch', reference: 'Reference / Form Number', notes: 'Notes', search: 'Search name, employee number or insurance number', all: 'All', insured: 'Total Insured Employees', notInsured: 'Not Insured', ended: 'Ended Insurance Records', history: 'Insurance History', empty: 'No records found.', error: 'Unable to load social insurance.', saved: 'Insurance record saved.', endedMessage: 'Insurance ended. The record is retained in history.', ending: 'Enter the end date to end this insurance record. Its history will be retained.', select: 'Select an employee', full: 'Open Social Insurance', Insured: 'Insured', NotInsured: 'Not Insured', Suspended: 'Suspended', Ended: 'Ended' },
  ar: { title: 'التأمينات الاجتماعية', number: 'الرقم التأميني', start: 'تاريخ بداية التأمين', end: 'تاريخ نهاية التأمين', salary: 'الأجر التأميني', status: 'حالة التأمين', actions: 'الإجراءات', employee: 'الموظف', employeeNumber: 'الرقم الوظيفي', name: 'اسم الموظف', department: 'القسم', position: 'الوظيفة', national: 'الرقم القومي', add: 'إضافة سجل تأمين', view: 'عرض', edit: 'تعديل', endAction: 'إنهاء التأمين', save: 'حفظ', cancel: 'إلغاء', office: 'مكتب / فرع التأمينات', reference: 'رقم المرجع / الاستمارة', notes: 'ملاحظات', search: 'بحث بالاسم أو الرقم الوظيفي أو التأميني', all: 'الكل', insured: 'إجمالي الموظفين المؤمن عليهم', notInsured: 'غير مؤمن', ended: 'سجلات التأمين المنتهية', history: 'سجل التأمينات', empty: 'لا توجد سجلات.', error: 'تعذر تحميل التأمينات الاجتماعية.', saved: 'تم حفظ سجل التأمين.', endedMessage: 'تم إنهاء التأمين مع الاحتفاظ بالسجل.', ending: 'أدخل تاريخ نهاية التأمين. سيتم الاحتفاظ بالسجل التاريخي.', select: 'اختر موظفًا', full: 'فتح التأمينات الاجتماعية', Insured: 'مؤمن عليه', NotInsured: 'غير مؤمن', Suspended: 'موقوف', Ended: 'منتهي' },
};
const statuses: InsuranceStatus[] = ['Insured', 'NotInsured', 'Suspended', 'Ended'];
const blank = (employeeId = ''): SaveInsurance => ({ employeeId, socialInsuranceNumber: '', insuranceStartDate: null, insurableSalary: 0, insuranceStatus: 'Insured', insuranceOffice: '', referenceNumber: '', notes: '' });

export function HrSocialInsurancePage({ employeeId: profileEmployeeId }: { employeeId?: string }) {
  const { language, t } = useLocalization(); const w = words[language]; const toast = useToast();
  const [params] = useSearchParams(); const employeeId = profileEmployeeId || params.get('employeeId') || undefined;
  const [data, setData] = useState<InsurancePage>(); const [departments, setDepartments] = useState<DepartmentOption[]>([]);
  const [search, setSearch] = useState(''); const [departmentId, setDepartment] = useState(''); const [status, setStatus] = useState(''); const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [message, setMessage] = useState<'saved' | 'endedMessage' | ''>('');
  const [dialog, setDialog] = useState<'create' | 'edit' | 'view' | 'end' | null>(null);
  const [selected, setSelected] = useState<InsuranceEmployee | null>(null); const [record, setRecord] = useState<InsuranceRecord | null>(null);
  const [form, setForm] = useState<SaveInsurance>(blank()); const [endDate, setEndDate] = useState(''); const [busy, setBusy] = useState(false); const [formError, setFormError] = useState('');
  const [history, setHistory] = useState<InsuranceRecord[]>([]);
  const loadVersion = useRef(0); const selectionVersion = useRef(0);
  const load = useCallback(async () => {
    const version = ++loadVersion.current;
    setLoading(true); setError('');
    try { const result = await service.list({ search, departmentId: departmentId || undefined, status: status || undefined, employeeId, page }); if (version === loadVersion.current) setData(result); }
    catch (e) { if (version === loadVersion.current) setError(getApiErrorMessage(e, w.error)); } finally { if (version === loadVersion.current) setLoading(false); }
  }, [search, departmentId, status, employeeId, page, w.error]);
  useEffect(() => { const timer = window.setTimeout(() => void load(), 200); return () => clearTimeout(timer); }, [load, language]);
  useEffect(() => { void hrEmployeeService.getDepartments().then(setDepartments).catch((reason) => toast.error(getApiErrorMessage(reason, t('loadDepartmentsError')))); }, [t, toast]);
  async function open(mode: NonNullable<typeof dialog>, employee: InsuranceEmployee | null, item = employee?.record ?? null) {
    setSelected(employee); setRecord(item); setForm(mode === 'create' ? blank(employee?.employeeId) : item ? { ...item } : blank());
    setEndDate(''); setFormError(''); setHistory([]); setDialog(mode);
    if (employee && mode === 'view') {
      setBusy(true);
      try { setHistory(await service.history(employee.employeeId)); } catch (e) { setFormError(getApiErrorMessage(e, w.error)); } finally { setBusy(false); }
    }
  }
  async function selectEmployee(id: string) {
    const version = ++selectionVersion.current;
    setSelected(null); setForm(blank(id)); setFormError('');
    if (!id) return;
    setBusy(true);
    try { const result = await service.list({ employeeId: id }); if (version === selectionVersion.current) setSelected(result.items[0] ?? null); }
    catch (e) { if (version === selectionVersion.current) setFormError(getApiErrorMessage(e, w.error)); } finally { if (version === selectionVersion.current) setBusy(false); }
  }
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setFormError('');
    try {
      if (dialog === 'end' && record) await service.end(record.id, endDate);
      else {
        if (!selected || !form.employeeId) throw new Error(w.select);
        await service.save(dialog === 'edit' ? record?.id : undefined, form);
      }
      setMessage(dialog === 'end' ? 'endedMessage' : 'saved'); setDialog(null); await load();
    } catch (e) { setFormError(getApiErrorMessage(e, w.error)); } finally { setBusy(false); }
  }
  const headers = [w.employeeNumber, w.name, w.department, w.position, w.number, w.start, w.end, w.salary, w.status, w.actions];
  const date = (value: string | null | undefined) => value ? new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB').format(new Date(value + 'T12:00:00')) : '—';
  const amount = (value: number | undefined) => value == null ? '—' : new Intl.NumberFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { minimumFractionDigits: 2 }).format(value);
  const detail = (item: InsuranceRecord) => <dl className="grid gap-4 sm:grid-cols-2">{[[w.number, item.socialInsuranceNumber], [w.start, date(item.insuranceStartDate)], [w.end, date(item.insuranceEndDate)], [w.salary, amount(item.insurableSalary)], [w.status, w[item.insuranceStatus]], [w.office, item.insuranceOffice], [w.reference, item.referenceNumber], [w.notes, item.notes]].map(([label, value]) => <div key={label}><dt className="text-sm text-slate-500">{label}</dt><dd className="whitespace-pre-wrap break-words font-medium">{value || '—'}</dd></div>)}</dl>;
  return <div className="space-y-5">
    <PageHeader title={w.title} actions={profileEmployeeId ? <div className="flex flex-wrap items-center gap-2.5">{data?.canManage ? <Button className="shrink-0 whitespace-nowrap" fullWidth={false} size="md" leftIcon={<Plus aria-hidden="true" className="h-4 w-4 shrink-0" />} onClick={() => void open('create', data.items[0] ?? null)}>{w.add}</Button> : null}<Link className="inline-flex h-10 shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded-xl border border-mis-primary bg-white px-4 text-sm font-bold text-mis-primary shadow-sm transition duration-150 hover:bg-mis-pale" to={`/hr/social-insurance?employeeId=${profileEmployeeId}`}>{w.full}</Link></div> : data?.canManage ? <div className="flex flex-wrap items-center gap-2.5 sm:flex-nowrap"><Button className="shrink-0 whitespace-nowrap" fullWidth={false} size="md" leftIcon={<Plus aria-hidden="true" className="h-4 w-4 shrink-0" />} onClick={() => void open('create', null)}>{w.add}</Button><Link className="inline-flex h-10 shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded-xl border border-mis-primary bg-white px-4 text-sm font-bold text-mis-primary shadow-sm transition duration-150 hover:bg-mis-pale" to="/hr/social-insurance/import"><Upload aria-hidden="true" className="h-4 w-4 shrink-0" />{language === 'ar' ? 'استيراد ملف التأمينات' : 'Import Insurance Sheet'}</Link></div> : undefined} />
    {!profileEmployeeId && data && <div className="grid gap-3 sm:grid-cols-3">{([[w.insured, data.totalInsured, 'Insured'], [w.notInsured, data.notInsured, 'NotInsured'], [w.ended, data.endedRecords, 'Ended']] as const).map(([label, value, next]) => <button className="text-start" key={label} onClick={() => { setStatus(status === next ? '' : next); setPage(1); }} type="button"><Card className={`p-4 transition hover:border-mis-primary ${status === next ? 'ring-2 ring-mis-primary' : ''}`}><p className="text-sm text-slate-500">{label}</p><p className="mt-1 text-2xl font-semibold">{value}</p></Card></button>)}</div>}
    {!employeeId && <div className="module-filter-grid rounded-2xl border border-mis-border bg-white p-4"><TextInput name="insurance-search" label={w.search} value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} /><SelectInput label={w.department} value={departmentId} onChange={e => { setDepartment(e.target.value); setPage(1); }}><option value="">{w.all}</option>{departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}</SelectInput><SelectInput label={w.status} value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">{w.all}</option>{statuses.map(s => <option key={s} value={s}>{w[s]}</option>)}</SelectInput></div>}
    {message && <p role="status" className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-semibold text-emerald-800">{w[message]}</p>}
    {error && <ErrorState compact message={error} onRetry={() => void load()} title={w.error} />}
    {loading ? <LoadingSpinner /> : !error && data && <Card className="overflow-hidden"><div className="overflow-x-auto"><table className="w-full min-w-[1100px] text-start text-sm"><thead className="bg-slate-50"><tr>{headers.map(h => <th key={h} className="whitespace-nowrap px-4 py-3 text-start">{h}</th>)}</tr></thead><tbody>{data.items.map(e => <tr className="border-t border-slate-100" key={e.employeeId}>
      {[e.employeeNumber, e.employeeName, e.department, e.position || '—', e.record?.socialInsuranceNumber || '—', date(e.record?.insuranceStartDate), date(e.record?.insuranceEndDate), amount(e.record?.insurableSalary), w[e.record?.insuranceStatus ?? 'NotInsured']].map((cell, i) => <td className="px-4 py-3" key={i}>{cell}</td>)}
      <td className="min-w-52 px-4 py-3"><div className="flex flex-wrap gap-2">{e.record && <Button fullWidth={false} size="sm" variant="secondary" onClick={() => void open('view', e)}>{w.view}</Button>}{data.canManage && (e.record && e.record.insuranceStatus !== 'Ended' ? <><Button fullWidth={false} size="sm" variant="secondary" onClick={() => void open('edit', e)}>{w.edit}</Button><Button fullWidth={false} size="sm" variant="outline" onClick={() => void open('end', e)}>{w.endAction}</Button></> : <Button fullWidth={false} size="sm" onClick={() => void open('create', e)}>{w.add}</Button>)}</div></td>
    </tr>)}{!data.items.length && <tr><td className="p-8 text-center text-slate-500" colSpan={10}>{w.empty}</td></tr>}</tbody></table></div><Pagination page={page} pageSize={data.pageSize} totalCount={data.totalCount} totalPages={data.totalPages} onPageChange={setPage} /></Card>}
    <Modal open={dialog !== null} onClose={() => { if (!busy) setDialog(null); }} title={dialog === 'create' ? w.add : dialog === 'edit' ? w.edit : dialog === 'end' ? w.endAction : w.view} size="lg">
        {formError && <p role="alert" className="mb-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{formError}</p>}
      {dialog === 'view' ? <div className="space-y-6">{selected && <p className="font-semibold">{selected.employeeNumber} · {selected.employeeName}</p>}{record && detail(record)}<h3 className="font-semibold">{w.history}</h3>{busy ? <LoadingSpinner /> : history.map(item => <button type="button" key={item.id} onClick={() => setRecord(item)} className={`block w-full rounded-lg border p-3 text-start ${record?.id === item.id ? 'border-mis-primary bg-slate-50' : 'border-slate-200'}`}>{item.socialInsuranceNumber} · {date(item.insuranceStartDate)} · {w[item.insuranceStatus]}</button>)}</div> : <form onSubmit={submit} className="space-y-5">
        {dialog === 'create' && <EmployeeSearchSelect disabled={busy || !!selected && !!employeeId} includeInactive label={w.employee} required value={form.employeeId} initialSelection={selected ? { id: selected.employeeId, employeeNumber: selected.employeeNumber, fullName: selected.employeeName } : null} onChange={id => void selectEmployee(id)} />}
        {selected && <div className="grid gap-3 rounded-lg bg-slate-50 p-4 sm:grid-cols-2">{[[w.employeeNumber, selected.employeeNumber], [w.name, selected.employeeName], [w.department, selected.department], [w.position, selected.position], ...(selected.nationalId ? [[w.national, selected.nationalId]] : [])].map(([label, value]) => <div key={label}><p className="text-xs text-slate-500">{label}</p><p>{value || '—'}</p></div>)}</div>}
        {dialog === 'end' ? <><p>{w.ending}</p><DateInput label={w.end} required min={record?.insuranceStartDate ?? undefined} value={endDate} onChange={e => setEndDate(e.target.value)} /></> : <div className="grid gap-4 sm:grid-cols-2">
          <TextInput name="insurance-number" label={w.number} required maxLength={50} value={form.socialInsuranceNumber} onChange={e => setForm({ ...form, socialInsuranceNumber: e.target.value })} />
          <DateInput label={w.start} required={form.insuranceStatus !== 'NotInsured'} value={form.insuranceStartDate || ''} onChange={e => setForm({ ...form, insuranceStartDate: e.target.value || null })} />
          <TextInput name="insurance-salary" label={w.salary} required type="number" min="0.01" step="0.01" value={form.insurableSalary || ''} onChange={e => setForm({ ...form, insurableSalary: Number(e.target.value) })} />
          <SelectInput label={w.status} value={form.insuranceStatus} onChange={e => setForm({ ...form, insuranceStatus: e.target.value as InsuranceStatus })}>{statuses.filter(s => s !== 'Ended').map(s => <option key={s} value={s}>{w[s]}</option>)}</SelectInput>
          <TextInput name="insurance-office" label={w.office} maxLength={160} value={form.insuranceOffice || ''} onChange={e => setForm({ ...form, insuranceOffice: e.target.value })} />
          <TextInput name="insurance-reference" label={w.reference} maxLength={100} value={form.referenceNumber || ''} onChange={e => setForm({ ...form, referenceNumber: e.target.value })} />
          <div className="sm:col-span-2"><TextAreaInput label={w.notes} maxLength={2000} value={form.notes || ''} onChange={e => setForm({ ...form, notes: e.target.value })} /></div>
        </div>}
        <div className="flex flex-wrap justify-end gap-3"><Button type="button" fullWidth={false} variant="secondary" disabled={busy} onClick={() => setDialog(null)}>{w.cancel}</Button><Button type="submit" fullWidth={false} disabled={busy || dialog !== 'end' && !selected}>{dialog === 'end' ? w.endAction : w.save}</Button></div>
      </form>}
    </Modal>
  </div>;
}
