import { Link2 } from 'lucide-react';
import { useEffect, useState, type ReactNode } from 'react';
import { Button } from '../../../components/common/Button';
import { ConfirmDialog } from '../../../components/common/ConfirmDialog';
import { useToast } from '../../../components/common/Toast';
import { useAuth } from '../../../context/AuthContext';
import { getApiErrorMessage } from '../../../services/apiClient';
import { useCollectionsLocalization } from '../localization/collectionsTranslations';
import { collectionsService } from '../services/collectionsService';
import type { ImportedCollectorLinkResult } from '../types/collections';

const ASSIGN_ROLES = ['Admin', 'CollectionsOperationsManager', 'CollectionsSupervisor'];

export function canManageCollectionAssignments(roles?: string[]) {
  return Boolean(roles?.some((role) => ASSIGN_ROLES.includes(role)));
}

export function KpiCard({ label, value, accent = 'blue', hint, icon }: { label: ReactNode; value: ReactNode; accent?: 'blue' | 'green' | 'amber' | 'red'; hint?: ReactNode; icon?: ReactNode }) {
  const colors = { blue: 'border-mis-sky/60 bg-white', green: 'border-emerald-200 bg-emerald-50/30', amber: 'border-amber-200 bg-amber-50/30', red: 'border-rose-200 bg-rose-50/30' };
  const iconTone = { blue: 'bg-sky-100 text-mis-primary', green: 'bg-emerald-100 text-emerald-700', amber: 'bg-amber-100 text-amber-700', red: 'bg-rose-100 text-rose-700' };
  return (
    <article className={`min-w-0 rounded-2xl border p-4 shadow-sm sm:p-5 ${colors[accent]}`}>
      <div className="flex items-start justify-between gap-3">
        <p className="min-w-0 flex-1 text-sm font-medium leading-6 text-slate-500 [overflow-wrap:anywhere]">{label}</p>
        {icon ? <span className={`inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-xl [&_svg]:size-4 ${iconTone[accent]}`}>{icon}</span> : null}
      </div>
      <p className="mt-2 text-xl font-bold tabular-nums leading-snug text-mis-navy sm:text-2xl" data-bidi="ltr" title={typeof value === 'string' || typeof value === 'number' ? String(value) : undefined}>{value}</p>
      {hint ? <p className="mt-1 text-xs leading-6 text-slate-500 [overflow-wrap:anywhere]">{hint}</p> : null}
    </article>
  );
}

export function OrgTypeBadge({ type }: { type: string }) {
  const { ct } = useCollectionsLocalization();
  const normalized = (type || '').toUpperCase();
  const tone = normalized === 'BANK' ? 'bg-indigo-50 text-indigo-700 border-indigo-200' : normalized === 'CONSUMER_FINANCE' || normalized === 'FINANCIAL_INSTITUTION' ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-slate-100 text-slate-600 border-slate-200';
  return <span className={`inline-flex max-w-full items-center rounded-full border px-2.5 py-1 text-xs font-semibold leading-5 ${tone}`}>{ct(normalized)}</span>;
}

export function CollectionStatus({ value }: { value: string }) {
  const { ct } = useCollectionsLocalization(); const tone = ['APPROVED', 'FULFILLED', 'HEALTHY'].includes(value) ? 'bg-emerald-100 text-emerald-800' : ['BROKEN', 'REJECTED', 'AT_RISK', 'HIGH'].includes(value) ? 'bg-rose-100 text-rose-800' : ['DUE_TODAY', 'PARTIALLY_FULFILLED', 'WATCH', 'MEDIUM'].includes(value) ? 'bg-amber-100 text-amber-800' : 'bg-sky-100 text-sky-800';
  return <span className={`inline-flex max-w-full rounded-full px-2.5 py-1 text-center text-xs font-bold leading-5 ${tone}`}>{ct(value)}</span>;
}

export function CollectorAssignmentLabel({ assignedId, assignedName, fileName }: { assignedId?: string | null; assignedName?: string | null; fileName?: string | null }) {
  const { ct } = useCollectionsLocalization();
  if (assignedId && (assignedName || fileName)) {
    return <span className="font-semibold text-mis-navy">{assignedName || fileName}</span>;
  }
  if (fileName) {
    return (
      <span className="inline-flex flex-col items-start gap-1">
        <span className="text-slate-700">{fileName}</span>
        <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-amber-800">{ct('fileCollectorOnly')}</span>
      </span>
    );
  }
  return <span className="text-slate-400">—</span>;
}

export function FileCollectorLinkBanner({ organizationId, refreshKey, onApplied }: { organizationId?: string; refreshKey?: number; onApplied?: () => void }) {
  const { ct } = useCollectionsLocalization();
  const format = useCollectionFormat();
  const toast = useToast();
  const { user } = useAuth();
  const allowed = canManageCollectionAssignments(user?.roles);
  const [preview, setPreview] = useState<ImportedCollectorLinkResult>();
  const [applying, setApplying] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);

  useEffect(() => {
    if (!allowed) { setPreview(undefined); return; }
    let active = true;
    void collectionsService.previewImportedCollectorLinks(organizationId || undefined)
      .then((value) => { if (active) setPreview(value); })
      .catch(() => { if (active) setPreview(undefined); });
    return () => { active = false; };
  }, [allowed, organizationId, refreshKey]);

  if (!allowed || !preview || (preview.linkedCases === 0 && preview.ambiguousCases === 0 && preview.unmatchedCases === 0)) return null;

  async function apply() {
    setApplying(true);
    try {
      const result = await collectionsService.applyImportedCollectorLinks(organizationId || undefined);
      const next = await collectionsService.previewImportedCollectorLinks(organizationId || undefined);
      setPreview(next);
      setConfirmOpen(false);
      toast.success(ct('fileCollectorsLinked').replace('{count}', format.number(result.linkedCases)));
      if (result.ambiguousCases > 0) toast.warning(ct('fileCollectorsAmbiguous').replace('{count}', format.number(result.ambiguousCases)));
      onApplied?.();
    } catch (failure) {
      toast.error(getApiErrorMessage(failure, ct('saveError')));
    } finally {
      setApplying(false);
    }
  }

  return (
    <section className="mb-4 overflow-hidden rounded-2xl border border-amber-200 bg-amber-50/70 shadow-sm">
      <div className="flex flex-col gap-4 p-4 sm:flex-row sm:items-start sm:justify-between sm:p-5">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <Link2 className="h-4 w-4 text-amber-800" />
            <h2 className="text-sm font-bold text-mis-navy">{ct('linkFileCollectors')}</h2>
          </div>
          <p className="mt-1 text-sm leading-6 text-slate-600">{ct('linkFileCollectorsHelp')}</p>
          <div className="mt-3 flex flex-wrap gap-2 text-xs font-semibold">
            {preview.linkedCases > 0 ? <span className="rounded-full bg-white px-3 py-1 text-mis-primary">{ct('fileCollectorsReady').replace('{count}', format.number(preview.linkedCases))}</span> : null}
            {preview.ambiguousCases > 0 ? <span className="rounded-full bg-white px-3 py-1 text-amber-800">{ct('fileCollectorsAmbiguous').replace('{count}', format.number(preview.ambiguousCases))}</span> : null}
            {preview.unmatchedCases > 0 ? <span className="rounded-full bg-white px-3 py-1 text-slate-600">{ct('fileCollectorsUnmatched').replace('{count}', format.number(preview.unmatchedCases))}</span> : null}
          </div>
          {preview.byCollector.length > 0 ? (
            <ul className="mt-3 grid gap-1 text-sm text-slate-700 sm:grid-cols-2">
              {preview.byCollector.slice(0, 8).map((item) => (
                <li key={`${item.collectorId}-${item.fileName}`}>
                  <span className="font-semibold text-mis-navy">{item.collectorName}</span>
                  {item.fileName !== item.collectorName ? <span className="text-slate-500"> ← {item.fileName}</span> : null}
                  <span className="ms-1 tabular-nums text-slate-500">· {format.number(item.cases)}</span>
                </li>
              ))}
            </ul>
          ) : null}
        </div>
        {preview.linkedCases > 0 ? (
          <Button fullWidth={false} isLoading={applying} onClick={() => setConfirmOpen(true)} size="sm">{ct('linkFileCollectorsAction')}</Button>
        ) : null}
      </div>
      {confirmOpen ? (
        <ConfirmDialog
          cancelLabel={ct('cancel')}
          confirmLabel={ct('linkFileCollectorsAction')}
          confirmVariant="primary"
          isConfirming={applying}
          message={(
            <div className="space-y-2">
              <p>{ct('linkFileCollectorsConfirm').replace('{count}', format.number(preview.linkedCases))}</p>
              {preview.unmatchedCases > 0 ? <p>{ct('fileCollectorsUnmatched').replace('{count}', format.number(preview.unmatchedCases))}</p> : null}
              {preview.ambiguousCases > 0 ? <p>{ct('fileCollectorsAmbiguous').replace('{count}', format.number(preview.ambiguousCases))}</p> : null}
            </div>
          )}
          onCancel={() => { if (!applying) setConfirmOpen(false); }}
          onConfirm={() => void apply()}
          open={confirmOpen}
          title={ct('linkFileCollectorsConfirmTitle')}
        />
      ) : null}
    </section>
  );
}

export function useCollectionFormat() {
  const { language, ct } = useCollectionsLocalization(); const locale = language === 'ar' ? 'ar-EG' : 'en-EG';
  return {
    money: (value: number) => new Intl.NumberFormat(locale, { notation: Math.abs(value) >= 1_000_000 ? 'compact' : 'standard', maximumFractionDigits: Math.abs(value) >= 1_000_000 ? 1 : 0 }).format(value) + ` ${ct('currency')}`,
    number: (value: number) => new Intl.NumberFormat(locale).format(value),
    date: (value?: string) => {
      if (!value) return '—';
      const parsed = new Date(value);
      return Number.isNaN(parsed.getTime()) ? '—' : new Intl.DateTimeFormat(locale, { dateStyle: 'medium' }).format(parsed);
    },
    dateTime: (value?: string) => {
      if (!value) return '—';
      const parsed = new Date(value);
      return Number.isNaN(parsed.getTime()) ? '—' : new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(parsed);
    },
  };
}
