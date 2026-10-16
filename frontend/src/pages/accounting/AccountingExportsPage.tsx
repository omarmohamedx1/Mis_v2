import { FileSpreadsheet, FolderDown } from 'lucide-react';
import { useState } from 'react';
import { AccountingExportButton, BookHeader, DateRange, monthRange, panel } from '../../features/accounting/bookUi';
import { useAccountingText } from '../../features/accounting/accountingUi';

const reports = [
  ['summary', 'إجمالي المصاريف والوارد', 'Income and expense summary', 'الإيرادات والمصروفات والتدفق النقدي وتفصيل البنود', 'Income, expense, cash flow and category breakdown'],
  ['cashbook', 'سجل المصاريف والوارد', 'Accounting register', 'المستندات والحسابات والموظفون وحالة كل حركة', 'Documents, accounts, employees and entry statuses'],
  ['advances', 'السلف والسلف المرحلة', 'Advances and carried balances', 'أصل السلفة والمسدد والمتبقي حتى نهاية الفترة', 'Original advances, repayments and balances as of the end date'],
  ['recurring', 'قوالب المصاريف الشهرية', 'Recurring expense templates', 'المبالغ والاستحقاق وفترة التكرار والحالة', 'Amounts, due days, schedule and status'],
  ['salaries', 'المرتبات', 'Salaries', 'الأساسي والبدلات والعمولات والمكافآت والخصومات والصافي', 'Basic salary, allowances, commissions, bonuses, deductions and net pay'],
  ['transportation', 'الانتقالات', 'Transportation', 'الموظف والمطالبة والمبلغ وحالة الصرف', 'Employee claims, amounts and payment status'],
  ['collector-commissions', 'عمولات المحصلين', 'Collector commissions', 'التحصيل والقاعدة والتعديلات والعمولة النهائية', 'Collections, rules, adjustments and final commissions'],
  ['supervisor-commissions', 'عمولات السوبر فايزر', 'Supervisor commissions', 'تفصيل عمولات المشرفين وحالة الاعتماد والصرف', 'Supervisor commissions, approval and payment statuses'],
  ['commission-calculation', 'معاينة حساب العمولات', 'Commission calculation preview', 'معاينة المحصلين والسوبر فايزر للشهور المختارة', 'Collector and supervisor preview for selected months'],
  ['commission-rules', 'قواعد العمولات', 'Commission rules', 'النسب والمبالغ الثابتة وسريان القواعد', 'Percentages, fixed amounts and effective rules'],
  ['accounts', 'دليل الحسابات', 'Chart of accounts', 'الأكواد والتصنيف والحساب الأب والأرصدة الحالية', 'Codes, types, parent accounts and current balances'],
  ['journals', 'القيود اليومية', 'Journals', 'كل قيد وتفصيل سطوره والحسابات المدينة والدائنة', 'Journals with debit and credit line details'],
  ['trial-balance', 'ميزان المراجعة', 'Trial balance', 'أرصدة وحركات الحسابات حتى نهاية الفترة', 'Account balances and movements as of the end date'],
];
export function AccountingExportsPage() {
  const a = useAccountingText(); const [range, setRange] = useState(monthRange); const valid = !!range.from && !!range.to && range.from <= range.to;
  return <div className="space-y-6"><BookHeader title={a.text('مركز سحبات المحاسبة', 'Accounting export center')} description={a.text('ملفات Excel منظمة، بمبالغ وتواريخ قابلة للتحليل، وعناوين وفترة استخراج واضحة. سحبات الشاشات نفسها تحترم التصفية الحالية.', 'Formatted Excel files with numeric amounts, real dates and clear period labels. Exports within each page follow its current filters.')} /><div className={panel}><DateRange {...range} onChange={(from, to) => setRange({ from, to })} /></div><article className="flex flex-col gap-5 rounded-2xl bg-mis-navy p-6 text-white sm:flex-row sm:items-center"><FolderDown className="h-12 w-12 text-sky-200" /><div className="flex-1"><h2 className="text-xl font-bold">{a.text('سحبة محاسبة شاملة', 'Complete accounting workbook')}</h2><p className="mt-2 text-sm leading-6 text-sky-100">{a.text('ملف واحد بشيتات منفصلة للمصاريف والوارد والسلف والمرتبات والعمولات والدليل والقيود وميزان المراجعة.', 'One workbook with separate sheets for income, expenses, advances, salaries, commissions, accounts, journals and trial balance.')}</p></div><AccountingExportButton report="all" filter={range} disabled={!valid} label={a.text('تنزيل الملف الشامل', 'Download full workbook')} /></article><div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{reports.map(([key, ar, en, descriptionAr, descriptionEn]) => <article key={key} className={`${panel} flex flex-col items-start gap-4`}><FileSpreadsheet className="h-7 w-7 text-mis-primary" /><div className="flex-1"><h2 className="font-bold text-mis-navy">{a.text(ar, en)}</h2><p className="mt-2 text-sm leading-6 text-slate-500">{a.text(descriptionAr, descriptionEn)}</p></div><AccountingExportButton report={key} filter={range} disabled={!valid} /></article>)}</div></div>;
}
