import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { DateControl } from '../../components/forms/DateControl';
import { ArrowRightLeft, Eye, RotateCcw, X } from 'lucide-react';
import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Pagination } from '../../components/common/Pagination';
import { FinanceStatus, useFinanceText } from '../../features/finance/financeUi';
import { financeService } from '../../features/finance/services/financeService';
import type { CollectionFinance, CollectionFinanceListItem, FinancePagedResult } from '../../features/finance/types/finance';
import { getApiErrorMessage } from '../../services/apiClient';

const today = () => new Date().toISOString().slice(0, 10);

export function FinanceCollectionsPage() {
  const f = useFinanceText();
  const [params, setParams] = useSearchParams();
  const page = Number(params.get('page') ?? 1);
  const status = params.get('status') ?? '';
  const channel = params.get('channel') ?? '';
  const [data, setData] = useState<FinancePagedResult<CollectionFinanceListItem>>();
  const [selected, setSelected] = useState<CollectionFinance>();
  const [detailsBusy, setDetailsBusy] = useState(false);
  const [error, setError] = useState('');
  const loadErrorText = f.ar ? 'تعذر تحميل التحصيلات المالية.' : 'Could not load financial collections.';

  const load = useCallback(() => {
    setError('');
    financeService.collections(page, status, channel).then(setData).catch((reason) =>
      setError(getApiErrorMessage(reason, loadErrorText)));
  }, [page, status, channel, loadErrorText]);

  useEffect(load, [load]);

  const openDetails = async (paymentId: string) => {
    setDetailsBusy(true);
    setError('');
    try { setSelected(await financeService.collection(paymentId)); }
    catch (reason) { setError(getApiErrorMessage(reason, f.text('تعذر تحميل تفاصيل التحصيل.', 'Could not load collection details.'))); }
    finally { setDetailsBusy(false); }
  };

  const updateFilters = (nextStatus: string, nextChannel: string) => {
    const next: Record<string, string> = {};
    if (nextStatus) next.status = nextStatus;
    if (nextChannel) next.channel = nextChannel;
    setParams(next);
  };

  if (error && !data) return <ErrorState title={error} onRetry={load} />;
  if (!data) return <div className="grid min-h-[420px] place-items-center"><LoadingSpinner /></div>;

  return <div>
    <header>
      <p className="text-xs font-bold uppercase tracking-[.18em] text-mis-primary">COLLECTION FINANCE</p>
      <h1 className="mt-2 text-3xl font-bold text-mis-navy">{f.text('التحصيلات المالية', 'Financial Collections')}</h1>
      <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-500">{f.text(
        'متابعة كل إيصال مع توزيعاته، موقع الأموال، قيد الإثبات، قيد التسوية، وأي عكس مرتبط به.',
        'Trace every receipt through allocations, money location, recognition journal, clearing journal, and linked reversals.')}</p>
    </header>

    <section aria-label={f.text('فلاتر التحصيلات', 'Collection filters')} className="mt-6 grid gap-3 rounded-2xl border border-mis-border bg-white p-4 sm:grid-cols-2">
      <label><span className="mb-2 block text-xs font-bold text-slate-500">{f.text('الحالة المالية', 'Financial status')}</span>
        <ProfessionalSelect className="field" value={status} onChange={(event) => updateFilters(event.target.value, channel)}>
          <option value="">{f.text('كل الحالات', 'All statuses')}</option>
          {['POSTED', 'CLEARED', 'REVERSED'].map((value) => <option key={value} value={value}>{f.status(value)}</option>)}
        </ProfessionalSelect>
      </label>
      <label><span className="mb-2 block text-xs font-bold text-slate-500">{f.text('قناة التحصيل', 'Collection channel')}</span>
        <ProfessionalSelect className="field" value={channel} onChange={(event) => updateFilters(status, event.target.value)}>
          <option value="">{f.text('كل القنوات', 'All channels')}</option>
          {['CASH_COLLECTOR', 'CASH_BRANCH', 'BANK_TRANSFER', 'CHEQUE', 'GATEWAY'].map((value) => <option key={value} value={value}>{f.channel(value)}</option>)}
        </ProfessionalSelect>
      </label>
    </section>

    {error ? <p role="alert" className="mt-4 rounded-xl bg-rose-50 p-3 text-sm text-rose-700">{error}</p> : null}
    <div className="mt-5 overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
      <div className="overflow-x-auto">
        <table className="w-full min-w-[1080px] text-sm">
          <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500"><tr>
            <th className="px-5 py-4 text-start">{f.text('مرجع الإيصال', 'Receipt reference')}</th>
            <th className="px-5 py-4 text-start">{f.text('التاريخ', 'Date')}</th>
            <th className="px-5 py-4 text-start">{f.text('العميل', 'Client')}</th>
            <th className="px-5 py-4 text-start">{f.text('القناة', 'Channel')}</th>
            <th className="px-5 py-4 text-start">{f.text('المحصل', 'Collector')}</th>
            <th className="px-5 py-4 text-end">{f.text('الإجمالي', 'Gross amount')}</th>
            <th className="px-5 py-4 text-start">{f.text('الحالة', 'Status')}</th>
            <th className="px-5 py-4 text-start">{f.text('قيد الإثبات', 'Recognition journal')}</th>
            <th className="px-5 py-4"><span className="sr-only">{f.text('الإجراءات', 'Actions')}</span></th>
          </tr></thead>
          <tbody className="divide-y divide-mis-border">{data.items.map((row) => <tr key={row.receiptId} className="hover:bg-slate-50">
            <td className="px-5 py-4 font-mono text-xs font-bold text-mis-primary">{row.referenceNumber}</td>
            <td className="px-5 py-4 text-slate-600">{f.date(row.paymentDate)}</td>
            <td className="px-5 py-4"><p className="font-semibold text-mis-navy">{f.ar ? row.clientNameArabic : row.clientNameEnglish}</p><p className="mt-1 text-xs text-slate-400">{row.clientCode}</p></td>
            <td className="px-5 py-4 text-slate-600">{f.channel(row.channel)}</td>
            <td className="px-5 py-4 text-slate-600">{row.collectorName}</td>
            <td className="px-5 py-4 text-end font-bold text-mis-navy" data-bidi="ltr">{f.money(row.grossAmount, row.currencyCode)}</td>
            <td className="px-5 py-4"><FinanceStatus value={row.status} /></td>
            <td className="px-5 py-4"><span className="font-mono text-xs text-slate-500">{row.journalNumber}</span></td>
            <td className="px-5 py-4 text-end"><button type="button" onClick={() => void openDetails(row.paymentId)} aria-label={f.text(`عرض الإيصال ${row.referenceNumber}`, `View receipt ${row.referenceNumber}`)} className="rounded-lg p-2 text-mis-primary hover:bg-mis-pale"><Eye className="h-4 w-4" /></button></td>
          </tr>)}</tbody>
        </table>
      </div>
      {data.items.length === 0 ? <p className="p-10 text-center text-sm text-slate-500">{f.text('لا توجد تحصيلات مالية مطابقة للفلاتر.', 'No financial collections match the selected filters.')}</p> : null}
      <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} totalPages={data.totalPages} onPageChange={(next) => setParams({ ...(status ? { status } : {}), ...(channel ? { channel } : {}), page: String(next) })} />
    </div>
    {detailsBusy ? <div className="fixed inset-0 z-50 grid place-items-center bg-mis-ink/45"><LoadingSpinner /></div> : null}
    {selected ? <CollectionDetailsDialog value={selected} close={() => setSelected(undefined)} changed={(value) => { setSelected(value); load(); }} /> : null}
  </div>;
}

function CollectionDetailsDialog({ value, close, changed }: { value: CollectionFinance; close: () => void; changed: (value: CollectionFinance) => void }) {
  const f = useFinanceText();
  const [mode, setMode] = useState<'clear' | 'reverse'>();
  const [date, setDate] = useState(today());
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true); setError('');
    try {
      const updated = mode === 'clear'
        ? await financeService.clearCollection(value.paymentId, date, reason)
        : await financeService.reverseCollection(value.paymentId, reason);
      changed(updated); setMode(undefined); setReason('');
    } catch (failure) { setError(getApiErrorMessage(failure, f.text('تعذر تنفيذ العملية.', 'Could not complete the action.'))); }
    finally { setBusy(false); }
  };

  return <div className="fixed inset-0 z-50 grid place-items-center bg-mis-ink/55 p-4" role="dialog" aria-modal="true" aria-labelledby="collection-dialog-title">
    <div className="max-h-[94vh] w-full max-w-5xl overflow-y-auto rounded-2xl bg-white shadow-2xl">
      <header className="sticky top-0 z-10 flex items-start border-b border-mis-border bg-white px-6 py-5">
        <div><p className="text-xs font-bold uppercase tracking-[.18em] text-mis-primary">FINANCIAL RECEIPT</p><h2 id="collection-dialog-title" className="mt-1 text-2xl font-bold text-mis-navy">{value.referenceNumber}</h2><p className="mt-1 text-sm text-slate-500">{f.ar ? value.clientNameArabic : value.clientNameEnglish}</p></div>
        <button type="button" onClick={close} aria-label={f.text('إغلاق', 'Close')} className="ms-auto rounded-lg p-2 text-slate-500 hover:bg-slate-100"><X /></button>
      </header>
      <div className="grid gap-4 p-6 sm:grid-cols-2 lg:grid-cols-4">
        <Info label={f.text('المبلغ الإجمالي', 'Gross amount')} value={f.money(value.grossAmount, value.currencyCode)} />
        <Info label={f.text('قناة التحصيل', 'Collection channel')} value={f.channel(value.channel)} />
        <Info label={f.text('المحصل', 'Collector')} value={value.collectorName ?? '—'} />
        <div className="rounded-xl bg-slate-50 p-4"><p className="text-xs font-bold text-slate-500">{f.text('الحالة المالية', 'Financial status')}</p><div className="mt-2"><FinanceStatus value={value.status} /></div></div>
      </div>
      <section className="mx-6 rounded-2xl border border-mis-border p-5" aria-labelledby="accounting-chain-title">
        <h3 id="accounting-chain-title" className="font-bold text-mis-navy">{f.text('سلسلة الأثر المحاسبي', 'Accounting trace')}</h3>
        <div className="mt-4 grid gap-3 sm:grid-cols-3">
          <JournalLink label={f.text('قيد إثبات التحصيل', 'Recognition journal')} id={value.journalEntryId} number={value.journalNumber} />
          <JournalLink label={f.text('قيد التسوية', 'Clearing journal')} id={value.clearingJournalEntryId} number={value.clearingJournalNumber} />
          <JournalLink label={f.text('قيد العكس', 'Reversal journal')} id={value.reversalJournalEntryId} number={value.reversalJournalNumber} />
        </div>
      </section>
      <section className="p-6" aria-labelledby="allocations-title"><h3 id="allocations-title" className="font-bold text-mis-navy">{f.text('توزيعات الإيصال', 'Receipt allocations')}</h3>
        <div className="mt-3 overflow-hidden rounded-xl border border-mis-border"><table className="w-full min-w-[650px] text-sm"><thead className="bg-slate-50 text-xs text-slate-500"><tr><th className="px-4 py-3 text-start">#</th><th className="px-4 py-3 text-start">{f.text('رقم الحالة', 'Case no.')}</th><th className="px-4 py-3 text-end">{f.text('المبلغ الموزع', 'Allocated')}</th><th className="px-4 py-3 text-end">{f.text('الرصيد قبل التحصيل', 'Outstanding before')}</th><th className="px-4 py-3 text-end">{f.text('المتأخر قبل التحصيل', 'Overdue before')}</th></tr></thead><tbody className="divide-y divide-mis-border">{value.allocations.map((line) => <tr key={line.id}><td className="px-4 py-3">{line.lineNumber}</td><td className="px-4 py-3 font-mono text-xs font-bold text-mis-primary">{line.caseNumber}</td><td className="px-4 py-3 text-end" data-bidi="ltr">{f.money(line.amount, value.currencyCode)}</td><td className="px-4 py-3 text-end" data-bidi="ltr">{f.money(line.outstandingBefore, value.currencyCode)}</td><td className="px-4 py-3 text-end" data-bidi="ltr">{f.money(line.overdueBefore, value.currencyCode)}</td></tr>)}</tbody></table></div>
      </section>
      {mode ? <form onSubmit={submit} className="mx-6 mb-6 rounded-2xl border border-amber-200 bg-amber-50 p-5"><h3 className="font-bold text-amber-900">{mode === 'clear' ? f.text('تأكيد تسوية التحصيل', 'Confirm collection clearing') : f.text('تأكيد عكس التحصيل', 'Confirm collection reversal')}</h3><p className="mt-1 text-xs leading-5 text-amber-800">{mode === 'clear' ? f.text('سيتم نقل الأموال إلى البنك وإعادة تصنيف التزام العميل في قيد واحد متوازن.', 'This posts one balanced journal moving cash to bank and reclassifying the client liability.') : f.text('سيتم عكس جميع القيود المرتبطة وإعادة رصيد الحالة من الـsnapshot المحفوظ.', 'All linked journals will be reversed and the case balance restored from its saved snapshot.')}</p><div className="mt-4 grid gap-4 sm:grid-cols-2">{mode === 'clear' ? <label><span className="mb-2 block text-sm font-semibold">{f.text('تاريخ التسوية', 'Clearing date')}</span><DateControl required  className="field" value={date} onChange={(event) => setDate(event.target.value)} /></label> : null}<label className={mode === 'reverse' ? 'sm:col-span-2' : ''}><span className="mb-2 block text-sm font-semibold">{mode === 'clear' ? f.text('مرجع البنك / التوريد', 'Bank / handover reference') : f.text('سبب العكس', 'Reversal reason')}</span><input required maxLength={200} className="field" value={reason} onChange={(event) => setReason(event.target.value)} /></label></div>{error ? <p role="alert" className="mt-3 text-sm text-rose-700">{error}</p> : null}<div className="mt-4 flex justify-end gap-2"><button type="button" onClick={() => setMode(undefined)} className="rounded-xl border border-mis-border bg-white px-4 py-2 text-sm font-bold">{f.text('إلغاء', 'Cancel')}</button><button disabled={busy} className={`rounded-xl px-5 py-2 text-sm font-bold text-white disabled:opacity-50 ${mode === 'reverse' ? 'bg-rose-700' : 'bg-mis-primary'}`}>{busy ? f.text('جارٍ التنفيذ…', 'Processing…') : f.text('تأكيد التنفيذ', 'Confirm action')}</button></div></form> : null}
      <footer className="sticky bottom-0 flex flex-wrap justify-end gap-3 border-t border-mis-border bg-white px-6 py-4">{value.status === 'POSTED' ? <button type="button" onClick={() => setMode('clear')} className="inline-flex items-center gap-2 rounded-xl bg-mis-primary px-5 py-2.5 text-sm font-bold text-white"><ArrowRightLeft className="h-4 w-4" />{f.text('تسوية / توريد', 'Clear / hand over')}</button> : null}{value.status !== 'REVERSED' ? <button type="button" onClick={() => setMode('reverse')} className="inline-flex items-center gap-2 rounded-xl border border-rose-200 px-5 py-2.5 text-sm font-bold text-rose-700"><RotateCcw className="h-4 w-4" />{f.text('عكس التحصيل', 'Reverse collection')}</button> : null}</footer>
    </div>
  </div>;
}

function Info({ label, value }: { label: string; value: string }) { return <div className="rounded-xl bg-slate-50 p-4"><p className="text-xs font-bold text-slate-500">{label}</p><p className="mt-2 font-semibold text-mis-navy">{value}</p></div>; }
function JournalLink({ label, id, number }: { label: string; id?: string; number?: string }) { return <div className="rounded-xl bg-slate-50 p-4"><p className="text-xs font-bold text-slate-500">{label}</p>{id && number ? <Link className="mt-2 block font-mono text-xs font-bold text-mis-primary hover:underline" to={`/finance/journals/${id}`}>{number}</Link> : <p className="mt-2 text-sm text-slate-400">—</p>}</div>; }
