import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { Landmark, RefreshCw, Search, SlidersHorizontal, Sparkles, UserMinus, UserRoundCheck, UsersRound } from 'lucide-react';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { Pagination } from '../../components/common/Pagination';
import { useToast } from '../../components/common/Toast';
import { CollectionStatus, FileCollectorLinkBanner, useCollectionFormat } from '../../features/collections/components/CollectionsUi';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { AssignmentPreview, AutoAssignmentPreview, ClientCard, CollectionCase, CollectionDashboard, CollectorLookup, PagedResult } from '../../features/collections/types/collections';
import { getApiErrorMessage } from '../../services/apiClient';

import { DistributionActions, DistributionScope, DistributionSteps } from '../../features/collections/components/DistributionWorkflow';

type Queue = 'unassigned' | 'assigned';
type Dialog = 'assign' | 'auto' | 'unassign';

export function CollectionAssignmentsPage() {
  const { ct } = useCollectionsLocalization();
  const format = useCollectionFormat();
  const toast = useToast();
  const [queue, setQueue] = useState<Queue>('unassigned');
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [organizationId, setOrganizationId] = useState('');
  const [collectorFilter, setCollectorFilter] = useState('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [cases, setCases] = useState<PagedResult<CollectionCase>>();
  const [collectors, setCollectors] = useState<CollectorLookup[]>([]);
  const [clients, setClients] = useState<ClientCard[]>([]);
  const [dashboard, setDashboard] = useState<CollectionDashboard>();
  const [selected, setSelected] = useState<string[]>([]);
  const [dialog, setDialog] = useState<Dialog>();
  const [collectorId, setCollectorId] = useState('');
  const [autoCollectors, setAutoCollectors] = useState<string[]>([]);
  const [capacity, setCapacity] = useState(500);
  const [reason, setReason] = useState('');
  const [autoScope, setAutoScope] = useState<'selected' | 'queue'>('queue');
  const [preview, setPreview] = useState<AssignmentPreview | AutoAssignmentPreview>();
  const [saving, setSaving] = useState(false);
  const tableRef = useRef<HTMLDivElement>(null);
  const filtersKey = `${queue}:${page}:${pageSize}:${search}:${organizationId}:${collectorFilter}:${status}`;
  const [loadedKey, setLoadedKey] = useState('');
  const ready = !loading && !error && loadedKey === filtersKey && searchInput.trim() === search;
  const validCapacity = Number.isInteger(capacity) && capacity >= 1 && capacity <= 5000;
  useEffect(() => { setSelected([]); }, [filtersKey]);
  const focusCases = () => { tableRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }); tableRef.current?.focus({ preventScroll: true }); };


  useEffect(() => {
    const timer = window.setTimeout(() => { setSearch(searchInput.trim()); setPage(1); }, 350);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(false);
    const filters = {
      page,
      pageSize,
      search,
      organizationId: organizationId || undefined,
      collectorId: queue === 'assigned' ? collectorFilter || undefined : undefined,
      status: status || undefined,
      unassigned: queue === 'unassigned',
    };
    void Promise.all([
      collectionsService.listCases(filters),
      collectionsService.collectors(),
      collectionsService.clients({ pageSize: 100, active: true }),
      collectionsService.dashboard(organizationId || undefined),
    ]).then(([pageResult, collectorRows, clientPage, snapshot]) => {
      if (!active) return;
      setLoadedKey(filtersKey);
      setCases(pageResult);
      setCollectors(collectorRows);
      setClients(clientPage.items);
      setDashboard(snapshot);
      setSelected((current) => current.filter((id) => pageResult.items.some((item) => item.id === id)));
      setAutoCollectors((current) => current.filter((id) => collectorRows.some((item) => item.id === id)));
    }).catch(() => { if (active) setError(true); }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [collectorFilter, organizationId, page, pageSize, queue, reload, search, status]);

  const allChecked = Boolean(cases?.items.length) && cases!.items.every((item) => selected.includes(item.id));
  const chosenCollector = collectors.find((item) => item.id === collectorId);
  const defaultReason = ct('assignmentReasonDefault');

  const openDialog = (next: Dialog) => {
    setDialog(next);
    setPreview(undefined);
    setCollectorId('');
    if (next === 'auto') setAutoScope(selected.length ? 'selected' : 'queue');
    setReason(next === 'unassign' ? ct('distributionUnassignReason') : defaultReason);
  };

  async function runAutomatic() {
    const usedReason = reason.trim();
    if (!validCapacity) { toast.error(ct('distributionInvalidCapacity')); return; }
    if (autoCollectors.length === 0) { toast.error(ct('autoNoCollectorsSelected')); return; }
    if (usedReason.length < 2) return;
    if (autoScope === 'selected' && selected.length === 0) { toast.error(ct('selectCasesHelp')); return; }
    if (autoScope === 'queue' && (cases?.totalCount ?? 0) === 0) { toast.error(ct('autoQueueEmpty')); return; }
    setSaving(true);
    try {
      let caseIds: string[];
      if (preview && 'assignments' in preview) caseIds = preview.assignments.map(item => item.caseId);
      else if (autoScope === 'selected') caseIds = [...selected];
      else {
        const filters = { page: 1, pageSize: 100, search, organizationId: organizationId || undefined, status: status || undefined, unassigned: true };
        const first = await collectionsService.listCases(filters);
        const rest = await Promise.all(Array.from({ length: Math.min(Math.max(first.totalPages - 1, 0), 4) }, (_, index) => collectionsService.listCases({ ...filters, page: index + 2 })));
        caseIds = [...first.items, ...rest.flatMap(page => page.items)].slice(0, 500).map(item => item.id);
      }
      if (!caseIds.length) { toast.error(ct('autoQueueEmpty')); return; }
      if (!preview) {
        setDialog('auto');
        setPreview(await collectionsService.previewAutomaticAssignment({ caseIds, collectorIds: autoCollectors, maxActiveCases: capacity, confirmed: false }));
        return;
      }
      await collectionsService.applyAutomaticAssignment({ caseIds, collectorIds: autoCollectors, maxActiveCases: capacity, confirmed: true });
      toast.success(ct('assignmentComplete'));
      setDialog(undefined);
      setPreview(undefined);
      setSelected([]);
      setReload((value) => value + 1);
    } catch (failure) {
      toast.error(getApiErrorMessage(failure, ct('distributionActionError')));
    } finally {
      setSaving(false);
    }
  }

  const clearFilters = () => {
    setSearchInput('');
    setSearch('');
    setOrganizationId('');
    setCollectorFilter('');
    setStatus('');
    setPage(1);
  };

  async function continueAction() {
    if (!dialog || reason.trim().length < 2) return;
    if (dialog === 'auto') { await runAutomatic(); return; }
    if (selected.length === 0) return;
    if (dialog === 'assign' && !collectorId) return;
    setSaving(true);
    try {
      if (!preview) {
        if (dialog === 'assign') setPreview(await collectionsService.previewAssignment({ caseIds: selected, collectorId, teamId: chosenCollector?.teamId, reason: reason.trim(), confirmed: false }));
        else setPreview(await collectionsService.previewUnassignment({ caseIds: selected, reason: reason.trim(), confirmed: false }));
        return;
      }
      if (dialog === 'assign') await collectionsService.assign({ caseIds: selected, collectorId, teamId: chosenCollector?.teamId, reason: reason.trim(), confirmed: true });
      else await collectionsService.unassign({ caseIds: selected, reason: reason.trim(), confirmed: true });
      toast.success(dialog === 'unassign' ? ct('unassignmentCompleted') : ct('assignmentComplete'));
      setDialog(undefined);
      setPreview(undefined);
      setSelected([]);
      setReload((value) => value + 1);
    } catch (failure) {
      toast.error(getApiErrorMessage(failure, ct('distributionActionError')));
    } finally {
      setSaving(false);
    }
  }

  const noCasesInSystem = (dashboard?.totalCases ?? 0) === 0;
  const emptyTitle = search || organizationId || collectorFilter || status
    ? ct('noFilteredCases')
    : noCasesInSystem
      ? ct('assignmentEmptyTitle')
      : queue === 'assigned'
        ? ct('noAssignedDistributionCases')
        : ct('noUnassignedQueue');

  return (
    <div>
      <PageHeader
        actions={<Button fullWidth={false} leftIcon={<RefreshCw className="h-4 w-4" />} onClick={() => setReload((value) => value + 1)} size="sm" variant="outline">{ct('refresh')}</Button>}
        description={ct('assignmentSubtitle')}
        eyebrow={ct('collections')}
        title={ct('assignments')}
      />

      <FileCollectorLinkBanner organizationId={organizationId || undefined} refreshKey={reload} onApplied={() => setReload((value) => value + 1)} />

      <section className="workspace-kpis mb-5 overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        {[[ct('totalCases'), dashboard?.totalCases], [ct('unassignedCases'), dashboard?.unassignedCases], [ct('assignedCases'), dashboard?.assignedCases], [ct('collectorsLabel'), dashboard?.activeCollectors ?? collectors.length]].map(([label, value]) => (
          <div key={String(label)}>
            <p className="text-xs font-semibold text-slate-500">{label}</p>
            <p className="mt-1 text-xl font-bold tabular-nums text-mis-navy">{value == null ? '—' : format.number(Number(value))}</p>
          </div>
        ))}
      </section>

      <details className="mb-5 overflow-hidden rounded-2xl border border-mis-border bg-white">
        <summary className="cursor-pointer flex flex-col gap-1 border-b border-mis-border px-5 py-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-2">
            <UsersRound className="h-4 w-4 text-mis-primary" />
            <h2 className="text-sm font-bold text-mis-navy">{ct('distributionTeamTitle')}</h2>
          </div>
          <p className="text-xs text-slate-500">{ct('distributionTeamHelp')}</p>
        </summary>
        {collectors.length === 0 ? (
          <div className="px-5 py-6">
            <p className="font-semibold text-mis-navy">{ct('noCollectorsWithAccounts')}</p>
            <p className="mt-1 text-sm text-slate-500">{ct('noCollectorsWithAccountsHelp')}</p>
          </div>
        ) : (
          <div className="grid gap-px bg-mis-border sm:grid-cols-2 xl:grid-cols-4">
            {collectors.map((item) => (
              <div className="bg-white p-4" key={item.id}>
                <p className="truncate font-semibold text-mis-navy">{item.name}</p>
                <p className="mt-1 text-xs text-slate-500">{item.teamName || ct('collectorTeam')}</p>
                <p className="mt-2 text-sm font-bold tabular-nums text-mis-primary">{format.number(item.activeWorkload)} {ct('assignedCases')}</p>
              </div>
            ))}
          </div>
        )}
      </details>
      <DistributionActions disabled={!ready || !collectors.length || queue === 'assigned' || (!selected.length && !cases?.totalCount)} onManual={focusCases} onAutomatic={() => openDialog('auto')} />
      <div ref={tableRef} tabIndex={-1} className="scroll-mt-5 rounded-xl outline-none focus-visible:ring-4 focus-visible:ring-mis-pale"><p className="mb-3 text-xs leading-6 text-slate-500">{ct('distributionSelectionHelp')}</p></div>
      <div className="mb-4 flex gap-2 border-b border-mis-border">
        <button className={`px-4 py-3 text-sm font-bold ${queue === 'unassigned' ? 'border-b-2 border-mis-primary text-mis-primary' : 'text-slate-500'}`} onClick={() => { setQueue('unassigned'); setSelected([]); setPage(1); setCollectorFilter(''); }} type="button">
          {ct('unassignedCases')} ({format.number(dashboard?.unassignedCases ?? 0)})
        </button>
        <button className={`px-4 py-3 text-sm font-bold ${queue === 'assigned' ? 'border-b-2 border-mis-primary text-mis-primary' : 'text-slate-500'}`} onClick={() => { setQueue('assigned'); setSelected([]); setPage(1); }} type="button">
          {ct('assignedCases')} ({format.number(dashboard?.assignedCases ?? 0)})
        </button>
      </div>

      <section className="module-filter-grid mb-4 rounded-2xl border border-mis-border bg-white p-4">
        <label className="relative">
          <Search className="absolute top-3.5 h-4 w-4 text-mis-muted" style={{ insetInlineStart: '.85rem' }} />
          <input className="field h-11 py-2 ps-10 leading-normal" onChange={(event) => setSearchInput(event.target.value)} placeholder={ct('searchCases')} value={searchInput} />
        </label>
        <ProfessionalSelect className="field h-11 py-2 leading-normal" onChange={(event) => { setOrganizationId(event.target.value); setPage(1); }} value={organizationId}>
          <option value="">{ct('allClients')}</option>
          {clients.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </ProfessionalSelect>
        {queue === 'assigned' ? (
          <ProfessionalSelect className="field h-11 py-2 leading-normal" onChange={(event) => { setCollectorFilter(event.target.value); setPage(1); }} value={collectorFilter}>
            <option value="">{ct('allCollectors')}</option>
            {collectors.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </ProfessionalSelect>
        ) : null}
        <ProfessionalSelect className="field h-11 py-2 leading-normal" onChange={(event) => { setStatus(event.target.value); setPage(1); }} value={status}>
          <option value="">{ct('allStatuses')}</option>
          {['ACTIVE', 'ON_HOLD', 'SETTLED', 'CLOSED', 'LEGAL', 'WRITE_OFF'].map((item) => <option key={item} value={item}>{ct(item)}</option>)}
        </ProfessionalSelect>
        <Button leftIcon={<SlidersHorizontal className="h-4 w-4" />} onClick={clearFilters} size="sm" variant="ghost">{ct('clearFilters')}</Button>
      </section>

      {ready && selected.length > 0 ? (
        <div className="sticky top-3 z-10 mb-4 flex flex-wrap items-center gap-3 rounded-xl border border-mis-sky bg-mis-pale px-4 py-3 shadow-sm">
          <div><strong className="text-sm text-mis-navy">{selected.length} {ct('casesSelected')}</strong><p className="mt-1 text-xs text-slate-500">{ct('distributionSelectedTotal')}: {format.money(cases?.items.filter(item => selected.includes(item.id)).reduce((total, item) => total + item.outstandingBalance, 0) ?? 0)}</p></div>
          <Button fullWidth={false} leftIcon={<UserRoundCheck className="h-4 w-4" />} onClick={() => openDialog('assign')} size="sm">{ct(queue === 'assigned' ? 'reassign' : 'assignToCollector')}</Button>
          {queue === 'unassigned' ? <Button fullWidth={false} leftIcon={<Sparkles className="h-4 w-4" />} onClick={() => { setAutoScope('selected'); openDialog('auto'); }} size="sm" variant="outline">{ct('autoDistribute')}</Button> : null}
          {queue === 'assigned' ? <Button fullWidth={false} leftIcon={<UserMinus className="h-4 w-4" />} onClick={() => openDialog('unassign')} size="sm" variant="danger">{ct('unassign')}</Button> : null}
          <Button fullWidth={false} onClick={() => setSelected([])} size="sm" variant="ghost">{ct('clearSelection')}</Button>
        </div>
      ) : null}

      {error ? <ErrorState title={ct('loadError')} onRetry={() => setReload((value) => value + 1)} /> : !ready ? (
        <div className="flex min-h-64 items-center justify-center"><LoadingSpinner /></div>
      ) : cases && cases.items.length === 0 ? (
        <EmptyState
          action={noCasesInSystem ? <Link className="inline-flex items-center gap-2 rounded-xl bg-mis-primary px-4 py-2.5 text-sm font-bold text-white" to="/banks"><Landmark className="h-4 w-4" />{ct('startFromCreditor')}</Link> : undefined}
          className="rounded-2xl border border-mis-border bg-white"
          description={noCasesInSystem ? ct('assignmentEmptyHelp') : undefined}
          title={emptyTitle}
        />
      ) : cases ? (
        <section className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[56rem] text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  <Th><input aria-label={ct('selectCurrentPage')} checked={allChecked} onChange={(event) => setSelected(event.target.checked ? cases.items.map((item) => item.id) : [])} type="checkbox" /></Th>
                  <Th>{ct('customer')}</Th>
                  <Th>{ct('clientPortfolio')}</Th>
                  <Th>{ct('outstandingAmount')}</Th>
                  <Th>{ct('dpdBucket')}</Th>
                  {queue === 'assigned' ? <Th>{ct('collector')}</Th> : null}
                  <Th>{ct('priority')}</Th>
                  <Th>{ct('actions')}</Th>
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {cases.items.map((item) => (
                  <tr className={selected.includes(item.id) ? "bg-mis-pale" : "hover:bg-slate-50"} key={item.id}>
                    <Td><input aria-label={`${ct('caseId')} ${item.caseNumber}`} checked={selected.includes(item.id)} onChange={(event) => setSelected((current) => event.target.checked ? [...new Set([...current, item.id])] : current.filter((id) => id !== item.id))} type="checkbox" /></Td>
                    <Td>
                      <p className="font-bold text-mis-navy">{item.customerName}</p>
                      <p className="mt-1 text-xs text-slate-500"><span data-bidi="ltr">{item.caseNumber}</span> · {item.customerCode}</p>
                    </Td>
                    <Td>
                      <p>{item.clientName}</p>
                      <p className="mt-1 text-xs text-slate-500">{item.portfolioName}</p>
                    </Td>
                    <Td bidi>{format.money(item.outstandingBalance)}</Td>
                    <Td><span className="font-semibold" data-bidi="ltr">{item.daysPastDue}</span><p className="mt-1 text-xs text-slate-500">{item.bucket}</p></Td>
                    {queue === 'assigned' ? <Td>{item.assignedCollectorName || '—'}</Td> : null}
                    <Td><CollectionStatus value={item.priority} /></Td>
                    <Td><Link className="font-bold text-mis-primary hover:underline" to={`/collections/cases/${item.id}`}>{ct('openCase')}</Link></Td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex flex-col gap-3 border-t border-mis-border px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
            <ProfessionalSelect className="field h-9 w-24 py-1" onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }} value={pageSize}>
              {[20, 50, 100].map((item) => <option key={item} value={item}>{item}</option>)}
            </ProfessionalSelect>
            <Pagination onPageChange={setPage} page={cases.page} pageSize={cases.pageSize} totalCount={cases.totalCount} totalPages={cases.totalPages} />
          </div>
        </section>
      ) : null}

      <Modal
        footer={(
          <>
            <Button disabled={saving} fullWidth={false} onClick={() => preview ? setPreview(undefined) : setDialog(undefined)} variant="outline">{ct(preview ? 'backToEdit' : 'cancel')}</Button>
            <Button disabled={(dialog === 'assign' && !collectorId) || (dialog === 'auto' && (autoCollectors.length === 0 || !validCapacity || (autoScope === 'selected' && !selected.length) || (autoScope === 'queue' && !cases?.totalCount))) || reason.trim().length < 2} fullWidth={false} isLoading={saving} onClick={() => void continueAction()} variant={dialog === 'unassign' ? 'danger' : 'primary'}>
              {ct(preview ? dialog === 'unassign' ? 'unassign' : 'confirmAssignment' : 'previewAssignment')}
            </Button>
          </>
        )}
        onClose={() => { if (!saving) { setDialog(undefined); setPreview(undefined); } }}
        open={Boolean(dialog)}
        size={dialog === 'auto' ? 'lg' : 'md'}
        title={ct(dialog === 'unassign' ? 'confirmUnassignment' : dialog === 'auto' ? 'autoDistribute' : queue === 'assigned' ? 'reassign' : 'assignCases')}
      >
        <DistributionSteps review={Boolean(preview)} />
        <p className="mb-4 text-xs leading-6 text-slate-500">{ct(preview ? 'distributionReviewHelp' : 'distributionNoChanges')}</p>
        {preview ? <Preview preview={preview} ct={ct} format={format} /> : (
          <div className="space-y-4">
            {dialog !== 'auto' && <p className="text-sm text-slate-600">{selected.length} {ct('casesSelected')}</p>}
            {dialog === 'assign' ? (
              <label className="block text-sm font-semibold">{ct('selectCollector')}
                <ProfessionalSelect className="field mt-2" onChange={(event) => setCollectorId(event.target.value)} value={collectorId}>
                  <option value="">{ct('selectPortfolioCollector')}</option>
                  {collectors.map((item) => <option key={item.id} value={item.id}>{item.name} — {format.number(item.activeWorkload)}</option>)}
                </ProfessionalSelect>
              </label>
            ) : null}
            {dialog === 'auto' ? (
              <>
                <DistributionScope value={autoScope} onChange={setAutoScope} selectedCount={selected.length} queueCount={queue === 'unassigned' ? cases?.totalCount ?? 0 : 0} />
                <p className="rounded-xl bg-mis-pale p-3 text-sm leading-7 text-mis-primary">{ct('distributionAutoPolicy')}</p>
                <label className="block text-sm font-semibold">{ct('maximumCapacity')}
                  <input className="field mt-2" max={5000} min={1} onChange={(event) => setCapacity(Number(event.target.value))} type="number" value={capacity} aria-invalid={!validCapacity} />
                  <p className="mt-2 text-xs font-normal leading-6 text-slate-500">{ct('distributionCapacityHelp')}</p>
                  {!validCapacity && <p role="alert" className="mt-1 text-xs text-red-600">{ct('distributionInvalidCapacity')}</p>}
                </label>
                <fieldset>
                  <legend className="mb-2 text-sm font-semibold">{ct('collectorsLabel')} · {autoCollectors.length}</legend>
                  <div className="mb-3 flex flex-wrap gap-4"><button className="text-xs font-bold text-mis-primary" onClick={() => setAutoCollectors(collectors.map(item => item.id))} type="button">{ct('selectAllCollectors')}</button><button className="text-xs font-bold text-slate-500" onClick={() => setAutoCollectors([])} type="button">{ct('clearCollectorSelection')}</button></div>
                  <div className="grid max-h-64 gap-2 overflow-auto sm:grid-cols-2">
                    {collectors.map((item) => (
                      <label className="flex items-center gap-3 rounded-xl border border-mis-border p-3" key={item.id}>
                        <input checked={autoCollectors.includes(item.id)} onChange={(event) => setAutoCollectors((current) => event.target.checked ? [...current, item.id] : current.filter((id) => id !== item.id))} type="checkbox" />
                        <span>
                          <strong className="block text-sm">{item.name}</strong>
                          <small className="text-slate-500">{format.number(item.activeWorkload)} · {item.teamName || ct('collectorTeam')}</small>
                        </span>
                      </label>
                    ))}
                  </div>
                </fieldset>
              </>
            ) : null}
            {dialog !== 'auto' && <label className="block text-sm font-semibold">{ct('assignmentReasonLabel')}
              <textarea className="field mt-2" maxLength={500} onChange={(event) => setReason(event.target.value)} rows={3} value={reason} />
            </label>}
          </div>
        )}
      </Modal>
    </div>
  );
}

function Preview({ preview, ct, format }: { preview: AssignmentPreview | AutoAssignmentPreview; ct: (key: string) => string; format: ReturnType<typeof useCollectionFormat> }) {
  const automatic = 'assignments' in preview;
  return (
    <div className="space-y-4">
      {automatic ? <p className="text-xs leading-6 text-mis-primary">{ct('distributionAutoPolicy')}</p> : null}
      <p className="text-sm font-semibold text-slate-600">{ct('selectedCases')}: {format.number(preview.caseCount)}</p>
      {preview.collectors.map((item) => (
        <div className="rounded-xl bg-slate-50 p-4" key={item.collectorId}>
          <p className="font-bold text-mis-navy">{item.collectorName}</p>
          <div className="mt-3 grid gap-3 text-sm sm:grid-cols-3">
            <div><p className="text-slate-500">{ct('currentWorkload')}</p><p className="mt-1 text-lg font-bold">{format.number(item.currentWorkload)}</p></div>
            <div><p className="text-slate-500">{ct('proposedCases')}</p><p className="mt-1 text-lg font-bold">{format.number(item.proposedAdditionalCases)}</p></div>
            <div><p className="text-slate-500">{ct('resultingWorkload')}</p><p className="mt-1 text-lg font-bold text-mis-primary">{format.number(item.resultingWorkload)}</p></div>
          </div>
        </div>
      ))}
    </div>
  );
}

function Th({ children }: { children?: ReactNode }) {
  return <th className="px-4 py-3 text-start font-bold">{children}</th>;
}

function Td({ children, bidi = false }: { children?: ReactNode; bidi?: boolean }) {
  return <td className="px-4 py-3 align-top" {...(bidi ? { 'data-bidi': 'ltr' } : {})}>{children}</td>;
}
