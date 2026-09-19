import { StatusBadge, type StatusTone } from '../../components/common/StatusBadge';
import { useLocalization } from '../../context/LocalizationContext';
import { LEGAL_ACTIONS, LEGAL_STAGES } from './types/legal';

export function useLegalText() {
  const { language } = useLocalization();
  const ar = language === 'ar';
  const text = (arabic: string, english: string) => (ar ? arabic : english);
  const money = (value: number) =>
    new Intl.NumberFormat(ar ? 'ar-EG' : 'en-EG', { style: 'currency', currency: 'EGP', minimumFractionDigits: 0, maximumFractionDigits: 0 }).format(value);
  const number = (value: number) => new Intl.NumberFormat(ar ? 'ar-EG' : 'en-EG').format(value);
  const date = (value?: string | null) => {
    if (!value) return '—';
    const parsed = value.length <= 10 ? new Date(`${value}T00:00:00`) : new Date(value);
    return new Intl.DateTimeFormat(ar ? 'ar-EG' : 'en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(parsed);
  };
  const dateTime = (value: string) =>
    new Intl.DateTimeFormat(ar ? 'ar-EG' : 'en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value));

  const stage = (value: string) =>
    ({
      INTAKE: text('استلام', 'Intake'),
      NOTICE: text('إنذار', 'Notice'),
      COURT: text('محكمة', 'Court'),
      HEARING: text('جلسة', 'Hearing'),
      JUDGMENT: text('حكم', 'Judgment'),
      SETTLEMENT: text('تسوية', 'Settlement'),
      RETURNED: text('معاد للتحصيل', 'Returned'),
    }[value] ?? value.replaceAll('_', ' '));

  const action = (value: string) =>
    ({
      NOTE: text('ملاحظة', 'Note'),
      NOTICE: text('إنذار / إعلان', 'Notice'),
      HEARING: text('جلسة', 'Hearing'),
      JUDGMENT: text('حكم', 'Judgment'),
      SETTLEMENT: text('تسوية', 'Settlement'),
      RETURN: text('إعادة للتحصيل', 'Return to collections'),
      OPENED: text('فتح الملف', 'File opened'),
      REFERRED: text('تحويل للشؤون القانونية', 'Referred to legal'),
    }[value] ?? value.replaceAll('_', ' '));

  const collectionStatus = (value: string) =>
    ({
      ACTIVE: text('نشطة', 'Active'),
      ON_HOLD: text('معلقة', 'On hold'),
      SETTLED: text('تمت التسوية', 'Settled'),
      CLOSED: text('مغلقة', 'Closed'),
      LEGAL: text('قانونية', 'Legal'),
      WRITE_OFF: text('مشطوبة', 'Written off'),
    }[value] ?? value.replaceAll('_', ' '));

  return { ar, text, money, number, date, dateTime, stage, action, collectionStatus, stages: LEGAL_STAGES, actions: LEGAL_ACTIONS };
}

export function LegalStageBadge({ value }: { value: string }) {
  const l = useLegalText();
  const tone: StatusTone =
    value === 'INTAKE' ? 'warning'
      : value === 'NOTICE' || value === 'COURT' ? 'info'
        : value === 'HEARING' ? 'purple'
          : value === 'SETTLEMENT' || value === 'JUDGMENT' ? 'success'
            : value === 'RETURNED' ? 'neutral'
              : 'info';
  return <StatusBadge dot tone={tone}>{l.stage(value)}</StatusBadge>;
}

export function LegalKpi({ label, value, hint, tone = 'amber' }: { label: string; value: string; hint?: string; tone?: 'amber' | 'blue' | 'green' | 'red' }) {
  const accents = {
    amber: 'from-amber-700 to-amber-400',
    blue: 'from-mis-primary to-mis-blue',
    green: 'from-emerald-600 to-emerald-400',
    red: 'from-rose-600 to-rose-400',
  };
  return (
    <article className="relative overflow-hidden rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
      <span className={`absolute inset-x-0 top-0 h-1 bg-gradient-to-r ${accents[tone]}`} />
      <p className="text-sm font-semibold text-slate-500">{label}</p>
      <p className="mt-3 text-2xl font-bold tracking-tight text-mis-navy" data-bidi="ltr">{value}</p>
      {hint ? <p className="mt-2 text-xs text-slate-400">{hint}</p> : null}
    </article>
  );
}

export function LegalPipeline({ current }: { current?: string | null }) {
  const l = useLegalText();
  const steps = [
    { key: 'INTAKE', label: l.stage('INTAKE') },
    { key: 'NOTICE', label: l.stage('NOTICE') },
    { key: 'COURT', label: l.stage('COURT') },
    { key: 'HEARING', label: l.stage('HEARING') },
    { key: 'OUTCOME', label: l.text('حكم / تسوية / إعادة', 'Judgment / settlement / return') },
  ];
  const order = current === 'RETURNED' || current === 'SETTLEMENT' || current === 'JUDGMENT' ? 4
    : current === 'HEARING' ? 3
      : current === 'COURT' ? 2
        : current === 'NOTICE' ? 1
          : current === 'INTAKE' ? 0
            : -1;
  return (
    <ol className="grid gap-2 sm:grid-cols-5">
      {steps.map((step, index) => {
        const active = order >= index;
        return (
          <li key={step.key} className={`rounded-2xl border px-3 py-3 text-sm font-bold ${active ? 'border-amber-700 bg-amber-50 text-amber-950' : 'border-mis-border bg-white text-slate-400'}`}>
            <span className="me-2 text-xs" data-bidi="ltr">{index + 1}</span>
            {step.label}
          </li>
        );
      })}
    </ol>
  );
}
