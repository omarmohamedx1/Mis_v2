import { CheckCircle2, RefreshCw, Save, Wallet, WalletCards, X } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { AccountingKpi, AccountingMonthSelect, AccountingStatus, useAccountingText } from '../../features/accounting/accountingUi';
import { accountingService } from '../../features/accounting/services/accountingService';
import type { AccountingEmployeePayroll, AccountingPayrollPeriod } from '../../features/accounting/types/accounting';
import { useAuth } from '../../context/AuthContext';
import { canAccessModule } from '../../features/modules/moduleAccess';
import { getApiErrorMessage } from '../../services/apiClient';

const editable = ['DRAFT', 'PENDING_REVIEW'];

export function AccountingSalariesPage() {
  const a = useAccountingText();
  const { user } = useAuth();
  const canOpenHrPayroll = user ? canAccessModule(user, 'hr') : false;
  const now = useMemo(() => new Date(), []);
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [periods, setPeriods] = useState<AccountingPayrollPeriod[]>([]);
  const [periodId, setPeriodId] = useState('');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [rows, setRows] = useState<AccountingEmployeePayroll[]>([]);
  const [selected, setSelected] = useState<AccountingEmployeePayroll>();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const refreshPeriods = useCallback(async () => {
    const list = await accountingService.periods();
    setPeriods(list);
    const match = list.find((period) => period.year === year && period.month === month);
    if (match) setPeriodId(match.id);
  }, [year, month]);

  const loadRows = useCallback(async (id: string) => {
    if (!id) { setRows([]); return; }
    const page = await accountingService.salaries(id, search, status);
    setRows(page.items);
  }, [search, status]);

  useEffect(() => { refreshPeriods().catch((reason) => setError(getApiErrorMessage(reason, a.text('تعذر تحميل الفترات', 'Failed to load periods')))); }, [refreshPeriods]);
  useEffect(() => { if (periodId) loadRows(periodId).catch((reason) => setError(getApiErrorMessage(reason, a.text('تعذر تحميل المرتبات', 'Failed to load salaries')))); }, [periodId, loadRows]);

  const generate = async () => {
    setBusy(true); setError('');
    try {
      const period = await accountingService.generatePayroll(year, month);
      setPeriodId(period.id);
      await refreshPeriods();
      await loadRows(period.id);
    } catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر إنشاء المسير', 'Could not generate payroll'))); }
    finally { setBusy(false); }
  };

  const saveSelected = async () => {
    if (!selected) return;
    setBusy(true); setError('');
    try {
      const updated = await accountingService.updateSalary(selected.id, {
        transportation: selected.transportation,
        commissions: selected.commissions,
        bonuses: selected.bonuses,
        deductions: selected.deductions,
        otherAdjustments: selected.otherAdjustments,
        notes: selected.notes ?? undefined,
      });
      setSelected(updated);
      await loadRows(periodId);
    } catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر الحفظ', 'Could not save'))); }
    finally { setBusy(false); }
  };

  const act = async (id: string, action: 'submit' | 'approve' | 'pay' | 'cancel') => {
    setBusy(true); setError('');
    try {
      const updated = await accountingService.salaryAction(id, action);
      if (selected?.id === id) setSelected(updated);
      await loadRows(periodId);
    } catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر تنفيذ الإجراء', 'Action failed'))); }
    finally { setBusy(false); }
  };

  const bulk = async (action: 'submit' | 'approve' | 'pay') => {
    const targets = rows.filter((row) => (
      action === 'submit' ? row.status === 'DRAFT'
        : action === 'approve' ? editable.includes(row.status)
          : row.status === 'APPROVED'
    ));
    if (!targets.length) return;
    setBusy(true); setError('');
    try {
      for (const row of targets) await accountingService.salaryAction(row.id, action);
      await loadRows(periodId);
    } catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر تنفيذ الإجراء الجماعي', 'Bulk action failed'))); }
    finally { setBusy(false); }
  };

  const currentPeriod = periods.find((period) => period.id === periodId);
  const totalNet = rows.reduce((sum, row) => sum + row.netSalary, 0);
  const pending = rows.filter((row) => editable.includes(row.status)).length;
  const approved = rows.filter((row) => row.status === 'APPROVED').length;
  const drafts = rows.filter((row) => row.status === 'DRAFT').length;

  return (
    <div className="space-y-5">
      <header className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-mis-navy">{a.text('المرتبات', 'Salaries')}</h1>
          <p className="text-sm text-slate-500">{a.text('مسير شهري من بيانات الموظفين والتعويضات الحالية. خصومات الغياب والتأخير وزيارات الجمعة تُحسب من الموارد البشرية تلقائيًا.', 'Monthly payroll from current employees and compensation. Absence, late, and Friday-visit deductions are calculated automatically from HR.')}</p>
          {canOpenHrPayroll ? <Link className="mt-2 inline-flex text-sm font-semibold text-mis-primary" to="/hr/payroll">{a.text('فتح صافي مرتبات الموارد البشرية', 'Open HR net salaries')}</Link> : null}
        </div>
        <div className="flex min-w-0 flex-wrap gap-2">
          <AccountingMonthSelect value={month} onChange={setMonth} />
          <input aria-label={a.text('السنة', 'Year')} className="h-11 w-24 rounded-xl border border-mis-border px-3 text-sm" type="number" value={year} onChange={(event) => setYear(Number(event.target.value))} />
          <Button disabled={busy} fullWidth={false} leftIcon={<RefreshCw className="h-4 w-4" />} onClick={() => void generate()} size="md">{a.text('توليد / تحديث', 'Generate / refresh')}</Button>
        </div>
      </header>

      {error ? <p className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p> : null}

      {periodId ? (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <AccountingKpi label={a.text('عدد الموظفين', 'Employees')} value={a.number(currentPeriod?.employeeCount ?? rows.length)} />
          <AccountingKpi label={a.text('صافي المسير', 'Net payroll')} value={a.money(currentPeriod?.totalNet ?? totalNet)} />
          <AccountingKpi label={a.text('بانتظار الاعتماد', 'Awaiting approval')} value={a.number(pending)} />
          <AccountingKpi label={a.text('جاهز للصرف', 'Ready to pay')} value={a.number(approved)} />
        </div>
      ) : null}

      <div className="flex flex-wrap gap-2">
        <input className="min-w-[220px] flex-1 rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('بحث بالموظف', 'Search employee')} value={search} onChange={(event) => setSearch(event.target.value)} />
        <ProfessionalSelect className="min-h-11 min-w-[12rem] rounded-xl border border-mis-border bg-white px-3 text-sm" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">{a.text('كل الحالات', 'All statuses')}</option>
          {['DRAFT', 'PENDING_REVIEW', 'APPROVED', 'PAID', 'CANCELLED'].map((value) => <option key={value} value={value}>{a.status(value)}</option>)}
        </ProfessionalSelect>
        <ProfessionalSelect className="min-h-11 min-w-[14rem] rounded-xl border border-mis-border bg-white px-3 text-sm" value={periodId} onChange={(event) => setPeriodId(event.target.value)}>
          <option value="">{a.text('اختر الفترة', 'Select period')}</option>
          {periods.map((period) => <option key={period.id} value={period.id}>{a.monthLabel(period.year, period.month)} · {a.number(period.employeeCount)}</option>)}
        </ProfessionalSelect>
        {drafts ? <Button disabled={busy} fullWidth={false} onClick={() => void bulk('submit')} size="sm" variant="outline">{a.text('إرسال المسودات', 'Submit drafts')}</Button> : null}
        {pending ? <Button disabled={busy} fullWidth={false} leftIcon={<CheckCircle2 className="h-4 w-4" />} onClick={() => void bulk('approve')} size="sm" variant="outline">{a.text('اعتماد المعروض', 'Approve listed')}</Button> : null}
        {approved ? <Button disabled={busy} fullWidth={false} onClick={() => void bulk('pay')} size="sm" variant="outline">{a.text('صرف المعتمد', 'Pay approved')}</Button> : null}
      </div>

      {!periodId ? (
        <EmptyState
          icon={<Wallet className="h-5 w-5" />}
          title={a.text('لا يوجد مسير لهذا الشهر', 'No payroll for this month')}
          description={a.text('اختر الشهر ثم ولّد المسير للموظفين الذين لديهم تعويض فعّال.', 'Pick a month then generate payroll for employees with effective compensation.')}
          action={<Button disabled={busy} fullWidth={false} leftIcon={<RefreshCw className="h-4 w-4" />} onClick={() => void generate()}>{a.text('توليد المسير', 'Generate payroll')}</Button>}
        />
      ) : (
        <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[48rem] text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  <th className="px-3 py-3 text-start">{a.text('الرقم', 'No.')}</th>
                  <th className="px-3 py-3 text-start">{a.text('الموظف', 'Employee')}</th>
                  <th className="px-3 py-3 text-start">{a.text('القسم', 'Dept')}</th>
                  <th className="px-3 py-3 text-end">{a.text('الأساسي', 'Basic')}</th>
                  <th className="px-3 py-3 text-end">{a.text('صافي', 'Net')}</th>
                  <th className="px-3 py-3 text-start">{a.text('الحالة', 'Status')}</th>
                  <th className="px-3 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {rows.map((row) => (
                  <tr key={row.id} className="hover:bg-slate-50">
                    <td className="px-3 py-2 font-mono text-xs">{row.employeeNumber}</td>
                    <td className="px-3 py-2 font-semibold text-mis-navy">{row.employeeName}</td>
                    <td className="px-3 py-2 text-slate-600">{row.departmentName ?? '—'}</td>
                    <td className="px-3 py-2 text-end" data-bidi="ltr">{a.money(row.basicSalary)}</td>
                    <td className="px-3 py-2 text-end font-bold" data-bidi="ltr">{a.money(row.netSalary)}</td>
                    <td className="px-3 py-2"><AccountingStatus value={row.status} /></td>
                    <td className="px-3 py-2 text-end">
                      <Button fullWidth={false} leftIcon={<WalletCards className="h-4 w-4" />} onClick={() => setSelected(row)} size="sm" variant="ghost">{a.text('تفاصيل', 'Details')}</Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!rows.length ? <EmptyState compact title={a.text('لا توجد مرتبات لهذه الفترة', 'No salary rows for this period')} description={a.text('غيّر البحث أو الحالة، أو حدّث المسير إذا أُضيف موظفون جدد.', 'Change the search or status, or refresh payroll if new employees were added.')} /> : null}
        </div>
      )}

      {selected ? (
        <div className="fixed inset-0 z-40 grid place-items-center bg-slate-900/40 p-4" onClick={() => setSelected(undefined)}>
          <div className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-6 shadow-xl" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 className="text-xl font-bold text-mis-navy">{selected.employeeName}</h2>
                <p className="text-sm text-slate-500">{selected.employeeNumber} · {selected.positionName ?? '—'}</p>
              </div>
              <AccountingStatus value={selected.status} />
            </div>
            <div className="mt-5 grid gap-3 sm:grid-cols-2">
              {([
                ['basicSalary', a.text('الأساسي', 'Basic'), true],
                ['allowances', a.text('البدلات', 'Allowances'), true],
                ['transportation', a.text('انتقالات', 'Transportation'), false],
                ['commissions', a.text('عمولات', 'Commissions'), false],
                ['bonuses', a.text('مكافآت', 'Bonuses'), false],
                ['deductions', a.text('خصومات', 'Deductions'), false],
                ['otherAdjustments', a.text('تسويات أخرى', 'Other adjustments'), false],
              ] as const).map(([key, label, readOnly]) => (
                <label key={key} className="text-sm">
                  <span className="mb-1 block text-slate-500">{label}</span>
                  <input
                    type="number"
                    className="w-full rounded-xl border border-mis-border px-3 py-2 disabled:bg-slate-50"
                    disabled={readOnly || !editable.includes(selected.status)}
                    value={selected[key]}
                    onChange={(event) => setSelected({ ...selected, [key]: Number(event.target.value) })}
                  />
                </label>
              ))}
            </div>
            <label className="mt-3 block text-sm">
              <span className="mb-1 block text-slate-500">{a.text('ملاحظات', 'Notes')}</span>
              <textarea className="w-full rounded-xl border border-mis-border px-3 py-2 disabled:bg-slate-50" disabled={!editable.includes(selected.status)} value={selected.notes ?? ''} onChange={(event) => setSelected({ ...selected, notes: event.target.value })} />
            </label>
            <p className="mt-4 text-lg font-bold text-mis-navy" data-bidi="ltr">{a.text('الصافي', 'Net')}: {a.money(selected.netSalary)}</p>
            <div className="mt-5 flex flex-wrap gap-2">
              {editable.includes(selected.status) ? <Button disabled={busy} fullWidth={false} leftIcon={<Save className="h-4 w-4" />} onClick={() => void saveSelected()}>{a.text('حفظ', 'Save')}</Button> : null}
              {selected.status === 'DRAFT' ? <Button disabled={busy} fullWidth={false} onClick={() => void act(selected.id, 'submit')} variant="outline">{a.text('إرسال للمراجعة', 'Submit')}</Button> : null}
              {editable.includes(selected.status) ? <Button disabled={busy} fullWidth={false} leftIcon={<CheckCircle2 className="h-4 w-4" />} onClick={() => void act(selected.id, 'approve')} variant="outline">{a.text('اعتماد', 'Approve')}</Button> : null}
              {selected.status === 'APPROVED' ? <Button disabled={busy} fullWidth={false} onClick={() => void act(selected.id, 'pay')} variant="outline">{a.text('تم الصرف', 'Mark paid')}</Button> : null}
              {selected.status !== 'PAID' && selected.status !== 'CANCELLED' ? <Button disabled={busy} fullWidth={false} onClick={() => void act(selected.id, 'cancel')} variant="danger">{a.text('إلغاء', 'Cancel')}</Button> : null}
              <Button fullWidth={false} leftIcon={<X className="h-4 w-4" />} onClick={() => setSelected(undefined)} variant="ghost">{a.text('إغلاق', 'Close')}</Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
