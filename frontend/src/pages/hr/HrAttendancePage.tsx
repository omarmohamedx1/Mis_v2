import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { DateControl } from '../../components/forms/DateControl';
import { CalendarCheck2, Eye, FileUp, Pencil, Plus, Search, Settings2, Trash2, WandSparkles } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { Pagination } from '../../components/common/Pagination';
import { StatusBadge, type StatusTone } from '../../components/common/StatusBadge';
import { useToast } from '../../components/common/Toast';
import { DateInput } from '../../components/forms/DateInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { useLocalization } from '../../context/LocalizationContext';
import { EmployeeSearchSelect } from '../../features/hr/components/EmployeeSearchSelect';
import { hrAttendanceService } from '../../features/hr/services/hrAttendanceService';
import { hrMasterDataService } from '../../features/hr/services/hrMasterDataService';
import { attendanceSources, attendanceStatuses, type AttendanceDetails, type AttendanceListItem, type AttendanceSource, type AttendanceStatus, type PagedAttendanceRecords, type ProcessAttendanceDayResult, type SaveManualAttendanceRequest } from '../../features/hr/types/attendance';
import type { EmployeeListItem } from '../../features/hr/types/employee';
import type { MasterDataLookup } from '../../features/hr/types/masterData';
import type { TranslationKey } from '../../localization/translations';
import { getApiErrorMessage } from '../../services/apiClient';

const pageSize = 20;
const emptyPage: PagedAttendanceRecords = { items: [], page: 1, pageSize, totalCount: 0, totalPages: 0 };

const statusLabels: Record<AttendanceStatus, TranslationKey> = {
  Absent: 'attendanceAbsent', Holiday: 'attendanceHoliday', Late: 'attendanceLate', Leave: 'attendanceOnLeave', Present: 'attendancePresent', Weekend: 'attendanceWeekend',
};
const sourceLabels: Record<AttendanceSource, TranslationKey> = {
  DeviceIntegration: 'sourceDeviceIntegration', ExcelImport: 'sourceExcelImport', Manual: 'sourceManual', SystemProcessing: 'sourceSystemProcessing',
};
const statusTones: Record<AttendanceStatus, StatusTone> = {
  Absent: 'danger', Holiday: 'purple', Late: 'warning', Leave: 'info', Present: 'success', Weekend: 'neutral',
};

function localDateTime(value: string | null): string {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
}

function isoDateTime(value: string): string | null {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

function displayDate(value: string, language: string): string {
  return new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { dateStyle: 'medium' }).format(new Date(`${value}T12:00:00`));
}

function displayDateTime(value: string | null, language: string): string {
  if (!value) return '—';
  return new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

function emptyManualForm(): SaveManualAttendanceRequest {
  return { attendanceDate: new Date().toISOString().slice(0, 10), checkIn: null, checkOut: null, employeeId: '', notes: null, status: 'Present' };
}

interface AttendanceFormProps {
  record: AttendanceDetails | null;
  onClose: () => void;
  onSaved: () => void;
}

function AttendanceForm({ record, onClose, onSaved }: AttendanceFormProps) {
  const { t } = useLocalization();
  const toast = useToast();
  const [form, setForm] = useState<SaveManualAttendanceRequest>(() => record ? {
    attendanceDate: record.attendanceDate,
    checkIn: localDateTime(record.checkIn),
    checkOut: localDateTime(record.checkOut),
    employeeId: record.employeeId,
    notes: record.notes,
    status: record.status,
  } : emptyManualForm());
  const [selectedEmployee, setSelectedEmployee] = useState<Pick<EmployeeListItem, 'id' | 'employeeNumber' | 'fullName'> | null>(() => record ? { employeeNumber: record.employeeNumber, fullName: record.employeeName, id: record.employeeId } : null);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const formId = 'attendance-manual-form';

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setError('');
    if (!form.employeeId || !form.attendanceDate) { setError(t('attendanceRequiredFields')); return; }
    const checkIn = isoDateTime(form.checkIn ?? '');
    const checkOut = isoDateTime(form.checkOut ?? '');
    if (checkIn && checkOut && new Date(checkOut) < new Date(checkIn)) { setError(t('attendanceTimeValidation')); return; }
    setSaving(true);
    try {
      const request = { ...form, checkIn, checkOut, notes: form.notes?.trim() || null };
      if (record) await hrAttendanceService.updateManual(record.id, request);
      else await hrAttendanceService.createManual(request);
      toast.success(t(record ? 'attendanceUpdatedSuccess' : 'attendanceCreatedSuccess'));
      onSaved();
    } catch (requestError) {
      const message = getApiErrorMessage(requestError, t('saveAttendanceError'));
      setError(message); toast.error(message);
    } finally { setSaving(false); }
  }

  return (
    <Modal closeLabel={t('close')} closeOnBackdrop={!saving} closeOnEscape={!saving} footer={<><Button disabled={saving} fullWidth={false} onClick={onClose} size="md" type="button" variant="outline">{t('cancel')}</Button><Button form={formId} fullWidth={false} isLoading={saving} size="md" type="submit">{t('saveChanges')}</Button></>} hideCloseButton={saving} onClose={onClose} open size="lg" title={t(record ? 'editAttendance' : 'addManualAttendance')}>
      <form className="grid gap-5 sm:grid-cols-2" id={formId} onSubmit={submit}>
        {error ? <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 sm:col-span-2" role="alert">{error}</div> : null}
        <div className="sm:col-span-2"><EmployeeSearchSelect initialSelection={selectedEmployee} label={t('employee')} onChange={(employeeId, employee) => { setForm((current) => ({ ...current, employeeId })); setSelectedEmployee(employee); }} required value={form.employeeId} /></div>
        <DateInput label={t('attendanceDate')} onChange={(event) => setForm((current) => ({ ...current, attendanceDate: event.target.value }))} required value={form.attendanceDate} />
        <SelectInput label={t('attendanceStatus')} onChange={(event) => setForm((current) => ({ ...current, status: event.target.value as AttendanceStatus }))} required value={form.status}>
          {attendanceStatuses.map((status) => <option key={status} value={status}>{t(statusLabels[status])}</option>)}
        </SelectInput>
        <label className="space-y-2 text-sm font-semibold text-slate-700"><span className="block">{t('checkIn')}</span><DateControl className="h-12 w-full rounded-form border border-mis-border bg-white px-4 text-sm outline-none focus:border-mis-blue focus:shadow-input" onChange={(event) => setForm((current) => ({ ...current, checkIn: event.target.value || null }))} mode="datetime" value={form.checkIn ?? ''} /></label>
        <label className="space-y-2 text-sm font-semibold text-slate-700"><span className="block">{t('checkOut')}</span><DateControl className="h-12 w-full rounded-form border border-mis-border bg-white px-4 text-sm outline-none focus:border-mis-blue focus:shadow-input" onChange={(event) => setForm((current) => ({ ...current, checkOut: event.target.value || null }))} mode="datetime" value={form.checkOut ?? ''} /></label>
        <div className="sm:col-span-2"><TextAreaInput label={t('notes')} maxLength={2000} onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value || null }))} rows={3} value={form.notes ?? ''} /></div>
        <p className="text-xs leading-5 text-slate-500 sm:col-span-2">{t('attendanceCalculationHelp')}</p>
      </form>
    </Modal>
  );
}

function DetailsModal({ details, onClose }: { details: AttendanceDetails; onClose: () => void }) {
  const { language, t } = useLocalization();
  const rows: Array<[TranslationKey, React.ReactNode]> = [
    ['employee', `${details.employeeNumber} — ${details.employeeName}`], ['department', details.departmentName],
    ['attendanceDate', displayDate(details.attendanceDate, language)], ['checkIn', displayDateTime(details.checkIn, language)], ['checkOut', displayDateTime(details.checkOut, language)],
    ['workingHours', details.workingHours], ['lateMinutes', details.lateMinutes], ['earlyLeaveMinutes', details.earlyLeaveMinutes], ['overtimeMinutes', details.overtimeMinutes],
    ['attendanceSource', t(sourceLabels[details.source])], ['notes', details.notes || '—'],
  ];
  return <Modal closeLabel={t('close')} footer={<Button fullWidth={false} onClick={onClose} size="md" type="button" variant="outline">{t('close')}</Button>} onClose={onClose} open size="lg" title={t('attendanceDetails')}><div className="grid gap-3 sm:grid-cols-2">{rows.map(([label, value]) => <div className="rounded-xl bg-slate-50 p-4" key={label}><dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t(label)}</dt><dd className="mt-2 break-words text-sm font-semibold text-mis-navy">{value}</dd></div>)}</div></Modal>;
}

function ProcessDayModal({ onClose, onProcessed }: { onClose: () => void; onProcessed: (result: ProcessAttendanceDayResult) => void }) {
  const { t } = useLocalization(); const toast = useToast();
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10)); const [notes, setNotes] = useState(''); const [processing, setProcessing] = useState(false); const [error, setError] = useState('');
  async function process() {
    if (!date) { setError(t('attendanceDateRequired')); return; }
    setProcessing(true); setError('');
    try { const result = await hrAttendanceService.processDay(date, notes); toast.success(t('attendanceProcessedSuccess')); onProcessed(result); }
    catch (requestError) { const message = getApiErrorMessage(requestError, t('processAttendanceError')); setError(message); toast.error(message); }
    finally { setProcessing(false); }
  }
  return <Modal closeOnBackdrop={!processing} closeOnEscape={!processing} footer={<><Button disabled={processing} fullWidth={false} onClick={onClose} size="md" type="button" variant="outline">{t('cancel')}</Button><Button fullWidth={false} isLoading={processing} onClick={() => void process()} size="md" type="button">{t('confirmProcessDay')}</Button></>} hideCloseButton={processing} onClose={onClose} open title={t('processAttendanceDay')}>
    <div className="space-y-5">{error ? <div className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</div> : null}<div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm leading-6 text-amber-800">{t('processAttendanceWarning')}</div><DateInput label={t('attendanceDate')} max={new Date().toISOString().slice(0, 10)} onChange={(event) => setDate(event.target.value)} required value={date} /><TextAreaInput label={t('notes')} maxLength={500} onChange={(event) => setNotes(event.target.value)} rows={3} value={notes} /></div>
  </Modal>;
}

export function HrAttendancePage() {
  const { language, t } = useLocalization(); const toast = useToast();
  const [searchParams] = useSearchParams();
  const [data, setData] = useState<PagedAttendanceRecords>(emptyPage); const [departments, setDepartments] = useState<MasterDataLookup[]>([]);
  const [employeeId, setEmployeeId] = useState(() => searchParams.get('employeeId') ?? ''); const [searchInput, setSearchInput] = useState(() => searchParams.get('employee') ?? ''); const [search, setSearch] = useState(() => searchParams.get('employee') ?? ''); const [departmentId, setDepartmentId] = useState(''); const [dateFrom, setDateFrom] = useState(''); const [dateTo, setDateTo] = useState(''); const [status, setStatus] = useState<AttendanceStatus | ''>(''); const [source, setSource] = useState<AttendanceSource | ''>(''); const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [formRecord, setFormRecord] = useState<AttendanceDetails | null | undefined>(undefined); const [details, setDetails] = useState<AttendanceDetails | null>(null); const [openingId, setOpeningId] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<AttendanceListItem | null>(null); const [deleteReason, setDeleteReason] = useState(''); const [deleting, setDeleting] = useState(false); const [processOpen, setProcessOpen] = useState(false); const [processResult, setProcessResult] = useState<ProcessAttendanceDayResult | null>(null);

  useEffect(() => { const timer = window.setTimeout(() => { setSearch(searchInput.trim()); setPage(1); }, 350); return () => window.clearTimeout(timer); }, [searchInput]);
  useEffect(() => { if (employeeId && search !== (searchParams.get('employee') ?? '')) setEmployeeId(''); }, [employeeId, search, searchParams]);
  useEffect(() => { hrMasterDataService.getLookup('departments').then(setDepartments).catch(() => setDepartments([])); }, []);
  const load = useCallback(async () => {
    setLoading(true); setError('');
    try { setData(await hrAttendanceService.getPaged({ dateFrom, dateTo, departmentId, employeeId, page, pageSize, search, source, status, sortBy: 'attendanceDate', sortDescending: true })); }
    catch (requestError) { setError(getApiErrorMessage(requestError, t('loadAttendanceError'))); }
    finally { setLoading(false); }
  }, [dateFrom, dateTo, departmentId, employeeId, page, search, source, status, t]);
  useEffect(() => { void load(); }, [load]);
  async function openRecord(item: AttendanceListItem, mode: 'details' | 'edit') { setOpeningId(item.id); try { const record = await hrAttendanceService.getDetails(item.id); if (mode === 'details') setDetails(record); else setFormRecord(record); } catch (requestError) { toast.error(getApiErrorMessage(requestError, t('loadAttendanceDetailsError'))); } finally { setOpeningId(''); } }
  async function remove() { if (!deleteTarget) return; setDeleting(true); try { await hrAttendanceService.deleteManual(deleteTarget.id, deleteReason); toast.success(t('attendanceDeletedSuccess')); setDeleteTarget(null); setDeleteReason(''); void load(); } catch (requestError) { toast.error(getApiErrorMessage(requestError, t('deleteAttendanceError'))); } finally { setDeleting(false); } }
  function clearFilters() { setEmployeeId(''); setSearchInput(''); setSearch(''); setDepartmentId(''); setDateFrom(''); setDateTo(''); setStatus(''); setSource(''); setPage(1); }
  const hasFilters = useMemo(() => Boolean(employeeId || search || departmentId || dateFrom || dateTo || status || source), [dateFrom, dateTo, departmentId, employeeId, search, source, status]);

  return <div className="mx-auto max-w-[1500px]">
    <PageHeader actions={<><Button fullWidth={false} leftIcon={<WandSparkles className="h-4 w-4" />} onClick={() => setProcessOpen(true)} size="md" variant="outline">{t('processDay')}</Button><Link className="inline-flex h-10 items-center justify-center gap-2 rounded-xl border border-mis-border bg-white px-4 text-sm font-semibold text-slate-700 hover:border-mis-blue hover:bg-mis-pale/50 hover:text-mis-primary" to="/hr/attendance/import"><FileUp className="h-4 w-4" />{t('importAttendance')}</Link><Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} onClick={() => setFormRecord(null)} size="md">{t('addManualAttendance')}</Button></>} description={t('attendanceSubtitle')} eyebrow={t('hrDepartment')} title={t('attendance')} />
    {processResult ? <div className="mb-5 rounded-2xl border border-emerald-200 bg-emerald-50 p-5"><div className="flex flex-wrap items-center justify-between gap-3"><div><p className="font-bold text-emerald-800">{t('processResultTitle')}</p><p className="mt-1 text-sm text-emerald-700">{displayDate(processResult.attendanceDate, language)}</p></div><button className="text-sm font-semibold text-emerald-800 underline" onClick={() => setProcessResult(null)} type="button">{t('dismiss')}</button></div><div className="mt-4 grid grid-cols-2 gap-3 text-sm sm:grid-cols-3 lg:grid-cols-6">{([['createdRecords', processResult.createdRecords], ['attendanceAbsent', processResult.absent], ['attendanceOnLeave', processResult.onLeave], ['attendanceHoliday', processResult.holiday], ['attendanceWeekend', processResult.weekend], ['existingRecordsSkipped', processResult.existingRecordsSkipped]] as Array<[TranslationKey, number]>).map(([key, value]) => <div className="rounded-xl bg-white/70 p-3" key={key}><p className="text-xs text-emerald-700">{t(key)}</p><p className="mt-1 text-lg font-bold text-emerald-900">{value}</p></div>)}</div></div> : null}
    <section className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
      <div className="grid gap-3 border-b border-mis-border p-4 sm:grid-cols-2 xl:grid-cols-[minmax(220px,1fr)_180px_160px_160px_150px_170px_auto]">
        <label className="relative"><span className="sr-only">{t('searchEmployeeLabel')}</span><Search className="absolute start-3 top-3 h-5 w-5 text-slate-400" /><input className="h-11 w-full rounded-xl border border-mis-border pe-3 ps-10 text-sm outline-none focus:border-mis-blue" onChange={(event) => setSearchInput(event.target.value)} placeholder={t('employeeIdOrName')} value={searchInput} /></label>
        <ProfessionalSelect aria-label={t('department')} className="h-11 rounded-xl border border-mis-border bg-white px-3 text-sm" onChange={(event) => { setDepartmentId(event.target.value); setPage(1); }} value={departmentId}><option value="">{t('allDepartments')}</option>{departments.map((item) => <option key={item.id} value={item.id}>{language === 'ar' && item.nameArabic ? item.nameArabic : item.nameEnglish}</option>)}</ProfessionalSelect>
        <DateControl aria-label={t('dateFrom')} className="h-11 rounded-xl border border-mis-border px-3 text-sm" onChange={(event) => { setDateFrom(event.target.value); setPage(1); }}  value={dateFrom} />
        <DateControl aria-label={t('dateTo')} className="h-11 rounded-xl border border-mis-border px-3 text-sm" onChange={(event) => { setDateTo(event.target.value); setPage(1); }}  value={dateTo} />
        <ProfessionalSelect aria-label={t('attendanceStatus')} className="h-11 rounded-xl border border-mis-border bg-white px-3 text-sm" onChange={(event) => { setStatus(event.target.value as AttendanceStatus | ''); setPage(1); }} value={status}><option value="">{t('allStatuses')}</option>{attendanceStatuses.map((item) => <option key={item} value={item}>{t(statusLabels[item])}</option>)}</ProfessionalSelect>
        <ProfessionalSelect aria-label={t('attendanceSource')} className="h-11 rounded-xl border border-mis-border bg-white px-3 text-sm" onChange={(event) => { setSource(event.target.value as AttendanceSource | ''); setPage(1); }} value={source}><option value="">{t('allSources')}</option>{attendanceSources.map((item) => <option key={item} value={item}>{t(sourceLabels[item])}</option>)}</ProfessionalSelect>
        <Button disabled={!hasFilters} fullWidth={false} leftIcon={<Settings2 className="h-4 w-4" />} onClick={clearFilters} size="md" variant="ghost">{t('clearFilters')}</Button>
      </div>
      {error ? <div className="p-5"><ErrorState description={error} onRetry={() => void load()} retryLabel={t('tryAgain')} title={t('attendanceUnavailableTitle')} /></div> : loading ? <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div> : data.items.length === 0 ? <EmptyState action={!hasFilters ? <Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} onClick={() => setFormRecord(null)} size="md">{t('addManualAttendance')}</Button> : undefined} description={hasFilters ? t('adjustFilters') : t('addFirstAttendance')} icon={<CalendarCheck2 />} title={t('noAttendanceFound')} /> : <><div className="overflow-x-auto"><table className="w-full min-w-[1120px] text-start"><thead className="bg-mis-surface text-xs uppercase tracking-wide text-slate-500"><tr><th className="px-5 py-4">{t('employee')}</th><th className="px-5 py-4">{t('department')}</th><th className="px-5 py-4">{t('attendanceDate')}</th><th className="px-5 py-4">{t('checkIn')}</th><th className="px-5 py-4">{t('checkOut')}</th><th className="px-5 py-4">{t('workingHours')}</th><th className="px-5 py-4">{t('status')}</th><th className="px-5 py-4">{t('source')}</th><th className="px-5 py-4 text-end">{t('actions')}</th></tr></thead><tbody className="divide-y divide-mis-border">{data.items.map((item) => <tr className="hover:bg-slate-50/70" key={item.id}><td className="px-5 py-4"><p className="font-semibold text-mis-navy">{item.employeeName}</p><p className="mt-1 text-xs text-slate-500">{item.employeeNumber}</p></td><td className="px-5 py-4 text-sm text-slate-600">{item.departmentName}</td><td className="px-5 py-4 text-sm font-medium text-slate-700">{displayDate(item.attendanceDate, language)}</td><td className="px-5 py-4 text-sm text-slate-600">{displayDateTime(item.checkIn, language)}</td><td className="px-5 py-4 text-sm text-slate-600">{displayDateTime(item.checkOut, language)}</td><td className="px-5 py-4 text-sm text-slate-600">{item.workingHours}</td><td className="px-5 py-4"><StatusBadge dot tone={statusTones[item.status]}>{t(statusLabels[item.status])}</StatusBadge></td><td className="px-5 py-4"><span className="text-xs font-semibold text-slate-600">{t(sourceLabels[item.source])}</span>{item.isManuallyAdjusted ? <span className="ms-1 text-xs text-amber-600">({t('adjusted')})</span> : null}</td><td className="px-5 py-4"><div className="flex justify-end gap-1"><Button disabled={openingId === item.id} fullWidth={false} leftIcon={<Eye className="h-4 w-4" />} onClick={() => void openRecord(item, 'details')} size="sm" variant="ghost">{t('view')}</Button><Button disabled={openingId === item.id} fullWidth={false} leftIcon={<Pencil className="h-4 w-4" />} onClick={() => void openRecord(item, 'edit')} size="sm" variant="ghost">{t('edit')}</Button>{item.source === 'Manual' ? <Button className="text-red-600 hover:bg-red-50" fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} onClick={() => setDeleteTarget(item)} size="sm" variant="ghost">{t('delete')}</Button> : null}</div></td></tr>)}</tbody></table></div><Pagination labels={{ nextPage: t('nextPage'), pageOf: (current, total) => t('pageOf', { page: current, total }), previousPage: t('previousPage'), showing: (from, to, total) => t('showing', { from, to, total }) }} onPageChange={setPage} page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} totalPages={data.totalPages} /></>}
    </section>
    {formRecord !== undefined ? <AttendanceForm onClose={() => setFormRecord(undefined)} onSaved={() => { setFormRecord(undefined); void load(); }} record={formRecord} /> : null}
    {details ? <DetailsModal details={details} onClose={() => setDetails(null)} /> : null}
    {deleteTarget ? <Modal closeOnBackdrop={!deleting} closeOnEscape={!deleting} footer={<><Button disabled={deleting} fullWidth={false} onClick={() => setDeleteTarget(null)} size="md" type="button" variant="outline">{t('cancel')}</Button><Button fullWidth={false} isLoading={deleting} onClick={() => void remove()} size="md" type="button" variant="danger">{t('delete')}</Button></>} hideCloseButton={deleting} onClose={() => setDeleteTarget(null)} open title={t('deleteAttendance')}><div className="space-y-4"><p className="text-sm leading-6 text-slate-600">{t('deleteAttendanceConfirm', { name: deleteTarget.employeeName, date: displayDate(deleteTarget.attendanceDate, language) })}</p><TextAreaInput label={t('reasonOptional')} maxLength={500} onChange={(event) => setDeleteReason(event.target.value)} rows={3} value={deleteReason} /></div></Modal> : null}
    {processOpen ? <ProcessDayModal onClose={() => setProcessOpen(false)} onProcessed={(result) => { setProcessResult(result); setProcessOpen(false); void load(); }} /> : null}
  </div>;
}
