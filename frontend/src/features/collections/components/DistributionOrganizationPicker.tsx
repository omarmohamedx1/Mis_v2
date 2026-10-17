import { ArrowUpLeft, ArrowUpRight, Search } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Pagination } from '../../../components/common/Pagination';
import type { AssignmentOrganization, CollectorLookup } from '../types/collections';
import { useCollectionsLocalization } from '../localization/collectionsTranslations';
import { BankLogo } from './BankLogo';
import { useCollectionFormat } from './CollectionsUi';

export function DistributionOrganizationPicker({ organizations, collectors, onChoose }: { organizations: AssignmentOrganization[]; collectors: CollectorLookup[]; onChoose: (id: string) => void }) {
  const { ct, language } = useCollectionsLocalization();
  const f = useCollectionFormat();
  const [search, setSearch] = useState('');
  const [kind, setKind] = useState('all');
  const [page, setPage] = useState(1);
  const filtered = useMemo(() => organizations.filter(item => (kind === 'all' || (item.organizationType === 'BANK' ? kind === 'bank' : kind === 'company')) && `${item.name} ${item.code}`.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase())).sort((a, b) => b.unassignedCases - a.unassignedCases || a.name.localeCompare(b.name)), [organizations, search, kind]);
  const rows = filtered.slice((page - 1) * 20, page * 20);
  const Arrow = language === 'ar' ? ArrowUpLeft : ArrowUpRight;
  const sums = organizations.reduce((total, item) => ({ cases: total.cases + item.totalCases, pending: total.pending + item.unassignedCases, assigned: total.assigned + item.assignedCases }), { cases: 0, pending: 0, assigned: 0 });
  return <div className="space-y-5" data-distribution-organizations>
    <section className="workspace-kpis overflow-hidden rounded-xl border border-mis-border bg-white">
      {[[ct('distributionOrganizationCount'), organizations.length], [ct('totalCases'), sums.cases], [ct('unassignedCases'), sums.pending], [ct('assignedCases'), sums.assigned]].map(([label, value]) => <div key={String(label)}><p className="text-xs font-medium text-slate-500">{label}</p><p className="mt-1 text-xl font-semibold tabular-nums">{f.number(Number(value))}</p></div>)}
    </section>
    <section className="relative overflow-hidden rounded-xl border border-mis-border bg-white">
      <header className="border-b border-slate-100 px-5 py-4"><h2 className="text-sm font-semibold">{ct('distributionChooseOrganization')}</h2><p className="mt-1 text-xs leading-6 text-slate-500">{ct('distributionOrganizationListHelp')}</p></header>
      <div className="flex flex-wrap items-center justify-between gap-3 p-4">
        <label className="relative block min-w-0 flex-1 sm:max-w-sm"><Search aria-hidden="true" className="absolute start-3 top-3 h-4 w-4 text-slate-400" /><input aria-label={ct('distributionOrganizationSearch')} className="field ps-9" placeholder={ct('distributionOrganizationSearch')} type="search" value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} /></label>
        <div className="overview-segments" role="group" aria-label={ct('distributionOrganizationType')}>{[['all', 'dashboardAllOrganizations'], ['bank', 'sidebarBanks'], ['company', 'sidebarFinanceCompanies']].map(([value, label]) => <button type="button" key={value} aria-pressed={kind === value} className={kind === value ? 'bg-white text-mis-primary shadow-sm' : 'text-slate-500'} onClick={() => { setKind(value); setPage(1); }}>{ct(label)}</button>)}</div>
      </div>
      {rows.length ? <div className="overview-table-scroll"><table className="overview-table collection-summary-table min-w-[56rem]">
        <thead><tr><th className="min-w-[15rem]">{ct('dashboardClient')}</th><th className="text-end">{ct('totalCases')}</th><th className="text-end">{ct('unassignedCases')}</th><th className="text-end">{ct('assignedCases')}</th><th className="text-end">{ct('distributionResponsibleTeam')}</th><th>{ct('actions')}</th></tr></thead>
        <tbody>{rows.map(item => <tr key={item.id} data-organization-id={item.id}><td><div className="flex items-center gap-3"><BankLogo className="h-10 w-10 shrink-0 rounded-lg" code={item.code} logoUrl={item.logoUrl} name={item.name} /><div><p className="font-semibold text-mis-ink">{item.name}</p><p className="mt-1 text-xs text-slate-500">{ct(item.organizationType === 'BANK' ? 'dashboardBankType' : 'dashboardFinanceType')} · <bdi>{item.code}</bdi></p></div></div></td>
          <td className="text-end">{f.number(item.totalCases)}</td><td className="text-end font-semibold text-mis-primary">{f.number(item.unassignedCases)}</td><td className="text-end">{f.number(item.assignedCases)}</td><td className="text-end">{f.number(collectors.filter(collector => collector.organizationIds?.includes(item.id)).length)}</td><td><button aria-label={`${ct('distributionOpenOrganization')} — ${item.name}`} className="inline-flex items-center gap-2 whitespace-nowrap rounded-lg border border-mis-border px-3 py-2 text-xs font-semibold text-mis-primary hover:bg-slate-50" type="button" onClick={() => onChoose(item.id)}>{ct(item.unassignedCases ? 'distributionOpenOrganization' : 'distributionManageOrganization')}<Arrow aria-hidden="true" className="h-3.5 w-3.5" /></button></td>
        </tr>)}</tbody>
      </table></div> : <p className="px-5 py-10 text-center text-sm text-slate-500">{ct(organizations.length ? 'distributionNoMatchingOrganization' : 'distributionNoOrganizations')}</p>}
      <Pagination page={page} pageSize={20} totalCount={filtered.length} totalPages={Math.ceil(filtered.length / 20)} onPageChange={setPage} />
    </section>
  </div>;
}
