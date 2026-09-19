import { Building2, Landmark, RefreshCw, Search } from 'lucide-react';
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
import { classificationPath, deskLabelKey, organizationPath, parseClassification, type OrganizationKind } from '../../features/collections/organizationClassification';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { CollectionCreditorBook, CollectionCreditorDashboard, CollectionCreditorDesk } from '../../features/collections/types/collections';

export function CollectionsCreditorsDashboardPage() {
  const { ct } = useCollectionsLocalization();
  const f = useCollectionFormat();
  const [data, setData] = useState<CollectionCreditorDashboard>();
  const [error, setError] = useState(false);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');

  const load = useCallback(() => {
    setError(false);
    setLoading(true);
    collectionsService.creditorDashboard()
      .then(setData)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { load(); }, [load]);

  const query = search.trim().toLowerCase();
  const banks = useMemo(() => filterBooks(data?.banks ?? [], query), [data, query]);
  const companies = useMemo(() => filterBooks(data?.companies ?? [], query), [data, query]);
  const all = [...(data?.banks ?? []), ...(data?.companies ?? [])];
  const totalCases = all.reduce((sum, item) => sum + item.cases, 0);
  const totalOutstanding = all.reduce((sum, item) => sum + item.outstanding, 0);
  const totalUnassigned = all.reduce((sum, item) => sum + item.unassigned, 0);

  if (error) return <ErrorState title={ct('loadError')} onRetry={load} />;
  if (!data) return <div className="flex min-h-[420px] items-center justify-center"><LoadingSpinner /></div>;

  const labels = {
    assigned: ct('assigned'),
    cases: ct('casesCount'),
    debt: ct('creditorDebt'),
    desks: ct('desksInside'),
    legal: ct('creditorLegal'),
    overdue: ct('creditorOverdueCol'),
    unassigned: ct('unassigned'),
    viewCases: ct('viewCreditorCases'),
  };

  return (
    <div>
      <PageHeader
        actions={(
          <div className="flex flex-wrap gap-2">
            <Button fullWidth={false} isLoading={loading} leftIcon={<RefreshCw className="h-4 w-4" />} onClick={load} variant="outline">{ct('refresh')}</Button>
            <Link className="inline-flex h-10 items-center rounded-xl border border-mis-border bg-white px-4 text-sm font-semibold text-mis-navy hover:border-mis-sky" to="/collections/dashboard">{ct('overview')}</Link>
            <Link className="inline-flex h-10 items-center rounded-xl bg-mis-primary px-4 text-sm font-semibold text-white hover:bg-mis-deep" to="/collections/collectors">{ct('collectorDashboard')}</Link>
          </div>
        )}
        description={ct('creditorDashboardSubtitle')}
        eyebrow={ct('collections')}
        title={ct('creditorDashboard')}
      />

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <SummaryCard hint={ct('bankBookCount').replace('{count}', f.number(data.banks.length))} icon={<Landmark className="h-5 w-5" />} label={ct('debtByBank')} value={f.number(data.banks.length)} />
        <SummaryCard hint={ct('companyBookCount').replace('{count}', f.number(data.companies.length))} icon={<Building2 className="h-5 w-5" />} label={ct('debtByCompany')} value={f.number(data.companies.length)} />
        <SummaryCard hint={`${f.number(totalCases)} ${ct('casesCount')}`} icon={<Landmark className="h-5 w-5" />} label={ct('totalOutstanding')} value={f.money(totalOutstanding)} />
        <SummaryCard hint={ct('unassigned')} icon={<Building2 className="h-5 w-5" />} label={ct('unassigned')} value={f.number(totalUnassigned)} />
      </section>

      <label className="relative mt-6 block">
        <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
        <input className="h-12 w-full rounded-2xl border border-mis-border bg-white pe-4 ps-10 text-sm shadow-sm outline-none focus:border-mis-primary" onChange={event => setSearch(event.target.value)} placeholder={ct('searchCreditors')} value={search} />
      </label>

      <div className="mt-8 space-y-10">
        <CreditorGroup
          books={banks}
          deskLabel={desk => deskTitle(ct, desk)}
          empty={ct('noBanksDebt')}
          icon={<Landmark className="h-5 w-5" />}
          kind="bank"
          labels={{ ...labels, open: ct('openBank') }}
          money={f.money}
          number={f.number}
          title={ct('debtByBank')}
        />
        <CreditorGroup
          books={companies}
          deskLabel={desk => deskTitle(ct, desk)}
          empty={ct('noCompaniesDebt')}
          icon={<Building2 className="h-5 w-5" />}
          kind="installment"
          labels={{ ...labels, open: ct('openInstallmentCompany') }}
          money={f.money}
          number={f.number}
          title={ct('debtByCompany')}
        />
      </div>
    </div>
  );
}

function filterBooks(books: CollectionCreditorBook[], query: string) {
  if (!query) return books;
  return books.filter(book => `${book.name} ${book.code}`.toLowerCase().includes(query) || book.desks.some(desk => desk.name.toLowerCase().includes(query)));
}

function deskTitle(ct: (key: string) => string, desk: CollectionCreditorDesk) {
  const parsed = parseClassification(desk.primaryClassification, desk.subClassification);
  if (parsed) return `${ct(deskLabelKey(parsed.primary))} · ${ct(deskLabelKey(parsed.secondary))}`;
  return desk.name || ct('unclassified');
}

function CreditorGroup({ books, deskLabel, empty, icon, kind, labels, money, number, title }: {
  books: CollectionCreditorBook[];
  deskLabel: (desk: CollectionCreditorDesk) => string;
  empty: string;
  icon: ReactNode;
  kind: OrganizationKind;
  labels: { assigned: string; cases: string; debt: string; desks: string; legal: string; overdue: string; open: string; unassigned: string; viewCases: string };
  money: (value: number) => string;
  number: (value: number) => string;
  title: string;
}) {
  return (
    <section>
      <header className="mb-4 flex items-center gap-3">
        <span className="grid h-10 w-10 place-items-center rounded-xl bg-mis-pale text-mis-primary">{icon}</span>
        <div>
          <h2 className="text-lg font-bold text-mis-navy">{title}</h2>
          <p className="text-sm text-slate-500">{number(books.length)}</p>
        </div>
      </header>
      {books.length ? (
        <div className="space-y-4">
          {books.map(book => (
            <article className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm" key={book.id}>
              <div className="flex flex-col gap-5 p-5 lg:flex-row lg:items-center lg:justify-between">
                <div className="flex min-w-0 items-center gap-4">
                  <BankLogo className="h-16 w-16 rounded-2xl" code={book.code} logoUrl={book.logoUrl} name={book.name} />
                  <div className="min-w-0">
                    <Link className="block truncate text-lg font-bold text-mis-navy hover:text-mis-primary" to={organizationPath(kind, book.id)}>{book.name}</Link>
                    <p className="mt-1 text-xs font-semibold uppercase tracking-[0.16em] text-slate-400" data-bidi="ltr">{book.code}</p>
                    <div className="mt-2 flex flex-wrap gap-3 text-xs font-semibold">
                      <Link className="text-mis-primary" to={`/collections/cases?organizationId=${book.id}`}>{labels.viewCases}</Link>
                      <Link className="text-slate-500 hover:text-mis-primary" to={organizationPath(kind, book.id)}>{labels.open}</Link>
                    </div>
                  </div>
                </div>
                <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                  <Metric label={labels.cases} value={number(book.cases)} />
                  <Metric label={labels.debt} money value={money(book.outstanding)} />
                  <Metric label={labels.unassigned} value={number(book.unassigned)} />
                  <Metric label={labels.legal} value={number(book.legalCases)} />
                </dl>
              </div>
              <div className="border-t border-mis-border bg-slate-50/70 px-5 py-4">
                <p className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">{labels.desks}</p>
                {book.desks.length ? (
                  <div className="overflow-x-auto rounded-xl border border-mis-border bg-white">
                    <table className="min-w-full text-sm">
                      <thead>
                        <tr className="border-b border-slate-100 text-start text-xs font-semibold uppercase tracking-wide text-slate-400">
                          <th className="px-4 py-2.5 font-semibold">{labels.desks}</th>
                          <th className="px-4 py-2.5 text-end font-semibold">{labels.cases}</th>
                          <th className="px-4 py-2.5 text-end font-semibold">{labels.assigned}</th>
                          <th className="px-4 py-2.5 text-end font-semibold">{labels.unassigned}</th>
                          <th className="px-4 py-2.5 text-end font-semibold">{labels.debt}</th>
                          <th className="px-4 py-2.5 text-end font-semibold">{labels.overdue}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {book.desks.map(desk => {
                          const parsed = parseClassification(desk.primaryClassification, desk.subClassification);
                          const href = parsed ? classificationPath(kind, book.id, parsed.primary, parsed.secondary) : organizationPath(kind, book.id);
                          return (
                            <tr className="border-t border-slate-50" key={desk.portfolioId}>
                              <td className="px-4 py-2.5">
                                <Link className="font-semibold text-mis-navy hover:text-mis-primary" to={href}>{deskLabel(desk)}</Link>
                                {desk.name && parsed ? <p className="mt-0.5 text-xs text-slate-400">{desk.name}</p> : null}
                              </td>
                              <td className="px-4 py-2.5 text-end tabular-nums">{number(desk.cases)}</td>
                              <td className="px-4 py-2.5 text-end tabular-nums">{number(desk.assigned)}</td>
                              <td className="px-4 py-2.5 text-end tabular-nums">{number(desk.unassigned)}</td>
                              <td className="px-4 py-2.5 text-end tabular-nums font-semibold text-mis-navy" data-bidi="ltr">{money(desk.outstanding)}</td>
                              <td className="px-4 py-2.5 text-end tabular-nums text-rose-700" data-bidi="ltr">{money(desk.overdue)}</td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                ) : <p className="text-sm text-slate-500">{empty}</p>}
              </div>
            </article>
          ))}
        </div>
      ) : <EmptyState compact description={empty} title={empty} />}
    </section>
  );
}

function Metric({ label, money, value }: { label: string; money?: boolean; value: string }) {
  return (
    <div className="min-w-[7.5rem] rounded-xl bg-slate-50 px-3 py-2 text-end">
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
      <p className="mt-2 text-xs text-slate-500">{hint}</p>
    </article>
  );
}
