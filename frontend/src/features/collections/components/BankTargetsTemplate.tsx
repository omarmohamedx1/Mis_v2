import { CheckCircle2, CircleHelp, Layers3, PieChart, ShieldCheck, Target } from 'lucide-react';
import type { ReactNode } from 'react';
import { targetTotal, validTargetNumber, type TargetDraft, type TargetField } from '../bankTargetDraft';
import type { BankTargets } from '../types/bankTargets';
import { westernDigits } from '../../../components/forms/normalizeDigits';

interface Props {
  draft: TargetDraft;
  editing: boolean;
  busy: boolean;
  preview: boolean;
  ar: boolean;
  scores?: BankTargets['scores'];
  onChange: (draft: TargetDraft) => void;
  t: (key: string) => string;
}

export function BankTargetsTemplate({ draft, editing, busy, preview, ar, scores, onChange, t }: Props) {
  const format = (value: number | null | undefined, digits = 0) => value == null ? '—' : new Intl.NumberFormat(ar ? 'ar-EG' : 'en-GB', {
    minimumFractionDigits: digits, maximumFractionDigits: 2,
  }).format(value);
  const total = targetTotal(draft.rows.map(row => row.weight));
  const classTotal = targetTotal([draft.activeClassWeight, draft.moreClassWeight]);
  const overTarget = [1, 2, 3, 4, 5].some(bucket => (targetTotal(draft.rows.filter(row => row.bucket === bucket).map(row => row.targetPercent)) ?? 0) > 100);
  const field = (value: string, label: string, change: (value: string) => void, percent = false, digits = 0) => (
    <span className={`inline-flex max-w-full items-center justify-center gap-1 tabular-nums ${percent ? 'font-bold text-mis-primary' : 'font-semibold text-mis-navy'}`} dir="ltr">
      {editing ? <input aria-label={label} aria-invalid={!validTargetNumber(value)} inputMode="decimal" type="text" value={value} disabled={busy}
        onChange={event => change(westernDigits(event.target.value).replace('٫', '.'))}
        className={`h-10 min-w-0 w-16 rounded-lg border bg-white px-1.5 text-center text-sm outline-none focus:border-mis-primary focus:ring-2 focus:ring-mis-primary/15 sm:w-20 ${validTargetNumber(value) ? 'border-mis-control' : 'border-amber-400'}`} />
        : <span>{value === '' ? '—' : format(Number(value), digits)}</span>}
      {percent && <span className="text-xs font-medium opacity-70">%</span>}
    </span>
  );
  const fixedField = (key: TargetField, label: string, percent = false, digits = 0) => field(draft[key], label,
    value => onChange({ ...draft, [key]: value }), percent, digits);
  const card = (title: string, icon: ReactNode, children: ReactNode) => <section className="min-w-0 overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
    <div className="flex items-center gap-3 border-b border-slate-100 px-4 py-4 sm:px-5"><span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-mis-pale text-mis-primary">{icon}</span><h2 className="text-base font-bold leading-snug text-mis-navy">{title}</h2></div>{children}
  </section>;

  return <div className="space-y-5" data-testid="targets-template" data-mode={preview ? 'preview' : editing ? 'editing' : 'saved'}>
    <section className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-4 sm:px-5">
        <div className="flex items-center gap-3"><span className="flex h-10 w-10 items-center justify-center rounded-xl bg-mis-pale text-mis-primary"><Layers3 className="h-5 w-5" /></span><div><h2 className="text-lg font-bold text-mis-navy">{t('quantitative')}</h2><p className="mt-0.5 text-xs text-slate-500">{t('matrixDescription')}</p></div></div>
        <span className="rounded-lg bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-600">{t(preview ? 'referenceOnly' : 'monthlyTargets')}</span>
      </div>
      <table className="w-full table-fixed text-sm" aria-label={t('quantitative')}>
        <colgroup><col className="w-[15%] sm:w-[12%]" /><col className="w-[43%] sm:w-[48%]" /><col className="w-[23%] sm:w-[20%]" /><col className="w-[19%] sm:w-[20%]" /></colgroup>
        <thead><tr className="border-y border-mis-border bg-slate-100/80 text-[11px] uppercase tracking-wide text-slate-500 sm:text-xs">
          <th className="px-2 py-3 text-center" scope="col">BKT</th><th className="px-3 py-3 text-start sm:px-5" scope="col">{t('movement')}</th>
          <th className="px-1 py-3 text-center text-mis-primary" scope="col">{t('target')}</th><th className="px-1 py-3 text-center" scope="col">{t('weight')}</th>
        </tr></thead>
        {[1, 2, 3, 4, 5].map(bucket => {
          const rows = draft.rows.map((row, index) => ({ ...row, index })).filter(row => row.bucket === bucket);
          return <tbody key={bucket} data-bucket={bucket} className={`border-b border-mis-border ${bucket % 2 === 0 ? 'bg-slate-50/80' : 'bg-white'}`}>
            {rows.map((row, i) => <tr key={row.movement} className="border-b border-slate-100 last:border-0">
              {i === 0 && <th rowSpan={rows.length} scope="rowgroup" className="border-b border-white/20 bg-mis-navy px-1 text-center text-white"><span className="block text-[10px] font-medium tracking-wider text-white/65 sm:text-xs">BKT</span><span className="mt-1 block text-lg font-bold sm:text-2xl" dir="ltr">{bucket}</span></th>}
              <th scope="row" className="px-3 py-3 text-start text-xs font-medium leading-5 text-slate-600 sm:px-5 sm:text-sm">{t(row.movement)}</th>
              {(['targetPercent', 'weight'] as const).map(key => <td key={key} className={`px-1 py-2.5 text-center ${key === 'targetPercent' ? 'bg-mis-pale/25' : ''}`}>
                {field(row[key], `BKT ${bucket} ${t(row.movement)} ${t(key === 'weight' ? 'weight' : 'target')}`,
                  value => onChange({ ...draft, rows: draft.rows.map((r, idx) => idx === row.index ? { ...r, [key]: value } : r) }), key === 'targetPercent', key === 'weight' ? 1 : 0)}
              </td>)}
            </tr>)}
          </tbody>;
        })}
      </table>
      <div className="flex flex-wrap items-center justify-between gap-3 bg-slate-50 px-4 py-4 sm:px-5" aria-live="polite">
        <div><p className="text-sm font-bold text-mis-navy">{t('total')}</p><p className="mt-1 text-xs text-slate-500">{t('weightRequired')}</p></div>
        <span className={`inline-flex items-center gap-2 rounded-xl border px-4 py-2 ${total === 100 ? 'border-emerald-200 bg-emerald-50 text-emerald-800' : 'border-amber-200 bg-amber-50 text-amber-900'}`}>
          {total === 100 && <CheckCircle2 className="h-4 w-4" />}<b className="text-xl tabular-nums" data-testid="quantitative-total">{format(total, 2)}</b><span className="text-xs opacity-70">/ 100</span>
        </span>
      </div>
      {editing && total !== 100 && <p role="alert" className="border-t border-amber-100 bg-amber-50 px-5 py-3 text-xs text-amber-900">{t('referenceWarning')}</p>}
    </section>

    <div className="grid items-stretch gap-5 lg:grid-cols-3">
      {card(t('more'), <Target className="h-4 w-4" />, <div className="p-4 sm:p-5">
        <div className="rounded-xl border border-blue-100 bg-mis-pale/50 p-5 text-center"><p className="mb-3 text-xs font-semibold text-mis-primary">{t('moreTargetPercent')}</p><div className="text-4xl font-bold">{fixedField('moreTargetPercent', t('moreTargetPercent'), true, 1)}</div><p className="mt-3 text-xs text-slate-500">{t('moreDescription')}</p></div>
        <div className="mt-4 flex items-center justify-between rounded-xl bg-slate-50 px-4 py-3"><span className="text-sm text-slate-600">{t('moreWeight')}</span><span className="text-lg">{fixedField('moreWeight', t('moreWeight'))}</span></div>
      </div>)}

      {card(t('distribution'), <PieChart className="h-4 w-4" />, <>
        <div className="flex h-2 bg-slate-100" aria-hidden="true"><span className="bg-mis-navy" style={{ width: `${Math.min(100, Math.max(0, Number(draft.activeClassWeight) || 0))}%` }} /><span className="bg-mis-sky" style={{ width: `${Math.min(100, Math.max(0, Number(draft.moreClassWeight) || 0))}%` }} /></div>
        <table className="w-full table-fixed text-sm"><thead className="text-xs text-slate-500"><tr>{['class', 'weight', 'score'].map(key => <th scope="col" key={key} className="px-2 py-3 text-center">{t(key)}</th>)}</tr></thead><tbody>
          {(['ACTIVE', 'MORE'] as const).map(cls => { const key = cls === 'ACTIVE' ? 'activeClassWeight' : 'moreClassWeight'; return <tr key={cls} className="border-t border-slate-100"><th scope="row" className="px-2 py-3 text-center text-xs font-bold text-mis-navy"><span className={`me-1.5 inline-block h-2 w-2 rounded-full ${cls === 'ACTIVE' ? 'bg-mis-navy' : 'bg-mis-sky'}`} />{cls}</th><td className="px-1 py-3 text-center">{fixedField(key, `${cls} ${t('weight')} %`, true)}</td><td className="px-1 py-3 text-center font-bold tabular-nums text-mis-navy">{format(editing || preview ? null : cls === 'ACTIVE' ? scores?.active : scores?.more, 2)}</td></tr>; })}
        </tbody></table>
        <div className="mx-4 flex items-center justify-between border-t border-slate-100 py-3 text-xs" aria-live="polite"><span className="text-slate-500">{t('classesTotal')}</span><b className={classTotal === 100 ? 'text-emerald-700' : 'text-amber-800'}>{format(classTotal)}%</b></div>
        <p className="px-4 pb-4 text-xs leading-5 text-slate-500">{t('scoreHint')}</p>
        {!editing && !preview && scores?.issues.map(issue => <p className="px-4 pb-3 text-xs leading-5 text-amber-800" key={issue}>{t(issue)}</p>)}
      </>)}

      {card(t('woff'), <ShieldCheck className="h-4 w-4" />, <div className="p-4 sm:p-5">
        <div className="space-y-3">{(['wOffBelow90', 'wOffAbove90'] as const).map(key => <div key={key} className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-mis-border bg-slate-50 px-3 py-3"><span className="text-xs font-medium text-slate-600">{t(key)}</span><span className="text-xl">{fixedField(key, t(key), true)}</span></div>)}</div>
        <div className="mt-4 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-xs leading-5 text-amber-900"><CircleHelp className="mt-0.5 h-4 w-4 shrink-0" /><p>{t('boundary')}</p></div>
      </div>)}
    </div>
    {!preview && <details className="rounded-xl border border-mis-border bg-white px-4 py-3 text-xs leading-6 text-slate-500"><summary className="cursor-pointer font-semibold text-mis-navy">{t('calculationNotes')}</summary><p className="mt-2">{t('integration')}</p>{overTarget && <p className="mt-1 text-amber-800">{t('negativeStab')}</p>}</details>}
  </div>;
}
