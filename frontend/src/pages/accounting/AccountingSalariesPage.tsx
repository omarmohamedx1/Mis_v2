import { useCallback, useEffect, useMemo, useState } from 'react';
import { getApiErrorMessage } from '../../services/apiClient';
import { AccountingStatus, useAccountingText } from '../../features/accounting/accountingUi';
import { accountingService } from '../../features/accounting/services/accountingService';
import type { AccountingEmployeePayroll, AccountingPayrollPeriod } from '../../features/accounting/types/accounting';

export function AccountingSalariesPage() {
  const a = useAccountingText();
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
    const match = list.find((p) => p.year === year && p.month === month);
    if (match) setPeriodId(match.id);
  }, [year, month]);

  const loadRows = useCallback(async (id: string) => {
    if (!id) { setRows([]); return; }
    const page = await accountingService.salaries(id, search, status);
    setRows(page.items);
  }, [search, status]);

  useEffect(() => { refreshPeriods().catch((e) => setError(getApiErrorMessage(e, 'Failed to load periods'))); }, [refreshPeriods]);
  useEffect(() => { if (periodId) loadRows(periodId).catch((e) => setError(getApiErrorMessage(e, 'Failed to load salaries'))); }, [periodId, loadRows]);

  const generate = async () => {
    setBusy(true); setError('');
    try {
      const period = await accountingService.generatePayroll(year, month);
      setPeriodId(period.id);
      await refreshPeriods();
      await loadRows(period.id);
    } catch (e) { setError(getApiErrorMessage(e, a.text('تعذر إنشاء المسير', 'Could not generate payroll'))); }
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
    } catch (e) { setError(getApiErrorMessage(e, a.text('تعذر الحفظ', 'Could not save'))); }
    finally { setBusy(false); }
  };

  const act = async (id: string, action: 'submit' | 'approve' | 'pay' | 'cancel') => {
    setBusy(true); setError('');
    try {
      const updated = await accountingService.salaryAction(id, action);
      if (selected?.id === id) setSelected(updated);
      await loadRows(periodId);
    } catch (e) { setError(getApiErrorMessage(e, a.text('تعذر تنفيذ الإجراء', 'Action failed'))); }
    finally { setBusy(false); }
  };

  return (
    <div className="space-y-5">
      <header className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-mis-navy">{a.text('المرتبات', 'Salaries')}</h1>
          <p className="text-sm text-slate-500">{a.text('مسير شهري من بيانات الموظفين والتعويضات الحالية', 'Monthly payroll from existing employees and compensation')}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <select className="rounded-xl border border-mis-border bg-white px-3 py-2 text-sm" value={month} onChange={(e) => setMonth(Number(e.target.value))}>
            {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
          <input className="w-24 rounded-xl border border-mis-border px-3 py-2 text-sm" type="number" value={year} onChange={(e) => setYear(Number(e.target.value))} />
          <button disabled={busy} onClick={generate} className="rounded-xl bg-mis-primary px-4 py-2 text-sm font-bold text-white disabled:opacity-50">{a.text('توليد / تحديث', 'Generate / refresh')}</button>
        </div>
      </header>

      {error ? <p className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p> : null}

      <div className="flex flex-wrap gap-2">
        <input className="min-w-[220px] flex-1 rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('بحث بالموظف', 'Search employee')} value={search} onChange={(e) => setSearch(e.target.value)} />
        <select className="rounded-xl border border-mis-border px-3 py-2 text-sm" value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="">{a.text('كل الحالات', 'All statuses')}</option>
          {['DRAFT', 'PENDING_REVIEW', 'APPROVED', 'PAID', 'CANCELLED'].map((s) => <option key={s} value={s}>{a.status(s)}</option>)}
        </select>
        <select className="rounded-xl border border-mis-border px-3 py-2 text-sm" value={periodId} onChange={(e) => setPeriodId(e.target.value)}>
          <option value="">{a.text('اختر الفترة', 'Select period')}</option>
          {periods.map((p) => <option key={p.id} value={p.id}>{a.monthLabel(p.year, p.month)} · {a.number(p.employeeCount)}</option>)}
        </select>
      </div>

      {!periodId ? <p className="text-sm text-slate-500">{a.text('اختر شهراً ثم ولّد المسير للموظفين الذين لديهم تعويض فعّال.', 'Pick a month then generate payroll for employees with effective compensation.')}</p> : null}

      <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[1100px] text-sm">
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
                    <button className="text-xs font-bold text-mis-primary" onClick={() => setSelected(row)}>{a.text('تفاصيل', 'Details')}</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!rows.length && periodId ? <div className="px-4 py-10 text-center text-sm text-slate-500">{a.text('لا توجد مرتبات لهذه الفترة.', 'No salary rows for this period.')}</div> : null}
      </div>

      {selected ? (
        <div className="fixed inset-0 z-40 grid place-items-center bg-slate-900/40 p-4" onClick={() => setSelected(undefined)}>
          <div className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-6 shadow-xl" onClick={(e) => e.stopPropagation()}>
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
                    disabled={readOnly || !['DRAFT', 'PENDING_REVIEW'].includes(selected.status)}
                    value={selected[key]}
                    onChange={(e) => setSelected({ ...selected, [key]: Number(e.target.value) })}
                  />
                </label>
              ))}
            </div>
            <p className="mt-4 text-lg font-bold text-mis-navy" data-bidi="ltr">{a.text('الصافي', 'Net')}: {a.money(selected.netSalary)}</p>
            <div className="mt-5 flex flex-wrap gap-2">
              {['DRAFT', 'PENDING_REVIEW'].includes(selected.status) ? <button disabled={busy} onClick={saveSelected} className="rounded-xl bg-mis-primary px-4 py-2 text-sm font-bold text-white">{a.text('حفظ', 'Save')}</button> : null}
              {selected.status === 'DRAFT' ? <button disabled={busy} onClick={() => act(selected.id, 'submit')} className="rounded-xl border px-4 py-2 text-sm font-bold">{a.text('إرسال للمراجعة', 'Submit')}</button> : null}
              {['DRAFT', 'PENDING_REVIEW'].includes(selected.status) ? <button disabled={busy} onClick={() => act(selected.id, 'approve')} className="rounded-xl border px-4 py-2 text-sm font-bold">{a.text('اعتماد', 'Approve')}</button> : null}
              {selected.status === 'APPROVED' ? <button disabled={busy} onClick={() => act(selected.id, 'pay')} className="rounded-xl border px-4 py-2 text-sm font-bold">{a.text('تم الصرف', 'Mark paid')}</button> : null}
              {selected.status !== 'PAID' && selected.status !== 'CANCELLED' ? <button disabled={busy} onClick={() => act(selected.id, 'cancel')} className="rounded-xl border border-rose-200 px-4 py-2 text-sm font-bold text-rose-700">{a.text('إلغاء', 'Cancel')}</button> : null}
              <button onClick={() => setSelected(undefined)} className="ms-auto rounded-xl border px-4 py-2 text-sm font-bold">{a.text('إغلاق', 'Close')}</button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
