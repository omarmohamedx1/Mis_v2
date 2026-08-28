import { AlertTriangle, Eye, Settings2, WalletCards, X } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { useAuth } from '../../context/AuthContext';
import { FinanceKpi, useFinanceText } from '../../features/finance/financeUi';
import { financeService } from '../../features/finance/services/financeService';
import type { CustodyDetails, CustodySummary } from '../../features/finance/types/finance';
import { getApiErrorMessage } from '../../services/apiClient';

export function FinanceCustodyPage() {
  const f = useFinanceText();
  const { user } = useAuth();
  const [rows, setRows] = useState<CustodySummary[]>();
  const [selected, setSelected] = useState<CustodyDetails>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const loadErrorText = f.ar ? 'تعذر تحميل عهد المحصلين.' : 'Could not load collector custody.';
  const canConfigure = Boolean(user?.roles.includes('Admin') || user?.permissions.includes('*') || user?.permissions.includes('finance.configuration.manage'));

  const load = useCallback(() => {
    setError('');
    financeService.custodies().then(setRows).catch((reason) => setError(getApiErrorMessage(reason, loadErrorText)));
  }, [loadErrorText]);
  useEffect(load, [load]);

  const openDetails = async (collectorId: string) => {
    setBusy(true); setError('');
    try { setSelected(await financeService.custody(collectorId)); }
    catch (reason) { setError(getApiErrorMessage(reason, f.text('تعذر تحميل حركة العهدة.', 'Could not load custody movements.'))); }
    finally { setBusy(false); }
  };

  const updateLimits = async (collectorId: string, softLimit: number, hardLimit: number, reason: string) => {
    const updated = await financeService.updateCustodyLimits(collectorId, softLimit, hardLimit, reason);
    setSelected(updated);
    setRows(await financeService.custodies());
  };

  if (error && !rows) return <ErrorState title={error} onRetry={load} />;
  if (!rows) return <div className="grid min-h-[420px] place-items-center"><LoadingSpinner /></div>;

  const total = rows.reduce((sum, row) => sum + row.balance, 0);
  const overSoft = rows.filter((row) => row.softLimitExceeded).length;
  const overHard = rows.filter((row) => row.hardLimitExceeded).length;

  return <div>
    <header><p className="text-xs font-bold uppercase tracking-[.18em] text-mis-primary">COLLECTOR CUSTODY</p><h1 className="mt-2 text-3xl font-bold text-mis-navy">{f.text('عهد المحصلين', 'Collector Custody')}</h1><p className="mt-2 max-w-3xl text-sm leading-6 text-slate-500">{f.text('رصيد فرعي لكل محصل مع حدود رقابية وعمر أقدم مبلغ لم يتم توريده وربط كل حركة بالقيد والإيصال.', 'A controlled subledger per collector with limits, oldest unhanded cash, and traceability to every journal and receipt.')}</p></header>
    <section className="mt-6 grid gap-4 sm:grid-cols-3"><FinanceKpi label={f.text('إجمالي العهد القائمة', 'Total outstanding custody')} value={f.money(total)} hint={f.text('أموال عملاء لدى المحصلين', 'Client money held by collectors')} tone="blue" /><FinanceKpi label={f.text('تجاوز الحد التنبيهي', 'Soft-limit breaches')} value={f.number(overSoft)} hint={f.text('تحتاج متابعة المشرف', 'Supervisor follow-up required')} tone={overSoft ? 'amber' : 'green'} /><FinanceKpi label={f.text('تجاوز الحد الأقصى', 'Hard-limit breaches')} value={f.number(overHard)} hint={f.text('يمنع تحصيلًا نقديًا جديدًا', 'Blocks new cash collections')} tone={overHard ? 'red' : 'green'} /></section>
    {error ? <p role="alert" className="mt-4 rounded-xl bg-rose-50 p-3 text-sm text-rose-700">{error}</p> : null}
    <div className="mt-5 overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm"><div className="overflow-x-auto"><table className="w-full min-w-[950px] text-sm"><thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500"><tr><th className="px-5 py-4 text-start">{f.text('المحصل', 'Collector')}</th><th className="px-5 py-4 text-end">{f.text('الرصيد القائم', 'Outstanding')}</th><th className="px-5 py-4 text-end">{f.text('حد التنبيه', 'Soft limit')}</th><th className="px-5 py-4 text-end">{f.text('الحد الأقصى', 'Hard limit')}</th><th className="px-5 py-4 text-start">{f.text('أقدم عهدة', 'Oldest outstanding')}</th><th className="px-5 py-4 text-start">{f.text('الموقف الرقابي', 'Control status')}</th><th className="px-5 py-4"><span className="sr-only">{f.text('الإجراءات', 'Actions')}</span></th></tr></thead><tbody className="divide-y divide-mis-border">{rows.map((row) => <tr key={row.accountId} className="hover:bg-slate-50"><td className="px-5 py-4"><p className="font-bold text-mis-navy">{row.collectorName}</p><p className="mt-1 text-xs text-slate-400">{row.currencyCode}</p></td><td className="px-5 py-4 text-end font-bold text-mis-navy" data-bidi="ltr">{f.money(row.balance, row.currencyCode)}</td><td className="px-5 py-4 text-end text-slate-600" data-bidi="ltr">{f.money(row.softLimit, row.currencyCode)}</td><td className="px-5 py-4 text-end text-slate-600" data-bidi="ltr">{f.money(row.hardLimit, row.currencyCode)}</td><td className="px-5 py-4 text-slate-600">{row.oldestOutstandingDate ? f.date(row.oldestOutstandingDate) : '—'}</td><td className="px-5 py-4">{row.hardLimitExceeded ? <ControlBadge tone="danger" text={f.text('تجاوز الحد الأقصى', 'Hard limit exceeded')} /> : row.softLimitExceeded ? <ControlBadge tone="warning" text={f.text('تجاوز حد التنبيه', 'Soft limit exceeded')} /> : <ControlBadge tone="success" text={f.text('ضمن الحدود', 'Within limits')} />}</td><td className="px-5 py-4 text-end"><button type="button" onClick={() => void openDetails(row.collectorId)} aria-label={f.text(`عرض عهدة ${row.collectorName}`, `View ${row.collectorName} custody`)} className="rounded-lg p-2 text-mis-primary hover:bg-mis-pale"><Eye className="h-4 w-4" /></button></td></tr>)}</tbody></table></div>{rows.length === 0 ? <div className="grid place-items-center p-12 text-center"><WalletCards className="h-10 w-10 text-slate-300" /><p className="mt-3 text-sm text-slate-500">{f.text('لا توجد عهد نقدية مسجلة حتى الآن.', 'No cash custody accounts have been created yet.')}</p></div> : null}</div>
    {busy ? <div className="fixed inset-0 z-50 grid place-items-center bg-mis-ink/45"><LoadingSpinner /></div> : null}
    {selected ? <CustodyDialog value={selected} close={() => setSelected(undefined)} canConfigure={canConfigure} saveLimits={updateLimits} /> : null}
  </div>;
}

function CustodyDialog({ value, close, canConfigure, saveLimits }: { value: CustodyDetails; close: () => void; canConfigure: boolean; saveLimits: (collectorId: string, softLimit: number, hardLimit: number, reason: string) => Promise<void> }) {
  const f = useFinanceText();
  const [editing, setEditing] = useState(false);
  const [softLimit, setSoftLimit] = useState(String(value.summary.softLimit));
  const [hardLimit, setHardLimit] = useState(String(value.summary.hardLimit));
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');

  const submitLimits = async (event: React.FormEvent) => {
    event.preventDefault();
    const soft = Number(softLimit); const hard = Number(hardLimit);
    if (!Number.isFinite(soft) || !Number.isFinite(hard) || soft < 0 || hard <= 0 || hard < soft || !reason.trim()) {
      setFormError(f.text('أدخل حدودًا صحيحة وسبب التعديل؛ الحد الأقصى يجب ألا يقل عن حد التنبيه.', 'Enter valid limits and a reason; the hard limit cannot be lower than the soft limit.'));
      return;
    }
    setSaving(true); setFormError('');
    try { await saveLimits(value.summary.collectorId, soft, hard, reason.trim()); setEditing(false); setReason(''); }
    catch (error) { setFormError(getApiErrorMessage(error, f.text('تعذر حفظ حدود العهدة.', 'Could not save custody limits.'))); }
    finally { setSaving(false); }
  };

  return <div className="fixed inset-0 z-50 grid place-items-center bg-mis-ink/55 p-4" role="dialog" aria-modal="true" aria-labelledby="custody-dialog-title"><div className="max-h-[94vh] w-full max-w-6xl overflow-y-auto rounded-2xl bg-white shadow-2xl"><header className="sticky top-0 z-10 flex items-start border-b border-mis-border bg-white px-6 py-5"><div><p className="text-xs font-bold uppercase tracking-[.18em] text-mis-primary">CUSTODY SUBLEDGER</p><h2 id="custody-dialog-title" className="mt-1 text-2xl font-bold text-mis-navy">{value.summary.collectorName}</h2><p className="mt-1 text-sm text-slate-500">{f.text('كشف حركة العهدة النقدية', 'Cash custody statement')}</p></div><button type="button" onClick={close} aria-label={f.text('إغلاق', 'Close')} className="ms-auto rounded-lg p-2 text-slate-500 hover:bg-slate-100"><X /></button></header>
    <div className="grid gap-4 p-6 sm:grid-cols-3"><FinanceKpi label={f.text('الرصيد الحالي', 'Current balance')} value={f.money(value.summary.balance, value.summary.currencyCode)} tone={value.summary.hardLimitExceeded ? 'red' : value.summary.softLimitExceeded ? 'amber' : 'blue'} /><FinanceKpi label={f.text('حد التنبيه', 'Soft limit')} value={f.money(value.summary.softLimit, value.summary.currencyCode)} /><FinanceKpi label={f.text('الحد الأقصى', 'Hard limit')} value={f.money(value.summary.hardLimit, value.summary.currencyCode)} /></div>
    {canConfigure ? <div className="mx-6 mb-5"><button type="button" onClick={() => setEditing((current) => !current)} className="inline-flex items-center gap-2 rounded-xl border border-mis-border px-4 py-2 text-sm font-bold text-mis-primary hover:bg-mis-pale"><Settings2 className="h-4 w-4" />{f.text('تعديل حدود العهدة', 'Edit custody limits')}</button>{editing ? <form onSubmit={(event) => void submitLimits(event)} className="mt-3 grid gap-3 rounded-xl border border-mis-border bg-slate-50 p-4 md:grid-cols-3"><label className="text-sm font-semibold text-slate-700">{f.text('حد التنبيه', 'Soft limit')}<input type="number" min="0" step="0.01" value={softLimit} onChange={(event) => setSoftLimit(event.target.value)} className="mt-1 w-full rounded-lg border border-mis-border bg-white px-3 py-2" required /></label><label className="text-sm font-semibold text-slate-700">{f.text('الحد الأقصى', 'Hard limit')}<input type="number" min="0.01" step="0.01" value={hardLimit} onChange={(event) => setHardLimit(event.target.value)} className="mt-1 w-full rounded-lg border border-mis-border bg-white px-3 py-2" required /></label><label className="text-sm font-semibold text-slate-700">{f.text('سبب التعديل', 'Change reason')}<input value={reason} onChange={(event) => setReason(event.target.value)} maxLength={1000} className="mt-1 w-full rounded-lg border border-mis-border bg-white px-3 py-2" required /></label>{formError ? <p role="alert" className="text-sm text-rose-700 md:col-span-3">{formError}</p> : null}<div className="flex justify-end md:col-span-3"><button type="submit" disabled={saving} className="rounded-lg bg-mis-primary px-4 py-2 text-sm font-bold text-white disabled:opacity-50">{saving ? f.text('جارٍ الحفظ…', 'Saving…') : f.text('حفظ الحدود', 'Save limits')}</button></div></form> : null}</div> : null}
    {value.summary.softLimitExceeded ? <div className="mx-6 mb-5 flex items-start gap-3 rounded-xl bg-amber-50 p-4 text-sm text-amber-900"><AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" /><p>{value.summary.hardLimitExceeded ? f.text('تجاوزت العهدة الحد الأقصى؛ التحصيل النقدي الجديد يجب أن يكون موقوفًا حتى التوريد أو اعتماد الاستثناء.', 'Custody exceeds the hard limit; new cash collection must remain blocked until handover or an approved override.') : f.text('العهدة تجاوزت حد التنبيه وتحتاج متابعة التوريد.', 'Custody exceeds the soft limit and requires handover follow-up.')}</p></div> : null}
    <div className="mx-6 mb-6 overflow-hidden rounded-xl border border-mis-border"><div className="overflow-x-auto"><table className="w-full min-w-[1000px] text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr><th className="px-4 py-3 text-start">{f.text('التاريخ', 'Date')}</th><th className="px-4 py-3 text-start">{f.text('الحركة', 'Movement')}</th><th className="px-4 py-3 text-start">{f.text('مرجع الإيصال', 'Receipt')}</th><th className="px-4 py-3 text-start">{f.text('رقم القيد', 'Journal')}</th><th className="px-4 py-3 text-end">{f.text('مدين', 'Debit')}</th><th className="px-4 py-3 text-end">{f.text('دائن', 'Credit')}</th><th className="px-4 py-3 text-end">{f.text('الرصيد الجاري', 'Running balance')}</th></tr></thead><tbody className="divide-y divide-mis-border">{value.transactions.map((row) => <tr key={row.id}><td className="px-4 py-3 text-slate-600">{f.date(row.transactionDate)}</td><td className="px-4 py-3 font-semibold text-mis-navy">{f.custodyType(row.transactionType)}</td><td className="px-4 py-3 font-mono text-xs text-slate-500">{row.paymentReference ?? '—'}</td><td className="px-4 py-3"><Link to={`/finance/journals/${row.journalEntryId}`} className="font-mono text-xs font-bold text-mis-primary hover:underline">{row.journalNumber}</Link></td><td className="px-4 py-3 text-end" data-bidi="ltr">{row.debit ? f.money(row.debit, value.summary.currencyCode) : '—'}</td><td className="px-4 py-3 text-end" data-bidi="ltr">{row.credit ? f.money(row.credit, value.summary.currencyCode) : '—'}</td><td className="px-4 py-3 text-end font-bold text-mis-navy" data-bidi="ltr">{f.money(row.runningBalance, value.summary.currencyCode)}</td></tr>)}</tbody></table></div></div>
    </div></div>;
}

function ControlBadge({ tone, text }: { tone: 'success' | 'warning' | 'danger'; text: string }) { const colors = { success: 'bg-emerald-50 text-emerald-700', warning: 'bg-amber-50 text-amber-800', danger: 'bg-rose-50 text-rose-700' }; return <span className={`inline-flex rounded-full px-3 py-1 text-xs font-bold ${colors[tone]}`}>{text}</span>; }
