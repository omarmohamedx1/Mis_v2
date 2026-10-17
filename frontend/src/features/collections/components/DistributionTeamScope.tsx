import { UsersRound } from 'lucide-react';
import { useState } from 'react';
import { useCollectionsLocalization } from '../localization/collectionsTranslations';
import { useCollectionFormat } from './CollectionsUi';

export type DistributionTeamMember = { id: string; name: string; workload: number; responsible: boolean };

export function DistributionTeamOption({ checked, onChange, disabled = false }: { checked: boolean; onChange: (checked: boolean) => void; disabled?: boolean }) {
  const { ct } = useCollectionsLocalization();
  return <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-mis-border bg-slate-50 p-3 text-sm">
    <input className="mt-1 shrink-0" type="checkbox" checked={checked} disabled={disabled} onChange={event => onChange(event.target.checked)} />
    <span><strong className="block font-medium text-mis-ink">{ct('distributionIncludeOthers')}</strong><small className="mt-1 block leading-6 text-slate-500">{ct('distributionIncludeOthersHelp')}</small></span>
  </label>;
}

export function DistributionTeamScope({ members, includeOthers, onIncludeOthersChange, organizationWorkload = false }: {
  members: DistributionTeamMember[]; includeOthers: boolean; onIncludeOthersChange: (checked: boolean) => void; organizationWorkload?: boolean;
}) {
  const { ct } = useCollectionsLocalization();
  const f = useCollectionFormat();
  const [expanded, setExpanded] = useState(false);
  const responsible = members.filter(member => member.responsible);
  const shown = members.filter(member => member.responsible || includeOthers).sort((a, b) => Number(b.responsible) - Number(a.responsible) || a.name.localeCompare(b.name));
  return <section className="mb-5 overflow-hidden rounded-xl border border-mis-border bg-white" aria-label={ct('distributionResponsibleTeam')}>
    <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 px-4 py-3">
      <div className="flex items-center gap-2"><UsersRound aria-hidden="true" className="h-4 w-4 text-mis-primary" /><h2 className="text-sm font-semibold">{ct('distributionResponsibleTeam')}</h2></div>
      <p className="text-xs text-slate-500">{ct('distributionResponsibleCount').replace('{count}', f.number(responsible.length))}</p>
    </header>
    <div className="space-y-3 p-4">
      {!responsible.length && <p className="text-sm leading-7 text-slate-600">{ct('distributionNoResponsibleTeam')}</p>}
      {shown.length > 0 && <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">{(expanded ? shown : shown.slice(0, 4)).map(member => <div className="min-w-0 rounded-lg border border-slate-100 p-3" key={member.id}>
        <p className="text-sm font-medium leading-7 text-mis-ink">{member.name}</p>
        <p className="mt-1 text-xs text-slate-500">{ct(organizationWorkload ? 'distributionOrganizationWorkload' : 'distributionActiveWorkload')}: <bdi className="font-semibold text-mis-primary">{f.number(member.workload)}</bdi></p>
        {!member.responsible && <p className="mt-2 text-xs font-medium text-amber-700">{ct('distributionExternalCollector')}</p>}
      </div>)}</div>}
      {shown.length > 4 && <button className="text-xs font-semibold text-mis-primary" type="button" onClick={() => setExpanded(value => !value)}>{ct(expanded ? 'distributionShowFewerCollectors' : 'distributionShowAllCollectors')} ({f.number(shown.length)})</button>}
      <DistributionTeamOption checked={includeOthers} onChange={onIncludeOthersChange} />
    </div>
  </section>;
}

export function DistributionExternalNotice({ names }: { names: string[] }) {
  const { ct } = useCollectionsLocalization();
  return names.length ? <p className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm leading-7 text-amber-900"><strong className="block">{ct('distributionExternalAssignment')}</strong>{names.join('، ')}<span className="mt-1 block text-xs">{ct('distributionIncludeOthersHelp')}</span></p> : null;
}
