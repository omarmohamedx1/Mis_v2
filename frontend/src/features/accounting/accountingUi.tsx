import { StatusBadge, type StatusTone } from '../../components/common/StatusBadge';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { useLocalization } from '../../context/LocalizationContext';

export function useAccountingText() {
  const { language } = useLocalization();
  const ar = language === 'ar';
  const text = (arabic: string, english: string) => (ar ? arabic : english);
  const money = (value: number) => new Intl.NumberFormat(ar ? 'ar-EG' : 'en-EG', { style: 'currency', currency: 'EGP', minimumFractionDigits: 2 }).format(value);
  const number = (value: number) => new Intl.NumberFormat(ar ? 'ar-EG' : 'en-EG').format(value);
  const monthName = (month: number) => new Intl.DateTimeFormat(ar ? 'ar-EG' : 'en-GB', { month: 'long' }).format(new Date(2000, month - 1, 1));
  const monthLabel = (year: number, month: number) => new Intl.DateTimeFormat(ar ? 'ar-EG' : 'en-GB', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1));
  const date = (value: string) => new Intl.DateTimeFormat(ar ? 'ar-EG' : 'en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(`${value.slice(0, 10)}T00:00:00`));
  const status = (value: string) =>
    ({
      DRAFT: text('مسودة', 'Draft'),
      PENDING_REVIEW: text('بانتظار المراجعة', 'Pending review'),
      PENDING: text('قيد الانتظار', 'Pending'),
      APPROVED: text('معتمد', 'Approved'),
      PAID: text('تم الصرف', 'Paid'),
      REJECTED: text('مرفوض', 'Rejected'),
      CANCELLED: text('ملغي', 'Cancelled'),
      ACTIVE: text('نشطة', 'Active'),
      INACTIVE: text('غير نشطة', 'Inactive'),
    }[value] ?? value.replaceAll('_', ' '));
  return { ar, text, money, number, monthName, monthLabel, date, status };
}

export function AccountingMonthSelect({ value, onChange }: { value: number; onChange: (month: number) => void }) {
  const a = useAccountingText();
  return (
    <ProfessionalSelect aria-label={a.text('الشهر', 'Month')} className="min-h-11 min-w-[10rem] rounded-xl border border-mis-border bg-white px-3 text-sm" value={String(value)} onChange={(event) => onChange(Number(event.target.value))}>
      {Array.from({ length: 12 }, (_, index) => index + 1).map((month) => <option key={month} value={month}>{a.monthName(month)}</option>)}
    </ProfessionalSelect>
  );
}

export function AccountingStatus({ value }: { value: string }) {
  const a = useAccountingText();
  const tone: StatusTone =
    value === 'PAID' || value === 'APPROVED' || value === 'ACTIVE'
      ? 'success'
      : value === 'PENDING_REVIEW' || value === 'PENDING'
        ? 'warning'
        : value === 'CANCELLED' || value === 'REJECTED' || value === 'INACTIVE'
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
