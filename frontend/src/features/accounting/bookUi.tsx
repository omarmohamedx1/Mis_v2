import { Download, FileSpreadsheet, Plus, X } from 'lucide-react';
import { useEffect, useState, type ReactNode } from 'react';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { DateControl } from '../../components/forms/DateControl';
import { useAccountingText } from './accountingUi';
import { accountingBookService } from './services/accountingBookService';
import type { ExportFilter } from './types/book';
import { getApiErrorMessage } from '../../services/apiClient';

export const categories: Record<string, [string, string, string]> = {
  PETTY: ['مصاريف نثرية', 'Petty expenses', '610110'], FIXED: ['مصاريف شهرية ثابتة', 'Fixed monthly expenses', '610120'],
  TRANSPORTATION: ['انتقالات', 'Transportation', '610130'], ADVANCE: ['سلف', 'Employee advances', '113100'], CARRIED_ADVANCE: ['سلف مرحلة', 'Carried advances', '113100'],
  SUPERVISOR_COMMISSION: ['عمولات سوبر فايزر', 'Supervisor commissions', '610180'], COLLECTOR_COMMISSION: ['عمولات محصلين', 'Collector commissions', '610170'],
  SALARY: ['المرتبات', 'Salaries', '610140'], BONUS: ['مكافآت', 'Bonuses', '610150'], LEGAL: ['مصاريف قضائية', 'Judicial expenses', '610160'],
  INCOME: ['وارد الشركة', 'Company income', '410100'], ADVANCE_REPAYMENT: ['سداد سلفة', 'Advance repayment', '113100'], PAYROLL_COMMISSION: ['عمولات ضمن المرتبات', 'Commissions in payroll', '610190'], OTHER: ['مصاريف أخرى', 'Other expenses', '610100'],
};
export const manualCategories = ['PETTY', 'FIXED', 'ADVANCE', 'CARRIED_ADVANCE', 'BONUS', 'LEGAL', 'INCOME', 'ADVANCE_REPAYMENT', 'OTHER'];
export const categoryPaths: Record<string, string> = { PETTY: 'petty-expenses', FIXED: 'fixed-expenses', TRANSPORTATION: 'transportation', ADVANCE: 'advances', CARRIED_ADVANCE: 'carried-advances', SUPERVISOR_COMMISSION: 'supervisor-commissions', COLLECTOR_COMMISSION: 'collector-commissions', SALARY: 'salaries', BONUS: 'bonuses', LEGAL: 'judicial-expenses', INCOME: 'income', PAYROLL_COMMISSION: 'salaries', OTHER: 'expenses' };
export const cairoToday = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Cairo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
export const monthRange = () => { const today = cairoToday(); return { from: today.slice(0, 7) + '-01', to: today }; };
export const panel = 'rounded-2xl border border-mis-border bg-white p-5 shadow-sm';
export const cell = 'px-4 py-3 text-start';
export const table = 'w-full min-w-[750px] text-sm [&_thead]:bg-slate-50 [&_thead]:text-slate-500 [&_tbody_tr]:border-t [&_tbody_tr]:border-mis-border [&_tbody_tr:hover]:bg-slate-50/70';
export function BookHeader({ title, description, children }: { title: string; description: string; children?: ReactNode }) {
  return <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between"><div><p className="mb-2 text-xs font-bold tracking-[.2em] text-mis-primary">MIS ACCOUNTING</p><h1 className="text-2xl font-bold text-mis-navy sm:text-3xl">{title}</h1><p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500">{description}</p></div><div className="flex shrink-0 flex-wrap gap-2">{children}</div></header>;
}
export function BookError({ error }: { error: string }) { return error ? <p role="alert" className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">{error}</p> : null; }
export function BookField({ label, children }: { label: string; children: ReactNode }) { return <label className="flex min-w-0 flex-col gap-2 text-sm font-semibold text-slate-700"><span>{label}</span>{children}</label>; }
export function DateRange({ from, to, onChange }: { from: string; to: string; onChange: (from: string, to: string) => void }) {
  const a = useAccountingText();
  return <div className="flex flex-wrap items-end gap-3"><BookField label={a.text('من تاريخ', 'From')}><DateControl className="w-44" value={from} onChange={e => onChange(e.target.value, to)} /></BookField><BookField label={a.text('إلى تاريخ', 'To')}><DateControl className="w-44" value={to} onChange={e => onChange(from, e.target.value)} /></BookField><Button fullWidth={false} variant="ghost" onClick={() => { const range = monthRange(); onChange(range.from, range.to); }}>{a.text('الشهر الحالي', 'This month')}</Button></div>;
}
export function AccountingExportButton({ report, filter = {}, label, disabled = false }: { report: string; filter?: ExportFilter; label?: string; disabled?: boolean }) {
  const a = useAccountingText(); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const run = async () => { setBusy(true); setError(''); try { await accountingBookService.export(report, filter); } catch (e) { setError(getApiErrorMessage(e, a.text('تعذر استخراج الملف', 'Export failed'))); } finally { setBusy(false); } };
  return <div className="flex flex-col gap-2"><Button fullWidth={false} variant="outline" disabled={disabled} isLoading={busy} leftIcon={<Download />} onClick={() => void run()}>{label ?? a.text('تصدير Excel', 'Export Excel')}</Button><BookError error={error} /></div>;
}
export function BookEmpty({ children }: { children?: ReactNode }) { const a = useAccountingText(); return <div className="grid place-items-center gap-3 p-12 text-center"><FileSpreadsheet className="h-9 w-9 text-slate-300" /><p className="font-semibold text-mis-navy">{a.text('لا توجد حركات لهذه التصفية', 'No records match these filters')}</p>{children}</div>; }
export function AddButton({ onClick, children }: { onClick: () => void; children: ReactNode }) { return <Button fullWidth={false} leftIcon={<Plus />} onClick={onClick}>{children}</Button>; }
export function ClearButton({ onClick }: { onClick: () => void }) { const a = useAccountingText(); return <Button fullWidth={false} variant="ghost" leftIcon={<X />} onClick={onClick}>{a.text('مسح التصفية', 'Clear filters')}</Button>; }
export function useDebounced<T>(value: T) { const [result, setResult] = useState(value); useEffect(() => { const timer = setTimeout(() => setResult(value), 300); return () => clearTimeout(timer); }, [value]); return result; }
export function useAccountingMoneyConfirm() {
  const a = useAccountingText(); const [pending, setPending] = useState<{ amount: number; details: ReactNode; run: () => Promise<void> }>(); const [busy, setBusy] = useState(false);
  const run = async () => { if (!pending || busy) return; setBusy(true); try { await pending.run(); setPending(undefined); } finally { setBusy(false); } };
  return { confirmMoney: (amount: number, details: ReactNode, action: () => Promise<void>) => setPending({ amount, details, run: action }), moneyDialog: <ConfirmDialog open={!!pending} onCancel={() => setPending(undefined)} onConfirm={() => void run()} isConfirming={busy} confirmVariant="primary" title={a.text('تأكد من المبلغ والبيانات', 'Confirm amount and details')} message={<div className="space-y-3"><p className="text-2xl font-bold text-mis-navy">{a.money(pending?.amount ?? 0)}</p><div className="whitespace-pre-line">{pending?.details}</div></div>} /> };
}
