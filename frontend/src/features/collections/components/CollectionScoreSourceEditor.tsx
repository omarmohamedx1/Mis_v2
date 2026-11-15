import { useEffect, useState } from 'react';
import { ProfessionalSelect } from '../../../components/forms/ProfessionalSelect';
import { LoadingSpinner } from '../../../components/common/LoadingSpinner';
import { useCollectionsLocalization } from '../localization/collectionsTranslations';
import { collectionsService } from '../services/collectionsService';
import type { CollectionScoreSource } from '../types/score';

export function CollectionScoreSourceEditor({ organizationId, portfolioType, portfolioId, reportingPeriod, onSaved }: {
  organizationId: string; portfolioType: string; portfolioId: string; reportingPeriod: string; onSaved: () => void;
}) {
  const { ct } = useCollectionsLocalization();
  const [source, setSource] = useState<CollectionScoreSource>();
  const [complete, setComplete] = useState(false);
  const [reconciled, setReconciled] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    let active = true;
    setSource(undefined); setError(false); setComplete(false); setReconciled(false);
    void collectionsService.scoreSource(organizationId, portfolioType, portfolioId, reportingPeriod)
      .then(value => { if (active) setSource(value); }).catch(() => { if (active) setError(true); });
    return () => { active = false; };
  }, [organizationId, portfolioType, portfolioId, reportingPeriod, retry]);
  const labels: Record<string, string> = { openingBucket: 'scoreSourceBucket', openingBalance: 'scoreSourceBalance', startingDues: 'scoreSourceDues', payment: 'scoreSourcePayment' };
  return <details className="mb-5 rounded-2xl border border-mis-border bg-white p-4">
    <summary className="cursor-pointer font-semibold text-mis-navy">{ct('scoreConfigureSource')}</summary>
    <p className="my-3 text-sm text-slate-600">{ct('scoreSourceHint')}</p>
    {error ? <div role="alert" className="my-3 text-sm text-red-700">{ct('scoreSourceError')} <button type="button" className="underline" onClick={() => setRetry(value => value + 1)}>{ct('scoreRetry')}</button></div> : null}
    {!source && !error ? <LoadingSpinner /> : null}
    {source ? <form onSubmit={async event => {
      event.preventDefault();
      if (!source.openingImportId || !complete || !reconciled) return;
      setBusy(true); setError(false);
      try {
        await collectionsService.saveScoreSource(organizationId, portfolioType, portfolioId, {
          reportingPeriod, expectedVersion: source.version, openingImportId: source.openingImportId,
          completeOpeningPopulation: complete, paymentsReconciled: reconciled, paymentSource: source.paymentSource, fields: source.fields,
        });
        onSaved();
      } catch { setError(true); } finally { setBusy(false); }
    }} className="space-y-4">
      <div className="grid gap-4 md:grid-cols-2">
        <label className="space-y-2 text-sm font-semibold"><span>{ct('scoreSourceFile')}</span>
          <ProfessionalSelect required disabled={busy} value={source.openingImportId ?? ''} onChange={event => setSource({ ...source, openingImportId: event.target.value })}>
            <option value="">{ct('scoreSourceChoose')}</option>
            {source.imports.map(item => <option key={item.id} value={item.id}>{item.fileName} ({item.caseCount})</option>)}
          </ProfessionalSelect>
        </label>
        <label className="space-y-2 text-sm font-semibold"><span>{ct('scoreSourcePayments')}</span>
          <ProfessionalSelect disabled={busy} value={source.paymentSource} onChange={event => setSource({ ...source, paymentSource: event.target.value })}>
            <option value="SYSTEM">{ct('scoreSourceSystem')}</option><option value="IMPORT">{ct('scoreSourceImport')}</option>
          </ProfessionalSelect>
        </label>
      </div>
      <details><summary className="cursor-pointer text-sm font-semibold">{ct('scoreSourceColumns')}</summary>
        <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{Object.entries(source.fields).map(([key, value]) => <label key={key} className="space-y-1 text-sm">
          <span>{labels[key] ? ct(labels[key]) : `BKT ${key.slice(4)}`}</span>
          <input required maxLength={200} disabled={busy} className="h-10 w-full rounded-lg border border-mis-control px-3" value={value} onChange={event => setSource({ ...source, fields: { ...source.fields, [key]: event.target.value } })} />
        </label>)}</div>
      </details>
      <label className="flex items-start gap-2 text-sm"><input type="checkbox" required disabled={busy} checked={complete} onChange={event => setComplete(event.target.checked)} />{ct('scoreSourceComplete')}</label>
      <label className="flex items-start gap-2 text-sm"><input type="checkbox" required disabled={busy} checked={reconciled} onChange={event => setReconciled(event.target.checked)} />{ct('scoreSourceReconciled')}</label>
      <button type="submit" disabled={busy || !complete || !reconciled || !source.openingImportId} className="min-h-10 rounded-xl bg-mis-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">{busy ? <LoadingSpinner /> : ct('scoreSave')}</button>
    </form> : null}
  </details>;
}
