import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { DateControl } from '../../components/forms/DateControl';
import { BadgeDollarSign, Ban, CalendarX2, CheckCircle2, Eye, Pencil, Plus, Search, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { FormError } from '../../components/common/FormError';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { Pagination } from '../../components/common/Pagination';
import { StatusBadge, type StatusTone } from '../../components/common/StatusBadge';
import { useToast } from '../../components/common/Toast';
import { DateInput } from '../../components/forms/DateInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { TextInput } from '../../components/forms/TextInput';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import { EmployeeSearchSelect } from '../../features/hr/components/EmployeeSearchSelect';
import { hrAbsenceService } from '../../features/hr/services/hrAbsenceService';
import { hrEmployeeService } from '../../features/hr/services/hrEmployeeService';
import type { AbsenceDetails, AbsenceListItem, AbsenceStatus, PagedAbsences, PayrollImpactStatus, SaveAbsenceRequest } from '../../features/hr/types/absence';
import type { DepartmentOption } from '../../features/hr/types/employee';
import type { TranslationKey } from '../../localization/translations';
import { getApiErrorMessage } from '../../services/apiClient';

const emptyPage: PagedAbsences = { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 };
const emptyForm: SaveAbsenceRequest = { employeeId: '', absenceDate: '', type: 'Absent', reason: '', status: 'Pending', notes: '', attendanceSource: 'Manual' };
const statuses: AbsenceStatus[] = ['Pending', 'Excused', 'Unexcused'];
const statusLabels: Record<AbsenceStatus, TranslationKey> = { Pending: 'pending', Excused: 'excused', Unexcused: 'unexcused' };
const statusTones: Record<AbsenceStatus, StatusTone> = { Pending: 'warning', Excused: 'success', Unexcused: 'danger' };
const payrollTones: Record<PayrollImpactStatus, StatusTone> = { NotApplicable: 'neutral', PendingReview: 'warning', Approved: 'success', Excluded: 'info' };
const payrollCopy = {
  en: { impact: 'Payroll impact', notApplicable: 'No deduction', pendingReview: 'Needs review', approved: 'Deduction approved', excluded: 'Excluded from payroll', suggested: 'Suggested deduction', final: 'Approved deduction', review: 'Review payroll', approve: 'Approve deduction', exclude: 'Exclude deduction', notes: 'Payroll review notes', reviewer: 'Reviewed by', explanation: 'The suggestion is calculated from the basic salary for the absence date ÷ 30. Nothing is deducted until an authorized HR manager approves it.', noSalary: 'No active basic salary was found for this date; review the amount before approval.', reviewError: 'Unable to save the payroll review.' },
  ar: { impact: 'التأثير على المرتب', notApplicable: 'بدون خصم', pendingReview: 'يحتاج مراجعة', approved: 'تم اعتماد الخصم', excluded: 'مستبعد من الخصم', suggested: 'الخصم المقترح', final: 'الخصم المعتمد', review: 'مراجعة الخصم', approve: 'اعتماد الخصم', exclude: 'استبعاد الخصم', notes: 'ملاحظات مراجعة المرتب', reviewer: 'تمت المراجعة بواسطة', explanation: 'يُحسب المقترح من الراتب الأساسي الساري في تاريخ الغياب ÷ 30، ولا يُخصم أي مبلغ إلا بعد اعتماد مدير الموارد البشرية.', noSalary: 'لا يوجد راتب أساسي سارٍ في هذا التاريخ؛ راجع المبلغ قبل الاعتماد.', reviewError: 'تعذر حفظ مراجعة تأثير الغياب على المرتب.' },
} as const;

function payrollLabel(status: PayrollImpactStatus, language: 'en' | 'ar'): string {
  const text = payrollCopy[language];
  return status === 'PendingReview' ? text.pendingReview : status === 'Approved' ? text.approved : status === 'Excluded' ? text.excluded : text.notApplicable;
}

function money(value: number | null, language: 'en' | 'ar'): string {
  if (value === null) return '—';
  return new Intl.NumberFormat(language === 'ar' ? 'ar-EG' : 'en-EG', { currency: 'EGP', style: 'currency', maximumFractionDigits: 2 }).format(value);
}

function displayDate(value: string, language: 'en' | 'ar'): string {
  return new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { dateStyle: 'medium' }).format(new Date(`${value}T12:00:00`));
}

function AbsenceForm({ absence, onClose, onSaved }: { absence: AbsenceDetails | null; onClose: () => void; onSaved: () => void }) {
  const { t } = useLocalization();
  const toast = useToast();
  const formId = 'absence-form';
  const [form, setForm] = useState<SaveAbsenceRequest>(() => absence ? {
    employeeId: absence.employeeId,
    absenceDate: absence.absenceDate,
    type: 'Absent',
    reason: absence.reason ?? '',
    status: absence.status,
    notes: absence.notes ?? '',
    attendanceSource: 'Manual',
  } : { ...emptyForm });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    if (!form.employeeId || !form.absenceDate) {
      setError(t('employeeDateRequired'));
      return;
    }

    setSaving(true);
    try {
      if (absence) {
        await hrAbsenceService.updateAbsence(absence.id, form);
        toast.success(t('absenceUpdatedSuccess'));
      } else {
        await hrAbsenceService.createAbsence(form);
        toast.success(t('absenceCreatedSuccess'));
      }
      onSaved();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, t('saveAbsenceError')));
    } finally {
      setSaving(false);
    }
  }

  const initialEmployee = absence ? { id: absence.employeeId, employeeNumber: absence.employeeNumber, fullName: absence.employeeName } : null;

  return (
    <Modal
      closeOnBackdrop={!saving}
      closeOnEscape={!saving}
      footer={(
        <>
          <Button disabled={saving} fullWidth={false} onClick={onClose} type="button" variant="outline">{t('cancel')}</Button>
          <Button form={formId} fullWidth={false} isLoading={saving} type="submit">{absence ? t('saveChanges') : t('recordAbsence')}</Button>
        </>
      )}
      hideCloseButton={saving}
      onClose={onClose}
      open
      title={t(absence ? 'editAbsence' : 'recordAbsence')}
    >
      <form className="space-y-5" id={formId} onSubmit={submit}>
        <FormError message={error} />
        <EmployeeSearchSelect
          includeInactive={Boolean(absence)}
          initialSelection={initialEmployee}
          label={t('employee')}
          onChange={(employeeId) => setForm((current) => ({ ...current, employeeId }))}
          required
          value={form.employeeId}
        />
        <DateInput label={t('date')} onChange={(event) => setForm((current) => ({ ...current, absenceDate: event.target.value }))} required value={form.absenceDate} />
        <SelectInput label={t('type')} value="Absent" disabled><option value="Absent">{t('absent')}</option></SelectInput>
        <TextAreaInput label={t('reason')} maxLength={500} onChange={(event) => setForm((current) => ({ ...current, reason: event.target.value }))} rows={3} value={form.reason} />
        <SelectInput label={t('status')} onChange={(event) => setForm((current) => ({ ...current, status: event.target.value as AbsenceStatus }))} value={form.status}>
          {statuses.map((status) => <option key={status} value={status}>{t(statusLabels[status])}</option>)}
        </SelectInput>
        <TextAreaInput label={t('notes')} maxLength={2000} onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))} rows={4} value={form.notes} />
        <SelectInput label={t('attendanceSource')} value="Manual" disabled><option value="Manual">{t('manual')}</option></SelectInput>
      </form>
    </Modal>
  );
}

function DetailField({ label, value, wide = false }: { label: string; value: ReactNode; wide?: boolean }) {
  return <div className={`rounded-xl bg-mis-surface p-4 ${wide ? 'sm:col-span-2' : ''}`}><dt className="text-xs font-semibold text-slate-500">{label}</dt><dd className="mt-1.5 break-words text-sm font-semibold text-mis-navy" dir="auto">{value}</dd></div>;
}

function DetailsModal({ absence, onClose }: { absence: AbsenceDetails; onClose: () => void }) {
  const { language, t } = useLocalization();
  const { user } = useAuth();
  const text = payrollCopy[language];
  const canReviewPayroll = user?.roles.includes('HrManager') ?? false;
  return (
    <Modal footer={<Button fullWidth={false} onClick={onClose}>{t('close')}</Button>} onClose={onClose} open title={t('absenceDetails')}>
      <dl className="grid gap-4 sm:grid-cols-2">
        <DetailField label={t('employee')} value={absence.employeeName} />
        <DetailField label={t('employeeId')} value={absence.employeeNumber} />
        <DetailField label={t('department')} value={absence.departmentName} />
        <DetailField label={t('date')} value={displayDate(absence.absenceDate, language)} />
        <DetailField label={t('type')} value={t('absent')} />
        <DetailField label={t('status')} value={<StatusBadge tone={statusTones[absence.status]}>{t(statusLabels[absence.status])}</StatusBadge>} />
        <DetailField label={t('reason')} value={absence.reason || t('unknown')} />
        <DetailField label={t('attendanceSource')} value={t('manual')} />
        <DetailField label={t('notes')} value={absence.notes || '—'} wide />
        {canReviewPayroll ? <><DetailField label={text.impact} value={<StatusBadge tone={payrollTones[absence.payrollImpactStatus]}>{payrollLabel(absence.payrollImpactStatus, language)}</StatusBadge>} /><DetailField label={text.suggested} value={money(absence.suggestedDeductionAmount, language)} /><DetailField label={text.final} value={money(absence.approvedDeductionAmount, language)} /><DetailField label={text.reviewer} value={absence.payrollReviewedByUsername || '—'} />{absence.payrollNotes ? <DetailField label={text.notes} value={absence.payrollNotes} wide /> : null}</> : null}
      </dl>
    </Modal>
  );
}

function PayrollReviewModal({ absence, onClose, onSaved }: { absence: AbsenceDetails; onClose: () => void; onSaved: () => void }) {
  const { language } = useLocalization();
  const toast = useToast();
  const text = payrollCopy[language];
  const [amount, setAmount] = useState(String(absence.approvedDeductionAmount ?? absence.suggestedDeductionAmount));
  const [notes, setNotes] = useState(absence.payrollNotes ?? '');
  const [saving, setSaving] = useState<'Approve' | 'Exclude' | null>(null);
  const [error, setError] = useState('');

  async function submit(decision: 'Approve' | 'Exclude') {
    const parsedAmount = Number(amount);
    if (decision === 'Approve' && (!Number.isFinite(parsedAmount) || parsedAmount < 0)) { setError(text.reviewError); return; }
    setSaving(decision); setError('');
    try {
      await hrAbsenceService.reviewPayrollImpact(absence.id, { decision, approvedDeductionAmount: decision === 'Approve' ? parsedAmount : null, notes });
      toast.success(decision === 'Approve' ? text.approved : text.excluded);
      onSaved();
    } catch (requestError) { setError(getApiErrorMessage(requestError, text.reviewError)); }
    finally { setSaving(null); }
  }

  return <Modal closeOnBackdrop={!saving} closeOnEscape={!saving} footer={<><Button disabled={Boolean(saving)} fullWidth={false} onClick={onClose} variant="outline">{language === 'ar' ? 'إلغاء' : 'Cancel'}</Button><Button disabled={Boolean(saving)} fullWidth={false} leftIcon={<Ban className="h-4 w-4" />} onClick={() => void submit('Exclude')} variant="outline">{text.exclude}</Button><Button fullWidth={false} isLoading={saving === 'Approve'} leftIcon={<CheckCircle2 className="h-4 w-4" />} onClick={() => void submit('Approve')}>{text.approve}</Button></>} hideCloseButton={Boolean(saving)} onClose={onClose} open title={text.review}>
    <div className="space-y-5">
      <div className="rounded-xl border border-sky-200 bg-sky-50 p-4 text-sm leading-6 text-sky-900"><div className="flex gap-3"><BadgeDollarSign className="mt-0.5 h-5 w-5 flex-none" /><p>{text.explanation}</p></div>{absence.suggestedDeductionAmount === 0 ? <p className="mt-2 font-bold text-amber-800">{text.noSalary}</p> : null}</div>
      <FormError message={error} />
      <TextInput label={text.final} min={0} onChange={(event) => setAmount(event.target.value)} required step="0.01" type="number" value={amount} />
      <TextAreaInput label={text.notes} maxLength={1000} onChange={(event) => setNotes(event.target.value)} rows={4} value={notes} />
    </div>
  </Modal>;
}

export function HrAbsencesPage() {
  const { language, t } = useLocalization();
  const { user } = useAuth();
  const toast = useToast();
  const [searchParams] = useSearchParams();
  const [data, setData] = useState(emptyPage);
  const [departments, setDepartments] = useState<DepartmentOption[]>([]);
  const [searchInput, setSearchInput] = useState(() => searchParams.get('employee') ?? '');
  const [search, setSearch] = useState(() => searchParams.get('employee') ?? '');
  const [departmentId, setDepartmentId] = useState('');
  const [date, setDate] = useState('');
  const [status, setStatus] = useState('all');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [formRecord, setFormRecord] = useState<AbsenceDetails | null | undefined>(undefined);
  const [details, setDetails] = useState<AbsenceDetails | null>(null);
  const [payrollReview, setPayrollReview] = useState<AbsenceDetails | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<AbsenceListItem | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 350);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    void hrEmployeeService.getDepartments().then(setDepartments).catch(() => setError(t('loadDepartmentsError')));
  }, [language, t]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setData(await hrAbsenceService.getAbsences({ page, pageSize: 20, search, departmentId, date, status }));
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, t('loadAbsencesError')));
    } finally {
      setLoading(false);
    }
  }, [date, departmentId, language, page, search, status, t]);

  useEffect(() => { void load(); }, [load]);

  async function openRecord(item: AbsenceListItem, mode: 'view' | 'edit' | 'payroll') {
    try {
      const record = await hrAbsenceService.getAbsence(item.id);
      if (mode === 'view') setDetails(record);
      else if (mode === 'edit') setFormRecord(record);
      else setPayrollReview(record);
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, t('loadAbsenceError')));
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await hrAbsenceService.deleteAbsence(deleteTarget.id);
      toast.success(t('absenceDeletedSuccess'));
      setDeleteTarget(null);
      await load();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, t('deleteAbsenceError')));
    } finally {
      setDeleting(false);
    }
  }

  const hasFilters = Boolean(search || departmentId || date || status !== 'all');
  const canReviewPayroll = user?.roles.includes('HrManager') ?? false;
  const headings = useMemo(() => [t('employeeId'), t('employee'), t('department'), t('date'), t('status'), payrollCopy[language].impact, t('actions')], [language, t]);

  return (
    <div className="mx-auto max-w-7xl">
      <PageHeader
        actions={<Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} onClick={() => setFormRecord(null)}>{t('recordAbsence')}</Button>}
        description={t('absencesSubtitle')}
        eyebrow={t('hrDepartment')}
        title={t('absencesTitle')}
      />
      <section className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="grid gap-3 border-b border-mis-border p-4 md:grid-cols-2 xl:grid-cols-[minmax(220px,1fr)_180px_170px_150px]">
          <label className="relative">
            <Search className="absolute start-3 top-3 h-5 w-5 text-slate-400" aria-hidden="true" />
            <input className="h-11 w-full rounded-xl border border-mis-border pe-3 ps-10 text-sm outline-none focus:border-mis-blue" onChange={(event) => setSearchInput(event.target.value)} placeholder={t('searchEmployee')} value={searchInput} />
          </label>
          <ProfessionalSelect aria-label={t('department')} className="h-11 rounded-xl border border-mis-border bg-white px-3 text-sm" onChange={(event) => { setDepartmentId(event.target.value); setPage(1); }} value={departmentId}>
            <option value="">{t('allDepartments')}</option>
            {departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}
          </ProfessionalSelect>
          <DateControl aria-label={t('absenceDateFilter')} className="h-11 rounded-xl border border-mis-border px-3 text-sm" onChange={(event) => { setDate(event.target.value); setPage(1); }}  value={date} />
          <ProfessionalSelect aria-label={t('status')} className="h-11 rounded-xl border border-mis-border bg-white px-3 text-sm" onChange={(event) => { setStatus(event.target.value); setPage(1); }} value={status}>
            <option value="all">{t('all')}</option>
            {statuses.map((item) => <option key={item} value={item.toLowerCase()}>{t(statusLabels[item])}</option>)}
          </ProfessionalSelect>
        </div>

        {error ? <div className="p-5"><ErrorState compact message={error} onRetry={() => void load()} title={t('loadAbsencesError')} /></div> : loading ? (
          <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>
        ) : data.items.length === 0 ? (
          <EmptyState
            action={!hasFilters ? <Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} onClick={() => setFormRecord(null)}>{t('recordAbsence')}</Button> : undefined}
            description={hasFilters ? t('adjustFilters') : t('addFirstAbsence')}
            icon={<CalendarX2 />}
            title={t('noAbsencesFound')}
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[850px] text-start">
                <thead className="bg-mis-surface text-xs uppercase tracking-wide text-slate-500"><tr>{headings.map((heading) => <th className="px-5 py-4 text-start" key={heading}>{heading}</th>)}</tr></thead>
                <tbody className="divide-y divide-mis-border">
                  {data.items.map((item) => (
                    <tr className="hover:bg-slate-50/70" key={item.id}>
                      <td className="px-5 py-4 text-sm font-semibold text-mis-navy">{item.employeeNumber}</td>
                      <td className="px-5 py-4 text-sm text-slate-700">{item.employeeName}</td>
                      <td className="px-5 py-4 text-sm text-slate-600">{item.departmentName}</td>
                      <td className="px-5 py-4 text-sm text-slate-600">{displayDate(item.absenceDate, language)}</td>
                      <td className="px-5 py-4"><StatusBadge tone={statusTones[item.status]}>{t(statusLabels[item.status])}</StatusBadge></td>
                      <td className="px-5 py-4"><div className="space-y-1"><StatusBadge tone={payrollTones[item.payrollImpactStatus]}>{payrollLabel(item.payrollImpactStatus, language)}</StatusBadge>{canReviewPayroll && item.payrollImpactStatus === 'Approved' ? <p className="text-xs font-bold text-emerald-700">{money(item.approvedDeductionAmount, language)}</p> : canReviewPayroll && item.payrollImpactStatus === 'PendingReview' ? <p className="text-xs font-semibold text-amber-700">{money(item.suggestedDeductionAmount, language)}</p> : null}</div></td>
                      <td className="px-5 py-4"><div className="flex justify-end gap-1">
                        <button aria-label={t('view')} className="rounded-lg p-2 text-mis-primary hover:bg-mis-pale" onClick={() => void openRecord(item, 'view')} type="button"><Eye className="h-4 w-4" /></button>
                        <button aria-label={t('edit')} className="rounded-lg p-2 text-mis-primary hover:bg-mis-pale" onClick={() => void openRecord(item, 'edit')} type="button"><Pencil className="h-4 w-4" /></button>
                        {canReviewPayroll && item.status === 'Unexcused' ? <button aria-label={payrollCopy[language].review} className="rounded-lg p-2 text-emerald-700 hover:bg-emerald-50" onClick={() => void openRecord(item, 'payroll')} title={payrollCopy[language].review} type="button"><BadgeDollarSign className="h-4 w-4" /></button> : null}
                        <button aria-label={t('delete')} className="rounded-lg p-2 text-red-600 hover:bg-red-50" onClick={() => setDeleteTarget(item)} type="button"><Trash2 className="h-4 w-4" /></button>
                      </div></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination onPageChange={setPage} page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} totalPages={data.totalPages} />
          </>
        )}
      </section>

      {formRecord !== undefined ? <AbsenceForm absence={formRecord} onClose={() => setFormRecord(undefined)} onSaved={() => { setFormRecord(undefined); void load(); }} /> : null}
      {details ? <DetailsModal absence={details} onClose={() => setDetails(null)} /> : null}
      {payrollReview ? <PayrollReviewModal absence={payrollReview} onClose={() => setPayrollReview(null)} onSaved={() => { setPayrollReview(null); void load(); }} /> : null}
      {deleteTarget ? (
        <Modal
          closeOnBackdrop={!deleting}
          closeOnEscape={!deleting}
          footer={(
            <>
              <Button disabled={deleting} fullWidth={false} onClick={() => setDeleteTarget(null)} variant="outline">{t('cancel')}</Button>
              <Button fullWidth={false} isLoading={deleting} onClick={() => void confirmDelete()} variant="danger">{t('delete')}</Button>
            </>
          )}
          hideCloseButton={deleting}
          onClose={() => setDeleteTarget(null)}
          open
          title={t('deleteAbsence')}
        >
          <p className="text-sm leading-6 text-slate-600">{t('deleteConfirmation', { name: deleteTarget.employeeName, date: displayDate(deleteTarget.absenceDate, language) })}</p>
        </Modal>
      ) : null}
    </div>
  );
}
