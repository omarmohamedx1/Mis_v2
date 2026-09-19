import { StatusBadge, type StatusTone } from '../../components/common/StatusBadge';
import { useLocalization } from '../../context/LocalizationContext';

export function useDataEntryText() {
  const { language } = useLocalization();
  const ar = language === 'ar';
  const text = (arabic: string, english: string) => (ar ? arabic : english);
  const money = (value: number, currency = 'EGP') =>
    new Intl.NumberFormat(ar ? 'ar-EG' : 'en-EG', { style: 'currency', currency, minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
  const number = (value: number) => new Intl.NumberFormat(ar ? 'ar-EG' : 'en-EG').format(value);
  const dateTime = (value: string) =>
    new Intl.DateTimeFormat(ar ? 'ar-EG' : 'en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value));

  const batchStatus = (value: string) =>
    ({
      DRAFT: text('مسودة', 'Draft'),
      SUBMITTED: text('مُرسل للمراجعة', 'Submitted'),
      ACCEPTED: text('مقبول', 'Accepted'),
      DISTRIBUTED: text('مُرسل للتوزيع', 'Distributed'),
      REJECTED: text('مرفوض', 'Rejected'),
    }[value] ?? value.replaceAll('_', ' '));

  const rowStatus = (value: string) =>
    ({
      READY: text('جاهز', 'Ready'),
      EXISTING_CUSTOMER: text('عميل موجود', 'Existing'),
      DUPLICATE_IN_FILE: text('مكرر في الملف', 'Duplicate'),
      INVALID_NATIONAL_ID: text('رقم قومي غير صالح', 'Invalid National ID'),
      INVALID_MOBILE: text('موبايل غير صالح', 'Invalid Mobile'),
      ERROR: text('خطأ', 'Error'),
    }[value] ?? value.replaceAll('_', ' '));

  const source = (value: string) =>
    ({
      MANUAL: text('يدوي', 'Manual'),
      IMPORTED: text('مستورد', 'Imported'),
    }[value] ?? value.replaceAll('_', ' '));

  const caseStatus = (value: string) =>
    ({
      ACTIVE: text('نشطة', 'Active'),
      CLOSED: text('مغلقة', 'Closed'),
      LEGAL: text('قانونية', 'Legal'),
    }[value] ?? value.replaceAll('_', ' '));

  return { ar, text, money, number, dateTime, batchStatus, rowStatus, source, caseStatus };
}

export function DataEntryBatchStatus({ value }: { value: string }) {
  const d = useDataEntryText();
  const tone: StatusTone =
    value === 'ACCEPTED' || value === 'DISTRIBUTED'
      ? 'success'
      : value === 'SUBMITTED'
        ? 'warning'
        : value === 'REJECTED'
          ? 'danger'
          : 'neutral';
  return (
    <StatusBadge dot tone={tone}>
      {d.batchStatus(value)}
    </StatusBadge>
  );
}

export function DataEntryRowStatus({ value }: { value: string }) {
  const d = useDataEntryText();
  const tone: StatusTone =
    value === 'READY'
      ? 'success'
      : value === 'EXISTING_CUSTOMER'
        ? 'warning'
        : value === 'DUPLICATE_IN_FILE' || value.startsWith('INVALID_') || value === 'ERROR'
          ? 'danger'
          : 'neutral';
  return (
    <StatusBadge tone={tone}>
      {d.rowStatus(value)}
    </StatusBadge>
  );
}

export function DataEntryKpi({ label, value, hint, tone = 'blue' }: { label: string; value: string; hint?: string; tone?: 'blue' | 'green' | 'amber' | 'red' }) {
  const accents = {
    blue: 'from-mis-primary to-mis-blue',
    green: 'from-emerald-600 to-emerald-400',
    amber: 'from-amber-600 to-amber-400',
    red: 'from-rose-600 to-rose-400',
  };
  return (
    <article className="relative overflow-hidden rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
      <span className={`absolute inset-x-0 top-0 h-1 bg-gradient-to-r ${accents[tone]}`} />
      <p className="text-sm font-semibold text-slate-500">{label}</p>
      <p className="mt-3 text-2xl font-bold tracking-tight text-mis-navy" data-bidi="ltr">
        {value}
      </p>
      {hint ? <p className="mt-2 text-xs text-slate-400">{hint}</p> : null}
    </article>
  );
}

export function DataEntryPipeline({ current }: { current?: string | null }) {
  const d = useDataEntryText();
  const steps = [
    { key: 'enter', label: d.text('إدخال', 'Enter') },
    { key: 'SUBMITTED', label: d.text('مراجعة التحصيل', 'Collections review') },
    { key: 'ACCEPTED', label: d.text('حالات التحصيل', 'Collection cases') },
    { key: 'DISTRIBUTED', label: d.text('التوزيع', 'Distribution') },
  ];
  const order = current === 'REJECTED' ? 1 : current === 'DISTRIBUTED' ? 3 : current === 'ACCEPTED' ? 2 : current === 'SUBMITTED' ? 1 : current ? 0 : -1;
  return (
    <ol className="grid gap-2 sm:grid-cols-4">
      {steps.map((step, index) => {
        const active = order >= index;
        return (
          <li key={step.key} className={`rounded-2xl border px-3 py-3 text-sm font-bold ${active ? 'border-mis-blue bg-mis-pale text-mis-navy' : 'border-mis-border bg-white text-slate-400'}`}>
            <span className="me-2 text-xs" data-bidi="ltr">{index + 1}</span>
            {step.label}
          </li>
        );
      })}
    </ol>
  );
}
