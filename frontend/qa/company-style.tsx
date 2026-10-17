// Real formatters, shared controls and payroll page, with a local synthetic API.
import { createRoot } from 'react-dom/client';
import { BrowserRouter, MemoryRouter, Route, Routes } from 'react-router-dom';
import { useEffect } from 'react';
import { LocalizationProvider, useLocalization } from '../src/context/LocalizationContext';
import { AuthProvider } from '../src/context/AuthContext';
import { ToastProvider } from '../src/components/common/Toast';
import { ProfessionalDateInput } from '../src/components/forms/ProfessionalDateInput';
import { useAccountingText } from '../src/features/accounting/accountingUi';
import { useFinanceText } from '../src/features/finance/financeUi';
import { useLegalText } from '../src/features/legal/legalUi';
import { useDataEntryText } from '../src/features/data-entry/dataEntryUi';
import { useCollectionFormat } from '../src/features/collections/components/CollectionsUi';
import { HrPayrollPage } from '../src/pages/hr/HrPayrollPage';
import { apiClient } from '../src/services/apiClient';
import { formatMoney, formatNumberWithUnit } from '../src/utils/formatMoney';
import { CollectionsDashboardPage } from '../src/pages/collections/CollectionsDashboardPage';
import { CollectionsCreditorsDashboardPage } from '../src/pages/collections/CollectionsCreditorsDashboardPage';
import { CollectionsCollectorsDashboardPage } from '../src/pages/collections/CollectionsCollectorsDashboardPage';
import { CollectionsLayout } from '../src/components/layout/CollectionsLayout';
import '../src/styles.css';

const options = new URLSearchParams(location.search);
localStorage.setItem('mis.language', options.get('lang') ?? 'en');
if (options.get('page') === 'workspace') {
  const role = options.get('role') ?? 'Admin';
  localStorage.setItem('mis.auth', JSON.stringify({ accessToken: 'offline-qa-only', user: { id: 'qa', username: 'qa', email: 'qa@example.invalid', loginCode: 'QA-001', fullName: 'MIS Administrator', department: 'Collections', role, roles: [role], permissions: [] } }));
  localStorage.setItem('mis.collections.sidebar', 'expanded');
}
const qa: any = (window as any).__companyStyleQa = { errors: [], ready: false, requests: [] };
window.addEventListener('error', event => qa.errors.push(event.message));
window.addEventListener('unhandledrejection', event => qa.errors.push(String(event.reason)));
apiClient.defaults.adapter = async config => {
  qa.requests.push(config.url);
  const reply = (data: unknown) => ({ config, status: 200, statusText: 'OK', headers: {}, data });
  const company = { id: '2656263e-8c57-44d5-b178-6b57451e6a12', name: 'بريميوم كارد', code: 'PREMIUM_CARD', organizationType: 'CONSUMER_FINANCE', cases: 1418, assigned: 520, unassigned: 898, legalCases: 0, outstanding: 25200000, overdue: 10200000 };
  const desk = { portfolioId: 'portfolio', name: 'محفظة متابعة الأقساط', primaryClassification: 'ACT', subClassification: 'AUTO', cases: 1418, assigned: 520, unassigned: 898, legalCases: 0, outstanding: 25200000, overdue: 10200000 };
  const organization = { organizationId: company.id, code: company.code, name: company.name, organizationType: company.organizationType, cases: company.cases, outstanding: company.outstanding, overdue: company.overdue, legalCases: 0 };
  const scenario = options.get('scenario');
  if (config.url === '/collections/dashboard') {
    if (scenario === 'summary-error') throw new Error('Synthetic dashboard failure');
    const summary = { totalCases: 1418, totalOutstanding: 25200000, totalOverdue: 10200000, assignedCases: 520, unassignedCases: 898, activeCollectors: 2, collectedToday: 0, collectedMonthToDate: 1000000, achievementPercent: 0, activePromises: 0, promisesDueToday: 0, brokenPromises: 0, visitsToday: 0, pendingReviews: 0, openComplaints: 0, highRiskCases: 0, overdueFollowUps: 0, byClient: [company], byBucket: [{ code: '31-60', name: '31–60 يوم', cases: 1418, outstanding: 25200000, overdue: 10200000 }], collectionTrend: [] as { date: string; collected: number }[], byStatus: [{ status: 'ACTIVE', cases: 1418, outstanding: 25200000 }] };
    if (scenario === 'empty') return reply({ ...summary, totalCases: 0, totalOutstanding: 0, totalOverdue: 0, assignedCases: 0, unassignedCases: 0, activeCollectors: 0, collectedMonthToDate: 0, byClient: [], byBucket: [], byStatus: [] });
    if (scenario === 'busy') return reply({ ...summary, pendingReviews: 7, brokenPromises: 4, promisesDueToday: 12, overdueFollowUps: 6, highRiskCases: 9, openComplaints: 2, visitsToday: 3, collectedToday: 12500, achievementPercent: 67.3, collectionTrend: Array.from({ length: 7 }, (_, index) => ({ date: `2026-10-${11 + index}`, collected: (index + 1) * 1500 })), byClient: Array.from({ length: 8 }, (_, index) => ({ ...company, id: `organization-${index}`, code: `CLIENT_${index}`, name: index % 2 ? `شركة التمويل الاستهلاكي ${index}` : `بنك التنمية والاستثمار ${index}`, organizationType: index % 2 ? 'CONSUMER_FINANCE' : 'BANK', outstanding: 1000000 + index * 250000 })) });
    return reply(summary);
  }
  if (config.url === '/collections/work-queue/my') {
    if (scenario === 'queue-error') throw new Error('Synthetic personal queue failure');
    const queue = { callsDue: [] as unknown[], highPriorityCases: [] as unknown[], promisesDue: [] as unknown[], brokenPromises: [] as unknown[], visitsToday: 0, pendingReviews: 0, openComplaints: 0 };
    if (scenario === 'busy') {
      const promise = { caseId: 'case-1', customerName: 'أحمد محمود عبدالرحمن', caseNumber: 'CASE-001', promiseDate: '2026-10-17', promisedAmount: 1500 };
      return reply({ ...queue, brokenPromises: [promise], promisesDue: [promise, { ...promise, caseId: 'case-2', caseNumber: 'CASE-002' }], callsDue: [{ id: 'case-3', customerName: 'سارة علي محمد', caseNumber: 'CASE-003', nextFollowUpAt: '2026-10-17' }] });
    }
    return reply(queue);
  }
  if (config.url === '/collections/dashboard/creditors') return reply({ banks: [], companies: [{ ...company, desks: [desk] }] });
  if (config.url === '/collections/dashboard/collectors') return reply({ unassignedCases: 898, unassignedOutstanding: 25200000, unassignedOverdue: 10200000, unassignedByOrganization: [organization], collectors: [{ id: 'collector', name: 'أحمد محمود عبدالرحمن', loginCode: 'MIS-001', cases: 520, outstanding: 25200000, overdue: 10200000, legalCases: 0, highRiskCases: 0, organizations: [organization] }] });
  if (config.url !== '/hr/payroll') throw new Error('No fixture: ' + config.url);
  const employees = Array.from({ length: 3 }, (_, index) => ({ employeeId: 'employee-' + index, employeeNumber: 'EMP-00' + index, employeeName: ['أحمد محمد عبدالرحمن', 'سارة علي محمود', 'عمر حسن محمد'][index], departmentName: 'إدارة التحصيل', basicSalary: 12345.67, allowances: 1000, grossSalary: 13345.67, dailyRate: 411.52, absenceDays: 2, fridayVisitDays: 1, offsetAbsenceDays: 1, chargeableAbsenceDays: 1, lateMinutes: 75, lateRemainderMinutes: 15, lateDeductionDays: 1, deductions: [], totalDeductions: 823.04, netSalary: 12522.63, hasCompensation: true, accountingStatus: 'READY' }));
  return { config, status: 200, statusText: 'OK', headers: {}, data: { year: 2026, month: 10, lateGraceMinutes: 15, lateMinutesPerDeductedDay: 60, monthDivisor: 30, employeeCount: employees.length, totalGross: 40037.01, totalDeductions: 2469.12, totalNet: 37567.89, employees } };
};
function CompanyStyle() {
  const { language, setLanguage } = useLocalization();
  const ar = language === 'ar';
  const accounting = useAccountingText(), finance = useFinanceText(), legal = useLegalText(), dataEntry = useDataEntryText(), collections = useCollectionFormat();
  useEffect(() => { qa.language = setLanguage; qa.ready = true; }, [setLanguage]);
  const formats = [
    ['accounting', accounting.number, accounting.money, accounting.date],
    ['finance', finance.number, finance.money, finance.date],
    ['legal', legal.number, legal.money, legal.date],
    ['data-entry', dataEntry.number, dataEntry.money, dataEntry.dateTime],
    ['collections', collections.number, collections.money, collections.dateTime],
  ] as const;
  return <main className="mx-auto max-w-7xl space-y-6 p-3 sm:p-6">
    <header className="space-y-2"><h1 className="text-2xl font-bold text-mis-navy">{ar ? 'إدارة العمليات والمتابعة المالية' : 'Operations and financial management'}</h1><p className="text-sm text-slate-500">{ar ? 'متابعة المحافظ والعملاء وأداء فريق التحصيل — أكتوبر 2026' : 'Portfolios, customers and collection team performance — October 2026'}</p></header>
    <section data-qa="formatters" className="overflow-hidden rounded-2xl border border-mis-border bg-white">
      <div className="overflow-x-auto"><table className="w-full min-w-[700px] text-sm"><thead className="bg-slate-50 text-slate-500"><tr>{[ar ? 'القسم' : 'Module', ar ? 'عدد الحالات' : 'Case count', ar ? 'المبلغ' : 'Amount', ar ? 'التاريخ والوقت' : 'Date and time'].map(label => <th key={label} className="px-5 py-3 text-start">{label}</th>)}</tr></thead><tbody className="divide-y divide-mis-border">
        {formats.map(([name, number, money, date]) => <tr key={name} data-module={name}><td className="px-4 py-3 font-semibold">{name}</td><td className="px-3 py-3" data-bidi="ltr">{number(1234567)}</td><td className="px-6 py-3" data-bidi="ltr">{money(-12345.67)}</td><td className="px-4 py-3">{date('2026-10-17')}</td></tr>)}
      </tbody></table></div>
    </section>
    <section data-qa="alignment" className="overflow-hidden rounded-2xl border border-mis-border bg-white"><div className="overflow-x-auto"><table className="w-full min-w-[700px] text-sm"><thead className="bg-slate-50"><tr><th className="px-5 py-3 text-start">{ar ? 'رقم الحساب' : 'Account no.'}</th><th className="px-3 py-3 text-start">{ar ? 'اسم العميل' : 'Customer name'}</th><th className="px-6 py-3 text-end">{ar ? 'إجمالي المديونية' : 'Total outstanding'}</th><th className="px-2 py-3 text-center">{ar ? 'عدد الحالات' : 'Case count'}</th></tr></thead><tbody><tr><td className="px-3 py-4 text-start" dir="ltr">ACC-00125</td><td className="px-6 py-4">{ar ? 'أحمد محمود عبدالرحمن' : 'Ahmed Mahmoud Abdelrahman'}</td><td className="px-4 py-4 text-end" data-bidi="ltr">{finance.money(987654.32)}</td><td className="px-5 py-4 text-center" data-bidi="ltr">125</td></tr></tbody></table></div></section>
    <section data-qa="currency-order" className="grid gap-3 rounded-2xl border border-mis-border bg-white p-4 sm:grid-cols-2">
      {(['rtl', 'ltr'] as const).flatMap(direction => formats.flatMap(([module, , money]) => [0, 55, -55, 12345.67, 1500000].map(value =>
        <p className="text-sm" dir={direction} key={`${module}-${direction}-${value}`}><span>{module}: </span><strong data-money-value data-module-name={module} data-context={direction} data-amount={value}>{money(value)}</strong></p>
      )))}
      {['USD', 'SAR'].map(currency => <p key={currency}><strong data-money-value data-context="rtl" data-amount="55" data-currency={currency}>{formatMoney(55, ar ? 'ar-EG-u-nu-latn' : 'en-EG', { currency, minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong></p>)}
      {(['rtl', 'ltr'] as const).flatMap(direction => [1500, 25200000, 10200000, 1500000000, -25200000].map(value => <p dir={direction} key={`compact-${direction}-${value}`}><strong data-compact-money data-context={direction} data-amount={value}>{formatMoney(value, ar ? 'ar-EG-u-nu-latn' : 'en-EG', { notation: 'compact', minimumFractionDigits: 0, maximumFractionDigits: 1 })}</strong></p>))}
      {(['rtl', 'ltr'] as const).flatMap(direction => [
        ['1,418', ar ? 'حالة' : 'cases'], ['40%', ar ? 'من المديونية' : 'of outstanding'],
      ].map(([value, unit]) => <p dir={direction} key={`quantity-${direction}-${value}`}><strong data-quantity-value data-unit={unit}>{formatNumberWithUnit(value, unit)}</strong></p>))}
    </section>
    <section className="rounded-2xl border border-mis-border bg-white p-4"><ProfessionalDateInput name="calendar" defaultValue="2026-10-17" /></section>
    <section data-qa="payroll"><HrPayrollPage /></section>
  </main>;
}
const page = options.get('page');
const content = page === 'dashboard' ? <CollectionsDashboardPage /> : page === 'creditors' ? <CollectionsCreditorsDashboardPage /> : page === 'collectors' ? <CollectionsCollectorsDashboardPage /> : null;
const workspace = <MemoryRouter initialEntries={['/collections/dashboard' + (options.has('organizationId') ? '?organizationId=organization-7' : '')]}><Routes><Route element={<CollectionsLayout />}><Route path="/collections/dashboard" element={<CollectionsDashboardPage />} /><Route path="*" element={<p data-qa="navigation-destination">Offline navigation destination</p>} /></Route></Routes></MemoryRouter>;
createRoot(document.getElementById('root')!).render(<LocalizationProvider><ToastProvider><AuthProvider>{page === 'workspace' ? workspace : <BrowserRouter>{content ? <main className="mx-auto max-w-7xl p-3 sm:p-6">{content}</main> : <CompanyStyle />}</BrowserRouter>}</AuthProvider></ToastProvider></LocalizationProvider>);
