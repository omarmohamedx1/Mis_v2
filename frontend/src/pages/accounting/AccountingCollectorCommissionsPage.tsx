import { Modal } from '../../components/common/Modal';
import { AccountingDeleteButton } from '../../features/accounting/AccountingDeleteButton';
import { AccountingExportButton, useAccountingMoneyConfirm } from '../../features/accounting/bookUi';
import { Banknote, Calculator, Percent, Save, X } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { AccountingMonthSelect, AccountingStatus, useAccountingText } from '../../features/accounting/accountingUi';
import { accountingService } from '../../features/accounting/services/accountingService';
import type { AccountingCollectorCommission, AccountingCollectorCommissionDetails } from '../../features/accounting/types/accounting';
import { getApiErrorMessage } from '../../services/apiClient';

const editable = ['DRAFT', 'PENDING_REVIEW'];

export function AccountingCollectorCommissionsPage() {
  const a = useAccountingText();
  const { confirmMoney, moneyDialog } = useAccountingMoneyConfirm();
  const navigate = useNavigate();
  const now = useMemo(() => new Date(), []);
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [rows, setRows] = useState<AccountingCollectorCommission[]>([]);
  const [details, setDetails] = useState<AccountingCollectorCommissionDetails>();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [adjustValue, setAdjustValue] = useState(0);

  const load = useCallback(async () => {
    const page = await accountingService.collectorCommissions({ year, month, search, status });
    setRows(page.items);
  }, [year, month, search, status]);

  useEffect(() => { load().catch((reason) => setError(getApiErrorMessage(reason, a.text('تعذر تحميل العمولات', 'Failed to load commissions')))); }, [load]);

  const calculate = async () => {
    setBusy(true); setError('');
    try { await accountingService.calculateCollectorCommissions(year, month); await load(); }
    catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر الحساب. تأكد من وجود قاعدة عمولة سارية للمحصلين.', 'Calculation failed. Ensure an active collector commission rule exists.'))); }
    finally { setBusy(false); }
  };

  const openDetails = async (id: string) => {
    setBusy(true); setError('');
    try {
      const data = await accountingService.collectorCommissionDetails(id);
      setDetails(data);
      setAdjustValue(data.summary.adjustments);
    } catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر فتح التفاصيل', 'Could not open details'))); }
    finally { setBusy(false); }
  };

  const adjust = async () => {
    if (!details) return;
    setBusy(true); setError('');
    try {
      const updated = await accountingService.adjustCollectorCommission(details.summary.id, adjustValue, details.summary.notes ?? undefined);
      setDetails({ ...details, summary: updated });
      await load();
    } catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر التعديل', 'Adjustment failed'))); }
    finally { setBusy(false); }
  };

  const act = async (id: string, action: string) => {
    setBusy(true); setError('');
    try {
      const updated = action === 'pay' && details?.summary.id === id && editable.includes(details.summary.status)
        ? await accountingService.recordCollectorCommission(id, adjustValue, details.summary.notes ?? undefined)
        : await accountingService.collectorCommissionAction(id, action);
      if (details?.summary.id === id) setDetails({ ...details, summary: updated });
      await load();
    } catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر تنفيذ الإجراء', 'Action failed'))); }
    finally { setBusy(false); }
  };

  const totalFinal = rows.reduce((sum, row) => sum + row.finalCommission, 0);

  return (
    <div className="space-y-5">{moneyDialog}
      <header className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-mis-navy">{a.text('عمولات المحصلين', 'Collector commissions')}</h1>
          <p className="text-sm text-slate-500">{a.text('من مدفوعات التحصيل المعتمدة فقط — بدون وعود أو PTP. القاعدة النشطة تُطبَّق عند الاحتساب.', 'From approved collection payments only — never PTP/promises. The active rule is applied on calculation.')}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <AccountingMonthSelect value={month} onChange={setMonth} />
          <input aria-label={a.text('السنة', 'Year')} type="number" className="h-11 w-24 rounded-xl border border-mis-border px-3 text-sm" value={year} onChange={(event) => setYear(Number(event.target.value))} />
          <Button disabled={busy} fullWidth={false} leftIcon={<Calculator className="h-4 w-4" />} onClick={() => void calculate()}>{a.text('احتساب الشهر', 'Calculate month')}</Button>
          <Button fullWidth={false} leftIcon={<Percent className="h-4 w-4" />} onClick={() => navigate('/finance/commission-rules')} variant="outline">{a.text('قواعد العمولة', 'Commission rules')}</Button>
        </div>
      <AccountingExportButton report="collector-commissions" filter={{ year, month, search: search || undefined, status: status || undefined }} /></header>

      {error ? <p className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p> : null}

      <div className="flex flex-wrap items-center gap-2">
        <input className="min-w-[200px] flex-1 rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('المحصل', 'Collector')} value={search} onChange={(event) => setSearch(event.target.value)} />
        <ProfessionalSelect className="min-h-11 min-w-[12rem] rounded-xl border border-mis-border bg-white px-3 text-sm" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">{a.text('كل الحالات', 'All statuses')}</option>
          {['READY', 'PAID', 'CANCELLED'].map((value) => <option key={value} value={value}>{a.status(value)}</option>)}
        </ProfessionalSelect>
        {rows.length ? <p className="ms-auto text-sm font-bold text-mis-navy" data-bidi="ltr">{a.text('إجمالي النهائي', 'Total final')}: {a.money(totalFinal)}</p> : null}
      </div>

      <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[48rem] text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-3 py-3 text-start">{a.text('المحصل', 'Collector')}</th>
                <th className="px-3 py-3 text-end">{a.text('المحصّل', 'Collected')}</th>
                <th className="px-3 py-3 text-end">{a.text('المؤهّل', 'Eligible')}</th>
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
                  <td className="px-3 py-2"><div className="font-semibold">{row.collectorName}</div><div className="font-mono text-xs text-slate-500">{row.employeeNumber ?? '—'}</div></td>
                  <td className="px-3 py-2 text-end" data-bidi="ltr">{a.money(row.collectedAmount)}</td>
                  <td className="px-3 py-2 text-end" data-bidi="ltr">{a.money(row.eligibleAmount)}</td>
                  <td className="px-3 py-2">{row.ruleCode} ({a.number(row.rateApplied)}%)</td>
                  <td className="px-3 py-2 text-end" data-bidi="ltr">{a.money(row.commissionAmount)}</td>
                  <td className="px-3 py-2 text-end font-bold" data-bidi="ltr">{a.money(row.finalCommission)}</td>
                  <td className="px-3 py-2"><AccountingStatus value={row.status} /></td>
                  <td className="px-3 py-2 text-end"><Button fullWidth={false} onClick={() => void openDetails(row.id)} size="sm" variant="ghost">{a.text('تفاصيل', 'Details')}</Button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!rows.length ? (
          <EmptyState
            compact
            icon={<Banknote className="h-5 w-5" />}
            title={a.text('لا توجد عمولات لهذا الشهر', 'No commissions for this month')}
            description={a.text('احسب الشهر بعد التأكد من وجود قاعدة عمولة سارية ومدفوعات معتمدة.', 'Calculate the month after an active commission rule and approved payments exist.')}
            action={<Button disabled={busy} fullWidth={false} leftIcon={<Calculator className="h-4 w-4" />} onClick={() => void calculate()}>{a.text('احتساب الشهر', 'Calculate month')}</Button>}
          />
        ) : null}
      </div>

      {details ? (
        <Modal open onClose={() => setDetails(undefined)} title={details.summary.collectorName} size="lg" hideHeader hideCloseButton bodyClassName="p-0">
          <div className="min-w-0 p-6" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 className="text-xl font-bold text-mis-navy">{details.summary.collectorName}</h2>
                <p className="text-sm text-slate-500">{a.monthLabel(details.summary.periodYear, details.summary.periodMonth)}</p>
              </div>
              <AccountingStatus value={details.summary.status} />
            </div>
            <dl className="mt-5 grid gap-2 text-sm sm:grid-cols-2">
              <div><dt className="text-slate-500">{a.text('التحصيلات المعتمدة', 'Approved collections')}</dt><dd className="font-bold" data-bidi="ltr">{a.money(details.summary.collectedAmount)}</dd></div>
              <div><dt className="text-slate-500">{a.text('المعاملات المؤهلة', 'Eligible transactions')}</dt><dd className="font-bold">{a.number(details.contributions.length)}</dd></div>
              <div><dt className="text-slate-500">{a.text('قاعدة العمولة', 'Commission rule')}</dt><dd className="font-bold">{details.summary.ruleCode} · {a.number(details.summary.rateApplied)}%</dd></div>
              <div><dt className="text-slate-500">{a.text('العمولة المحسوبة', 'Calculated commission')}</dt><dd className="font-bold" data-bidi="ltr">{a.money(details.summary.commissionAmount)}</dd></div>
              <div><dt className="text-slate-500">{a.text('تسويات يدوية', 'Manual adjustments')}</dt><dd className="font-bold" data-bidi="ltr">{a.money(details.summary.adjustments)}</dd></div>
              <div><dt className="text-slate-500">{a.text('العمولة النهائية', 'Final commission')}</dt><dd className="font-bold" data-bidi="ltr">{a.money(editable.includes(details.summary.status) ? details.summary.commissionAmount + adjustValue : details.summary.finalCommission)}</dd></div>
            </dl>

            {editable.includes(details.summary.status) ? (
              <div className="mt-4 flex flex-wrap items-end gap-2">
                <label className="text-sm"><span className="mb-1 block text-slate-500">{a.text('تسوية', 'Adjustment')}</span>
                  <input type="number" step="0.01" className="rounded-xl border border-mis-border px-3 py-2" value={adjustValue} onChange={(event) => setAdjustValue(Number(event.target.value))} />
                </label>
                <Button disabled={busy} fullWidth={false} leftIcon={<Save className="h-4 w-4" />} onClick={() => confirmMoney(details.summary.commissionAmount + adjustValue, details.summary.collectorName + '\n' + a.money(adjustValue), adjust)} variant="outline">{a.text('حفظ التسوية', 'Save adjustment')}</Button>
              </div>
            ) : null}
            {[...editable, 'APPROVED'].includes(details.summary.status) ? <Button disabled={busy} fullWidth={false} onClick={() => confirmMoney(editable.includes(details.summary.status) ? details.summary.commissionAmount + adjustValue : details.summary.finalCommission, details.summary.collectorName + ' / ' + a.monthLabel(year, month) + '\n' + details.summary.ruleCode + '\n' + a.text('التسوية: ', 'Adjustment: ') + a.money(editable.includes(details.summary.status) ? adjustValue : details.summary.adjustments), () => act(details.summary.id, 'pay'))} variant="outline">{a.text('تأكيد وصرف العمولة', 'Confirm and pay commission')}</Button> : null}

            <h3 className="mb-2 mt-6 text-sm font-bold text-mis-navy">{a.text('المعاملات المساهمة', 'Contributing transactions')}</h3>
            <div className="overflow-x-auto rounded-xl border border-mis-border">
              <table className="w-full min-w-[700px] text-sm">
                <thead className="bg-slate-50 text-xs text-slate-500"><tr><th className="px-3 py-2 text-start">{a.text('الحالة', 'Case')}</th><th className="px-3 py-2 text-start">{a.text('التاريخ', 'Date')}</th><th className="px-3 py-2 text-start">{a.text('المرجع', 'Reference')}</th><th className="px-3 py-2 text-end">{a.text('المبلغ', 'Amount')}</th></tr></thead>
                <tbody className="divide-y divide-mis-border">
                  {details.contributions.map((contribution) => (
                    <tr key={contribution.paymentId}>
                      <td className="px-3 py-2 font-mono text-xs">{contribution.caseNumber ?? contribution.caseId}</td>
                      <td className="px-3 py-2">{a.date(contribution.paymentDate)}</td>
                      <td className="px-3 py-2 font-mono text-xs">{contribution.referenceNumber}</td>
                      <td className="px-3 py-2 text-end" data-bidi="ltr">{a.money(contribution.amount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {!details.contributions.length ? <p className="mt-3 text-sm text-slate-500">{a.text('لا توجد معاملات مساهمة مسجّلة.', 'No contributing transactions were recorded.')}</p> : null}
            <AccountingDeleteButton kind="COLLECTOR" id={details.summary.id} amount={details.summary.finalCommission} description={`${details.summary.collectorName} / ${a.monthLabel(details.summary.periodYear, details.summary.periodMonth)}`} onDeleted={async () => { setDetails(undefined); await load(); } } />
            <Button className="mt-4" fullWidth={false} leftIcon={<X className="h-4 w-4" />} onClick={() => setDetails(undefined)} variant="ghost">{a.text('إغلاق', 'Close')}</Button>
          </div>
        </Modal>
      ) : null}
    </div>
  );
}
