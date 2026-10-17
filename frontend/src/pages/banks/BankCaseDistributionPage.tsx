import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { Eye, RefreshCw, Search, SlidersHorizontal, Sparkles, Upload, UserMinus, UserRoundCheck, UsersRound } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { Pagination } from '../../components/common/Pagination';
import { StatusBadge } from '../../components/common/StatusBadge';
import { useToast } from '../../components/common/Toast';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { AutoDistributionPreview, BankPortfolioCaseDetails, CaseDistributionPage, CaseDistributionSummary, DistributionCollector, DistributionImport, DistributionPreview } from '../../features/collections/types/collections';
import { getApiErrorMessage } from '../../services/apiClient';
import type { BankWorkspaceContext } from './BankWorkspaceLayout';
import { BankDistributionUploadModal } from './BankDistributionUploadModal';

import { DistributionActions, DistributionScope, DistributionSteps } from '../../features/collections/components/DistributionWorkflow';

type Action = 'assign' | 'reassign' | 'unassign';

export function BankCaseDistributionPage() {
  const { bank } = useOutletContext<BankWorkspaceContext>(); const { language, ct } = useCollectionsLocalization(); const toast = useToast();
  const [assigned, setAssigned] = useState(false), [summary, setSummary] = useState<CaseDistributionSummary>(), [data, setData] = useState<CaseDistributionPage>();
  const [collectors, setCollectors] = useState<DistributionCollector[]>([]), [imports, setImports] = useState<DistributionImport[]>([]);
  const [searchInput, setSearchInput] = useState(''), [search, setSearch] = useState(''), [status, setStatus] = useState(''), [collectorId, setCollectorId] = useState(''), [importId, setImportId] = useState(''), [sortBy, setSortBy] = useState('');
  const [page, setPage] = useState(1), [pageSize, setPageSize] = useState(20), [selected, setSelected] = useState<string[]>([]), [reload, setReload] = useState(0), [loading, setLoading] = useState(true), [error, setError] = useState('');
  const [dialog, setDialog] = useState<Action>(), [targetCollector, setTargetCollector] = useState(''), [reason, setReason] = useState('Distribution assignment'), [preview, setPreview] = useState<DistributionPreview>(), [saving, setSaving] = useState(false);
  const [autoOpen, setAutoOpen] = useState(false), [autoCollectors, setAutoCollectors] = useState<string[]>([]), [method, setMethod] = useState('EQUAL_COUNT'), [autoPreview, setAutoPreview] = useState<AutoDistributionPreview>();
  const [autoReason, setAutoReason] = useState('Automatic distribution');
  const [autoScope, setAutoScope] = useState<'selected' | 'queue'>('queue');
  const [details, setDetails] = useState<BankPortfolioCaseDetails>();
  const [uploadOpen, setUploadOpen] = useState(false);
  const tableRef = useRef<HTMLDivElement>(null);
  const [autoCaseIds, setAutoCaseIds] = useState<string[]>([]);
  const filtersKey = `${bank.id}:${assigned}:${page}:${pageSize}:${search}:${status}:${collectorId}:${importId}:${sortBy}`;
  const [loadedKey, setLoadedKey] = useState('');
  const ready = !loading && !error && loadedKey === filtersKey && searchInput.trim() === search;
  useEffect(() => { setSelected([]); }, [filtersKey]);
  const openAutomatic = (scope: 'selected' | 'queue') => {
    setAutoScope(scope); setAutoPreview(undefined); setAutoCaseIds([]);
    setAutoReason(ct('distributionAutoReason')); setAutoOpen(true);
  };
  const focusCases = () => { tableRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }); tableRef.current?.focus({ preventScroll: true }); };
  async function openDetails(id: string) {
    try { setDetails(await collectionsService.bankPortfolioCase(bank.id, id)); }
    catch (failure) { toast.error(getApiErrorMessage(failure, ct('distributionLoadError'))); }
  }

  useEffect(() => { const id = window.setTimeout(() => { setSearch(searchInput.trim()); setPage(1); }, 350); return () => window.clearTimeout(id); }, [searchInput]);
  useEffect(() => { let active = true; setLoading(true); setError(''); const query = { page, pageSize, search, status, collectorId: assigned ? collectorId : undefined, importId, sortBy };
    void Promise.all([collectionsService.distributionSummary(bank.id), collectionsService.distributionCases(bank.id, assigned, query), collectionsService.distributionCollectors(bank.id), collectionsService.distributionImports(bank.id)])
      .then(([s, d, c, i]) => { if (active) { setLoadedKey(filtersKey); setSummary(s); setData(d); setCollectors(c); setImports(i); setSelected(current => current.filter(id => d.items.some(x => x.id === id))); setAutoCollectors(current => current.filter(id => c.some(x => x.id === id))); } }).catch(e => { if (active) setError(getApiErrorMessage(e, ct('distributionLoadError'))); }).finally(() => { if (active) setLoading(false); }); return () => { active = false; }; }, [assigned, bank.id, collectorId, ct, importId, page, pageSize, reload, search, sortBy, status]);
  const money = (v: number) => new Intl.NumberFormat(language === 'ar' ? 'ar-EG' : 'en-EG', { style: 'currency', currency: 'EGP', maximumFractionDigits: 0 }).format(v);
  const date = (v?: string) => v ? new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { dateStyle: 'medium' }).format(new Date(v)) : '—';
  const allPage = Boolean(data?.items.length) && data!.items.every(x => selected.includes(x.id));
  const chosenCollector = collectors.find(x => x.id === targetCollector);
  const additionalCases = data?.items.filter(x => selected.includes(x.id) && x.collectorId !== targetCollector).length ?? 0;
  const clear = () => { setSearchInput(''); setSearch(''); setStatus(''); setCollectorId(''); setImportId(''); setSortBy(''); setPage(1); };
  const start = (action: Action, ids = selected) => { setSelected(ids); setDialog(action); setTargetCollector(''); setReason(ct(action === 'unassign' ? 'distributionUnassignReason' : 'distributionManualReason')); setPreview(undefined); };
  async function nextAction() { if (!dialog || reason.trim().length < 2 || (dialog !== 'unassign' && !targetCollector)) return; const value = { caseIds: selected, collectorId: dialog === 'unassign' ? undefined : targetCollector, reason: reason.trim() }; setSaving(true); try { if (!preview) setPreview(await collectionsService.previewDistribution(bank.id, dialog, value)); else { const result = await collectionsService.applyDistribution(bank.id, dialog, value); toast.success(dialog === 'unassign' ? ct('unassignmentCompleted') : `${result.caseCount} ${ct('assignmentCompletedTo')} ${result.collectorName}`); setDialog(undefined); setPreview(undefined); setSelected([]); setReload(v => v + 1); } } catch (e) { toast.error(getApiErrorMessage(e, ct('distributionActionError'))); } finally { setSaving(false); } }
  async function nextAuto(confirm = false) {
    if (!autoCollectors.length) { toast.error(ct('autoNoCollectorsSelected')); return; }
    if (autoReason.trim().length < 2) return;
    if (autoScope === 'selected' && selected.length === 0) { toast.error(ct('selectCasesHelp')); return; }
    if (autoScope === 'queue' && (data?.totalCount ?? 0) === 0) { toast.error(ct('autoQueueEmpty')); return; }
    setSaving(true);
    try {
      const caseIds = confirm ? autoCaseIds : autoScope === 'selected' ? [...selected] : (await collectionsService.distributionCases(bank.id, false, { page: 1, pageSize: 100, search, status, importId, sortBy, allMatching: true })).items.slice(0, 500).map(item => item.id);
      if (!caseIds.length) { toast.error(ct('autoQueueEmpty')); return; }
      const value = { caseIds, collectorIds: [...autoCollectors], method, reason: autoReason.trim() };
      if (!confirm) { setAutoCaseIds(caseIds); setAutoPreview(await collectionsService.previewAutoDistribution(bank.id, value)); }
      else { await collectionsService.confirmAutoDistribution(bank.id, value); toast.success(ct('autoDistributionCompleted')); setAutoOpen(false); setAutoPreview(undefined); setSelected([]); setReload(v => v + 1); }
    } catch (e) { toast.error(getApiErrorMessage(e, ct('distributionActionError'))); }
    finally { setSaving(false); }
  }
  return <div>
    <header className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h2 className="text-2xl font-bold text-mis-navy">{ct('caseDistribution')}</h2><p className="mt-2 text-sm text-slate-500">{ct('caseDistributionDescription')}</p></div><div className="flex flex-wrap gap-2"><Button fullWidth={false} leftIcon={<Upload className="h-4 w-4" />} onClick={() => setUploadOpen(true)} size="sm">{ct('uploadDistributionFile')}</Button><Button fullWidth={false} leftIcon={<RefreshCw className="h-4 w-4" />} onClick={() => setReload(v => v + 1)} size="sm" variant="outline">{ct('refresh')}</Button></div></header>
    <section className="workspace-kpis mb-4 overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">{[[ct('totalCases'), summary?.totalCases], [ct('unassignedCases'), summary?.unassignedCases], [ct('assignedCases'), summary?.assignedCases], [ct('collectorsLabel'), summary?.collectors]].map(([label,value]) => <div key={String(label)}><p className="text-xs font-semibold text-slate-500">{label}</p><p className="mt-1 text-xl font-bold text-mis-navy">{value ?? '—'}</p></div>)}</section>
    <details className="mb-4 overflow-hidden rounded-2xl border border-mis-border bg-white">
      <summary className="cursor-pointer flex flex-col gap-1 border-b border-mis-border px-5 py-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-2">
          <UsersRound className="h-4 w-4 text-mis-primary" />
          <h2 className="text-sm font-bold text-mis-navy">{ct('distributionTeamTitle')}</h2>
        </div>
        <p className="text-xs text-slate-500">{ct('distributionTeamHelp')}</p>
      </summary>
      {collectors.length === 0 ? (
        <div className="px-5 py-6"><p className="font-semibold text-mis-navy">{ct('noCollectorsWithAccounts')}</p><p className="mt-1 text-sm text-slate-500">{ct('noCollectorsWithAccountsHelp')}</p></div>
      ) : (
        <div className="grid gap-px bg-mis-border sm:grid-cols-2 xl:grid-cols-4">
          {collectors.map(x => (
            <div className="bg-white p-4" key={x.id}>
              <p className="truncate font-semibold text-mis-navy">{x.name}</p>
              <p className="mt-2 text-sm font-bold tabular-nums text-mis-primary">{x.assignedCases} {ct('assignedCases')}</p>
              <p className="mt-1 text-xs text-slate-500" data-bidi="ltr">{money(x.totalOutstanding)}</p>
            </div>
          ))}
        </div>
      )}
    </details>
    <DistributionActions disabled={!ready || !collectors.length || (!selected.length && (data?.totalCount ?? 0) === 0) || assigned} onManual={focusCases} onAutomatic={() => openAutomatic(selected.length ? 'selected' : 'queue')} onUpload={() => setUploadOpen(true)} />
    <div ref={tableRef} tabIndex={-1} className="scroll-mt-5 rounded-xl outline-none focus-visible:ring-4 focus-visible:ring-mis-pale"><p className="mb-3 text-xs leading-6 text-slate-500">{ct('distributionSelectionHelp')}</p></div>
    <div className="mb-4 flex gap-2 border-b border-mis-border"><button className={`px-4 py-3 text-sm font-bold ${!assigned ? 'border-b-2 border-mis-primary text-mis-primary' : 'text-slate-500'}`} onClick={() => { setAssigned(false); setSelected([]); setPage(1); }}>{ct('unassignedCases')} ({summary?.unassignedCases ?? 0})</button><button className={`px-4 py-3 text-sm font-bold ${assigned ? 'border-b-2 border-mis-primary text-mis-primary' : 'text-slate-500'}`} onClick={() => { setAssigned(true); setSelected([]); setPage(1); }}>{ct('assignedCases')} ({summary?.assignedCases ?? 0})</button></div>
<section className="module-filter-grid mb-4 rounded-2xl border border-mis-border bg-white p-4">
      <label className="relative"><Search className="absolute top-3.5 h-4 w-4 text-mis-muted" style={{ insetInlineStart: '.85rem' }} /><input className="field h-11 py-2 ps-10 leading-normal" placeholder={ct('searchPortfolioCases')} value={searchInput} onChange={e => setSearchInput(e.target.value)} /></label>
      <ProfessionalSelect className="field h-11 py-2 leading-normal" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">{ct('allPortfolioStatuses')}</option>{['ACTIVE','ON_HOLD','SETTLED','CLOSED','LEGAL','WRITE_OFF'].map(x => <option key={x} value={x}>{ct(x)}</option>)}</ProfessionalSelect>
      {assigned ? <ProfessionalSelect className="field h-11 py-2 leading-normal" value={collectorId} onChange={e => { setCollectorId(e.target.value); setPage(1); }}><option value="">{ct('allCollectors')}</option>{collectors.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</ProfessionalSelect> : null}
      <ProfessionalSelect className="field h-11 py-2 leading-normal" value={importId} onChange={e => { setImportId(e.target.value); setPage(1); }}><option value="">{ct('allPortfolios')}</option>{imports.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</ProfessionalSelect>
      <ProfessionalSelect className="field h-11 py-2 leading-normal" value={sortBy} onChange={e => { setSortBy(e.target.value); setPage(1); }}><option value="">{ct('sortBy')}</option><option value="customer">{ct('customerName')}</option><option value="outstanding">{ct('outstandingAmount')}</option><option value="status">{ct('status')}</option>{assigned && <option value="assigned">{ct('assignedDate')}</option>}</ProfessionalSelect>
      <Button leftIcon={<SlidersHorizontal className="h-4 w-4" />} onClick={clear} size="sm" variant="ghost">{ct('clearFilters')}</Button>
    </section>
    {ready && selected.length ? <div className="sticky top-3 z-10 mb-4 flex flex-wrap items-center gap-3 rounded-xl border border-mis-sky bg-mis-pale px-4 py-3 shadow-sm"><div><strong className="text-sm text-mis-navy">{selected.length} {ct('casesSelected')}</strong><p className="mt-1 text-xs text-slate-500">{ct('distributionSelectedTotal')}: {money(data?.items.filter(x => selected.includes(x.id)).reduce((total, x) => total + x.outstandingAmount, 0) ?? 0)}</p></div><Button fullWidth={false} leftIcon={<UserRoundCheck className="h-4 w-4" />} onClick={() => start(assigned ? 'reassign' : 'assign')} size="sm">{ct(assigned ? 'reassign' : 'assignToCollector')}</Button>{!assigned && <Button fullWidth={false} leftIcon={<Sparkles className="h-4 w-4" />} onClick={() => openAutomatic('selected')} size="sm" variant="outline">{ct('autoDistribute')}</Button>}{assigned && <Button fullWidth={false} leftIcon={<UserMinus className="h-4 w-4" />} onClick={() => start('unassign')} size="sm" variant="danger">{ct('unassign')}</Button>}<Button fullWidth={false} onClick={() => setSelected([])} size="sm" variant="ghost">{ct('clearSelection')}</Button></div> : null}
    {error ? <ErrorState title={error} onRetry={() => setReload(v => v + 1)} /> : !ready ? <div className="flex min-h-64 items-center justify-center"><LoadingSpinner /></div> : data?.items.length === 0 ? <EmptyState className="rounded-2xl border border-mis-border bg-white" title={search || status || collectorId || importId ? ct('noFilteredCases') : ct(assigned ? 'noAssignedDistributionCases' : 'allCasesAssigned')} /> : data ? <section className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm"><div className="overflow-x-auto"><table className="w-full min-w-[48rem] text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr><Th><input aria-label={ct('selectCurrentPage')} checked={allPage} onChange={e => setSelected(e.target.checked ? data.items.map(x => x.id) : [])} type="checkbox" /></Th><Th>{ct('caseId')}</Th><Th>{ct('customerName')}</Th><Th>{ct('mobile')}</Th><Th>{ct('outstandingAmount')}</Th>{assigned && <Th>{ct('assignedTo')}</Th>}<Th>{ct('status')}</Th><Th>{assigned ? ct('assignedDate') : ct('importPortfolioLabel')}</Th><Th>{ct('actions')}</Th></tr></thead><tbody className="divide-y divide-mis-border">{data.items.map(x => <tr key={x.id} className={selected.includes(x.id) ? "bg-mis-pale" : "hover:bg-slate-50"}><Td><input aria-label={`${ct('caseId')} ${x.caseNumber}`} checked={selected.includes(x.id)} onChange={e => setSelected(v => e.target.checked ? [...new Set([...v,x.id])] : v.filter(id => id !== x.id))} type="checkbox" /></Td><Td><button className="font-bold text-mis-primary hover:underline" onClick={() => void openDetails(x.id)}>{x.caseNumber}</button></Td><Td><button className="font-semibold text-mis-navy hover:text-mis-primary" onClick={() => void openDetails(x.id)}>{x.customerName}</button></Td><Td bidi>{x.mobile || '—'}</Td><Td bidi>{money(x.outstandingAmount)}</Td>{assigned && <Td>{x.collectorName}</Td>}<Td><StatusBadge tone="info">{ct(x.status)}</StatusBadge></Td><Td>{assigned ? date(x.assignedAt) : x.importName || '—'}</Td><Td><Button fullWidth={false} leftIcon={<Eye className="h-4 w-4" />} onClick={() => void openDetails(x.id)} size="sm" variant="ghost">{ct('openPortfolioCase')}</Button></Td></tr>)}</tbody></table></div><div className="flex flex-col gap-3 border-t border-mis-border px-4 py-3 sm:flex-row sm:items-center sm:justify-between"><ProfessionalSelect className="field h-9 w-20 py-1" value={pageSize} onChange={e => { setPageSize(Number(e.target.value)); setPage(1); }}>{[20,50,100].map(x => <option key={x}>{x}</option>)}</ProfessionalSelect><Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} totalPages={data.totalPages} onPageChange={setPage} /></div></section> : null}
    <Modal open={Boolean(dialog)} onClose={() => !saving && setDialog(undefined)} size="md" title={ct(dialog === 'unassign' ? 'confirmUnassignment' : dialog === 'reassign' ? 'confirmReassignment' : 'assignCases')} footer={<><Button disabled={saving} fullWidth={false} onClick={() => preview ? setPreview(undefined) : setDialog(undefined)} variant="outline">{ct(preview ? 'backToEdit' : 'cancel')}</Button><Button disabled={!selected.length || reason.trim().length < 2 || (dialog !== 'unassign' && !targetCollector)} fullWidth={false} isLoading={saving} onClick={() => void nextAction()} variant={dialog === 'unassign' ? 'danger' : 'primary'}>{ct(preview ? dialog === 'unassign' ? 'unassign' : dialog === 'reassign' ? 'confirmReassignment' : 'confirmPortfolioAssignment' : 'distributionReview')}</Button></>}>
      <DistributionSteps review={Boolean(preview)} />
      {preview ? <div className="space-y-4"><PreviewBlock preview={preview} money={money} ct={ct} />{chosenCollector && <Workload collector={chosenCollector} additionalCases={additionalCases} ct={ct} />}<Info label={ct('assignmentReasonLabel')} value={reason} /></div> : <div className="space-y-4"><p className="text-sm text-slate-600">{selected.length} {ct('casesSelected')}</p>{dialog !== 'unassign' && <><label className="block text-sm font-semibold">{ct('collector')}<ProfessionalSelect className="field mt-2" value={targetCollector} onChange={e => setTargetCollector(e.target.value)}><option value="">{ct('selectPortfolioCollector')}</option>{collectors.map(x => <option key={x.id} value={x.id}>{x.name} — {x.assignedCases}</option>)}</ProfessionalSelect></label>{chosenCollector && <Workload collector={chosenCollector} additionalCases={additionalCases} ct={ct} />}</>}<label className="block text-sm font-semibold">{ct('assignmentReasonLabel')}<textarea className="field mt-2" maxLength={500} rows={3} value={reason} onChange={e => setReason(e.target.value)} /></label></div>}
    </Modal>
    <Modal open={autoOpen} onClose={() => { if (!saving) { setAutoOpen(false); setAutoPreview(undefined); } }} size="lg" title={ct(autoPreview ? 'distributionPreview' : 'autoDistributionSection')}
      description={ct(autoPreview ? 'distributionReviewHelp' : 'distributionNoChanges')}
      footer={<><Button disabled={saving} fullWidth={false} onClick={() => autoPreview ? setAutoPreview(undefined) : setAutoOpen(false)} variant="outline">{ct(autoPreview ? 'backToEdit' : 'cancel')}</Button>
        <Button disabled={!autoCollectors.length || autoCollectors.length > 100 || autoReason.trim().length < 2 || (autoScope === 'selected' && !selected.length) || (autoScope === 'queue' && !data?.totalCount)} fullWidth={false} isLoading={saving} onClick={() => void nextAuto(Boolean(autoPreview))}>{ct(autoPreview ? 'confirmDistribution' : 'distributionReview')}</Button></>}>
      <DistributionSteps review={Boolean(autoPreview)} />
      {autoPreview ? <div className="space-y-4">
        <div className="grid grid-cols-2 gap-3 rounded-xl bg-mis-pale p-4"><Info label={ct('casesCount')} value={String(autoPreview.totalCases)} /><Info label={ct('totalOutstanding')} value={money(autoPreview.totalOutstanding)} /><Info label={ct('distributionMethod')} value={ct(autoPreview.method === 'EQUAL_COUNT' ? 'equalByCaseCount' : 'balancedByOutstanding')} /><Info label={ct('distributionCollectorCount')} value={String(autoCollectors.length)} /></div>
        <div className="overflow-x-auto rounded-xl border border-mis-border"><table className="w-full min-w-[32rem] text-sm"><thead className="bg-slate-50"><tr><Th>{ct('collector')}</Th><Th>{ct('currentWorkload')}</Th><Th>{ct('proposedCases')}</Th><Th>{ct('resultingWorkload')}</Th><Th>{ct('totalOutstanding')}</Th></tr></thead><tbody className="divide-y divide-mis-border">{autoCollectors.map(id => {
          const collector = collectors.find(x => x.id === id)!;
          const proposal = autoPreview.collectors.find(x => x.collectorId === id);
          return <tr key={id}><Td>{collector.name}</Td><Td>{collector.assignedCases}</Td><Td>{proposal?.caseCount ?? 0}</Td><Td>{collector.assignedCases + (proposal?.caseCount ?? 0)}</Td><Td bidi>{money(proposal?.outstandingAmount ?? 0)}</Td></tr>;
        })}</tbody></table></div><Info label={ct('assignmentReasonLabel')} value={autoReason} />
      </div> : <div>        <div className="grid gap-4 lg:grid-cols-2">
          <label className="block text-sm font-semibold">{ct('distributionMethod')}
            <ProfessionalSelect className="field mt-2" value={method} onChange={e => setMethod(e.target.value)}>
              <option value="EQUAL_COUNT">{ct('equalByCaseCount')}</option>
              <option value="BALANCED_AMOUNT">{ct('balancedByOutstanding')}</option>
            </ProfessionalSelect><p className="mt-2 text-xs font-normal leading-6 text-slate-500">{ct(method === 'EQUAL_COUNT' ? 'distributionCountMethodHelp' : 'distributionAmountMethodHelp')}</p>
          </label>
<DistributionScope value={autoScope} onChange={setAutoScope} selectedCount={selected.length} queueCount={assigned ? 0 : data?.totalCount ?? 0} />
        </div>
        <fieldset className="mt-4">
          <legend className="mb-2 text-sm font-semibold">{ct('collectorsLabel')}</legend><div className="mb-3 flex flex-wrap gap-4">
            <button className="text-xs font-bold text-mis-primary" onClick={() => setAutoCollectors(collectors.map(x => x.id))} type="button">{ct('selectAllCollectors')}</button>
            <button className="text-xs font-bold text-slate-500" onClick={() => setAutoCollectors([])} type="button">{ct('clearCollectorSelection')}</button>
          </div>
          {collectors.length === 0 ? <p className="text-sm text-slate-500">{ct('noCollectorsWithAccounts')}</p> : (
            <div className="grid max-h-56 gap-2 overflow-auto sm:grid-cols-2 xl:grid-cols-3">
              {collectors.map(x => (
                <label className="flex items-center gap-3 rounded-xl border border-mis-border p-3" key={x.id}>
                  <input checked={autoCollectors.includes(x.id)} onChange={e => setAutoCollectors(v => e.target.checked ? [...v, x.id] : v.filter(id => id !== x.id))} type="checkbox" />
                  <span><strong className="block text-sm">{x.name}</strong><small className="text-slate-500">{x.assignedCases} · {money(x.totalOutstanding)}</small></span>
                </label>
              ))}
            </div>
          )}
        </fieldset>
        <label className="mt-4 block text-sm font-semibold">{ct('assignmentReasonLabel')}
          <textarea className="field mt-2" maxLength={500} rows={2} value={autoReason} onChange={e => setAutoReason(e.target.value)} />
        </label>
      <p className="mt-3 text-xs font-semibold text-mis-primary">{autoCollectors.length} {ct('distributionCollectorCount')}</p>{autoCollectors.length > 100 && <p role="alert" className="mt-2 text-xs text-red-600">{ct('distributionCollectorLimit')}</p>}</div>}
    </Modal>
    <Modal open={Boolean(details)} onClose={() => setDetails(undefined)} size="lg" title={details?.customerName || ct('openPortfolioCase')}>{details && <div className="grid gap-4 sm:grid-cols-2"><Info label={ct('caseId')} value={details.caseNumber} /><Info label={ct('status')} value={ct(details.status)} /><Info label={ct('mobile')} value={details.mobile} /><Info label={ct('outstandingAmount')} value={money(details.outstandingAmount)} /><Info label={ct('assignedTo')} value={details.assignedCollectorName} /><Info label={ct('importedFrom')} value={details.importedFrom} /></div>}</Modal>
    <BankDistributionUploadModal
      bankId={bank.id}
      open={uploadOpen}
      onClose={() => setUploadOpen(false)}
      onCompleted={(result) => {
        setSelected([]);
        setReload((value) => value + 1);
        toast.success(`${ct('assignedCount')}: ${result.assigned} · ${ct('reassignedCount')}: ${result.reassigned} · ${ct('skippedCount')}: ${result.skipped} · ${ct('failedCount')}: ${result.failed}`);
      }}
    />
  </div>;
}

function PreviewBlock({ preview, money, ct }: { preview: DistributionPreview; money: (v:number)=>string; ct:(k:string)=>string }) { return <div className="space-y-4"><Info label={ct('casesCount')} value={String(preview.caseCount)} /><Info label={ct('totalOutstanding')} value={money(preview.totalOutstanding)} />{preview.collectorName && <Info label={ct('collector')} value={preview.collectorName} />}{preview.previousCollectors.length > 0 && <Info label={ct('fromCollectors')} value={preview.previousCollectors.join(', ')} />}</div>; }
function Workload({ collector, additionalCases, ct }: { collector: DistributionCollector; additionalCases: number; ct:(k:string)=>string }) { return <div className="grid grid-cols-2 rounded-xl bg-slate-50 p-3 text-sm"><Info label={ct('currentWorkload')} value={String(collector.assignedCases)} /><Info label={ct('resultingWorkload')} value={String(collector.assignedCases + additionalCases)} /></div>; }
function Info({ label, value }: { label:string; value?:string }) { return <div><p className="text-xs font-semibold text-slate-500">{label}</p><p className="mt-1 font-semibold text-mis-navy">{value || '—'}</p></div>; }
function Th({ children }: { children: React.ReactNode }) { return <th className="px-4 py-3 text-start font-bold">{children}</th>; }
function Td({ children, bidi }: { children: React.ReactNode; bidi?:boolean }) { return <td className="px-4 py-3 text-slate-600" data-bidi={bidi ? 'ltr' : undefined}>{children}</td>; }
