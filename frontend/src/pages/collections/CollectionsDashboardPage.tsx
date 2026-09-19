import { AlertTriangle, ArrowUpRight, BarChart3, Briefcase, Building2, ClipboardList, Flame, HandCoins, HeartCrack, Landmark, Layers, MapPin, MessageSquareWarning, PhoneCall, PiggyBank, RefreshCw, Scale, UserX, Wallet } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { Section } from '../../components/common/Section';
import { StatusBadge, type StatusTone } from '../../components/common/StatusBadge';
import { useAuth } from '../../context/AuthContext';
import { CollectionStatus, useCollectionFormat } from '../../features/collections/components/CollectionsUi';
import { BankLogo } from '../../features/collections/components/BankLogo';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { organizationPath } from '../../features/collections/organizationClassification';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { CollectionDashboard, CollectionDashboardBucketSlice, CollectionDashboardClientSlice, CollectionDashboardTrendPoint, PromiseItem, WorkQueue } from '../../features/collections/types/collections';

export function CollectionsDashboardPage() {
  const { ct, language } = useCollectionsLocalization();
  const f = useCollectionFormat();
  const { user } = useAuth();
  const [params] = useSearchParams();
  const organizationId = params.get('organizationId') ?? undefined;
  const isManager = user?.roles.some(role => ['Admin', 'CollectionsOperationsManager', 'CollectionsSupervisor'].includes(role));
  const locale = language === 'ar' ? 'ar-EG' : 'en-EG';
  const [data, setData] = useState<CollectionDashboard>();
  const [work, setWork] = useState<WorkQueue>();
  const [error, setError] = useState(false);
  const [loading, setLoading] = useState(false);

  const load = useCallback(() => {
    setError(false);
    setLoading(true);
    Promise.all([collectionsService.dashboard(organizationId), collectionsService.myWork()])
      .then(([dashboard, queue]) => { setData(dashboard); setWork(queue); })
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }, [organizationId]);

  useEffect(() => { load(); }, [load]);

  const overdueShare = data && data.totalOutstanding > 0 ? Math.round((data.totalOverdue / data.totalOutstanding) * 100) : 0;
  const coverage = data && data.totalCases > 0 ? Math.round((data.assignedCases / data.totalCases) * 100) : 0;
  const buckets = data?.byBucket ?? [];
  const trend = data?.collectionTrend ?? [];
  const statuses = data?.byStatus ?? [];
  const clients = data?.byClient ?? [];
  const hasCreditorTypes = clients.some(item => item.organizationType);
  const banks = useMemo(() => hasCreditorTypes ? clients.filter(item => item.organizationType === 'BANK') : clients, [clients, hasCreditorTypes]);
  const companies = useMemo(() => hasCreditorTypes ? clients.filter(item => item.organizationType !== 'BANK') : [], [clients, hasCreditorTypes]);
  const legalCases = statuses.find(item => item.status === 'LEGAL')?.cases ?? 0;
  const actionItems = useMemo(() => {
    if (!data) return [];
    return [
      { value: data.unassignedCases, label: ct('unassigned'), href: isManager ? '/collections/assignments' : '/collections/cases', icon: <UserX className="h-5 w-5" />, tone: data.unassignedCases ? 'warning' : 'success' },
      { value: data.overdueFollowUps ?? 0, label: ct('overdueFollowUpsLabel'), href: '/collections/cases', icon: <PhoneCall className="h-5 w-5" />, tone: (data.overdueFollowUps ?? 0) ? 'danger' : 'success' },
      { value: data.promisesDueToday, label: ct('dueToday'), href: '/collections/promises?status=DUE_TODAY', icon: <HandCoins className="h-5 w-5" />, tone: data.promisesDueToday ? 'warning' : 'success' },
      { value: data.brokenPromises, label: ct('brokenPtp'), href: '/collections/promises?status=BROKEN', icon: <HeartCrack className="h-5 w-5" />, tone: data.brokenPromises ? 'danger' : 'success' },
      { value: data.pendingReviews, label: ct('pendingReviews'), href: '/collections/payments', icon: <ClipboardList className="h-5 w-5" />, tone: data.pendingReviews ? 'warning' : 'success' },
      { value: data.openComplaints, label: ct('complaints'), href: '/collections/complaints', icon: <MessageSquareWarning className="h-5 w-5" />, tone: data.openComplaints ? 'danger' : 'success' },
      { value: data.highRiskCases, label: ct('highRisk'), href: '/collections/cases?priority=HIGH', icon: <Flame className="h-5 w-5" />, tone: data.highRiskCases ? 'danger' : 'success' },
      { value: data.visitsToday, label: ct('visitsToday'), href: '/collections/visits', icon: <MapPin className="h-5 w-5" />, tone: 'info' },
    ] as const;
  }, [ct, data, isManager]);
  const urgentCount = actionItems.filter(item => item.tone === 'danger' || item.tone === 'warning').reduce((sum, item) => sum + item.value, 0);

  if (error) return <ErrorState title={ct('loadError')} onRetry={load} />;
  if (!data || !work) return <div className="flex min-h-[420px] items-center justify-center"><LoadingSpinner /></div>;

  return (
    <div>
      <PageHeader
        actions={(
          <div className="flex flex-wrap gap-2">
            <Button fullWidth={false} isLoading={loading} leftIcon={<RefreshCw className="h-4 w-4" />} onClick={load} variant="outline">{ct('refresh')}</Button>
            <Link className="inline-flex h-10 items-center gap-2 rounded-xl border border-mis-border bg-white px-4 text-sm font-semibold text-mis-navy hover:border-mis-sky" to="/collections/creditors">{ct('creditorDashboard')}</Link>
            <Link className="inline-flex h-10 items-center gap-2 rounded-xl border border-mis-border bg-white px-4 text-sm font-semibold text-mis-navy hover:border-mis-sky" to="/collections/collectors">{ct('collectorDashboard')}</Link>
            <Link className="inline-flex h-10 items-center gap-2 rounded-xl bg-mis-primary px-4 text-sm font-semibold text-white hover:bg-mis-deep" to="/collections/reports"><BarChart3 className="h-4 w-4" />{ct('goToReports')}</Link>
          </div>
        )}
        description={ct('dashboardSubtitle')}
        eyebrow={ct('collections')}
        title={ct('commandCenter')}
      />

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <HeroMetric hint={ct('coverageHint').replace('{assigned}', f.number(data.assignedCases)).replace('{total}', f.number(data.totalCases))} href="/collections/cases" icon={<Layers className="h-5 w-5" />} label={ct('totalCases')} tone="navy" value={f.number(data.totalCases)} />
        <HeroMetric hint={`${banks.length + companies.length} ${ct('clients')}`} href="/banks" icon={<Wallet className="h-5 w-5" />} label={ct('totalOutstanding')} tone="navy" value={f.money(data.totalOutstanding)} />
        <HeroMetric hint={`${overdueShare}% ${ct('ofOutstanding')}`} href="/collections/cases" icon={<AlertTriangle className="h-5 w-5" />} label={ct('totalOverdue')} tone="red" value={f.money(data.totalOverdue)} />
        <HeroMetric hint={`${data.achievementPercent.toFixed(1)}% ${ct('ofMonthlyTarget')}`} href="/collections/payments" icon={<PiggyBank className="h-5 w-5" />} label={ct('collectedToday')} tone="green" value={f.money(data.collectedToday)} />
      </section>

      <div className="mt-6 grid gap-6 xl:grid-cols-2">
        <CreditorBook
          empty={ct('noBanksDebt')}
          href="/banks"
          icon={<Landmark className="h-5 w-5" />}
          items={banks}
          kind="bank"
          labels={{ cases: ct('casesCount'), debt: ct('creditorDebt'), desk: ct('openBank'), legal: ct('creditorLegal'), overdue: ct('creditorOverdueCol'), unassigned: ct('unassigned'), viewCases: ct('viewCreditorCases') }}
          money={f.money}
          number={f.number}
          summary={ct('bankBookCount').replace('{count}', f.number(banks.length))}
          title={ct('debtByBank')}
        />
        <CreditorBook
          empty={ct('noCompaniesDebt')}
          href="/installment-companies"
          icon={<Building2 className="h-5 w-5" />}
          items={companies}
          kind="installment"
          labels={{ cases: ct('casesCount'), debt: ct('creditorDebt'), desk: ct('openInstallmentCompany'), legal: ct('creditorLegal'), overdue: ct('creditorOverdueCol'), unassigned: ct('unassigned'), viewCases: ct('viewCreditorCases') }}
          money={f.money}
          number={f.number}
          summary={ct('companyBookCount').replace('{count}', f.number(companies.length))}
          title={ct('debtByCompany')}
        />
      </div>

      <Section className="mt-6" description={ct('caseMixHelp')} title={ct('caseMix')}>
        {statuses.length ? (
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {statuses.map(item => (
              <Link className="rounded-xl border border-mis-border bg-white p-4 transition hover:border-mis-sky hover:shadow-sm" key={item.status} to={`/collections/cases?status=${encodeURIComponent(item.status)}`}>
                <div className="flex items-center justify-between gap-3">
                  <p className="text-sm font-semibold text-slate-600">{statusLabel(ct, item.status)}</p>
                  {item.status === 'LEGAL' ? <Scale className="h-4 w-4 text-amber-700" /> : null}
                </div>
                <p className="mt-2 text-2xl font-bold tabular-nums text-mis-navy">{f.number(item.cases)}</p>
                <p className="mt-1 text-xs text-slate-500" data-bidi="ltr">{f.money(item.outstanding)}</p>
              </Link>
            ))}
          </div>
        ) : <EmptyState compact description={ct('noClientBreakdown')} title={ct('noClientBreakdown')} />}
      </Section>

      <Section className="mt-6" description={ct('actionRequiredHelp')} title={ct('actionRequired')}>
        {urgentCount === 0 ? <p className="mb-4 text-sm font-semibold text-emerald-700">{ct('clearQueues')}</p> : null}
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {actionItems.map(item => (
            <Link className={`rounded-xl border p-4 transition hover:-translate-y-0.5 hover:shadow-sm ${actionTone(item.tone)}`} key={item.label} to={item.href}>
              <div className="flex items-center justify-between gap-3">
                <span>{item.icon}</span>
                <strong className="text-2xl tabular-nums leading-snug">{f.number(item.value)}</strong>
              </div>
              <p className="mt-3 text-sm font-semibold leading-6">{item.label}</p>
            </Link>
          ))}
        </div>
      </Section>

      <div className="mt-6 grid gap-6 xl:grid-cols-[minmax(0,1.4fr)_minmax(280px,0.6fr)]">
        <Section action={<Link className="text-sm font-semibold leading-snug text-mis-primary" to="/collections/reports">{ct('reports')}</Link>} title={ct('bucketAging')}>
          <MoneyBars casesLabel={count => ct('casesOnFile').replace('{count}', f.number(count))} emptyText={ct('noBucketBreakdown')} hrefFor={item => `/collections/cases?bucket=${encodeURIComponent(item.id)}`} items={buckets.map((item: CollectionDashboardBucketSlice) => ({ id: item.code, name: item.name, amount: item.overdue || item.outstanding, cases: item.cases }))} money={f.money} />
        </Section>
        <Section title={ct('operationalHealth')}>
          <dl className="space-y-4 text-sm">
            <SnapshotRow label={ct('assignmentCoverage')} value={`${coverage}%`} hint={ct('coverageHint').replace('{assigned}', f.number(data.assignedCases)).replace('{total}', f.number(data.totalCases))} />
            <SnapshotRow label={ct('collectedMtd')} value={f.money(data.collectedMonthToDate)} hint={`${data.achievementPercent.toFixed(1)}% ${ct('ofMonthlyTarget')}`} href="/collections/reports" />
            <SnapshotRow label={ct('activeCollectors')} value={f.number(data.activeCollectors)} />
            <SnapshotRow label={ct('legalCases')} value={f.number(legalCases)} href="/collections/cases?status=LEGAL" />
            <SnapshotRow label={ct('activePtp')} value={f.number(data.activePromises)} href="/collections/promises" />
            <SnapshotRow label={ct('visitsToday')} value={f.number(data.visitsToday)} href="/collections/visits" />
          </dl>
        </Section>
      </div>

      <Section className="mt-6" description={ct('lastSevenDays')} title={ct('collectionTrend')}>
        <CollectionTrend emptyText={ct('noCollectionTrend')} locale={locale} money={f.money} points={trend} />
      </Section>

      <div className="mt-6 mb-2 flex items-end justify-between gap-3">
        <div>
          <h2 className="text-xl font-bold text-mis-navy">{ct('myWork')}</h2>
          <p className="mt-1 text-sm text-slate-500">{ct('workQueueHelp')}</p>
        </div>
        <Link className="inline-flex items-center gap-1 text-sm font-semibold text-mis-primary" to="/collections/cases">{ct('cases')}<ArrowUpRight className="h-4 w-4 rtl:rotate-[-90deg]" /></Link>
      </div>
      <div className="grid gap-5 xl:grid-cols-2">
        <Queue empty={ct('noWork')} icon={<PhoneCall className="h-5 w-5" />} openLabel={ct('openCase')} rows={work.callsDue} title={ct('callsDue')} />
        <Queue empty={ct('noWork')} icon={<AlertTriangle className="h-5 w-5" />} openLabel={ct('openCase')} rows={work.highPriorityCases} title={ct('priorityCases')} />
        <PromiseQueue date={f.date} empty={ct('noQueueItems')} icon={<HandCoins className="h-5 w-5" />} money={f.money} rows={work.promisesDue} title={ct('promisesDueQueue')} />
        <PromiseQueue date={f.date} empty={ct('noQueueItems')} icon={<HeartCrack className="h-5 w-5" />} money={f.money} rows={work.brokenPromises} title={ct('brokenPromisesQueue')} />
      </div>
    </div>
  );
}

function CreditorBook({ empty, href, icon, items, kind, labels, money, number, summary, title }: {
  empty: string;
  href: string;
  icon: ReactNode;
  items: CollectionDashboardClientSlice[];
  kind: 'bank' | 'installment';
  labels: { cases: string; debt: string; desk: string; legal: string; overdue: string; unassigned: string; viewCases: string };
  money: (value: number) => string;
  number: (value: number) => string;
  summary: string;
  title: string;
}) {
  const cases = items.reduce((sum, item) => sum + item.cases, 0);
  const outstanding = items.reduce((sum, item) => sum + item.outstanding, 0);
  return (
    <Section
          action={<Link className="text-sm font-semibold text-mis-primary" to={href}>{labels.desk}</Link>}
      title={title}
    >
      <div className="mb-4 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-slate-500">
        <span className="inline-flex items-center gap-2 font-semibold text-mis-navy">{icon}{summary}</span>
        <span>{number(cases)} {labels.cases}</span>
        <span data-bidi="ltr">{money(outstanding)}</span>
      </div>
      {items.length ? (
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200 text-start text-xs font-semibold uppercase tracking-wide text-slate-400">
                <th className="pb-2 pe-3 font-semibold">{title}</th>
                <th className="pb-2 pe-3 text-end font-semibold">{labels.cases}</th>
                <th className="pb-2 pe-3 text-end font-semibold">{labels.debt}</th>
                <th className="pb-2 pe-3 text-end font-semibold">{labels.overdue}</th>
                <th className="pb-2 pe-3 text-end font-semibold">{labels.unassigned}</th>
                <th className="pb-2 text-end font-semibold">{labels.legal}</th>
              </tr>
            </thead>
            <tbody>
              {items.map(item => (
                <tr className="border-b border-slate-100 last:border-0" key={item.id}>
                  <td className="py-3 pe-3">
                    <div className="flex items-center gap-3">
                      <BankLogo className="h-11 w-11 rounded-xl" code={item.code ?? ''} logoUrl={item.logoUrl} name={item.name} />
                      <div className="min-w-0">
                        <Link className="font-semibold text-mis-navy hover:text-mis-primary" to={organizationPath(kind, item.id)}>{item.name}</Link>
                        {item.code ? <p className="mt-0.5 text-[11px] font-semibold uppercase tracking-wide text-slate-400" data-bidi="ltr">{item.code}</p> : null}
                        <div className="mt-1 flex gap-3 text-xs">
                          <Link className="text-mis-primary" to={`/collections/cases?organizationId=${item.id}`}>{labels.viewCases}</Link>
                          <Link className="text-slate-400 hover:text-mis-primary" to={organizationPath(kind, item.id)}>{labels.desk}</Link>
                        </div>
                      </div>
                    </div>
                  </td>
                  <td className="py-3 pe-3 text-end tabular-nums font-semibold text-mis-navy">{number(item.cases)}</td>
                  <td className="py-3 pe-3 text-end tabular-nums font-semibold text-mis-navy" data-bidi="ltr">{money(item.outstanding)}</td>
                  <td className="py-3 pe-3 text-end tabular-nums text-rose-700" data-bidi="ltr">{money(item.overdue)}</td>
                  <td className="py-3 pe-3 text-end tabular-nums">{number(item.unassigned ?? 0)}</td>
                  <td className="py-3 text-end tabular-nums">{number(item.legalCases ?? 0)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : <EmptyState compact description={empty} title={empty} />}
    </Section>
  );
}

function statusLabel(ct: (key: 'ACTIVE' | 'ON_HOLD' | 'SETTLED' | 'CLOSED' | 'LEGAL' | 'WRITE_OFF') => string, status: string) {
  switch (status) {
    case 'ACTIVE':
    case 'ON_HOLD':
    case 'SETTLED':
    case 'CLOSED':
    case 'LEGAL':
    case 'WRITE_OFF':
      return ct(status);
    default:
      return status;
  }
}

function HeroMetric({ hint, href, icon, label, tone, value }: { hint: string; href: string; icon: ReactNode; label: string; tone: 'navy' | 'red' | 'green'; value: string }) {
  const iconTone = { navy: 'bg-mis-pale text-mis-primary', red: 'bg-rose-50 text-rose-700', green: 'bg-emerald-50 text-emerald-700' };
  return (
    <Link className="group relative block rounded-2xl border border-mis-border bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-mis-primary hover:shadow-md" to={href}>
      <span className="pointer-events-none absolute inset-0 overflow-hidden rounded-2xl" aria-hidden="true">
        <span className="absolute -end-6 -top-6 h-24 w-24 rounded-full bg-mis-pale/60" />
      </span>
      <div className="relative flex items-start justify-between gap-3">
        <p className="min-w-0 text-sm font-semibold leading-6 text-slate-500 [overflow-wrap:anywhere]">{label}</p>
        <span className={`grid h-11 w-11 shrink-0 place-items-center rounded-xl ${iconTone[tone]}`}>{icon}</span>
      </div>
      <p className="relative mt-3 text-2xl font-bold tabular-nums leading-snug text-mis-navy sm:text-3xl" data-bidi="ltr">{value}</p>
      <p className="relative mt-2 text-xs leading-6 text-slate-500 [overflow-wrap:anywhere]">{hint}</p>
    </Link>
  );
}

function actionTone(tone: StatusTone) {
  if (tone === 'danger') return 'border-rose-200 bg-rose-50 text-rose-800';
  if (tone === 'warning') return 'border-amber-200 bg-amber-50 text-amber-800';
  if (tone === 'success') return 'border-emerald-100 bg-emerald-50/70 text-emerald-800';
  return 'border-mis-border bg-white text-mis-navy';
}

function MoneyBars({ casesLabel, emptyText, hrefFor, items, money }: { casesLabel: (count: number) => string; emptyText: string; hrefFor: (item: { id: string }) => string; items: { id: string; name: string; amount: number; cases: number }[]; money: (value: number) => string }) {
  const populated = items.filter(item => item.amount > 0 || item.cases > 0);
  const maximum = Math.max(...populated.map(item => item.amount), 1);
  if (!populated.length) return <EmptyState compact description={emptyText} title={emptyText} />;
  return (
    <div className="space-y-4">
      {populated.map(item => (
        <Link className="-m-1 block rounded-xl p-1 hover:bg-mis-pale/40" key={item.id} to={hrefFor(item)}>
          <div className="mb-2 flex items-center justify-between gap-4 text-sm">
            <span className="min-w-0 truncate font-semibold text-slate-700">{item.name}</span>
            <span className="shrink-0 font-bold tabular-nums text-mis-navy" data-bidi="ltr">{money(item.amount)}</span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-mis-pale"><div className="h-full rounded-full bg-mis-primary" style={{ width: `${Math.max((item.amount / maximum) * 100, item.amount ? 4 : 0)}%` }} /></div>
          <p className="mt-1 text-xs text-slate-400">{casesLabel(item.cases)}</p>
        </Link>
      ))}
    </div>
  );
}

function CollectionTrend({ emptyText, locale, money, points }: { emptyText: string; locale: string; money: (value: number) => string; points: CollectionDashboardTrendPoint[] }) {
  const maximum = Math.max(...points.map(point => point.collected), 0);
  if (!points.length || maximum <= 0) return <EmptyState compact description={emptyText} icon={<PiggyBank className="h-5 w-5" />} title={emptyText} />;
  return (
    <div>
      <div className="flex h-44 items-end gap-2 border-b border-slate-200 pb-1" role="img">
        {points.map(point => (
          <div className="flex h-full min-w-0 flex-1 flex-col items-center justify-end" key={point.date} title={`${point.date}: ${money(point.collected)}`}>
            <div className="w-full max-w-8 rounded-t-md bg-mis-primary" style={{ height: `${Math.max((point.collected / maximum) * 100, point.collected ? 6 : 2)}%` }} />
          </div>
        ))}
      </div>
      <div className="mt-2 flex justify-between text-[11px] text-slate-400">
        <span>{formatDay(points[0]?.date, locale)}</span>
        <span>{formatDay(points.at(-1)?.date, locale)}</span>
      </div>
    </div>
  );
}

function SnapshotRow({ hint, href, label, value }: { hint?: string; href?: string; label: string; value: string }) {
  const content = (
    <div className="flex items-start justify-between gap-3">
      <div className="min-w-0">
        <dt className="font-semibold text-slate-500">{label}</dt>
        {hint ? <p className="mt-1 text-xs text-slate-400">{hint}</p> : null}
      </div>
      <dd className="text-lg font-bold tabular-nums text-mis-navy">{value}</dd>
    </div>
  );
  return href ? <Link className="block rounded-xl p-1 -m-1 hover:bg-mis-pale/40" to={href}>{content}</Link> : content;
}

function Queue({ empty, icon, openLabel, rows, title }: { empty: string; icon: ReactNode; openLabel: string; rows: WorkQueue['callsDue']; title: string }) {
  return (
    <article className="min-w-0 overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
      <header className="flex items-center gap-2 border-b border-mis-border px-5 py-4 font-bold text-mis-navy">
        <span className="shrink-0 text-mis-primary">{icon}</span>
        <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">{title}</span>
        <StatusBadge tone={rows.length ? 'warning' : 'success'}>{rows.length}</StatusBadge>
      </header>
      {rows.length ? (
        <ul className="divide-y divide-mis-border">
          {rows.slice(0, 6).map(row => (
            <li className="flex min-w-0 items-center gap-3 px-5 py-4" key={row.id}>
              <div className="min-w-0 flex-1">
                <p className="truncate font-semibold text-mis-navy">{row.customerName}</p>
                <p className="mt-1 text-xs text-slate-500"><span data-bidi="ltr">{row.caseNumber}</span> · {row.clientName} · {row.bucket}</p>
              </div>
              <CollectionStatus value={row.priority} />
              <Link aria-label={openLabel} className="shrink-0 rounded-lg p-2 text-mis-primary hover:bg-mis-pale" to={`/collections/cases/${row.id}`}><ArrowUpRight className="h-4 w-4 rtl:rotate-[-90deg]" /></Link>
            </li>
          ))}
        </ul>
      ) : <EmptyState compact description={empty} icon={<Briefcase className="h-5 w-5" />} title={empty} />}
    </article>
  );
}

function PromiseQueue({ date, empty, icon, money, rows, title }: { date: (value?: string) => string; empty: string; icon: ReactNode; money: (value: number) => string; rows: PromiseItem[]; title: string }) {
  return (
    <article className="min-w-0 overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
      <header className="flex items-center gap-2 border-b border-mis-border px-5 py-4 font-bold text-mis-navy">
        <span className="shrink-0 text-mis-primary">{icon}</span>
        <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">{title}</span>
        <StatusBadge tone={rows.length ? 'warning' : 'success'}>{rows.length}</StatusBadge>
      </header>
      {rows.length ? (
        <ul className="divide-y divide-mis-border">
          {rows.slice(0, 6).map(row => (
            <li className="flex min-w-0 items-center gap-3 px-5 py-4" key={row.id}>
              <div className="min-w-0 flex-1">
                <Link className="block truncate font-semibold text-mis-primary" to={`/collections/cases/${row.caseId}`}>{row.customerName}</Link>
                <p className="mt-1 text-xs text-slate-500"><span data-bidi="ltr">{row.caseNumber}</span> · {date(row.promiseDate)}</p>
              </div>
              <span className="shrink-0 text-sm font-bold text-mis-navy" data-bidi="ltr">{money(row.promisedAmount)}</span>
            </li>
          ))}
        </ul>
      ) : <EmptyState compact description={empty} icon={<HandCoins className="h-5 w-5" />} title={empty} />}
    </article>
  );
}

function formatDay(value: string | undefined, locale: string) {
  if (!value) return '';
  return new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'short' }).format(new Date(`${value}T00:00:00`));
}
