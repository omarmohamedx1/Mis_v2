import { StatusBadge, type StatusTone } from '../../components/common/StatusBadge';
import { useLocalization } from '../../context/LocalizationContext';

export function useFinanceText() {
  const { language } = useLocalization(); const ar = language === 'ar';
  const text = (arabic: string, english: string) => ar ? arabic : english;
  const money = (value: number, currency = 'EGP') => new Intl.NumberFormat(ar ? 'ar-EG' : 'en-EG', { style: 'currency', currency, minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
  const number = (value: number) => new Intl.NumberFormat(ar ? 'ar-EG' : 'en-EG').format(value);
  const date = (value: string) => new Intl.DateTimeFormat(ar ? 'ar-EG' : 'en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(`${value}T00:00:00`));
  const status = (value: string) => ({ POSTED: text('مُرحّل', 'Posted'), CLEARED: text('تمت التسوية', 'Cleared'), REVERSED: text('معكوس', 'Reversed'), OPEN: text('مفتوحة', 'Open'), SOFT_CLOSED: text('إقفال مبدئي', 'Soft closed'), CLOSED: text('مغلقة', 'Closed'), DRAFT: text('مسودة', 'Draft'), PENDING_APPROVAL: text('بانتظار الاعتماد', 'Pending approval'), APPROVED: text('معتمد', 'Approved') }[value] ?? value.replaceAll('_', ' '));
  const channel = (value: string) => ({ CASH_COLLECTOR: text('نقدي مع المحصل', 'Cash with collector'), CASH_BRANCH: text('نقدي بالفرع', 'Branch cash'), BANK_TRANSFER: text('تحويل بنكي', 'Bank transfer'), CHEQUE: text('شيك', 'Cheque'), GATEWAY: text('بوابة دفع / محفظة', 'Gateway / wallet'), DIRECT_CLIENT: text('سداد مباشر للعميل', 'Direct to client') }[value] ?? value.replaceAll('_', ' '));
  const custodyType = (value: string) => ({ COLLECTION: text('تحصيل نقدي', 'Cash collection'), HANDOVER: text('توريد / تسليم', 'Handover'), REVERSAL: text('عكس حركة', 'Reversal'), SHORTAGE: text('عجز', 'Shortage'), OVERAGE: text('زيادة', 'Overage'), ADJUSTMENT: text('تسوية', 'Adjustment') }[value] ?? value.replaceAll('_', ' '));
  return { ar, text, money, number, date, status, channel, custodyType };
}

export function FinanceStatus({ value }: { value: string }) {
  const f = useFinanceText();
  const tone: StatusTone = value === 'POSTED' || value === 'CLEARED' || value === 'OPEN' ? 'success' : value === 'PENDING_APPROVAL' || value === 'SOFT_CLOSED' || value === 'APPROVED' ? 'warning' : value === 'REVERSED' || value === 'CLOSED' ? 'danger' : 'neutral';
  return <StatusBadge dot tone={tone}>{f.status(value)}</StatusBadge>;
}

export function FinanceKpi({ label, value, hint, tone = 'blue' }: { label: string; value: string; hint?: string; tone?: 'blue' | 'green' | 'amber' | 'red' }) {
  const accents = { blue: 'from-mis-primary to-mis-blue', green: 'from-emerald-600 to-emerald-400', amber: 'from-amber-600 to-amber-400', red: 'from-rose-600 to-rose-400' };
  return <article className="relative overflow-hidden rounded-2xl border border-mis-border bg-white p-5 shadow-sm"><span className={`absolute inset-x-0 top-0 h-1 bg-gradient-to-r ${accents[tone]}`} /><p className="text-sm font-semibold text-slate-500">{label}</p><p className="mt-3 text-2xl font-bold tracking-tight text-mis-navy" data-bidi="ltr">{value}</p>{hint && <p className="mt-2 text-xs text-slate-400">{hint}</p>}</article>;
}
