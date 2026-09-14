import { useCallback, useEffect, useMemo, useState } from 'react';
import { getApiErrorMessage } from '../../services/apiClient';
import { AccountingStatus, useAccountingText } from '../../features/accounting/accountingUi';
import { accountingService } from '../../features/accounting/services/accountingService';
import type { AccountingSupervisorCommission } from '../../features/accounting/types/accounting';

export function AccountingSupervisorCommissionsPage() {
  const a = useAccountingText();
  const now = useMemo(() => new Date(), []);
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [rows, setRows] = useState<AccountingSupervisorCommission[]>([]);
  const [selected, setSelected] = useState<AccountingSupervisorCommission>();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [adjustValue, setAdjustValue] = useState(0);

  const load = useCallback(async () => {
    const page = await accountingService.supervisorCommissions({ year, month, search, status });
    setRows(page.items);
  }, [year, month, search, status]);

  useEffect(() => { load().catch((e) => setError(getApiErrorMessage(e, 'Failed to load supervisor commissions'))); }, [load]);

  const calculate = async () => {
    setBusy(true); setError('');
    try { await accountingService.calculateSupervisorCommissions(year, month); await load(); }
    catch (e) { setError(getApiErrorMessage(e, a.text('تعذر الحساب', 'Calculation failed'))); }
    finally { setBusy(false); }
  };

  const adjust = async () => {
    if (!selected) return;
    setBusy(true); setError('');
    try {
      const updated = await accountingService.adjustSupervisorCommission(selected.id, adjustValue, selected.notes ?? undefined);
      setSelected(updated);
      await load();
    } catch (e) { setError(getApiErrorMessage(e, a.text('تعذر التعديل', 'Adjustment failed'))); }
    finally { setBusy(false); }
  };

  const act = async (id: string, action: string) => {
    setBusy(true); setError('');
    try {
      const updated = await accountingService.supervisorCommissionAction(id, action);
      if (selected?.id === id) setSelected(updated);
      await load();
    } catch (e) { setError(getApiErrorMessage(e, a.text('تعذر تنفيذ الإجراء', 'Action failed'))); }
    finally { setBusy(false); }
  };

  return (
    <div className="space-y-5">
      <header className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-mis-navy">{a.text('عمولات المشرفين', 'Supervisor commissions')}</h1>
          <p className="text-sm text-slate-500">{a.text('حسب فرق التحصيل عبر علاقة المشرف بأعضاء الفريق الحالية', 'Based on existing supervisor → team member relationships')}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <select className="rounded-xl border px-3 py-2 text-sm" value={month} onChange={(e) => setMonth(Number(e.target.value))}>{Array.from({ length: 12 }, (_, i) => i + 1).map((m) => <option key={m} value={m}>{m}</option>)}</select>
          <input type="number" className="w-24 rounded-xl border px-3 py-2 text-sm" value={year} onChange={(e) => setYear(Number(e.target.value))} />
          <button disabled={busy} onClick={calculate} className="rounded-xl bg-mis-primary px-4 py-2 text-sm font-bold text-white">{a.text('احتساب الشهر', 'Calculate month')}</button>
        </div>
      </header>

      {error ? <p className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p> : null}

      <div className="flex flex-wrap gap-2">
        <input className="min-w-[200px] flex-1 rounded-xl border px-3 py-2 text-sm" placeholder={a.text('المشرف', 'Supervisor')} value={search} onChange={(e) => setSearch(e.target.value)} />
        <select className="rounded-xl border px-3 py-2 text-sm" value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="">{a.text('كل الحالات', 'All statuses')}</option>
          {['DRAFT', 'PENDING_REVIEW', 'APPROVED', 'PAID', 'CANCELLED'].map((s) => <option key={s} value={s}>{a.status(s)}</option>)}
        </select>
      </div>

      <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[1100px] text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-3 py-3 text-start">{a.text('المشرف', 'Supervisor')}</th>
                <th className="px-3 py-3 text-start">{a.text('الفريق', 'Team')}</th>
                <th className="px-3 py-3 text-end">{a.text('تحصيل الفريق', 'Team collected')}</th>
                <th className="px-3 py-3 text-start">{a.text('القاعدة', 'Rule')}</th>
                <th className="px-3 py-3 text-end">{a.text('النهائي', 'Final')}</th>
                <th className="px-3 py-3 text-start">{a.text('الحالة', 'Status')}</th>
                <th className="px-3 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-mis-border">
              {rows.map((row) => (
                <tr key={row.id}>
                  <td className="px-3 py-2"><div className="font-semibold">{row.supervisorName}</div><div className="font-mono text-xs text-slate-500">{row.employeeNumber ?? '—'}</div></td>
                  <td className="px-3 py-2 text-slate-600">{row.teamSummary ?? a.number(row.collectorCount)}</td>
                  <td className="px-3 py-2 text-end" data-bidi="ltr">{a.money(row.teamCollectedAmount)}</td>
                  <td className="px-3 py-2">{row.ruleCode} ({a.number(row.rateApplied)}%)</td>
                  <td className="px-3 py-2 text-end font-bold" data-bidi="ltr">{a.money(row.finalCommission)}</td>
                  <td className="px-3 py-2"><AccountingStatus value={row.status} /></td>
                  <td className="px-3 py-2 text-end"><button className="text-xs font-bold text-mis-primary" onClick={() => { setSelected(row); setAdjustValue(row.adjustments); }}>{a.text('تفاصيل', 'Details')}</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {selected ? (
        <div className="fixed inset-0 z-40 grid place-items-center bg-slate-900/40 p-4" onClick={() => setSelected(undefined)}>
          <div className="w-full max-w-xl rounded-2xl bg-white p-6 shadow-xl" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 className="text-xl font-bold text-mis-navy">{selected.supervisorName}</h2>
                <p className="text-sm text-slate-500">{a.monthLabel(selected.periodYear, selected.periodMonth)}</p>
              </div>
              <AccountingStatus value={selected.status} />
            </div>
            <dl className="mt-5 grid gap-2 sm:grid-cols-2 text-sm">
              <div><dt className="text-slate-500">{a.text('عدد المحصلين', 'Collectors')}</dt><dd className="font-bold">{a.number(selected.collectorCount)}</dd></div>
              <div><dt className="text-slate-500">{a.text('تحصيل الفريق', 'Team collected')}</dt><dd className="font-bold" data-bidi="ltr">{a.money(selected.teamCollectedAmount)}</dd></div>
              <div><dt className="text-slate-500">{a.text('المبلغ المؤهل', 'Eligible')}</dt><dd className="font-bold" data-bidi="ltr">{a.money(selected.eligibleAmount)}</dd></div>
              <div><dt className="text-slate-500">{a.text('القاعدة / النسبة', 'Rule / rate')}</dt><dd className="font-bold">{selected.ruleCode} · {a.number(selected.rateApplied)}%</dd></div>
              <div><dt className="text-slate-500">{a.text('العمولة', 'Commission')}</dt><dd className="font-bold" data-bidi="ltr">{a.money(selected.commissionAmount)}</dd></div>
              <div><dt className="text-slate-500">{a.text('النهائي', 'Final')}</dt><dd className="font-bold" data-bidi="ltr">{a.money(selected.finalCommission)}</dd></div>
            </dl>
            {['DRAFT', 'PENDING_REVIEW'].includes(selected.status) ? (
              <div className="mt-4 flex flex-wrap items-end gap-2">
                <label className="text-sm"><span className="mb-1 block text-slate-500">{a.text('تسوية', 'Adjustment')}</span>
                  <input type="number" className="rounded-xl border px-3 py-2" value={adjustValue} onChange={(e) => setAdjustValue(Number(e.target.value))} />
                </label>
                <button disabled={busy} onClick={adjust} className="rounded-xl border px-4 py-2 text-sm font-bold">{a.text('حفظ التسوية', 'Save adjustment')}</button>
                {selected.status === 'DRAFT' ? <button disabled={busy} onClick={() => act(selected.id, 'submit')} className="rounded-xl border px-4 py-2 text-sm font-bold">{a.text('إرسال', 'Submit')}</button> : null}
                <button disabled={busy} onClick={() => act(selected.id, 'approve')} className="rounded-xl bg-mis-primary px-4 py-2 text-sm font-bold text-white">{a.text('اعتماد', 'Approve')}</button>
              </div>
            ) : null}
            {selected.status === 'APPROVED' ? <button disabled={busy} onClick={() => act(selected.id, 'pay')} className="mt-4 rounded-xl border px-4 py-2 text-sm font-bold">{a.text('تم الصرف', 'Mark paid')}</button> : null}
            <button className="mt-4 ms-2 rounded-xl border px-4 py-2 text-sm font-bold" onClick={() => setSelected(undefined)}>{a.text('إغلاق', 'Close')}</button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
