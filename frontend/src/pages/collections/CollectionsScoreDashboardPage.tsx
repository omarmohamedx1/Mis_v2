import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { ArrowLeft, ChevronDown, ChevronUp, Pencil, Save, X } from 'lucide-react';
import { useCallback, useEffect, useState, type ChangeEvent, type MouseEvent } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { useActionDialog } from '../../components/common/useActionDialog';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { BankLogo } from '../../features/collections/components/BankLogo';
import { CollectionScoreSourceEditor } from '../../features/collections/components/CollectionScoreSourceEditor';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { displayPrimary, isUuid, normalizePrimary, type PrimaryClassification } from '../../features/collections/organizationClassification';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { CollectionScoreBucket, CollectionScoreCell, CollectionScoreDashboard, CollectionScoreField, CollectionScorePeriods, SaveCollectionScoreBucket } from '../../features/collections/types/score';

type DraftCell = Record<CollectionScoreField, string> & { category: string };
type BucketDrafts = Record<string, DraftCell[]>;

const columns: { field: CollectionScoreField | null; labelKey: string }[] = [
  { field: null, labelKey: 'scoreColumnBKT' },
  { field: 'caseCount', labelKey: 'scoreColumnCases' },
  { field: 'actualBalance', labelKey: 'scoreColumnActualBalance' },
  { field: 'targetPercent', labelKey: 'scoreColumnTargetPercent' },
  { field: 'targetBalance', labelKey: 'scoreColumnTargetBalance' },
  { field: 'missing', labelKey: 'scoreColumnMissing' },
  { field: 'percent', labelKey: 'scoreColumnPercent' },
  { field: 'weight', labelKey: 'scoreColumnWeight' },
  { field: 'actualVsTarget', labelKey: 'scoreColumnActualVsTarget' },
  { field: 'score', labelKey: 'scoreColumnScore' },
];

const numericFields: CollectionScoreField[] = [
  'caseCount', 'actualBalance', 'targetPercent', 'targetBalance', 'missing', 'percent', 'weight', 'actualVsTarget', 'score',
];

function toDraft(cell: CollectionScoreCell): DraftCell {
  return {
    category: cell.category,
    caseCount: cell.caseCount?.toString() ?? '',
    actualBalance: cell.actualBalance?.toString() ?? '',
    targetPercent: cell.targetPercent?.toString() ?? '',
    targetBalance: cell.targetBalance?.toString() ?? '',
    missing: cell.missing?.toString() ?? '',
    percent: cell.percent?.toString() ?? '',
    weight: cell.weight?.toString() ?? '',
    actualVsTarget: cell.actualVsTarget?.toString() ?? '',
    score: cell.score?.toString() ?? '',
  };
}

function toSaveCell(draft: DraftCell): CollectionScoreCell {
  const parsed = (field: CollectionScoreField) => {
    const raw = draft[field].trim();
    return raw === '' ? null : Number(raw);
  };
  const count = parsed('caseCount');
  return {
    category: draft.category,
    caseCount: count === null ? null : Number.parseInt(draft.caseCount, 10),
    actualBalance: parsed('actualBalance'),
    targetPercent: parsed('targetPercent'),
    targetBalance: parsed('targetBalance'),
    missing: parsed('missing'),
    percent: parsed('percent'),
    weight: parsed('weight'),
    actualVsTarget: parsed('actualVsTarget'),
    score: parsed('score'),
  };
}

function validateDraft(drafts: DraftCell[], ct: (key: string) => string) {
  const errors: string[] = [];
  for (const draft of drafts) {
    for (const field of numericFields) {
      if (!isEditable(draft.category, field)) continue;
      const raw = draft[field].trim();
      if (!raw) continue;
      const value = Number(raw);
      if (!Number.isFinite(value)) {
        errors.push(ct('scoreValidationNumber'));
        continue;
      }
      if (field === 'caseCount' && (!/^\d+$/.test(raw) || !Number.isSafeInteger(value) || value > 2147483647)) errors.push(ct('scoreValidationCases'));
      if (['actualBalance', 'targetBalance', 'missing', 'weight', 'score'].includes(field) && value < 0) errors.push(ct('scoreValidationNonNegative'));
      if (['targetPercent', 'percent'].includes(field) && (value < 0 || value > 100)) errors.push(ct('scoreValidationPercent'));
      const maxDecimals = ['actualBalance', 'targetBalance', 'missing'].includes(field) ? 2
        : ['targetPercent', 'percent'].includes(field) ? 4 : 6;
      const decimals = raw.includes('.') ? raw.length - raw.indexOf('.') - 1 : 0;
      if (decimals > maxDecimals) errors.push(ct('scoreValidationPrecision').replace('{digits}', String(maxDecimals)));
    }
  }
  return [...new Set(errors)];
}

function formatCell(value: number | null, field: CollectionScoreField, locale: string) {
  if (value === null || value === undefined) return '—';
  const formatted = new Intl.NumberFormat(locale, {
    minimumFractionDigits: ['targetPercent', 'percent', 'actualVsTarget', 'score', 'weight'].includes(field) ? 2 : 0,
    maximumFractionDigits: field === 'caseCount' ? 0
      : ['actualBalance', 'targetBalance', 'missing'].includes(field) ? 2
        : 2,
  }).format(Math.abs(value));
  const percentage = ['targetPercent', 'percent', 'actualVsTarget'].includes(field);
  return `${value < 0 ? percentage ? `-${formatted}` : `(${formatted})` : formatted}${percentage ? '%' : ''}`;
}

function isEditable(category: string, field: CollectionScoreField, bucket?: string) {
  return category !== 'STAB' && (field === 'targetPercent' || (field === 'weight' && category !== 'MORE' && !(bucket === 'BKT1' && category === 'NORM')));
}

function useScoreDashboard(organizationId: string, portfolio: PrimaryClassification, portfolioId: string) {
  const [periods, setPeriods] = useState<CollectionScorePeriods>();
  const [reportingPeriod, setReportingPeriod] = useState('');
  const [dashboard, setDashboard] = useState<CollectionScoreDashboard>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [retryToken, setRetryToken] = useState(0);

  const retry = useCallback(() => setRetryToken(value => value + 1), []);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(false);
    setDashboard(undefined);

    void (async () => {
      try {
        const availablePeriods = await collectionsService.scorePeriods(organizationId, portfolio, portfolioId);
        const selectedPeriod = availablePeriods.periods.includes(reportingPeriod)
          ? reportingPeriod
          : availablePeriods.currentPeriod;
        const scoreDashboard = await collectionsService.scoreDashboard(organizationId, portfolio, portfolioId, selectedPeriod);
        if (!active) return;
        setPeriods(availablePeriods);
        setReportingPeriod(selectedPeriod);
        setDashboard(scoreDashboard);
      } catch {
        if (active) setError(true);
      } finally {
        if (active) setLoading(false);
      }
    })();

    return () => { active = false; };
  }, [organizationId, portfolio, portfolioId, reportingPeriod, retryToken]);

  const changePeriod = useCallback((period: string) => setReportingPeriod(period), []);
  const reload = useCallback(() => setRetryToken(value => value + 1), []);
  return { periods, reportingPeriod, dashboard, loading, error, retry, changePeriod, reload };
}

export function CollectionsScoreDashboardPage() {
  const navigate = useNavigate();
  const actionDialog = useActionDialog();
  const { ct, language, isRtl } = useCollectionsLocalization();
  const { organizationId = '', portfolioType = '', portfolioId = '' } = useParams();
  const portfolio = normalizePrimary(portfolioType);
  const { periods, reportingPeriod, dashboard, loading, error, retry, changePeriod, reload } =
    useScoreDashboard(organizationId, portfolio ?? 'ACT', portfolioId);
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set());
  const [editing, setEditing] = useState<BucketDrafts>({});
  const [bucketErrors, setBucketErrors] = useState<Record<string, string[]>>({});
  const [saveError, setSaveError] = useState('');
  const [saveSuccess, setSaveSuccess] = useState('');
  const [savingBucket, setSavingBucket] = useState('');
  const locale = language === 'ar' ? 'ar-EG-u-nu-latn' : 'en';
  const dirty = Object.keys(editing).length > 0;

  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  const confirmNavigation = useCallback((event: MouseEvent<HTMLAnchorElement>) => {
    if (!dirty || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    const destination = event.currentTarget.getAttribute('href');
    void actionDialog.confirm(ct('scoreUnsavedConfirm')).then(confirmed => {
      if (confirmed && destination) navigate(destination);
    });
  }, [ct, dirty, actionDialog.confirm, navigate]);

  const toggleBucket = (code: string) => setExpanded(current => {
    const next = new Set(current);
    if (next.has(code)) next.delete(code);
    else next.add(code);
    return next;
  });

  const startEditing = (bucket: CollectionScoreBucket) => {
    setEditing(current => ({ ...current, [bucket.code]: bucket.categories.map(toDraft) }));
    setExpanded(current => new Set(current).add(bucket.code));
    setBucketErrors(current => ({ ...current, [bucket.code]: [] }));
    setSaveError('');
    setSaveSuccess('');
  };

  const cancelEditing = (bucket: string) => {
    setEditing(current => {
      const next = { ...current };
      delete next[bucket];
      return next;
    });
    setBucketErrors(current => ({ ...current, [bucket]: [] }));
  };

  const changeDraft = (bucket: string, rowIndex: number, field: CollectionScoreField, event: ChangeEvent<HTMLInputElement>) => {
    setEditing(current => ({
      ...current,
      [bucket]: current[bucket].map((row, index) => index === rowIndex ? { ...row, [field]: event.target.value } : row),
    }));
    setBucketErrors(current => ({ ...current, [bucket]: [] }));
    setSaveError('');
    setSaveSuccess('');
  };

  const save = async (bucket: CollectionScoreBucket) => {
    const drafts = editing[bucket.code];
    if (!drafts || !dashboard) return;
    const errors = validateDraft(drafts, ct);
    if (errors.length) {
      setBucketErrors(current => ({ ...current, [bucket.code]: errors }));
      return;
    }

    const request: SaveCollectionScoreBucket = {
      reportingPeriod,
      expectedVersion: bucket.version,
      categories: drafts.map(toSaveCell),
    };
    setSavingBucket(bucket.code);
    setSaveError('');
    setSaveSuccess('');
    try {
      await collectionsService.saveScoreBucket(organizationId, dashboard.portfolioType, portfolioId, bucket.code, request);
      cancelEditing(bucket.code);
      setSaveSuccess(ct('scoreSaveSuccess'));
      reload();
    } catch {
      setSaveError(ct('scoreSaveError'));
    } finally {
      setSavingBucket('');
    }
  };

  if (!isUuid(organizationId)) return <Navigate to="/collections/score" replace />;
  if (!portfolio || !isUuid(portfolioId)) return <Navigate to={`/collections/score/${organizationId}/${portfolioType}`} replace />;
  if (loading) return <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>;
  if (error || !dashboard || !periods) {
    return <ErrorState description={ct('scoreLoadError')} onRetry={retry} retryLabel={ct('scoreRetry')} title={ct('score')} />;
  }

  const organizationName = language === 'ar'
    ? dashboard.organization.nameArabic || dashboard.organization.nameEnglish
    : dashboard.organization.nameEnglish || dashboard.organization.nameArabic;
  const portfolioLabel = ct(`scorePortfolio${portfolio}`);
  const numberFormat = new Intl.NumberFormat(locale, { maximumFractionDigits: 2 });
  const periodAvailable = !dashboard.buckets.some(bucket => bucket.issues?.includes('MISSING_PERIOD_SOURCE'));
  const summary = [
    { label: ct('scoreTotalDue'), value: dashboard.summary.totalDueAmount },
    { label: ct('scorePaid'), value: dashboard.summary.paid },
    { label: ct('scoreUpperTable'), value: dashboard.summary.upperTableScore },
  ];

  return (
    <div className="mx-auto max-w-[1480px]" dir={isRtl ? 'rtl' : 'ltr'}>
      {actionDialog.dialog}
      <Link
        className="mb-5 inline-flex items-center gap-2 text-sm font-semibold text-mis-primary hover:text-mis-deep"
        onClick={confirmNavigation}
        to={`/collections/score/${organizationId}`}
      >
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" />
        {ct('scoreBackToOrganization')}
      </Link>

      <PageHeader
        eyebrow={ct('scoreDashboard')}
        title={(
          <span className="flex min-w-0 flex-wrap items-center gap-4">
            <BankLogo
              className="h-16 w-16 shrink-0 rounded-2xl"
              code={dashboard.organization.code}
              logoUrl={dashboard.organization.logoUrl ?? undefined}
              name={organizationName}
            />
            <span className="min-w-0">
              <span className="block truncate">{organizationName}</span>
              <span className="mt-1 block text-base font-semibold text-mis-primary">{portfolioLabel}</span>
              <span className="mt-1 block text-sm font-medium text-slate-500">{language === 'ar' ? dashboard.portfolio.nameArabic : dashboard.portfolio.nameEnglish} · {dashboard.portfolio.code}</span>
            </span>
          </span>
        )}
        actions={(
          <label className="flex min-h-11 items-center gap-2 text-sm text-slate-600">
            <span className="shrink-0">{ct('scoreReportingPeriod')}</span>
            <ProfessionalSelect
              aria-label={ct('scoreReportingPeriod')}
              className="w-40 font-semibold"
              onChange={async event => {
                const nextPeriod = event.target.value;
                if (!dirty) {
                  changePeriod(nextPeriod);
                } else if (await actionDialog.confirm(ct('scoreUnsavedConfirm'))) {
                  setEditing({});
                  setBucketErrors({});
                  changePeriod(nextPeriod);
                }
              }}
              value={reportingPeriod}
            >
              {periods.periods.map(period => <option key={period} value={period}>{period}</option>)}
            </ProfessionalSelect>
          </label>
        )}
      />

      <section aria-label={ct('scoreSummary')} className="mb-7 grid gap-4 sm:grid-cols-3">
        {summary.map(item => (
          <article className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm" key={item.label}>
            <p className="text-sm font-semibold text-slate-500">{item.label}</p>
            <p className="mt-3 text-2xl font-bold text-mis-navy" dir="ltr">
              {item.value === null ? '—' : numberFormat.format(item.value)}
            </p>
          </article>
        ))}
      </section>

      <div className="mb-5 flex flex-wrap gap-4 text-sm text-slate-600">
        <span>{ct('scoreSystemCases')}: <b>{numberFormat.format(dashboard.summary.systemCaseCount)}</b></span>
        <span>{ct('scorePeriodCases')}: <b>{periodAvailable ? numberFormat.format(dashboard.summary.periodCaseCount) : '—'}</b></span>
        {dashboard.summary.unclassifiedCaseCount > 0 ? <span className="text-amber-800">{ct('scoreUnclassified')}: <b>{numberFormat.format(dashboard.summary.unclassifiedCaseCount)}</b></span> : null}
        {dashboard.summary.unmappedSystemCaseCount > 0 ? <span className="text-amber-800">{ct('scoreUnmappedSystemCases')}: <b>{numberFormat.format(dashboard.summary.unmappedSystemCaseCount)}</b></span> : null}
      </div>
      {!periodAvailable ? <p className="mb-5 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900" role="status">{ct('scoreIssueMISSING_PERIOD_SOURCE')}</p> : null}
      {dashboard.canEdit && !dirty ? <CollectionScoreSourceEditor organizationId={organizationId} portfolioType={portfolio} portfolioId={portfolioId} reportingPeriod={reportingPeriod} onSaved={reload} /> : null}

      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-xl font-bold text-mis-navy">{ct('scoreDashboard')}</h2>
          <p className="mt-1 text-sm text-slate-500">{ct('scoreCalculatedHelp')}</p>
          <p className="mt-1 text-sm text-slate-500">{ct('scoreDefaultsHelp')}</p>
        </div>
        {saveSuccess ? <p className="text-sm font-semibold text-emerald-700" role="status">{saveSuccess}</p> : null}
        {saveError ? <p className="text-sm font-semibold text-red-700" role="alert">{saveError}</p> : null}
      </div>

      <section aria-label={ct('scoreBktGroups')} className="space-y-4">
        {dashboard.buckets.map(bucket => {
          const isExpanded = expanded.has(bucket.code);
          const drafts = editing[bucket.code];
          const validationErrors = bucketErrors[bucket.code] ?? [];
          return (
            <article className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm" key={bucket.code}>
              <div className="flex flex-wrap items-center justify-between gap-3 bg-slate-50 p-4 sm:px-5">
                <button
                  aria-controls={`${bucket.code}-table`}
                  aria-expanded={isExpanded}
                  className="flex min-w-0 flex-1 items-center gap-3 text-start"
                  onClick={() => toggleBucket(bucket.code)}
                  type="button"
                >
                  {isExpanded ? <ChevronUp className="h-5 w-5 shrink-0 text-mis-primary" /> : <ChevronDown className="h-5 w-5 shrink-0 text-mis-primary" />}
                  <span className="text-lg font-bold text-mis-navy" dir="ltr">{bucket.code.replace('BKT', 'BKT ')}</span>
                  <span className="rounded-full bg-mis-pale px-2.5 py-1 text-xs font-semibold text-mis-primary">
                    {ct('scoreSystemCases')}: {numberFormat.format(bucket.systemCaseCount)}
                  </span>
                  <span className="text-sm font-semibold text-mis-navy">{ct('scoreBucketScore')}: {formatCell(bucket.score, 'score', locale)}</span>
                </button>
                {dashboard.canEdit && !drafts ? (
                  <button
                    className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-mis-control bg-white px-3 py-2 text-sm font-semibold text-mis-navy hover:border-mis-primary hover:text-mis-primary"
                    onClick={() => startEditing(bucket)}
                    type="button"
                  >
                    <Pencil className="h-4 w-4" />
                    {ct('scoreEditNumbers')}
                  </button>
                ) : null}
              </div>

              {isExpanded ? (
                <div className="p-3 sm:p-5" id={`${bucket.code}-table`}>
                  {bucket.issues?.length ? <ul className="mb-3 space-y-1 rounded-xl bg-amber-50 p-3 text-sm text-amber-900">{bucket.issues.map(issue => <li key={issue}>{ct(`scoreIssue${issue}`)}</li>)}</ul> : null}
                  <div className="overflow-x-auto rounded-xl border border-slate-200">
                    <table className="min-w-[1180px] w-full border-collapse text-sm">
                      <thead className="bg-slate-100 text-start text-xs font-bold uppercase text-slate-600">
                        <tr>
                          {columns.map(column => (
                            <th className={`whitespace-nowrap border-b border-slate-200 px-3 py-3 ${column.field ? 'text-end' : 'text-start'}`} key={column.labelKey}>
                              {ct(column.labelKey)}
                            </th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        <tr className="bg-slate-900 font-bold text-white">
                          <th className="px-3 py-3 text-start" scope="row">{ct('scoreBucketTotal')}</th>
                          {numericFields.map(field => {
                            const value = field === 'caseCount' ? bucket.caseCount : field === 'actualBalance' ? bucket.actualBalance
                              : field === 'targetPercent' ? 100 : field === 'targetBalance' ? bucket.targetBalance
                                : field === 'weight' ? bucket.weight : field === 'score' ? bucket.score : null;
                            return <td className="whitespace-nowrap px-3 py-3 text-end tabular-nums" key={field}><bdi dir="ltr">{formatCell(value, field, locale)}</bdi></td>;
                          })}
                        </tr>
                        {bucket.categories.map((cell, rowIndex) => {
                          const draft = drafts?.[rowIndex];
                          return (
                            <tr className={`border-b border-slate-200 last:border-b-0 ${cell.category === 'STAB' ? 'bg-lime-100' : cell.category === 'BWD' ? 'bg-sky-100' : cell.category === 'FWD' ? 'bg-red-100' : 'bg-white'}`} key={cell.category}>
                              <th className="whitespace-nowrap px-3 py-3 text-start font-semibold text-mis-navy" scope="row">
                                {ct(`scoreCategory${cell.category}`)}
                              </th>
                              {numericFields.map(field => (
                                <td className="min-w-28 px-3 py-2 text-end tabular-nums text-slate-700" key={field}>
                                  {draft && isEditable(cell.category, field, bucket.code) ? (
                                    <input
                                      aria-label={`${ct(`scoreCategory${cell.category}`)} · ${ct(columns.find(column => column.field === field)?.labelKey ?? field)}`}
                                      className="h-10 w-28 rounded-lg border border-mis-control bg-white px-2 text-end text-sm outline-none focus:border-mis-primary focus:ring-2 focus:ring-mis-primary/10"
                                      inputMode={field === 'caseCount' ? 'numeric' : 'decimal'}
                                      onChange={event => changeDraft(bucket.code, rowIndex, field, event)}
                                      placeholder="—"
                                      type="text"
                                      value={draft[field]}
                                    />
                                  ) : <bdi dir="ltr">{formatCell(cell[field], field, locale)}</bdi>}
                                </td>
                              ))}
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>

                  {validationErrors.length ? (
                    <ul className="mt-3 space-y-1 text-sm text-red-700" role="alert">
                      {validationErrors.map((message, index) => <li key={`${message}-${index}`}>{message}</li>)}
                    </ul>
                  ) : null}

                  {drafts ? (
                    <div className="mt-4 flex flex-wrap items-center gap-2">
                      <button
                        className="inline-flex min-h-10 items-center gap-2 rounded-xl bg-mis-primary px-4 py-2 text-sm font-semibold text-white hover:bg-mis-deep disabled:opacity-60"
                        disabled={savingBucket === bucket.code}
                        onClick={() => void save(bucket)}
                        type="button"
                      >
                        {savingBucket === bucket.code ? <LoadingSpinner /> : <Save className="h-4 w-4" />}
                        {ct('scoreSave')}
                      </button>
                      <button
                        className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-mis-control bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                        onClick={async () => {
                          if (await actionDialog.confirm(ct('scoreCancelConfirm'))) cancelEditing(bucket.code);
                        }}
                        type="button"
                      >
                        <X className="h-4 w-4" />
                        {ct('scoreCancel')}
                      </button>
                    </div>
                  ) : null}
                </div>
              ) : null}
            </article>
          );
        })}
      </section>
    </div>
  );
}
