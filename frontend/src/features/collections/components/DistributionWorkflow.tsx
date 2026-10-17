import { ListChecks, Sparkles, Upload } from 'lucide-react';
import { useCollectionsLocalization } from '../localization/collectionsTranslations';

export function DistributionActions({ onManual, onAutomatic, onUpload, disabled }: {
  onManual: () => void; onAutomatic: () => void; onUpload?: () => void; disabled: boolean;
}) {
  const { ct } = useCollectionsLocalization();
  const actions = [
    { title: 'distributionManualTitle', help: 'distributionManualHelp', icon: ListChecks, onClick: onManual, disabled: false },
    { title: 'autoDistributionSection', help: 'distributionAutomaticHelp', icon: Sparkles, onClick: onAutomatic, disabled },
    ...(onUpload ? [{ title: 'uploadDistributionFile', help: 'distributionFileHelp', icon: Upload, onClick: onUpload, disabled: false }] : []),
  ];
  return <section aria-label={ct('distributionChooseAction')} className={`mb-5 grid gap-3 ${onUpload ? 'lg:grid-cols-3' : 'sm:grid-cols-2'}`}>
    {actions.map(({ title, help, icon: Icon, onClick, disabled: unavailable }) => <button
      className="group flex min-w-0 items-start gap-3 rounded-2xl border border-mis-border bg-white p-4 text-start transition hover:border-mis-primary hover:shadow-sm focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-mis-pale disabled:cursor-not-allowed disabled:opacity-50"
      disabled={unavailable} key={title} onClick={onClick} type="button">
      <span className="rounded-xl bg-mis-pale p-2.5 text-mis-primary"><Icon className="h-5 w-5" /></span>
      <span className="min-w-0"><strong className="block text-sm text-mis-navy">{ct(title)}</strong><span className="mt-1 block text-xs leading-6 text-slate-500">{ct(help)}</span></span>
    </button>)}
  </section>;
}

export function DistributionSteps({ review }: { review: boolean }) {
  const { ct } = useCollectionsLocalization();
  return <ol aria-label={ct('distributionSteps')} className="mb-5 grid grid-cols-2 gap-2">
    {['distributionSetup', 'distributionReview'].map((key, index) => <li aria-current={Number(review) === index ? 'step' : undefined}
      className={`flex items-center gap-2 rounded-xl px-3 py-3 text-sm font-semibold ${Number(review) === index ? 'bg-mis-pale text-mis-primary' : 'bg-slate-50 text-slate-500'}`} key={key}>
      <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full border border-current text-xs">{index + 1}</span>{ct(key)}
    </li>)}
  </ol>;
}

export function DistributionScope({ value, onChange, selectedCount, queueCount }: {
  value: 'selected' | 'queue'; onChange: (value: 'selected' | 'queue') => void; selectedCount: number; queueCount: number;
}) {
  const { ct } = useCollectionsLocalization();
  return <fieldset className="mb-4">
    <legend className="mb-2 text-sm font-semibold">{ct('autoScopeLabel')}</legend>
    <div className="grid gap-2 sm:grid-cols-2">
      {(['selected', 'queue'] as const).map(scope => <label className={`flex items-start gap-3 rounded-xl border p-3 text-sm ${value === scope ? 'border-mis-primary bg-mis-pale' : 'border-mis-border'} ${scope === 'selected' && !selectedCount ? 'opacity-50' : 'cursor-pointer'}`} key={scope}>
        <input checked={value === scope} disabled={scope === 'selected' && !selectedCount} name="distribution-scope" onChange={() => onChange(scope)} type="radio" />
        <span><strong className="block">{ct(scope === 'selected' ? 'autoScopeSelected' : 'distributionFilteredQueue')}</strong>
          <small className="mt-1 block text-slate-500">{scope === 'selected' ? selectedCount : Math.min(queueCount, 500)} {ct('casesCount')}</small></span>
      </label>)}
    </div>
    {value === 'queue' ? <p className="mt-2 text-xs leading-6 text-slate-500">{ct('distributionQueueHelp')}</p> : null}
  </fieldset>;
}
