import { AlertCircle, ArrowUpLeft, ArrowUpRight, BarChart3, CalendarDays, CheckCircle2, ChevronLeft, ChevronRight, HandCoins, Layers, RefreshCw, Search, UserRoundCheck, Wallet } from 'lucide-react';
import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { useAuth } from '../../context/AuthContext';
import { BankLogo } from '../../features/collections/components/BankLogo';
import { useCollectionFormat } from '../../features/collections/components/CollectionsUi';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { organizationPath } from '../../features/collections/organizationClassification';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { CollectionDashboard, CollectionDashboardClientSlice, CollectionDashboardTrendPoint, WorkQueue } from '../../features/collections/types/collections';
import { formatNumberWithUnit } from '../../utils/formatMoney';

type Priority = { label: string; value: number; href: string; urgent?: boolean };
type FollowUp = { caseId: string; name: string; number: string; label: string; date?: string; urgent?: boolean };
type Format = ReturnType<typeof useCollectionFormat>;

export function CollectionsDashboardPage() {
  const { ct, language } = useCollectionsLocalization();
  const f = useCollectionFormat();
  const { user } = useAuth();
  const [params] = useSearchParams();
  const organizationId = params.get('organizationId') ?? undefined;
  const scope = organizationId ?? '';
  const canAssign = Boolean(user?.roles.some(role => ['Admin', 'CollectionsOperationsManager', 'CollectionsSupervisor'].includes(role)));
  const [data, setData] = useState<CollectionDashboard>();
  const [work, setWork] = useState<WorkQueue>();
  const [loadedScope, setLoadedScope] = useState<string>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [workError, setWorkError] = useState(false);
  const [workLoading, setWorkLoading] = useState(true);
  const [reload, setReload] = useState(0);
  const [updatedAt, setUpdatedAt] = useState<Date>();
  const [showAllPriorities, setShowAllPriorities] = useState(false);

  useEffect(() => {
    let active = true;
    setLoading(true); setError(false); setWorkError(false); setWorkLoading(true);
    void collectionsService.dashboard(organizationId).then(snapshot => {
      if (!active) return;
      setData(snapshot); setLoadedScope(scope); setUpdatedAt(new Date());
    }).catch(() => { if (active) setError(true); }).finally(() => { if (active) setLoading(false); });
    void collectionsService.myWork().then(queue => {
      if (active) setWork(queue);
    }).catch(() => { if (active) { setWork(undefined); setWorkError(true); } }).finally(() => { if (active) setWorkLoading(false); });
    return () => { active = false; };
  }, [organizationId, scope, reload, language]);

  const ready = data && loadedScope === scope;
  const coverage = data?.totalCases ? data.assignedCases / data.totalCases * 100 : 0;
  const overdueShare = data?.totalOutstanding ? data.totalOverdue / data.totalOutstanding * 100 : 0;
  const assignmentsHref = canAssign && !organizationId ? '/collections/assignments' : casesHref({ organizationId, collectorId: 'unassigned' });
  const priorities: Priority[] = data ? [
    { label: ct('dashboardActionUnassigned'), value: data.unassignedCases, href: assignmentsHref },
    { label: ct('dashboardActionReview'), value: data.pendingReviews, href: '/collections/payments' },
    { label: ct('dashboardActionBrokenPromises'), value: data.brokenPromises, href: '/collections/promises', urgent: true },
    { label: ct('dashboardActionDuePromises'), value: data.promisesDueToday, href: '/collections/promises?status=DUE_TODAY' },
    { label: ct('dashboardActionFollowUps'), value: data.overdueFollowUps, href: casesHref({ organizationId }), urgent: true },
    { label: ct('dashboardActionRisk'), value: data.highRiskCases, href: casesHref({ organizationId, priority: 'HIGH' }), urgent: true },
    { label: ct('dashboardActionComplaints'), value: data.openComplaints, href: '/collections/complaints' },
    { label: ct('dashboardActionVisits'), value: data.visitsToday, href: '/collections/visits' },
  ].filter(item => item.value > 0) : [];
  const followUps = useMemo(() => {
    if (!work) return [];
    const rows: FollowUp[] = [
      ...work.brokenPromises.map(item => ({ caseId: item.caseId, name: item.customerName, number: item.caseNumber, label: ct('dashboardFollowUpBroken'), date: item.promiseDate, urgent: true })),
      ...work.promisesDue.map(item => ({ caseId: item.caseId, name: item.customerName, number: item.caseNumber, label: ct('dashboardFollowUpPromise'), date: item.promiseDate })),
      ...work.callsDue.map(item => ({ caseId: item.id, name: item.customerName, number: item.caseNumber, label: ct('dashboardFollowUpCall'), date: item.nextFollowUpAt })),
      ...work.highPriorityCases.map(item => ({ caseId: item.id, name: item.customerName, number: item.caseNumber, label: ct('dashboardFollowUpPriority'), date: item.nextFollowUpAt, urgent: true })),
    ];
    return rows.filter((row, index) => rows.findIndex(other => other.caseId === row.caseId) === index).slice(0, 6);
  }, [ct, work]);
  const trend = data?.collectionTrend ?? [];
  const showTrend = trend.some(point => point.collected > 0);
  const Arrow = language === 'ar' ? ArrowUpLeft : ArrowUpRight;
  const time = updatedAt ? new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG-u-nu-latn' : 'en-GB', { hour: '2-digit', minute: '2-digit', timeZone: 'Africa/Cairo' }).format(updatedAt) : '';

  return <div className="collections-overview space-y-5">
    <header className="overview-heading">
      <div className="min-w-0">
        <h1 className="text-2xl font-bold text-mis-ink">{ct('dashboardTitle')}</h1>
        <p className="mt-1.5 text-sm leading-7 text-slate-500">{ct('dashboardDescription')}</p>
        <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-slate-500" aria-live="polite">
          {time && <span className="inline-flex items-center gap-1.5"><span className={`h-1.5 w-1.5 rounded-full ${error ? 'bg-amber-500' : 'bg-emerald-500'}`} />{ct(loading ? 'dashboardRefreshing' : 'dashboardLastUpdated')} <bdi dir="ltr">{time}</bdi></span>}
          {organizationId && <Link className="font-medium text-mis-primary hover:underline" to="/collections/dashboard">{ct('dashboardSingleOrganization')} · {ct('dashboardAllScope')}</Link>}
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <Button fullWidth={false} size="sm" variant="outline" leftIcon={<RefreshCw />} isLoading={loading} onClick={() => setReload(value => value + 1)}>{ct('refresh')}</Button>
        <Link className="overview-primary-action" to="/collections/reports"><BarChart3 aria-hidden="true" className="h-4 w-4" />{ct('sidebarReports')}</Link>
      </div>
    </header>

    {error && <ErrorState compact title={ct('dashboardLoadFailed')} onRetry={() => setReload(value => value + 1)} />}
    {!ready || !data ? !error && <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div> : <>
      <section className="overview-kpis" aria-label={ct('dashboardTitle')}>
        <OverviewStat label={ct('collectedToday')} value={f.money(data.collectedToday)} hint={ct('dashboardApprovedToday')} icon={<HandCoins />} href="/collections/payments" />
        <OverviewStat label={ct('dashboardMonthCollected')} value={f.money(data.collectedMonthToDate)} hint={data.achievementPercent > 0 ? formatNumberWithUnit(`${data.achievementPercent.toFixed(1)}%`, ct('dashboardMonthTarget')) : ct('dashboardApprovedMonth')} icon={<Wallet />} href="/collections/reports" />
        <OverviewStat label={ct('totalOutstanding')} value={f.money(data.totalOutstanding)} hint={<><span data-bidi="ltr">{f.money(data.totalOverdue)}</span> {ct('dashboardOverdueAmount')}</>} icon={<Layers />} href={casesHref({ organizationId })} />
        <OverviewStat label={ct('dashboardAssigned')} value={`${f.number(data.assignedCases)} / ${f.number(data.totalCases)}`} hint={formatNumberWithUnit(f.number(data.unassignedCases), ct('dashboardAwaitingAssignment'))} icon={<UserRoundCheck />} href={assignmentsHref} />
      </section>

      <div className="overview-panels">
        <OverviewPanel title={ct(showTrend ? 'collectionTrend' : 'dashboardPortfolioHealth')} description={ct(showTrend ? 'dashboardTrendHelp' : 'dashboardPortfolioHealthHelp')}>
          {showTrend ? <CollectionTrend points={trend} format={f} language={language} /> : <div className="space-y-5 py-1">
            <CoverageRow label={ct('dashboardCoverage')} value={coverage} description={`${f.number(data.assignedCases)} / ${f.number(data.totalCases)}`} />
            <CoverageRow label={ct('dashboardOverdueShare')} value={overdueShare} description={f.money(data.totalOverdue)} warning />
          </div>}
          <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-4 text-xs text-slate-500">
            <span>{ct('activeCollectors')}: <bdi className="font-semibold text-mis-ink">{f.number(data.activeCollectors)}</bdi></span>
            <Link className="inline-flex items-center gap-1 font-semibold text-mis-primary" to="/collections/collectors">{ct('sidebarTeam')}<Arrow aria-hidden="true" className="h-3.5 w-3.5" /></Link>
          </div>
        </OverviewPanel>
        <OverviewPanel title={ct('dashboardPriorities')} description={ct('dashboardPrioritiesHelp')}>
          {priorities.length ? <div className="-my-1 divide-y divide-slate-100">
            {(showAllPriorities ? priorities : priorities.slice(0, 4)).map(item => <Link className="overview-priority" to={item.href} key={item.label}>
              <span className={`overview-priority-count ${item.urgent ? 'bg-rose-50 text-rose-700' : 'bg-slate-100 text-mis-ink'}`}>{f.number(item.value)}</span>
              <span className="min-w-0 flex-1 text-sm font-medium text-slate-700">{item.label}</span>
              <Arrow aria-hidden="true" className="h-4 w-4 shrink-0 text-slate-400" />
            </Link>)}
            {priorities.length > 4 && <button type="button" className="w-full pt-3 text-start text-xs font-semibold text-mis-primary" onClick={() => setShowAllPriorities(value => !value)}>{ct(showAllPriorities ? 'dashboardFewerPriorities' : 'dashboardMorePriorities')}</button>}
          </div> : <div className="flex items-start gap-3 rounded-lg bg-emerald-50 px-3 py-4 text-sm leading-7 text-emerald-800"><CheckCircle2 aria-hidden="true" className="mt-1 h-5 w-5 shrink-0" />{ct('dashboardNoPriorities')}</div>}
        </OverviewPanel>
      </div>

      <ClientPortfolios key={scope} clients={data.byClient ?? []} format={f} />

      <OverviewPanel title={ct('dashboardMyQueue')} description={ct(organizationId ? 'dashboardMyQueueGlobalHelp' : 'dashboardMyQueueHelp')} action={<Link className="overview-text-action" to="/collections/cases">{ct('dashboardOpenCases')}<Arrow aria-hidden="true" className="h-3.5 w-3.5" /></Link>}>
        {workLoading ? <div className="flex min-h-20 items-center justify-center" aria-busy="true"><LoadingSpinner /></div> : workError ? <ErrorState compact title={ct('dashboardQueueError')} onRetry={() => setReload(value => value + 1)} /> : followUps.length ? <div className="overview-table-scroll">
          <table className="overview-table min-w-[42rem]">
            <thead><tr><th>{ct('customerName')}</th><th>{ct('dashboardFollowUpType')}</th><th>{ct('dashboardFollowUpDate')}</th><th><span className="sr-only">{ct('actions')}</span></th></tr></thead>
            <tbody>{followUps.map(row => <tr key={row.caseId}>
              <td><Link className="font-semibold text-mis-ink hover:text-mis-primary" to={`/collections/cases/${row.caseId}`}>{row.name}</Link><p className="mt-1 text-xs text-slate-500"><bdi>{row.number}</bdi></p></td>
              <td><span className={`inline-flex rounded-md px-2 py-1 text-xs font-medium ${row.urgent ? 'bg-amber-50 text-amber-800' : 'bg-slate-100 text-slate-600'}`}>{row.label}</span></td>
              <td className="whitespace-nowrap text-slate-600">{f.date(row.date)}</td>
              <td><Link className="overview-text-action whitespace-nowrap" to={`/collections/cases/${row.caseId}`}>{ct('openCase')}<Arrow aria-hidden="true" className="h-3.5 w-3.5" /></Link></td>
            </tr>)}</tbody>
          </table>
        </div> : <p className="flex items-start gap-2 text-sm leading-7 text-slate-500"><CalendarDays aria-hidden="true" className="mt-1 h-4 w-4 shrink-0" />{ct('dashboardQueueEmpty')}</p>}
      </OverviewPanel>
    </>}
  </div>;
}

function casesHref(values: Record<string, string | undefined>) {
  const query = new URLSearchParams(Object.entries(values).filter((entry): entry is [string, string] => Boolean(entry[1])));
  return '/collections/cases' + (query.size ? `?${query}` : '');
}

function OverviewStat({ label, value, hint, icon, href }: { label: string; value: string; hint: ReactNode; icon: ReactNode; href: string }) {
  return <Link className="overview-stat collection-metric" to={href}>
    <div className="flex items-center justify-between gap-3"><p className="text-[13px] font-medium text-slate-500">{label}</p><span aria-hidden="true" className="text-slate-400 [&_svg]:h-[18px] [&_svg]:w-[18px]">{icon}</span></div>
    <p className="collection-metric-value mt-4 font-semibold tracking-tight text-mis-ink" data-bidi="ltr">{value}</p>
    <p className="mt-2 text-xs leading-6 text-slate-500">{hint}</p>
  </Link>;
}

function OverviewPanel({ title, description, action, children }: { title: string; description?: string; action?: ReactNode; children: ReactNode }) {
  return <section className="overview-panel">
    <header className="overview-panel-heading"><div className="min-w-0"><h2 className="text-sm font-semibold text-mis-ink">{title}</h2>{description && <p className="mt-1 text-xs leading-6 text-slate-500">{description}</p>}</div>{action}</header>
    <div className="overview-panel-body">{children}</div>
  </section>;
}

function CoverageRow({ label, value, description, warning = false }: { label: string; value: number; description: string; warning?: boolean }) {
  return <div><div className="mb-2 flex flex-wrap items-center justify-between gap-2 text-sm"><span className="flex flex-wrap items-center gap-x-3 gap-y-1 text-slate-600"><span>{label}</span><span className="text-xs text-slate-400" data-bidi="ltr">{description}</span></span><bdi dir="ltr" className="font-semibold text-mis-ink">{value.toFixed(0)}%</bdi></div>
    <div role="progressbar" aria-label={label} aria-valuenow={Math.max(0, Math.min(value, 100))} aria-valuemin={0} aria-valuemax={100} className="h-2 overflow-hidden rounded-full bg-slate-100"><div className={`h-full rounded-full ${warning ? 'bg-amber-500' : 'bg-mis-primary'}`} style={{ width: `${Math.max(0, Math.min(value, 100))}%` }} /></div>
  </div>;
}

function CollectionTrend({ points, format, language }: { points: CollectionDashboardTrendPoint[]; format: Format; language: string }) {
  const maximum = Math.max(...points.map(point => point.collected), 1);
  const date = new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG-u-nu-latn' : 'en-GB', { day: 'numeric', month: 'short' });
  return <div className="overview-trend">{points.slice(-7).map(point => <div className="flex min-w-0 flex-col items-center gap-2" key={point.date}>
    <span className="sr-only">{date.format(new Date(`${point.date}T12:00:00`))}: {format.money(point.collected)}</span>
    <div className="flex h-32 w-full items-end justify-center rounded-md bg-slate-50" title={format.money(point.collected)}><div className="w-7 max-w-full rounded-t bg-mis-primary" style={{ height: `${point.collected ? Math.max(point.collected / maximum * 100, 3) : 0}%` }} /></div>
    <span className="whitespace-nowrap text-[10px] text-slate-500">{date.format(new Date(`${point.date}T12:00:00`))}</span>
  </div>)}</div>;
}

function ClientPortfolios({ clients, format: f }: { clients: CollectionDashboardClientSlice[]; format: Format }) {
  const { ct, language } = useCollectionsLocalization();
  const [search, setSearch] = useState('');
  const [kind, setKind] = useState('all');
  const filtered = useMemo(() => clients.filter(item => (kind === 'all' || (kind === 'bank' ? item.organizationType === 'BANK' : item.organizationType !== 'BANK')) && `${item.name} ${item.code ?? ''}`.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase())).sort((left, right) => right.outstanding - left.outstanding), [clients, kind, search]);
  const hasBanks = clients.some(item => item.organizationType === 'BANK');
  const hasCompanies = clients.some(item => item.organizationType !== 'BANK');
  const Arrow = language === 'ar' ? ChevronLeft : ChevronRight;
  const rows = filtered.slice(0, 6);
  return <section className="overview-panel">
    <header className="overview-panel-heading"><div><h2 className="text-sm font-semibold text-mis-ink">{ct('dashboardClients')}</h2><p className="mt-1 text-xs leading-6 text-slate-500">{ct('dashboardClientsHelp')}</p></div><Link className="overview-text-action" to="/collections/creditors">{ct('dashboardViewClientPerformance')}<Arrow aria-hidden="true" className="h-4 w-4" /></Link></header>
    {clients.length > 0 && <div className="overview-client-tools">
      <label className="relative block min-w-0 flex-1 sm:max-w-xs"><Search aria-hidden="true" className="pointer-events-none absolute start-3 top-3 h-4 w-4 text-slate-400" /><input aria-label={ct('dashboardOrganizationSearch')} className="field h-10 min-h-10 ps-9" placeholder={ct('dashboardOrganizationSearch')} value={search} onChange={event => setSearch(event.target.value)} type="search" /></label>
      {hasBanks && hasCompanies && <div className="overview-segments" role="group" aria-label={ct('dashboardOrganizationFilters')}>{[['all', 'dashboardAllOrganizations'], ['bank', 'sidebarBanks'], ['installment', 'sidebarFinanceCompanies']].map(([value, label]) => <button aria-pressed={kind === value} className={kind === value ? 'bg-white text-mis-primary shadow-sm' : 'text-slate-500'} key={value} onClick={() => setKind(value)} type="button">{ct(label)}</button>)}</div>}
    </div>}
    {rows.length ? <div className="overview-table-scroll"><table className="overview-table collection-summary-table min-w-[54rem]">
      <thead><tr><th className="min-w-[14rem]">{ct('dashboardClient')}</th><th className="text-end">{ct('casesCount')}</th><th className="text-end">{ct('totalOutstanding')}</th><th className="text-end">{ct('totalOverdue')}</th><th className="text-end">{ct('unassigned')}</th><th><span className="sr-only">{ct('actions')}</span></th></tr></thead>
      <tbody>{rows.map(item => {
        const bank = item.organizationType === 'BANK';
        return <tr key={item.id}><td><div className="flex items-center gap-3"><BankLogo className="h-10 w-10 shrink-0 rounded-lg" code={item.code ?? ''} logoUrl={item.logoUrl} name={item.name} /><div className="min-w-0"><Link className="font-semibold text-mis-ink hover:text-mis-primary" to={organizationPath(bank ? 'bank' : 'installment', item.id)}>{item.name}</Link><p className="mt-0.5 text-xs text-slate-500">{ct(bank ? 'dashboardBankType' : 'dashboardFinanceType')}{item.code && <> · <bdi>{item.code}</bdi></>}</p></div></div></td>
          <td className="text-end tabular-nums">{f.number(item.cases)}</td><td className="text-end font-medium" data-bidi="ltr">{f.money(item.outstanding)}</td><td className="text-end text-amber-700" data-bidi="ltr">{f.money(item.overdue)}</td><td className="text-end"><Link className="font-medium text-mis-primary hover:underline" to={casesHref({ organizationId: item.id, collectorId: 'unassigned' })}>{f.number(item.unassigned)}</Link></td><td><Link aria-label={`${ct('dashboardOpenWorkspace')} — ${item.name}`} className="overview-text-action whitespace-nowrap" to={organizationPath(bank ? 'bank' : 'installment', item.id)}>{ct('dashboardOpenWorkspace')}<Arrow aria-hidden="true" className="h-4 w-4" /></Link></td></tr>;
      })}</tbody>
    </table></div> : <div className="flex flex-wrap items-center gap-3 px-5 py-8 text-sm text-slate-500"><AlertCircle aria-hidden="true" className="h-5 w-5 shrink-0" />{ct(clients.length ? 'dashboardNoClientMatch' : 'dashboardNoClients')}{clients.length > 0 && <button className="font-semibold text-mis-primary" type="button" onClick={() => { setSearch(''); setKind('all'); }}>{ct('clearFilters')}</button>}</div>}
    {rows.length > 0 && <footer className="border-t border-slate-100 px-5 py-3 text-xs text-slate-500">{ct('dashboardShownClients').replace('{shown}', f.number(rows.length)).replace('{total}', f.number(filtered.length))}</footer>}
  </section>;
}
