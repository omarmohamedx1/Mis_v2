import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { getApiErrorMessage } from '../../services/apiClient';
import { AccountingKpi, useAccountingText } from '../../features/accounting/accountingUi';
import { accountingService } from '../../features/accounting/services/accountingService';
import type { AccountingDashboard } from '../../features/accounting/types/accounting';

export function AccountingDashboardPage() {
  const a = useAccountingText();
  const now = useMemo(() => new Date(), []);
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [data, setData] = useState<AccountingDashboard>();
  const [error, setError] = useState('');

  useEffect(() => {
    setError('');
    accountingService.dashboard(year, month).then(setData).catch((reason) => setError(getApiErrorMessage(reason, 'Could not load accounting dashboard')));
  }, [year, month]);

  if (error) return <ErrorState title={error} onRetry={() => location.reload()} />;
  if (!data) return <div className="grid min-h-[420px] place-items-center"><LoadingSpinner /></div>;

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-mis-navy">{a.text('لوحة الحسابات', 'Accounting dashboard')}</h1>
          <p className="mt-1 text-sm text-slate-500">{a.monthLabel(year, month)}</p>
        </div>
        <div className="flex gap-2">
          <select className="rounded-xl border border-mis-border bg-white px-3 py-2 text-sm" value={month} onChange={(e) => setMonth(Number(e.target.value))}>
            {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
          <input className="w-24 rounded-xl border border-mis-border bg-white px-3 py-2 text-sm" type="number" value={year} onChange={(e) => setYear(Number(e.target.value))} />
        </div>
      </header>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
        <AccountingKpi label={a.text('إجمالي المرتبات', 'Total payroll')} value={a.money(data.totalPayroll)} />
        <AccountingKpi label={a.text('الانتقالات', 'Transportation')} value={a.money(data.totalTransportation)} />
        <AccountingKpi label={a.text('عمولات المحصلين', 'Collector commissions')} value={a.money(data.totalCollectorCommissions)} />
        <AccountingKpi label={a.text('عمولات المشرفين', 'Supervisor commissions')} value={a.money(data.totalSupervisorCommissions)} />
        <AccountingKpi label={a.text('بانتظار الاعتماد', 'Pending approvals')} value={a.number(data.pendingApprovals)} />
      </div>

      <div className="flex flex-wrap gap-3 text-sm font-bold text-mis-primary">
        <Link to="/accounting/salaries">{a.text('فتح المرتبات', 'Open salaries')}</Link>
        <Link to="/accounting/transportation">{a.text('فتح الانتقالات', 'Open transportation')}</Link>
        <Link to="/accounting/collector-commissions">{a.text('عمولات المحصلين', 'Collector commissions')}</Link>
        <Link to="/accounting/supervisor-commissions">{a.text('عمولات المشرفين', 'Supervisor commissions')}</Link>
      </div>
    </div>
  );
}
