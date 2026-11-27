import { useEffect, useState } from 'react';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { DateControl } from '../../components/forms/DateControl';
import { useAccountingText } from '../../features/accounting/accountingUi';
import { AccountingExportButton, BookError, BookHeader, BookField, categories, monthRange, panel } from '../../features/accounting/bookUi';
import { accountingBookService } from '../../features/accounting/services/accountingBookService';
import type { ProfitLossCategory, ProfitLossReport } from '../../features/accounting/types/book';
import { getApiErrorMessage } from '../../services/apiClient';

export function FinanceProfitLossPage() {
  const a = useAccountingText();
  const initialRange = monthRange();
  const [from, setFrom] = useState(initialRange.from);
  const [to, setTo] = useState(initialRange.to);
  const [report, setReport] = useState<ProfitLossReport>();
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    if (!from || !to || from > to) {
      setReport(undefined);
      setLoading(false);
      setError(a.text('اختر نطاقًا زمنيًا صحيحًا.', 'Choose a valid date range.'));
      return () => { active = false; };
    }

    setLoading(true);
    setError('');
    accountingBookService.profitLoss(from, to)
      .then(result => { if (active) setReport(result); })
      .catch(reason => { if (active) { setReport(undefined); setError(getApiErrorMessage(reason, a.text('تعذر تحميل التقرير.', 'Could not load the report.'))); } })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [from, to, a.ar]);

  const categoryName = (category: string) => category === 'CLAIMS' ? a.text('المطالبات', 'Claims') : categories[category]?.[a.ar ? 0 : 1] ?? category.replaceAll('_', ' ');
  const claimCategories = report?.claimCategories ?? [];
  const expenseCategories = report?.expenseCategories ?? [];
  const resultLabel = report?.netProfitLoss === undefined || report.netProfitLoss === 0
    ? a.text('صافي الربح / الخسارة', 'Net Profit / Loss')
    : report.netProfitLoss > 0 ? a.text('صافي الربح', 'Net Profit') : a.text('صافي الخسارة', 'Net Loss');

  const renderCategories = (items: ProfitLossCategory[], emptyLabel: string, claims = false) => items.length
    ? <div className="divide-y divide-mis-border">{items.map(category => <details key={category.category} className="group py-4 first:pt-0 last:pb-0">
        <summary className="flex cursor-pointer list-none items-center justify-between gap-4 [&::-webkit-details-marker]:hidden">
          <span className="font-semibold text-mis-navy">{categoryName(category.category)}</span>
          <span className="flex items-center gap-4">
            <span className="text-xs text-slate-500">{a.text('عرض الحركات', 'View transactions')}</span>
            <span className="font-bold text-mis-navy" dir="ltr">{a.money(category.amount)}</span>
          </span>
        </summary>
        <div className="mt-4 overflow-x-auto">
          <table className="w-full min-w-[600px] text-sm">
            <thead className="bg-slate-50 text-xs text-slate-500">
              <tr>
                <th className="px-3 py-2 text-start">{a.text('التاريخ', 'Date')}</th>
                <th className="px-3 py-2 text-start">{claims ? a.text('رقم المطالبة', 'Claim Number') : a.text('رقم القيد', 'Journal')}</th>
                <th className="px-3 py-2 text-start">{a.text('البيان', 'Description')}</th>
                <th className="px-3 py-2 text-end">{a.text('المبلغ', 'Amount')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-mis-border">{category.transactions.map((transaction, index) => <tr key={`${transaction.journalNumber ?? transaction.date}-${index}`}>
              <td className="px-3 py-2">{a.date(transaction.date)}</td>
              <td className="px-3 py-2 font-mono">{transaction.journalNumber ?? '—'}</td>
              <td className="px-3 py-2">{transaction.description}</td>
              <td className="px-3 py-2 text-end font-semibold" dir="ltr">{a.money(transaction.amount)}</td>
            </tr>)}</tbody>
          </table>
        </div>
      </details>)}</div>
    : <p className="py-2 text-sm text-slate-500">{emptyLabel}</p>;

  return <div dir={a.ar ? 'rtl' : 'ltr'} className="space-y-6">
    <BookHeader
      title={a.text('قائمة الأرباح والخسائر', 'Profit & Loss')}
      description={a.text('تقرير إداري محسوب من إجمالي المطالبات المسجلة والمصروفات خلال الفترة، بغض النظر عن التحصيل.', 'Management report based on registered claims and expenses during the period, regardless of collection.')}
      eyebrow={a.text('التقارير المالية', 'FINANCIAL REPORTS')}
    >
      <AccountingExportButton report="profit-loss" filter={{ from, to }} />
    </BookHeader>

    <section className={`${panel} grid gap-4 sm:grid-cols-2`}>
      <BookField label={a.text('من تاريخ', 'From Date')}>
        <DateControl className="field" value={from} onChange={event => setFrom(event.target.value)} />
      </BookField>
      <BookField label={a.text('إلى تاريخ', 'To Date')}>
        <DateControl className="field" value={to} onChange={event => setTo(event.target.value)} />
      </BookField>
    </section>

    {error && <BookError error={error} />}
    {loading && <div className="grid min-h-48 place-items-center"><LoadingSpinner /></div>}
    {!loading && report && <>
      <section className="grid gap-4 md:grid-cols-3">
        <article className={panel}>
          <p className="text-sm font-semibold text-slate-500">{a.text('إجمالي المطالبات', 'Total Claims')}</p>
          <p className="mt-3 text-2xl font-bold text-mis-navy" dir="ltr">{a.money(report.totalClaims)}</p>
        </article>
        <article className={panel}>
          <p className="text-sm font-semibold text-slate-500">{a.text('إجمالي المصروفات', 'Total Expenses')}</p>
          <p className="mt-3 text-2xl font-bold text-mis-navy" dir="ltr">{a.money(report.totalExpenses)}</p>
        </article>
        <article className={panel}>
          <p className="text-sm font-semibold text-slate-500">{resultLabel}</p>
          <p className={`mt-3 text-2xl font-bold ${report.netProfitLoss < 0 ? 'text-rose-700' : 'text-emerald-700'}`} dir="ltr">{a.money(Math.abs(report.netProfitLoss))}</p>
        </article>
      </section>

      <section className={`${panel} space-y-4`}>
        <h2 className="text-lg font-bold text-mis-navy">{a.text('المطالبات', 'Claims')}</h2>
        {renderCategories(claimCategories, a.text('لا توجد مطالبات في هذه الفترة.', 'No claims in this period.'), true)}
      </section>

      <section className={`${panel} space-y-4`}>
        <h2 className="text-lg font-bold text-mis-navy">{a.text('المصروفات', 'Expenses')}</h2>
        {renderCategories(expenseCategories, a.text('لا توجد مصروفات في هذه الفترة.', 'No expenses in this period.'))}
      </section>

      <section className={`${panel} flex flex-wrap items-center justify-between gap-3 border-mis-primary bg-slate-50`}>
        <h2 className="text-lg font-bold text-mis-navy">{resultLabel}</h2>
        <p className={`text-2xl font-extrabold ${report.netProfitLoss < 0 ? 'text-rose-700' : 'text-emerald-700'}`} dir="ltr">{a.money(Math.abs(report.netProfitLoss))}</p>
      </section>
    </>}
  </div>;
}
