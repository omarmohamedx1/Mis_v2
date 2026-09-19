import { Calculator, CheckCircle2, Percent, Save, UsersRound, X } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { AccountingMonthSelect, AccountingStatus, useAccountingText } from '../../features/accounting/accountingUi';
import { accountingService } from '../../features/accounting/services/accountingService';
import type { AccountingSupervisorCommission } from '../../features/accounting/types/accounting';
import { getApiErrorMessage } from '../../services/apiClient';

const editable = ['DRAFT', 'PENDING_REVIEW'];

export function AccountingSupervisorCommissionsPage() {
  const a = useAccountingText();
  const navigate = useNavigate();
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

  useEffect(() => { load().catch((reason) => setError(getApiErrorMessage(reason, a.text('تعذر تحميل عمولات المشرفين', 'Failed to load supervisor commissions')))); }, [load]);

  const calculate = async () => {
    setBusy(true); setError('');
    try { await accountingService.calculateSupervisorCommissions(year, month); await load(); }
    catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر الحساب. تأكد من وجود قاعدة عمولة سارية للمشرفين.', 'Calculation failed. Ensure an active supervisor commission rule exists.'))); }
    finally { setBusy(false); }
  };

  const adjust = async () => {
    if (!selected) return;
    setBusy(true); setError('');
    try {
      const updated = await accountingService.adjustSupervisorCommission(selected.id, adjustValue, selected.notes ?? undefined);
      setSelected(updated);
      await load();
    } catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر التعديل', 'Adjustment failed'))); }
    finally { setBusy(false); }
  };

  const act = async (id: string, action: string) => {
    setBusy(true); setError('');
    try {
      const updated = await accountingService.supervisorCommissionAction(id, action);
      if (selected?.id === id) setSelected(updated);
      await load();
    } catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر تنفيذ الإجراء', 'Action failed'))); }
    finally { setBusy(false); }
  };

  const totalFinal = rows.reduce((sum, row) => sum + row.finalCommission, 0);

  return (
    <div className="space-y-5">
      <header className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-mis-navy">{a.text('عمولات المشرفين', 'Supervisor commissions')}</h1>
          <p className="text-sm text-slate-500">{a.text('حسب فرق التحصيل عبر علاقة المشرف بأعضاء الفريق الحالية، من المدفوعات المعتمدة فقط.', 'Based on current supervisor-to-team relationships, from approved payments only.')}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <AccountingMonthSelect value={month} onChange={setMonth} />
          <input aria-label={a.text('السنة', 'Year')} type="number" className="h-11 w-24 rounded-xl border border-mis-border px-3 text-sm" value={year} onChange={(event) => setYear(Number(event.target.value))} />
          <Button disabled={busy} fullWidth={false} leftIcon={<Calculator className="h-4 w-4" />} onClick={() => void calculate()}>{a.text('احتساب الشهر', 'Calculate month')}</Button>
          <Button fullWidth={false} leftIcon={<Percent className="h-4 w-4" />} onClick={() => navigate('/finance/commission-rules')} variant="outline">{a.text('قواعد العمولة', 'Commission rules')}</Button>
        </div>
      </header>

      {error ? <p className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p> : null}

      <div className="flex flex-wrap items-center gap-2">
        <input className="min-w-[200px] flex-1 rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('المشرف', 'Supervisor')} value={search} onChange={(event) => setSearch(event.target.value)} />
        <ProfessionalSelect className="min-h-11 min-w-[12rem] rounded-xl border border-mis-border bg-white px-3 text-sm" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">{a.text('كل الحالات', 'All statuses')}</option>
          {['DRAFT', 'PENDING_REVIEW', 'APPROVED', 'PAID', 'CANCELLED'].map((value) => <option key={value} value={value}>{a.status(value)}</option>)}
        </ProfessionalSelect>
        {rows.length ? <p className="ms-auto text-sm font-bold text-mis-navy" data-bidi="ltr">{a.text('إجمالي النهائي', 'Total final')}: {a.money(totalFinal)}</p> : null}
      </div>

      <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[48rem] text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-3 py-3 text-start">{a.text('المشرف', 'Supervisor')}</th>
                <th className="px-3 py-3 text-start">{a.text('الفريق', 'Team')}</th>
                <th className="px-3 py-3 text-end">{a.text('تحصيل الفريق', 'Team collected')}</th>
                <th className="px-3 py-3 text-start">{a.text('القاعدة', 'Rule')}</th>
                <th className="px-3 py-3 text-end">{a.text('العمولة', 'Commission')}</th>
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
                  <td className="px-3 py-2 text-end" data-bidi="ltr">{a.money(row.commissionAmount)}</td>
                  <td className="px-3 py-2 text-end font-bold" data-bidi="ltr">{a.money(row.finalCommission)}</td>
                  <td className="px-3 py-2"><AccountingStatus value={row.status} /></td>
                  <td className="px-3 py-2 text-end"><Button fullWidth={false} onClick={() => { setSelected(row); setAdjustValue(row.adjustments); }} size="sm" variant="ghost">{a.text('تفاصيل', 'Details')}</Button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!rows.length ? (
          <EmptyState
            compact
            icon={<UsersRound className="h-5 w-5" />}
            title={a.text('لا توجد عمولات مشرفين لهذا الشهر', 'No supervisor commissions for this month')}
            description={a.text('احسب الشهر بعد التأكد من وجود قاعدة سارية وعلاقة مشرف بفريق التحصيل.', 'Calculate the month after an active rule and supervisor-to-team relationships exist.')}
            action={<Button disabled={busy} fullWidth={false} leftIcon={<Calculator className="h-4 w-4" />} onClick={() => void calculate()}>{a.text('احتساب الشهر', 'Calculate month')}</Button>}
          />
        ) : null}
      </div>

      {selected ? (
        <div className="fixed inset-0 z-40 grid place-items-center bg-slate-900/40 p-4" onClick={() => setSelected(undefined)}>
          <div className="max-h-[90vh] w-full max-w-xl overflow-y-auto rounded-2xl bg-white p-6 shadow-xl" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 className="text-xl font-bold text-mis-navy">{selected.supervisorName}</h2>
                <p className="text-sm text-slate-500">{a.monthLabel(selected.periodYear, selected.periodMonth)}</p>
              </div>
              <AccountingStatus value={selected.status} />
            </div>
            <dl className="mt-5 grid gap-2 text-sm sm:grid-cols-2">
              <div><dt className="text-slate-500">{a.text('عدد المحصلين', 'Collectors')}</dt><dd className="font-bold">{a.number(selected.collectorCount)}</dd></div>
              <div><dt className="text-slate-500">{a.text('تحصيل الفريق', 'Team collected')}</dt><dd className="font-bold" data-bidi="ltr">{a.money(selected.teamCollectedAmount)}</dd></div>
              <div><dt className="text-slate-500">{a.text('المبلغ المؤهل', 'Eligible')}</dt><dd className="font-bold" data-bidi="ltr">{a.money(selected.eligibleAmount)}</dd></div>
              <div><dt className="text-slate-500">{a.text('القاعدة / النسبة', 'Rule / rate')}</dt><dd className="font-bold">{selected.ruleCode} · {a.number(selected.rateApplied)}%</dd></div>
              <div><dt className="text-slate-500">{a.text('العمولة', 'Commission')}</dt><dd className="font-bold" data-bidi="ltr">{a.money(selected.commissionAmount)}</dd></div>
              <div><dt className="text-slate-500">{a.text('النهائي', 'Final')}</dt><dd className="font-bold" data-bidi="ltr">{a.money(selected.finalCommission)}</dd></div>
            </dl>
            {selected.teamSummary ? <p className="mt-3 rounded-xl bg-slate-50 px-3 py-2 text-sm text-slate-600">{selected.teamSummary}</p> : null}
            {selected.notes ? <p className="mt-2 text-sm text-slate-500">{selected.notes}</p> : null}
            {editable.includes(selected.status) ? (
              <div className="mt-4 flex flex-wrap items-end gap-2">
                <label className="text-sm"><span className="mb-1 block text-slate-500">{a.text('تسوية', 'Adjustment')}</span>
                  <input type="number" className="rounded-xl border border-mis-border px-3 py-2" value={adjustValue} onChange={(event) => setAdjustValue(Number(event.target.value))} />
                </label>
                <Button disabled={busy} fullWidth={false} leftIcon={<Save className="h-4 w-4" />} onClick={() => void adjust()} variant="outline">{a.text('حفظ التسوية', 'Save adjustment')}</Button>
                {selected.status === 'DRAFT' ? <Button disabled={busy} fullWidth={false} onClick={() => void act(selected.id, 'submit')} variant="outline">{a.text('إرسال', 'Submit')}</Button> : null}
                <Button disabled={busy} fullWidth={false} leftIcon={<CheckCircle2 className="h-4 w-4" />} onClick={() => void act(selected.id, 'approve')}>{a.text('اعتماد', 'Approve')}</Button>
                <Button disabled={busy} fullWidth={false} onClick={() => void act(selected.id, 'cancel')} variant="danger">{a.text('إلغاء', 'Cancel')}</Button>
              </div>
            ) : null}
            {selected.status === 'APPROVED' ? <Button className="mt-4" disabled={busy} fullWidth={false} onClick={() => void act(selected.id, 'pay')} variant="outline">{a.text('تم الصرف', 'Mark paid')}</Button> : null}
            <Button className="mt-4" fullWidth={false} leftIcon={<X className="h-4 w-4" />} onClick={() => setSelected(undefined)} variant="ghost">{a.text('إغلاق', 'Close')}</Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
