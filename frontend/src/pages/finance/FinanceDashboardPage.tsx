import { AlertCircle, ArrowUpRight, Banknote, BookOpenCheck, Car, Landmark, Percent, Scale, UsersRound, Wallet, WalletCards } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { AccountingMonthSelect, useAccountingText } from '../../features/accounting/accountingUi';
import { accountingService } from '../../features/accounting/services/accountingService';
import type { AccountingDashboard } from '../../features/accounting/types/accounting';
import { FinanceKpi, useFinanceText } from '../../features/finance/financeUi';
import { financeService } from '../../features/finance/services/financeService';
import type { FinanceDashboard } from '../../features/finance/types/finance';

export function FinanceDashboardPage() {
  const f = useFinanceText();
  const a = useAccountingText();
  const now = useMemo(() => new Date(), []);
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [data, setData] = useState<FinanceDashboard>();
  const [payroll, setPayroll] = useState<AccountingDashboard>();
  const [error, setError] = useState(false);

  useEffect(() => { financeService.dashboard().then(setData).catch(() => setError(true)); }, []);
  useEffect(() => {
    accountingService.dashboard(year, month).then(setPayroll).catch(() => setPayroll(undefined));
  }, [year, month]);

  if (error) return <ErrorState title={f.text('تعذر تحميل لوحة المالية', 'Could not load the finance dashboard')} onRetry={() => location.reload()} />;
  if (!data) return <div className="grid min-h-[440px] place-items-center"><LoadingSpinner /></div>;

  const controls = [
    { value: data.pendingJournals, ar: 'قيود تنتظر الإجراء', en: 'Journals awaiting action', icon: BookOpenCheck, to: '/finance/journals?status=PENDING_APPROVAL' },
    { value: data.failedEvents, ar: 'أحداث ترحيل فاشلة', en: 'Failed posting events', icon: AlertCircle, to: '/finance/journals' },
    { value: data.openPeriods, ar: 'فترات مفتوحة', en: 'Open periods', icon: Scale, to: '/finance/periods' },
    { value: payroll?.pendingApprovals ?? 0, ar: 'مرتبات وعمولات بانتظار الاعتماد', en: 'Payroll awaiting approval', icon: Wallet, to: '/finance/salaries' },
  ];
  const payrollLinks = [
    { to: '/finance/salaries', icon: Wallet, ar: 'المرتبات', en: 'Salaries' },
    { to: '/finance/transportation', icon: Car, ar: 'الانتقالات', en: 'Transportation' },
    { to: '/finance/collector-commissions', icon: Banknote, ar: 'عمولات المحصلين', en: 'Collector commissions' },
    { to: '/finance/supervisor-commissions', icon: UsersRound, ar: 'عمولات المشرفين', en: 'Supervisor commissions' },
    { to: '/finance/commission-rules', icon: Percent, ar: 'قواعد العمولة', en: 'Commission rules' },
  ];

  return (
    <div className="space-y-8">
      <header className="flex flex-col justify-between gap-4 xl:flex-row xl:items-end">
        <div>
          <p className="text-xs font-bold uppercase tracking-[.18em] text-mis-primary">{f.text('الوضع المالي الآن', 'FINANCIAL POSITION NOW')}</p>
          <h1 className="mt-2 text-3xl font-bold text-mis-navy">{f.text('مركز القيادة المالية', 'Finance Command Center')}</h1>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-500">{f.text('قراءة موحدة من القيود المُرحّلة والمرتبات والعمولات، مع فصل أموال البنوك والشركات عن إيراد الشركة.', 'One controlled view of posted journals, payroll, and commissions, with bank and company money kept distinct from company revenue.')}</p>
        </div>
        <div className="flex items-center gap-2 rounded-xl border border-mis-border bg-white px-4 py-3 text-sm text-slate-500"><Landmark className="h-4 w-4 text-mis-primary" />{f.text('الكيان: MIS Egypt', 'Entity: MIS Egypt')} · {data.baseCurrencyCode}</div>
      </header>

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <FinanceKpi label={f.text('تحصيلات اليوم', 'Today gross collections')} value={f.money(data.grossCollectionsToday)} hint={f.text('إجمالي وليس إيرادًا', 'Gross, not revenue')} tone="blue" />
        <FinanceKpi label={f.text('تحصيلات الشهر', 'MTD gross collections')} value={f.money(data.grossCollectionsMonthToDate)} tone="blue" />
        <FinanceKpi label={f.text('أموال مستحقة للبنوك والشركات', 'Bank / company money liability')} value={f.money(data.clientMoneyLiability)} hint={f.text('التزام منفصل', 'Separate liability')} tone="amber" />
        <FinanceKpi label={f.text('صافي نتيجة التشغيل', 'Net operating result')} value={f.money(data.netOperatingResult)} tone={data.netOperatingResult >= 0 ? 'green' : 'red'} />
      </section>

      <section className="grid gap-4 md:grid-cols-2">
        <div className="rounded-2xl border border-mis-border bg-white p-6 shadow-sm">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-semibold text-slate-500">{f.text('إيرادات الشهر', 'MTD revenue')}</p>
              <p className="mt-3 text-3xl font-bold text-emerald-700" data-bidi="ltr">{f.money(data.revenueMonthToDate)}</p>
            </div>
            <span className="grid h-12 w-12 place-items-center rounded-2xl bg-emerald-50 text-emerald-700"><WalletCards /></span>
          </div>
        </div>
        <div className="rounded-2xl border border-mis-border bg-white p-6 shadow-sm">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-semibold text-slate-500">{f.text('مصروفات الشهر', 'MTD expenses')}</p>
              <p className="mt-3 text-3xl font-bold text-rose-700" data-bidi="ltr">{f.money(data.expensesMonthToDate)}</p>
            </div>
            <span className="grid h-12 w-12 place-items-center rounded-2xl bg-rose-50 text-rose-700"><ArrowUpRight /></span>
          </div>
        </div>
      </section>

      <section>
        <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h2 className="text-xl font-bold text-mis-navy">{f.text('المرتبات والعمولات', 'Payroll and commissions')}</h2>
            <p className="mt-1 text-sm text-slate-500">{a.monthLabel(year, month)}</p>
          </div>
          <div className="flex min-w-0 flex-wrap gap-2">
            <AccountingMonthSelect value={month} onChange={setMonth} />
            <input aria-label={a.text('السنة', 'Year')} className="h-11 w-24 rounded-xl border border-mis-border bg-white px-3 text-sm" type="number" value={year} onChange={(event) => setYear(Number(event.target.value))} />
          </div>
        </div>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <FinanceKpi label={a.text('إجمالي المرتبات', 'Total payroll')} value={a.money(payroll?.totalPayroll ?? 0)} tone="blue" />
          <FinanceKpi label={a.text('الانتقالات', 'Transportation')} value={a.money(payroll?.totalTransportation ?? 0)} tone="amber" />
          <FinanceKpi label={a.text('عمولات المحصلين', 'Collector commissions')} value={a.money(payroll?.totalCollectorCommissions ?? 0)} tone="green" />
          <FinanceKpi label={a.text('عمولات المشرفين', 'Supervisor commissions')} value={a.money(payroll?.totalSupervisorCommissions ?? 0)} tone="green" />
        </div>
        <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
          {payrollLinks.map(({ to, icon: Icon, ar, en }) => (
            <Link key={to} to={to} className="group flex items-center gap-3 rounded-2xl border border-mis-border bg-white px-4 py-3 text-sm font-bold text-mis-navy shadow-sm transition hover:-translate-y-0.5 hover:border-mis-blue">
              <span className="grid h-10 w-10 place-items-center rounded-xl bg-emerald-50 text-emerald-700"><Icon className="h-5 w-5" /></span>
              {f.text(ar, en)}
              <ArrowUpRight className="ms-auto h-4 w-4 text-slate-400 transition group-hover:text-mis-primary" />
            </Link>
          ))}
        </div>
      </section>

      <section>
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-xl font-bold text-mis-navy">{f.text('الرقابة اليومية', 'Daily controls')}</h2>
          <Link to="/finance/journals" className="text-sm font-bold text-mis-primary">{f.text('فتح دفتر اليومية', 'Open journal register')}</Link>
        </div>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          {controls.map(({ value, ar, en, icon: Icon, to }) => (
            <Link key={en} to={to} className="group flex items-center gap-4 rounded-2xl border border-mis-border bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-mis-blue">
              <span className={`grid h-11 w-11 place-items-center rounded-xl ${value ? 'bg-amber-50 text-amber-700' : 'bg-emerald-50 text-emerald-700'}`}><Icon className="h-5 w-5" /></span>
              <div>
                <p className="text-2xl font-bold text-mis-navy">{f.number(value)}</p>
                <p className="text-sm text-slate-500">{f.text(ar, en)}</p>
              </div>
              <ArrowUpRight className="ms-auto h-4 w-4 text-slate-400 transition group-hover:text-mis-primary" />
            </Link>
          ))}
        </div>
      </section>
    </div>
  );
}
