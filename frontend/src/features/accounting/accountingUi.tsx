import { StatusBadge, type StatusTone } from '../../components/common/StatusBadge';
import { useLocalization } from '../../context/LocalizationContext';

export function useAccountingText() {
  const { language } = useLocalization();
  const ar = language === 'ar';
  const text = (arabic: string, english: string) => (ar ? arabic : english);
  const money = (value: number) => new Intl.NumberFormat(ar ? 'ar-EG' : 'en-EG', { style: 'currency', currency: 'EGP', minimumFractionDigits: 2 }).format(value);
  const number = (value: number) => new Intl.NumberFormat(ar ? 'ar-EG' : 'en-EG').format(value);
  const monthLabel = (year: number, month: number) => new Intl.DateTimeFormat(ar ? 'ar-EG' : 'en-GB', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1));
  const status = (value: string) =>
    ({
      DRAFT: text('مسودة', 'Draft'),
      PENDING_REVIEW: text('بانتظار المراجعة', 'Pending review'),
      PENDING: text('قيد الانتظار', 'Pending'),
      APPROVED: text('معتمد', 'Approved'),
      PAID: text('تم الصرف', 'Paid'),
      REJECTED: text('مرفوض', 'Rejected'),
      CANCELLED: text('ملغي', 'Cancelled'),
    }[value] ?? value.replaceAll('_', ' '));
  return { ar, text, money, number, monthLabel, status };
}

export function AccountingStatus({ value }: { value: string }) {
  const a = useAccountingText();
  const tone: StatusTone =
    value === 'PAID' || value === 'APPROVED'
      ? 'success'
      : value === 'PENDING_REVIEW' || value === 'PENDING'
        ? 'warning'
        : value === 'CANCELLED' || value === 'REJECTED'
          ? 'danger'
          : 'neutral';
  return <StatusBadge dot tone={tone}>{a.status(value)}</StatusBadge>;
}

export function AccountingKpi({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <article className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
      <p className="text-sm font-semibold text-slate-500">{label}</p>
      <p className="mt-3 text-2xl font-bold tracking-tight text-mis-navy" data-bidi="ltr">{value}</p>
      {hint ? <p className="mt-2 text-xs text-slate-400">{hint}</p> : null}
    </article>
  );
}
