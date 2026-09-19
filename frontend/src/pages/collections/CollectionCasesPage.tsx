import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { Landmark, RefreshCw, Search, X } from 'lucide-react';
import { useEffect, useState, type ReactNode } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { Pagination } from '../../components/common/Pagination';
import { CollectionStatus, CollectorAssignmentLabel, FileCollectorLinkBanner, useCollectionFormat } from '../../features/collections/components/CollectionsUi';
import { useCollectionsLocalization, type CollectionsTranslationKey } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { ClientCard, CollectionCase, CollectorLookup, NationalIdLookup, PagedResult } from '../../features/collections/types/collections';

const PAGE_SIZES = [50, 100, 200];
const UNASSIGNED = 'unassigned';
const SEARCH_FIELDS = ['ALL', 'NATIONAL_ID', 'MOBILE', 'NAME', 'CASE', 'CARD'] as const;
type SearchField = (typeof SEARCH_FIELDS)[number];

const SEARCH_PLACEHOLDERS: Record<SearchField, CollectionsTranslationKey> = {
  ALL: 'searchPlaceholderAll',
  NATIONAL_ID: 'searchPlaceholderNationalId',
  MOBILE: 'searchPlaceholderMobile',
  NAME: 'searchPlaceholderName',
  CASE: 'searchPlaceholderCase',
  CARD: 'searchPlaceholderCard',
};

const SEARCH_LABELS: Record<SearchField, CollectionsTranslationKey> = {
  ALL: 'searchByAll',
  NATIONAL_ID: 'searchByNationalId',
  MOBILE: 'searchByMobile',
  NAME: 'searchByName',
  CASE: 'searchByCase',
  CARD: 'searchByCard',
};

function digitsOnly(value?: string) {
  return (value ?? '').replace(/\D/g, '');
}

export function CollectionCasesPage() {
  const { ct } = useCollectionsLocalization();
  const format = useCollectionFormat();
  const navigate = useNavigate();
  const [url] = useSearchParams();
  const [searchField, setSearchField] = useState<SearchField>('ALL');
  const [searchInput, setSearchInput] = useState(url.get('search') ?? '');
  const [search, setSearch] = useState(url.get('search') ?? '');
  const [organizationId, setOrganizationId] = useState(url.get('organizationId') ?? '');
  const [collectorId, setCollectorId] = useState(url.get('collectorId') ?? '');
  const [status, setStatus] = useState(url.get('status') ?? '');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [data, setData] = useState<PagedResult<CollectionCase>>();
  const [clients, setClients] = useState<ClientCard[]>([]);
  const [collectors, setCollectors] = useState<CollectorLookup[]>([]);
  const [nid, setNid] = useState<NationalIdLookup>();
  const [error, setError] = useState(false);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    const timer = window.setTimeout(() => { setSearch(searchInput.trim()); setPage(1); }, 350);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    let active = true;
    void Promise.all([
      collectionsService.clients({ pageSize: 100, active: true }),
      collectionsService.caseCollectors().catch(() => [] as CollectorLookup[]),
    ]).then(([clientPage, collectorRows]) => {
      if (!active) return;
      setClients(clientPage.items);
      setCollectors(Array.from(new Map(collectorRows.map((row) => [row.id, row])).values()));
    }).catch(() => { if (active) setError(true); });
    return () => { active = false; };
  }, [reload]);

  useEffect(() => {
    let active = true;
    setError(false);
    void collectionsService.listCases({
      page,
      pageSize,
      search,
      searchField: searchField === 'ALL' ? undefined : searchField,
      organizationId: organizationId || undefined,
      collectorId: collectorId && collectorId !== UNASSIGNED ? collectorId : undefined,
      unassigned: collectorId === UNASSIGNED ? true : undefined,
      status: status || undefined,
      priority: url.get('priority') || undefined,
      bucket: url.get('bucket') || undefined,
    }).then((pageResult) => {
      if (!active) return;
      setData(pageResult);
    }).catch(() => { if (active) setError(true); });
    return () => { active = false; };
  }, [collectorId, organizationId, page, pageSize, reload, search, searchField, status, url]);

  useEffect(() => {
    const digits = digitsOnly(search);
    if (searchField !== 'NATIONAL_ID' || digits.length < 10) { setNid(undefined); return; }
    let active = true;
    void collectionsService.lookupNationalId(digits).then((value) => { if (active) setNid(value); }).catch(() => { if (active) setNid(undefined); });
    return () => { active = false; };
  }, [search, searchField]);

  const clearFilters = () => { setSearchInput(''); setSearch(''); setSearchField('ALL'); setOrganizationId(''); setCollectorId(''); setStatus(''); setPage(1); };
  const from = data && data.totalCount > 0 ? (data.page - 1) * data.pageSize + 1 : 0;
  const to = data ? Math.min(data.page * data.pageSize, data.totalCount) : 0;
  const showing = ct('showingCases').replace('{from}', format.number(from)).replace('{to}', format.number(to)).replace('{total}', format.number(data?.totalCount ?? 0));
  const hasFilters = Boolean(search || organizationId || collectorId || status || searchField !== 'ALL');
  const numericSearch = searchField === 'NATIONAL_ID' || searchField === 'MOBILE' || searchField === 'CARD';

  return (
    <div>
      <PageHeader
        actions={<Button fullWidth={false} leftIcon={<RefreshCw className="h-4 w-4" />} onClick={() => setReload((value) => value + 1)} size="sm" variant="outline">{ct('refresh')}</Button>}
        eyebrow={ct('collections')}
        title={ct('cases')}
      />

      {nid && nid.organizationCount > 0 ? (
        <section className="mb-4 overflow-hidden rounded-2xl border border-mis-sky bg-white">
          <p className="bg-mis-pale px-5 py-2.5 text-sm font-bold text-mis-navy">{ct('nidLookupTitle')}</p>
          {nid.cases.map((item) => (
            <Link className="flex items-center justify-between gap-3 border-t border-mis-border px-5 py-2.5 text-sm hover:bg-slate-50" key={item.caseId} to={`/collections/cases/${item.caseId}`}>
              <span className="font-semibold text-mis-navy">{item.organizationName}</span>
              <span className="tabular-nums" data-bidi="ltr">{format.money(item.outstandingBalance)}</span>
            </Link>
          ))}
        </section>
      ) : null}

      <FileCollectorLinkBanner organizationId={organizationId || undefined} refreshKey={reload} onApplied={() => setReload((value) => value + 1)} />

      <section className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="border-b border-mis-border bg-slate-50/60 p-4 sm:p-5">
          <div className="grid min-w-0 gap-3 sm:grid-cols-[minmax(180px,240px)_minmax(0,1fr)]">
            <ProfessionalSelect aria-label={ct('searchBy')} className="h-11" onChange={(event) => { setSearchField(event.target.value as SearchField); setPage(1); }} value={searchField}>
              {SEARCH_FIELDS.map((item) => <option key={item} value={item}>{ct(SEARCH_LABELS[item])}</option>)}
            </ProfessionalSelect>
            <label className="relative min-w-0">
              <Search aria-hidden="true" className="pointer-events-none absolute start-3 top-3 h-5 w-5 text-slate-400" />
              <input
                autoComplete="off"
                className="h-11 w-full rounded-xl border border-mis-border bg-white pe-10 ps-10 text-sm outline-none transition focus:border-mis-blue focus:shadow-input"
                inputMode={numericSearch ? 'numeric' : 'search'}
                maxLength={160}
                onChange={(event) => setSearchInput(event.target.value)}
                placeholder={ct(SEARCH_PLACEHOLDERS[searchField])}
                type="search"
                value={searchInput}
              />
              {searchInput ? <button aria-label={ct('clearFilters')} className="absolute end-2 top-1.5 flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700" onClick={() => setSearchInput('')} type="button"><X className="h-4 w-4" /></button> : null}
            </label>
          </div>
          <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-3">
            <ProfessionalSelect aria-label={ct('filterByCollector')} className="h-11" onChange={(event) => { setCollectorId(event.target.value); setPage(1); }} value={collectorId}>
              <option value="">{ct('allCollectors')}</option>
              <option value={UNASSIGNED}>{ct('unassignedCollector')}</option>
              {collectors.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </ProfessionalSelect>
            <ProfessionalSelect aria-label={ct('filterByClient')} className="h-11" onChange={(event) => { setOrganizationId(event.target.value); setPage(1); }} value={organizationId}>
              <option value="">{ct('allClients')}</option>
              {clients.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </ProfessionalSelect>
            <ProfessionalSelect aria-label={ct('filterByStatus')} className="h-11" onChange={(event) => { setStatus(event.target.value); setPage(1); }} value={status}>
              <option value="">{ct('allStatuses')}</option>
              {['ACTIVE', 'ON_HOLD', 'SETTLED', 'CLOSED', 'LEGAL', 'WRITE_OFF'].map((item) => <option key={item} value={item}>{ct(item)}</option>)}
            </ProfessionalSelect>
          </div>
          {hasFilters ? (
            <div className="mt-4 border-t border-slate-200 pt-4">
              <button className="inline-flex items-center gap-2 text-xs font-bold text-slate-600 hover:text-red-600" onClick={clearFilters} type="button">
                <X className="h-4 w-4" />{ct('clearFilters')}
              </button>
            </div>
          ) : null}
        </div>

        {error ? (
          <div className="p-5"><ErrorState title={ct('loadError')} onRetry={() => setReload((value) => value + 1)} /></div>
        ) : !data ? (
          <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>
        ) : data.items.length === 0 ? (
          <EmptyState
            action={<Link className="inline-flex items-center gap-2 rounded-xl bg-mis-primary px-4 py-2.5 text-sm font-bold text-white" to="/banks"><Landmark className="h-4 w-4" />{ct('startFromCreditor')}</Link>}
            description={hasFilters ? undefined : ct('assignmentEmptyHelp')}
            title={ct('noCases')}
          />
        ) : (
          <>
            <div className="flex min-h-14 items-center justify-between gap-3 border-b border-mis-border px-4 py-3 sm:px-5">
              <p className="text-sm text-slate-500">{showing}</p>
              <span className="rounded-full bg-mis-pale px-3 py-1 text-sm font-black tabular-nums text-mis-primary">{format.number(data.totalCount)}</span>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[56rem] text-start text-sm">
                <thead className="bg-mis-surface text-xs font-semibold uppercase tracking-wide text-slate-500">
                  <tr>
                    <Th>{ct('customer')}</Th>
                    <Th>{ct('mobile1')}</Th>
                    <Th>{ct('mobile2')}</Th>
                    <Th>{ct('thisCreditor')}</Th>
                    <Th>{ct('currentBalance')}</Th>
                    <Th>{ct('collector')}</Th>
                    <Th>{ct('status')}</Th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((row) => {
                    const others = (row.relatedOrganizationNames ?? []).filter((name) => name !== row.clientName);
                    return (
                      <tr
                        className="cursor-pointer border-t border-mis-border hover:bg-slate-50/80"
                        key={row.id}
                        onClick={() => navigate(`/collections/cases/${row.id}`)}
                        onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); navigate(`/collections/cases/${row.id}`); } }}
                        role="link"
                        tabIndex={0}
                      >
                        <Td>
                          <p className="font-semibold text-mis-navy">{row.customerName}</p>
                          <p className="mt-0.5 text-xs text-slate-400" data-bidi="ltr">{row.caseNumber}</p>
                        </Td>
                        <Td><span className="whitespace-nowrap tabular-nums" data-bidi="ltr">{row.primaryPhone || '—'}</span></Td>
                        <Td><span className="whitespace-nowrap tabular-nums" data-bidi="ltr">{row.alternatePhone || row.tertiaryPhone || '—'}</span></Td>
                        <Td>
                          <p className="text-slate-700">{row.clientName}</p>
                          {others.length > 0 ? <p className="mt-0.5 text-xs text-slate-400">{ct('otherCreditors')}: {others.join(' · ')}</p> : null}
                        </Td>
                        <Td><span className="font-semibold tabular-nums" data-bidi="ltr">{format.money(row.outstandingBalance)}</span></Td>
                        <Td><CollectorAssignmentLabel assignedId={row.assignedCollectorId} assignedName={row.assignedCollectorName} fileName={row.fileCollectorName} /></Td>
                        <Td><CollectionStatus value={row.status} /></Td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <div className="flex flex-col gap-3 border-t border-mis-border px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:px-5">
              <ProfessionalSelect aria-label={ct('pageSize')} className="h-9 w-28 py-1" onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }} value={pageSize}>
                {PAGE_SIZES.map((item) => <option key={item} value={item}>{item}</option>)}
              </ProfessionalSelect>
              <Pagination className="border-0 px-0 py-0" onPageChange={setPage} page={data.page} pageSize={data.pageSize} showSummary={false} totalCount={data.totalCount} totalPages={data.totalPages} />
            </div>
          </>
        )}
      </section>
    </div>
  );
}

function Th({ children }: { children?: ReactNode }) {
  return <th className="px-5 py-3">{children}</th>;
}

function Td({ children }: { children?: ReactNode }) {
  return <td className="px-5 py-4 align-middle">{children}</td>;
}
