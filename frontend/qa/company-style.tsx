// Real formatters, shared controls and payroll page, with a local synthetic API.
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
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
import '../src/styles.css';

const options = new URLSearchParams(location.search);
localStorage.setItem('mis.language', options.get('lang') ?? 'en');
const qa: any = (window as any).__companyStyleQa = { errors: [], ready: false, requests: [] };
window.addEventListener('error', event => qa.errors.push(event.message));
window.addEventListener('unhandledrejection', event => qa.errors.push(String(event.reason)));
apiClient.defaults.adapter = async config => {
  qa.requests.push(config.url);
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
    <section className="rounded-2xl border border-mis-border bg-white p-4"><ProfessionalDateInput name="calendar" defaultValue="2026-10-17" /></section>
    <section data-qa="payroll"><HrPayrollPage /></section>
  </main>;
}
createRoot(document.getElementById('root')!).render(<BrowserRouter><LocalizationProvider><ToastProvider><AuthProvider><CompanyStyle /></AuthProvider></ToastProvider></LocalizationProvider></BrowserRouter>);
