import { RefreshCw, Search, UserRound, UserX } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { BankLogo } from '../../features/collections/components/BankLogo';
import { useCollectionFormat } from '../../features/collections/components/CollectionsUi';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { organizationPath, type OrganizationKind } from '../../features/collections/organizationClassification';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { CollectionCollectorDashboard, CollectionCollectorOrgSlice } from '../../features/collections/types/collections';

export function CollectionsCollectorsDashboardPage() {
  const { ct } = useCollectionsLocalization();
  const f = useCollectionFormat();
  const [data, setData] = useState<CollectionCollectorDashboard>();
  const [error, setError] = useState(false);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');

  const load = useCallback(() => {
    setError(false);
    setLoading(true);
    collectionsService.collectorDashboard()
      .then(setData)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { load(); }, [load]);

  const query = search.trim().toLowerCase();
  const collectors = useMemo(() => {
    const rows = data?.collectors ?? [];
    if (!query) return rows;
    return rows.filter(row => `${row.name} ${row.loginCode}`.toLowerCase().includes(query) || row.organizations.some(org => `${org.name} ${org.code ?? ''}`.toLowerCase().includes(query)));
  }, [data, query]);

  if (error) return <ErrorState title={ct('loadError')} onRetry={load} />;
  if (!data) return <div className="flex min-h-[420px] items-center justify-center"><LoadingSpinner /></div>;

  const orgLabels = { cases: ct('casesCount'), debt: ct('creditorDebt'), legal: ct('creditorLegal'), overdue: ct('creditorOverdueCol') };

  return (
    <div>
      <PageHeader
        actions={(
          <div className="flex flex-wrap gap-2">
            <Button fullWidth={false} isLoading={loading} leftIcon={<RefreshCw className="h-4 w-4" />} onClick={load} variant="outline">{ct('refresh')}</Button>
            <Link className="inline-flex h-10 items-center rounded-xl border border-mis-border bg-white px-4 text-sm font-semibold text-mis-navy hover:border-mis-sky" to="/collections/dashboard">{ct('overview')}</Link>
            <Link className="inline-flex h-10 items-center rounded-xl bg-mis-primary px-4 text-sm font-semibold text-white hover:bg-mis-deep" to="/collections/creditors">{ct('creditorDashboard')}</Link>
          </div>
        )}
        description={ct('collectorDashboardSubtitle')}
        eyebrow={ct('collections')}
        title={ct('collectorDashboard')}
      />

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <SummaryCard hint={ct('activeCollectors')} icon={<UserRound className="h-5 w-5" />} label={ct('collectorsLabel')} value={f.number(data.collectors.length)} />
        <SummaryCard hint={ct('casesCount')} icon={<UserRound className="h-5 w-5" />} label={ct('assigned')} value={f.number(data.collectors.reduce((sum, item) => sum + item.cases, 0))} />
        <SummaryCard hint={ct('unassignedPoolHelp')} icon={<UserX className="h-5 w-5" />} label={ct('unassignedPool')} value={f.number(data.unassignedCases)} />
        <SummaryCard hint={ct('creditorDebt')} icon={<UserX className="h-5 w-5" />} label={ct('totalOutstanding')} value={f.money(data.unassignedOutstanding)} />
      </section>

      <section className="mt-8 overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <header className="flex flex-wrap items-start justify-between gap-3 border-b border-mis-border px-5 py-4">
          <div>
            <h2 className="font-bold text-mis-navy">{ct('unassignedPool')}</h2>
            <p className="mt-1 text-sm text-slate-500">{ct('unassignedPoolHelp')}</p>
          </div>
          <Link className="text-sm font-semibold text-mis-primary" to="/collections/cases?collectorId=unassigned">{ct('viewCollectorCases')}</Link>
        </header>
        <div className="p-5">
          {data.unassignedByOrganization.length
            ? <OrgRows kindLabel={ct} labels={orgLabels} money={f.money} number={f.number} rows={data.unassignedByOrganization} unassigned />
            : <EmptyState compact description={ct('noUnassignedQueue')} title={ct('unassignedPool')} />}
        </div>
      </section>

      <label className="relative mt-8 block">
        <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
        <input className="h-12 w-full rounded-2xl border border-mis-border bg-white pe-4 ps-10 text-sm shadow-sm outline-none focus:border-mis-primary" onChange={event => setSearch(event.target.value)} placeholder={ct('searchCollectors')} value={search} />
      </label>

      <section className="mt-6">
        <h2 className="mb-4 text-lg font-bold text-mis-navy">{ct('collectorDashboard')}</h2>
        {collectors.length ? (
          <div className="space-y-4">
            {collectors.map(collector => (
              <article className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm" key={collector.id}>
                <div className="flex flex-col gap-5 p-5 lg:flex-row lg:items-center lg:justify-between">
                  <div className="flex min-w-0 items-center gap-4">
                    <span className="grid h-16 w-16 shrink-0 place-items-center rounded-2xl bg-mis-pale text-lg font-bold text-mis-primary">{initials(collector.name)}</span>
                    <div className="min-w-0">
                      <p className="truncate text-lg font-bold text-mis-navy">{collector.name}</p>
                      <p className="mt-1 text-xs text-slate-500">{ct('loginCode')}: <span data-bidi="ltr">{collector.loginCode}</span></p>
                      <p className="mt-1 text-xs text-slate-500">{ct('coversCreditors').replace('{count}', f.number(collector.organizations.length))}</p>
                      <Link className="mt-2 inline-block text-xs font-semibold text-mis-primary" to={`/collections/cases?collectorId=${collector.id}`}>{ct('viewCollectorCases')}</Link>
                    </div>
                  </div>
                  <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                    <Metric label={ct('casesCount')} value={f.number(collector.cases)} />
                    <Metric label={ct('creditorDebt')} money value={f.money(collector.outstanding)} />
                    <Metric label={ct('legalCases')} value={f.number(collector.legalCases)} />
                    <Metric label={ct('highRisk')} value={f.number(collector.highRiskCases)} />
                  </dl>
                </div>
                <div className="border-t border-mis-border bg-slate-50/70 px-5 py-4">
                  <p className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">{ct('responsibleFor')}</p>
                  {collector.organizations.length
                    ? <OrgRows collectorId={collector.id} kindLabel={ct} labels={orgLabels} money={f.money} number={f.number} rows={collector.organizations} />
                    : <p className="text-sm text-slate-500">{ct('noOrgCoverage')}</p>}
                </div>
              </article>
            ))}
          </div>
        ) : <EmptyState compact description={ct('noCollectorsBook')} title={ct('noCollectorsBook')} />}
      </section>
    </div>
  );
}

function OrgRows({ collectorId, kindLabel, labels, money, number, rows, unassigned }: {
  collectorId?: string;
  kindLabel: (key: string) => string;
  labels: { cases: string; debt: string; legal: string; overdue: string };
  money: (value: number) => string;
  number: (value: number) => string;
  rows: CollectionCollectorOrgSlice[];
  unassigned?: boolean;
}) {
  return (
    <ul className="space-y-3">
      {rows.map(row => {
        const kind: OrganizationKind = row.organizationType === 'BANK' ? 'bank' : 'installment';
        const casesHref = unassigned
          ? `/collections/cases?organizationId=${row.organizationId}&collectorId=unassigned`
          : `/collections/cases?organizationId=${row.organizationId}${collectorId ? `&collectorId=${collectorId}` : ''}`;
        const typeKey = row.organizationType === 'BANK' ? 'BANK' : row.organizationType === 'FINANCIAL_INSTITUTION' ? 'FINANCIAL_INSTITUTION' : 'CONSUMER_FINANCE';
        return (
          <li className="flex flex-col gap-4 rounded-xl border border-mis-border bg-white p-4 sm:flex-row sm:items-center sm:justify-between" key={row.organizationId}>
            <div className="flex min-w-0 items-center gap-3">
              <BankLogo className="h-12 w-12 rounded-xl" code={row.code ?? ''} logoUrl={row.logoUrl} name={row.name} />
              <div className="min-w-0">
                <Link className="block truncate font-bold text-mis-navy hover:text-mis-primary" to={organizationPath(kind, row.organizationId)}>{row.name}</Link>
                <p className="mt-0.5 text-xs text-slate-400">{kindLabel(typeKey)}{row.code ? <span data-bidi="ltr"> · {row.code}</span> : null}</p>
                <Link className="mt-1 inline-block text-xs font-semibold text-mis-primary" to={casesHref}>{kindLabel('viewCreditorCases')}</Link>
              </div>
            </div>
            <dl className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              <Metric label={labels.cases} value={number(row.cases)} />
              <Metric label={labels.debt} money value={money(row.outstanding)} />
              <Metric label={labels.overdue} money value={money(row.overdue)} />
              <Metric label={labels.legal} value={number(row.legalCases)} />
            </dl>
          </li>
        );
      })}
    </ul>
  );
}

function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  return (parts.slice(0, 2).map(part => part[0]).join('') || 'C').toUpperCase();
}

function Metric({ label, money, value }: { label: string; money?: boolean; value: string }) {
  return (
    <div className="min-w-[7rem] rounded-xl bg-slate-50 px-3 py-2 text-end">
      <dt className="text-[11px] font-semibold uppercase tracking-wide text-slate-400">{label}</dt>
      <dd className="mt-1 text-sm font-bold tabular-nums text-mis-navy" data-bidi={money ? 'ltr' : undefined}>{value}</dd>
    </div>
  );
}

function SummaryCard({ hint, icon, label, value }: { hint: string; icon: ReactNode; label: string; value: string }) {
  return (
    <article className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
      <div className="flex items-start justify-between gap-3">
        <p className="text-sm font-semibold text-slate-500">{label}</p>
        <span className="grid h-11 w-11 place-items-center rounded-xl bg-mis-pale text-mis-primary">{icon}</span>
      </div>
      <p className="mt-3 text-2xl font-bold tabular-nums text-mis-navy" data-bidi="ltr">{value}</p>
      <p className="mt-2 text-xs leading-6 text-slate-500">{hint}</p>
    </article>
  );
}
