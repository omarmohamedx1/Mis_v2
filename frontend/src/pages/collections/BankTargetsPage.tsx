import { useEffect, useState, type ReactNode } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { isAxiosError } from 'axios';
import { CheckCircle2, ClipboardCheck, Copy, FilePlus2, LockKeyhole, Pencil, Save, Target } from 'lucide-react';
import { Button } from '../../components/common/Button';
import { PageHeader } from '../../components/common/PageHeader';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { useActionDialog } from '../../components/common/useActionDialog';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { westernDigits } from '../../components/forms/normalizeDigits';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { bankTargetsText } from '../../features/collections/localization/bankTargetsText';
import { collectionsService } from '../../features/collections/services/collectionsService';
import { bankTargetsService } from '../../features/collections/services/bankTargetsService';
import { parseTargetDraft, targetDraft, targetTotal, validTargetNumber, validTargetPeriod, type TargetDraft, type TargetField } from '../../features/collections/bankTargetDraft';
import type { BankTargets, BankTargetScope, BankTargetAudit } from '../../features/collections/types/bankTargets';
import type { BankDirectoryItem } from '../../features/collections/types/collections';
import type { CollectionScorePortfolio } from '../../features/collections/types/score';

const control = 'min-h-11 w-full rounded-xl border border-mis-control bg-white px-3 py-2 text-sm text-mis-navy outline-none focus:border-mis-primary focus:ring-2 focus:ring-mis-primary/15 disabled:bg-slate-50 disabled:text-slate-600';
const cell = 'px-4 py-3 text-start';
function cairoMonth() {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Cairo', year: 'numeric', month: '2-digit' }).formatToParts(new Date());
  return `${parts.find(x => x.type === 'year')!.value}-${parts.find(x => x.type === 'month')!.value}`;
}

export function BankTargetsPage() {
  const { language, isRtl, ct } = useCollectionsLocalization();
  const ar = language === 'ar';
  const t = (key: string) => bankTargetsText(key, ar);
  const [params] = useSearchParams();
  const [scope, setScope] = useState<BankTargetScope>(() => ({ organizationId: params.get('organizationId') ?? '', portfolioId: params.get('portfolioId') ?? '',
    portfolioType: params.get('portfolioType') === 'WO' ? 'WO' : 'ACT', period: params.get('reportingPeriod') ?? cairoMonth() }));
  const [organizations, setOrganizations] = useState<BankDirectoryItem[]>([]);
  const [portfolios, setPortfolios] = useState<CollectionScorePortfolio[]>([]);
  const [directoryLoading, setDirectoryLoading] = useState(true);
  const [portfolioLoading, setPortfolioLoading] = useState(false);
  const [directoryError, setDirectoryError] = useState(false);
  const [portfolioError, setPortfolioError] = useState(false);
  const [data, setData] = useState<BankTargets | null>(null);
  const [draft, setDraft] = useState<TargetDraft>(() => targetDraft());
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [retry, setRetry] = useState(0);
  const [audit, setAudit] = useState<BankTargetAudit | null>(null);
  const { confirm, dialog } = useActionDialog();
  const configured = data?.configuration;
  const validScope = !!scope.organizationId && !!scope.portfolioId && validTargetPeriod(scope.period);
  const total = targetTotal(draft.rows.map(row => row.weight));
  const classTotal = targetTotal([draft.activeClassWeight, draft.moreClassWeight]);
  const parsed = parseTargetDraft(draft);
  const canReview = !!parsed && total === 100 && classTotal === 100 && parsed.moreWeight === 100
    && parsed.moreTargetPercent > 0 && parsed.rows.every(row => row.targetPercent > 0);
  const overTarget = [1, 2, 3, 4, 5].some(bucket => (targetTotal(draft.rows.filter(row => row.bucket === bucket).map(row => row.targetPercent)) ?? 0) > 100);
  const number = (value: number | null | undefined) => value == null ? '—' : new Intl.NumberFormat(ar ? 'ar-EG' : 'en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
  const date = (value: string) => new Intl.DateTimeFormat(ar ? 'ar-EG' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Africa/Cairo' }).format(new Date(value));
  const name = (item: { nameArabic: string; nameEnglish: string }) => ar ? item.nameArabic || item.nameEnglish : item.nameEnglish || item.nameArabic;

  useEffect(() => {
    let active = true; setDirectoryLoading(true); setDirectoryError(false);
    Promise.all([collectionsService.banks(), collectionsService.installmentCompanies()]).then(([banks, companies]) => {
      if (active) setOrganizations([...banks, ...companies]);
    }).catch(() => { if (active) setDirectoryError(true); }).finally(() => { if (active) setDirectoryLoading(false); });
    return () => { active = false; };
  }, [retry]);

  useEffect(() => {
    let active = true; setPortfolios([]); setPortfolioError(false);
    if (!scope.organizationId) { setPortfolioLoading(false); return; }
    setPortfolioLoading(true);
    collectionsService.scorePortfolios(scope.organizationId, scope.portfolioType).then(items => {
      if (active) setPortfolios(items);
    }).catch(() => { if (active) setPortfolioError(true); }).finally(() => { if (active) setPortfolioLoading(false); });
    return () => { active = false; };
  }, [scope.organizationId, scope.portfolioType, retry]);

  useEffect(() => {
    let active = true;
    setData(null); setError(''); setNotice(''); setAudit(null); setDraft(targetDraft()); setEditing(false);
    if (!scope.organizationId || !scope.portfolioId || !validTargetPeriod(scope.period)) { setLoading(false); return; }
    setLoading(true);
    bankTargetsService.get(scope).then(result => {
      if (active) { setData(result); setDraft(targetDraft(result.configuration?.values)); }
    }).catch(e => { if (active) setError(errorKey(e)); }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [scope, retry]);

  function errorKey(e: unknown) {
    const message = isAxiosError(e) ? e.response?.data?.message : null;
    return typeof message === 'string' && message.startsWith('TARGETS_') ? message : 'error';
  }
  async function perform(action: () => Promise<BankTargets>, message: string) {
    setBusy(true); setError(''); setNotice('');
    try { const result = await action(); setData(result); setDraft(targetDraft(result.configuration?.values)); setEditing(false); setNotice(message); }
    catch (e) { setError(errorKey(e)); }
    finally { setBusy(false); }
  }
  async function useTemplate() {
    if (!await confirm(t('templateConfirm'))) return;
    setBusy(true); setError('');
    try { setDraft(targetDraft(await bankTargetsService.template())); setEditing(true); setNotice(''); }
    catch (e) { setError(errorKey(e)); } finally { setBusy(false); }
  }
  const change = (key: TargetField, value: string) => setDraft(previous => ({ ...previous, [key]: westernDigits(value).replace('٫', '.') }));
  function input(value: string, label: string, onChange: (value: string) => void) {
    return editing ? <input aria-label={label} aria-invalid={!validTargetNumber(value)} className={`${control} min-w-24 max-w-36 text-end ${!validTargetNumber(value) ? 'border-amber-400' : ''}`}
      dir="ltr" inputMode="decimal" type="text" value={value} disabled={busy} onChange={event => onChange(event.target.value)} />
      : <span className="tabular-nums" dir="ltr">{value === '' ? '—' : number(Number(value))}</span>;
  }
  const block = (title: string, children: ReactNode, description?: string) => <section className="min-w-0 rounded-2xl border border-mis-border bg-white shadow-sm">
    <div className="border-b border-slate-100 px-5 py-4"><h2 className="text-lg font-bold text-mis-navy">{title}</h2>{description && <p className="mt-1 text-sm leading-6 text-slate-500">{description}</p>}</div>{children}</section>;

  return <div dir={isRtl ? 'rtl' : 'ltr'} className="min-w-0 space-y-5">
    <PageHeader title={t('title')} eyebrow={ct('score')} description={t('subtitle')} actions={<Link className="inline-flex items-center gap-2 rounded-xl border border-mis-control bg-white px-4 py-3 text-sm font-semibold text-mis-primary" to="/collections/score"><Target className="h-4 w-4" />{ct('score')}</Link>} />
    {dialog}
    {block(t('selection'), <div className="space-y-4 p-5">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <label className="space-y-2 text-sm font-semibold text-mis-navy"><span>{t('organization')}</span><ProfessionalSelect className={control} value={scope.organizationId} disabled={editing || busy || directoryLoading || directoryError}
          onChange={e => setScope({ ...scope, organizationId: e.target.value, portfolioId: '' })}><option value="">{t('choose')}</option>{organizations.map(item => <option value={item.id} key={item.id}>{name(item)}</option>)}</ProfessionalSelect></label>
        <label className="space-y-2 text-sm font-semibold text-mis-navy"><span>{t('type')}</span><ProfessionalSelect className={control} value={scope.portfolioType} disabled={editing || busy}
          onChange={e => setScope({ ...scope, portfolioType: e.target.value as 'ACT' | 'WO', portfolioId: '' })}><option value="ACT">ACT</option><option value="WO">WO</option></ProfessionalSelect></label>
        <label className="space-y-2 text-sm font-semibold text-mis-navy"><span>{t('portfolio')}</span><ProfessionalSelect className={control} value={scope.portfolioId} disabled={editing || busy || portfolioLoading || !scope.organizationId || portfolioError}
          onChange={e => setScope({ ...scope, portfolioId: e.target.value })}><option value="">{t('choose')}</option>{portfolios.map(item => <option value={item.id} key={item.id}>{name(item)}</option>)}</ProfessionalSelect></label>
        <label className="space-y-2 text-sm font-semibold text-mis-navy"><span>{t('period')}</span><input className={control} type="text" inputMode="numeric" dir="ltr" maxLength={7} placeholder="YYYY-MM" value={scope.period}
          aria-invalid={!validTargetPeriod(scope.period)} disabled={editing || busy} onChange={e => setScope({ ...scope, period: westernDigits(e.target.value) })} /></label>
      </div>
      {!validTargetPeriod(scope.period) && <p role="alert" className="text-sm text-red-700">{t('TARGETS_PERIOD_INVALID')}</p>}
      {scope.organizationId && !portfolioLoading && !portfolioError && portfolios.length === 0 && <p className="text-sm text-slate-500">{t('emptyPortfolio')}</p>}
      {(directoryLoading || portfolioLoading) && <p role="status" className="text-sm text-slate-500">{t('loading')}</p>}
      <div className="flex flex-wrap gap-2 border-t border-slate-100 pt-4">
        {data?.canEdit && <>
          <Button fullWidth={false} variant="outline" leftIcon={<Pencil />} disabled={busy || loading || editing || configured?.status === 'APPROVED'} onClick={() => { setEditing(true); setNotice(''); }}>{configured ? t('edit') : t('create')}</Button>
          <Button fullWidth={false} variant="outline" leftIcon={<Copy />} disabled={busy || loading || editing || !!configured} onClick={async () => { if (await confirm(t('copyConfirm'))) void perform(() => bankTargetsService.copy(scope), 'copied'); }}>{t('copy')}</Button>
          <Button fullWidth={false} variant="secondary" leftIcon={<FilePlus2 />} disabled={busy || loading || configured?.status === 'APPROVED'} onClick={() => void useTemplate()}>{t('template')}</Button>
          <Button fullWidth={false} leftIcon={<Save />} disabled={busy || !editing || !parsed} onClick={() => { if (parsed) void perform(() => bankTargetsService.save(scope, parsed, configured?.version ?? null), 'saved'); }}>{t('save')}</Button>
        </>}
        {editing && <Button fullWidth={false} variant="ghost" disabled={busy} onClick={() => { setDraft(targetDraft(configured?.values)); setEditing(false); setError(''); }}>{t('cancel')}</Button>}
        {validScope && <Link className={`inline-flex items-center rounded-xl px-3 py-2 text-sm font-semibold text-mis-primary ${editing || busy ? 'pointer-events-none opacity-50' : ''}`} aria-disabled={editing || busy}
          tabIndex={editing || busy ? -1 : 0} to={`/collections/score/${scope.organizationId}/${scope.portfolioType.toLowerCase()}/portfolios/${scope.portfolioId}?reportingPeriod=${scope.period}`}>{t('dashboard')} ↗</Link>}
      </div>
    </div>)}

    {(error || directoryError || portfolioError) && <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"><span>{t(error || 'error')}</span><Button fullWidth={false} variant="outline" disabled={busy || editing} onClick={() => setRetry(x => x + 1)}>{t('retry')}</Button></div>}
    {notice && <p role="status" className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800">{t(notice)}</p>}
    {loading && <div role="status" className="flex items-center justify-center gap-3 p-12 text-slate-500"><LoadingSpinner className="h-5 w-5" />{t('loading')}</div>}
    {!validScope && <p className="rounded-2xl border border-dashed border-mis-control bg-slate-50 p-8 text-center text-sm text-slate-500">{t('selectScope')}</p>}
    {data && !loading && <>
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-mis-border bg-white p-4">
        <div className="flex items-center gap-3"><span className={`rounded-full px-3 py-1 text-xs font-bold ${configured?.status === 'APPROVED' ? 'bg-emerald-50 text-emerald-800' : 'bg-amber-50 text-amber-900'}`}>{t(editing ? 'DRAFT' : configured?.status ?? 'MISSING')}</span>
          {configured && <span className="text-xs text-slate-500">{t('updated')}: {date(configured.updatedAt)}</span>}</div>
        {data.canApprove && configured && configured.status !== 'APPROVED' && <div className="flex flex-wrap gap-2">
          <Button fullWidth={false} variant="outline" leftIcon={<ClipboardCheck />} disabled={busy || editing || !canReview || configured.status === 'REVIEWED'} onClick={() => void perform(() => bankTargetsService.review(scope, configured.version, false), 'reviewed')}>{t('review')}</Button>
          <Button fullWidth={false} leftIcon={<CheckCircle2 />} disabled={busy || editing || !canReview || configured.status !== 'REVIEWED'} onClick={async () => { if (await confirm(t('approveConfirm'))) void perform(() => bankTargetsService.review(scope, configured.version, true), 'approved'); }}>{t('approve')}</Button>
        </div>}
      </div>
      {!configured && !editing && <p role="status" className="rounded-xl bg-mis-pale p-4 text-sm text-mis-navy">{t('missing')}</p>}
      {!data.canEdit && <p className="text-sm text-slate-500">{t('readOnly')}</p>}
      {editing && <p role="status" className="rounded-xl border border-blue-200 bg-blue-50 p-4 text-sm text-blue-900">{t('unsaved')}{!parsed && <span className="mt-1 block">{t('TARGETS_NUMBER_INVALID')}</span>}</p>}
      {configured?.status === 'APPROVED' && <p className="flex items-center gap-2 text-sm text-emerald-800"><LockKeyhole className="h-4 w-4" />{t('immutable')}</p>}
      {total !== null && total !== 100 && <p role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">{t('referenceWarning')}</p>}
      {block(t('quantitative'), <>
        <div className="overflow-x-auto"><table className="w-full min-w-[560px] text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr>{['BKT', t('movement'), t('target'), t('weight')].map(label => <th key={label} scope="col" className={cell}>{label}</th>)}</tr></thead>
          <tbody className="divide-y divide-slate-100">{draft.rows.map((row, index) => <tr key={`${row.bucket}-${row.movement}`} className={row.movement === 'FWD' ? 'border-t border-mis-border' : ''}>
            <th scope="row" className={`${cell} font-semibold text-mis-navy`}><span dir="ltr">BKT {row.bucket}</span></th><td className={`${cell} text-slate-600`}>{t(row.movement)}</td>
            {(['targetPercent', 'weight'] as const).map(key => <td className={cell} key={key}>{input(row[key], `BKT ${row.bucket} ${t(row.movement)} ${t(key === 'weight' ? 'weight' : 'target')}`, value => setDraft(old => ({ ...old, rows: old.rows.map((r, i) => i === index ? { ...r, [key]: westernDigits(value).replace('٫', '.') } : r) })))}</td>)}
          </tr>)}</tbody>
        </table></div><div className="flex flex-wrap items-center justify-between gap-3 border-t border-mis-border bg-slate-50 px-5 py-4 text-sm font-bold"><span>{t('total')}</span><span className={total === 100 ? 'text-emerald-700' : 'text-amber-800'} aria-live="polite">{number(total)} / 100.00</span></div><div className="space-y-2 px-5 py-4 text-xs leading-6 text-slate-500"><p>{t('totalsHint')}</p>{overTarget && <p className="text-amber-800">{t('negativeStab')}</p>}</div>
      </>)}
      <div className="grid items-start gap-5 xl:grid-cols-2">
        {block(t('more'), <div className="grid gap-5 p-5 sm:grid-cols-2">{(['moreTargetPercent', 'moreWeight'] as const).map(key => <div key={key}><p className="mb-2 text-sm font-medium text-slate-600">{t(key)}</p>{input(draft[key], t(key), value => change(key, value))}</div>)}</div>)}
        {block(t('distribution'), <><div className="overflow-x-auto"><table className="w-full text-sm"><thead className="bg-slate-50 text-xs text-slate-500"><tr>{['class', 'weight', 'score'].map(key => <th scope="col" key={key} className={cell}>{t(key)}</th>)}</tr></thead>
          <tbody className="divide-y divide-slate-100">{(['ACTIVE', 'MORE'] as const).map(cls => { const key = cls === 'ACTIVE' ? 'activeClassWeight' : 'moreClassWeight'; return <tr key={cls}><th scope="row" className={`${cell} text-mis-navy`}>{cls}</th><td className={cell}>{input(draft[key], `${cls} ${t('weight')} %`, value => change(key, value))}<span className="ms-1 text-slate-400">%</span></td><td className={`${cell} font-semibold tabular-nums`}>{number(editing ? null : cls === 'ACTIVE' ? data.scores.active : data.scores.more)}</td></tr>; })}</tbody>
          <tfoot className="bg-slate-50"><tr><th className={cell}>{t('classesTotal')}</th><td colSpan={2} className={`${cell} font-bold ${classTotal === 100 ? 'text-emerald-700' : 'text-amber-800'}`} aria-live="polite">{number(classTotal)}%</td></tr></tfoot></table></div>
          <div className="space-y-2 p-5 text-xs leading-6 text-slate-500"><p>{t('scoreHint')}</p><p>{t('integration')}</p>{!editing && data.scores.issues.map(issue => <p className="text-amber-800" key={issue}>{t(issue)}</p>)}</div></>)}
      </div>
      {block(t('woff'), <><div className="overflow-x-auto"><table className="w-full text-sm"><thead className="bg-slate-50 text-xs text-slate-500"><tr><th scope="col" className={cell}>{t('condition')}</th><th scope="col" className={cell}>{t('value')}</th></tr></thead><tbody className="divide-y divide-slate-100">{(['wOffBelow90', 'wOffAbove90'] as const).map(key => <tr key={key}><th scope="row" className={`${cell} font-medium text-slate-700`}>{t(key)}</th><td className={cell}>{input(draft[key], t(key), value => change(key, value))}<span className="ms-1 text-slate-400">%</span></td></tr>)}</tbody></table></div><p className="m-5 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm leading-6 text-amber-900">{t('boundary')}</p></>)}
      {block(t('history'), <div className="p-5">{!data.history.length ? <p className="text-sm text-slate-500">{t('noHistory')}</p> : <div className="overflow-x-auto"><table className="w-full min-w-[620px] text-sm"><thead className="text-xs text-slate-500"><tr>{['date', 'action', 'status', 'actor', 'details'].map(key => <th className={cell} scope="col" key={key}>{t(key)}</th>)}</tr></thead><tbody className="divide-y divide-slate-100">{data.history.map(item => <tr key={item.id}><td className={cell}>{date(item.occurredAt)}</td><td className={cell}>{t(item.action)}</td><td className={cell}>{t(item.status)}</td><td className={`${cell} font-mono text-xs`} dir="ltr">{item.actorId}</td><td className={cell}><Button variant="ghost" fullWidth={false} size="sm" onClick={() => setAudit(audit?.id === item.id ? null : item)}>{t('details')}</Button></td></tr>)}</tbody></table></div>}
        {audit && <div className="mt-4 overflow-x-auto rounded-xl border border-mis-border bg-slate-50 p-4"><p className="mb-3 text-sm font-bold text-mis-navy">{t(audit.action)} · {date(audit.occurredAt)}</p><table className="w-full min-w-[450px] text-xs"><thead><tr>{['BKT', t('movement'), t('target'), t('weight')].map(label => <th key={label} className={cell}>{label}</th>)}</tr></thead><tbody>{audit.values.rows.map(row => <tr key={`${row.bucket}-${row.movement}`}><td className={cell}>{row.bucket}</td><td className={cell}>{t(row.movement)}</td><td className={cell}>{number(row.targetPercent)}%</td><td className={cell}>{number(row.weight)}</td></tr>)}</tbody></table><p className="mt-3 text-xs">MORE: {number(audit.values.moreTargetPercent)}% / {number(audit.values.moreWeight)} · ACTIVE / MORE: {number(audit.values.activeClassWeight)}% / {number(audit.values.moreClassWeight)}% · W-Off &lt;90 / &gt;90: {number(audit.values.wOffBelow90)}% / {number(audit.values.wOffAbove90)}%</p></div>}
      </div>, t('historyHint'))}
    </>}
  </div>;
}
