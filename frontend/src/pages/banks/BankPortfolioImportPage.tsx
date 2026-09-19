import { CheckCircle2, FileSpreadsheet, Pencil, RefreshCw, Search, Trash2, Upload } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useOutletContext } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { Pagination } from '../../components/common/Pagination';
import { StatusBadge } from '../../components/common/StatusBadge';
import { useToast } from '../../components/common/Toast';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankPortfolioImport, BankPortfolioImportPage, BankPortfolioReplacementPreview } from '../../features/collections/types/collections';
import { getApiErrorMessage, getApiErrorStatus } from '../../services/apiClient';
import type { BankWorkspaceContext } from './BankWorkspaceLayout';

const maximumBytes = 20 * 1024 * 1024;
const maximumNotesLength = 1000;
const supportedExtensions = ['.xlsx', '.xls', '.csv'];
const extension = (name: string) => name.slice(name.lastIndexOf('.')).toLowerCase();

export function BankPortfolioImportPage() {
  const { bank, workspaceBase } = useOutletContext<BankWorkspaceContext>();
  const { language, ct } = useCollectionsLocalization();
  const toast = useToast();
  const uploadRef = useRef<HTMLInputElement>(null), replaceRef = useRef<HTMLInputElement>(null);
  const deleteInFlightRef = useRef(false), deleteToastRef = useRef<string | undefined>(undefined);
  const [stage, setStage] = useState<'upload' | 'review' | 'success'>('upload');
  const [candidate, setCandidate] = useState<BankPortfolioImport>();
  const [reviewNotes, setReviewNotes] = useState('');
  const [uploading, setUploading] = useState(false), [confirming, setConfirming] = useState(false);
  const [history, setHistory] = useState<BankPortfolioImportPage>(), [historyError, setHistoryError] = useState(false);
  const [page, setPage] = useState(1), [searchInput, setSearchInput] = useState(''), [search, setSearch] = useState(''), [refresh, setRefresh] = useState(0);
  const [editing, setEditing] = useState<BankPortfolioImport>(), [editNotes, setEditNotes] = useState(''), [saving, setSaving] = useState(false);
  const [replacement, setReplacement] = useState<BankPortfolioReplacementPreview>(), [replacing, setReplacing] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<BankPortfolioImport>(), [deleting, setDeleting] = useState(false);
  const bankName = language === 'ar' ? bank.nameArabic : bank.nameEnglish;
  const date = (value: string) => new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value));
  const number = (value: number) => new Intl.NumberFormat(language === 'ar' ? 'ar-EG' : 'en-GB').format(value);

  useEffect(() => { const timer = window.setTimeout(() => { setSearch(searchInput.trim()); setPage(1); }, 300); return () => clearTimeout(timer); }, [searchInput]);
  useEffect(() => { let active = true; setHistoryError(false); void collectionsService.bankPortfolioImports(bank.id, { page, pageSize: 10, search }).then(v => { if (active) setHistory(v); }).catch(() => { if (active) setHistoryError(true); }); return () => { active = false; }; }, [bank.id, page, refresh, search]);

  const validateFile = useCallback((file: File) => {
    if (!supportedExtensions.includes(extension(file.name))) { toast.error(ct('unsupportedPortfolioFile')); return false; }
    if (file.size <= 0) { toast.error(ct('emptyPortfolioFile')); return false; }
    if (file.size > maximumBytes) { toast.error(ct('portfolioFileTooLarge')); return false; }
    return true;
  }, [ct, toast]);

  const upload = useCallback(async (file?: File) => {
    if (!file || !validateFile(file)) return;
    setUploading(true);
    try { setCandidate(await collectionsService.uploadBankPortfolio(bank.id, file)); setReviewNotes(''); setStage('review'); }
    catch (error) { toast.error(getApiErrorMessage(error, ct('portfolioUploadFailed'))); }
    finally { setUploading(false); if (uploadRef.current) uploadRef.current.value = ''; }
  }, [bank.id, ct, toast, validateFile]);

  async function confirm() {
    if (!candidate || confirming) return; setConfirming(true);
    try {
      const confirmed = await collectionsService.confirmBankPortfolio(bank.id, candidate.id, reviewNotes);
      setCandidate(confirmed.import);
      setStage('success');
      setRefresh(v => v + 1);
      toast.success(`${ct('portfolioImportConfirmed')} · ${ct('importedCount')}: ${confirmed.imported} · ${ct('skippedCount')}: ${confirmed.skipped} · ${ct('failedCount')}: ${confirmed.invalid}`);
    }
    catch (error) { toast.error(getApiErrorMessage(error, ct('portfolioUploadFailed'))); } finally { setConfirming(false); }
  }
  function openEdit(item: BankPortfolioImport) { setEditing(item); setEditNotes(item.notes ?? ''); setReplacement(undefined); }
  function closeEdit() { if (!saving && !replacing) { setEditing(undefined); setReplacement(undefined); } }
  async function saveNotes() {
    if (!editing || saving) return; setSaving(true);
    try { const updated = await collectionsService.updateBankPortfolio(bank.id, editing.id, editNotes); setEditing(updated); setRefresh(v => v + 1); toast.success(ct('saved')); }
    catch (error) { toast.error(getApiErrorMessage(error, ct('saveError'))); } finally { setSaving(false); }
  }
  async function previewReplacement(file?: File) {
    if (!file || !editing || !validateFile(file)) return; setReplacing(true);
    try { setReplacement(await collectionsService.previewBankPortfolioReplacement(bank.id, editing.id, file)); }
    catch (error) { toast.error(getApiErrorMessage(error, ct('portfolioUploadFailed'))); } finally { setReplacing(false); if (replaceRef.current) replaceRef.current.value = ''; }
  }
  async function confirmReplacement() {
    if (!editing || !replacement || replacing) return; setReplacing(true);
    try { const updated = await collectionsService.confirmBankPortfolioReplacement(bank.id, editing.id, replacement.token); setEditing(updated); setReplacement(undefined); setRefresh(v => v + 1); toast.success(ct('replacementConfirmed')); }
    catch (error) { toast.error(getApiErrorMessage(error, ct('saveError'))); } finally { setReplacing(false); }
  }
  async function deleteImport() {
    if (!deleteTarget || deleteInFlightRef.current) return;
    deleteInFlightRef.current = true; setDeleting(true);
    try {
      await collectionsService.deleteBankPortfolio(bank.id, deleteTarget.id);
      setDeleteTarget(undefined); setEditing(undefined); setRefresh(v => v + 1);
      if (deleteToastRef.current) toast.dismissToast(deleteToastRef.current);
      deleteToastRef.current = toast.success(ct('importDeletedSuccessfully'));
    }
    catch (error) {
      if (deleteToastRef.current) toast.dismissToast(deleteToastRef.current);
      const status = getApiErrorStatus(error);
      const message = status && status < 500 ? getApiErrorMessage(error, ct('deleteImportFailed')) : ct('deleteImportFailed');
      deleteToastRef.current = toast.error(message);
    }
    finally { deleteInFlightRef.current = false; setDeleting(false); }
  }

  return <div>
    <section className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <div><p className="text-xs font-bold uppercase tracking-[0.18em] text-mis-primary">{bankName}</p><h2 className="mt-2 text-2xl font-bold text-mis-navy">{ct('importPortfolio')}</h2><p className="mt-2 text-sm text-slate-500">{ct('importPortfolioDescription')}</p></div>
      <div className="shrink-0 self-start sm:self-auto"><Button disabled={uploading || stage === 'review'} fullWidth={false} isLoading={uploading} leftIcon={<Upload className="h-4 w-4" />} onClick={() => uploadRef.current?.click()} size="sm">{ct('uploadPortfolioFile')}</Button><input ref={uploadRef} accept=".xlsx,.xls,.csv" className="sr-only" onChange={e => void upload(e.target.files?.[0])} type="file" /></div>
    </section>
    {stage !== 'upload' && candidate ? <ImportSummary candidate={candidate} bankName={bankName} confirming={confirming} ct={ct} date={date} number={number} notes={reviewNotes} setNotes={setReviewNotes} stage={stage} onBack={() => { setCandidate(undefined); setReviewNotes(''); setStage('upload'); }} onConfirm={() => void confirm()} /> : null}
    <section className={`${stage === 'upload' ? '' : 'mt-8 '}overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm`}>
      <div className="flex flex-col gap-4 border-b border-mis-border p-6 sm:flex-row sm:items-center sm:justify-between"><div><h2 className="text-lg font-bold text-mis-navy">{ct('bankImportHistory')}</h2><p className="mt-1 text-sm text-slate-500">{ct('importHistoryDescription')}</p></div><label className="relative block sm:w-80"><span className="sr-only">{ct('searchPortfolioImports')}</span><Search className="absolute top-3 h-4 w-4 text-slate-400" style={{ insetInlineStart: '0.8rem' }} /><input className="field h-10 py-2 pe-3 ps-10 text-sm" value={searchInput} onChange={e => setSearchInput(e.target.value)} placeholder={ct('searchPortfolioImports')} /></label></div>
      {historyError ? <ErrorState compact title={ct('loadError')} onRetry={() => setRefresh(v => v + 1)} /> : !history ? <div className="flex min-h-48 items-center justify-center"><LoadingSpinner /></div> : history.items.length === 0 ? <EmptyState compact icon={<FileSpreadsheet />} title={ct('noPortfolioImports')} /> : <><div className="overflow-x-auto"><table className="w-full min-w-[48rem]"><thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500"><tr><th className="px-6 py-4 text-start">{ct('fileName')}</th><th className="px-6 py-4 text-start">{ct('importDate')}</th><th className="px-6 py-4 text-start">{ct('records')}</th><th className="px-6 py-4 text-start">{ct('uploadedBy')}</th><th className="px-6 py-4 text-start">{ct('status')}</th><th className="px-6 py-4 text-end">{ct('actions')}</th></tr></thead><tbody className="divide-y divide-mis-border">{history.items.map(item => <tr key={item.id}><td className="px-6 py-4"><p className="font-semibold text-mis-navy">{item.originalFileName}</p><p className="mt-1 text-xs text-slate-400">{item.portfolioName}</p></td><td className="px-6 py-4 text-sm text-slate-600">{date(item.uploadedAt)}</td><td className="px-6 py-4 text-sm font-semibold">{number(item.rowCount)}</td><td className="px-6 py-4 text-sm text-slate-600">{item.uploadedBy}</td><td className="px-6 py-4"><StatusBadge dot tone="success">{ct('completed')}</StatusBadge></td><td className="px-6 py-4"><div className="flex flex-wrap justify-end gap-2"><Button fullWidth={false} leftIcon={<Pencil className="h-4 w-4" />} onClick={() => openEdit(item)} size="sm" variant="ghost">{ct('edit')}</Button><Button fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} onClick={() => setDeleteTarget(item)} size="sm" variant="danger">{ct('deleteImport')}</Button></div></td></tr>)}</tbody></table></div><Pagination className="border-t border-mis-border" page={history.page} pageSize={history.pageSize} totalCount={history.totalCount} totalPages={history.totalPages} onPageChange={setPage} /></>}
    </section>
    <Modal footer={editing && !replacement ? <><Button fullWidth={false} onClick={closeEdit} size="md" variant="outline">{ct('cancel')}</Button><Button fullWidth={false} isLoading={saving} onClick={() => void saveNotes()} size="md">{ct('saveChanges')}</Button></> : undefined} onClose={closeEdit} open={Boolean(editing)} size="lg" title={replacement ? ct('replacePortfolioFile') : ct('editImport')}>
      {editing && replacement ? <div><ReadOnly label={ct('currentFile')} value={editing.originalFileName} /><div className="mt-4"><ReadOnly label={ct('newFile')} value={replacement.originalFileName} /></div><div className="mt-4"><ReadOnly label={ct('records')} value={number(replacement.rowCount)} /></div><div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end"><Button fullWidth={false} onClick={() => setReplacement(undefined)} size="md" variant="outline">{ct('cancel')}</Button><Button fullWidth={false} isLoading={replacing} onClick={() => void confirmReplacement()} size="md">{ct('confirmReplacement')}</Button></div></div> : editing ? <div>
        <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2"><ReadOnly label={ct('file')} value={editing.originalFileName} /><ReadOnly label={ct('bank')} value={bankName} /><ReadOnly label={ct('importDate')} value={date(editing.uploadedAt)} /><ReadOnly label={ct('records')} value={number(editing.rowCount)} /><ReadOnly label={ct('status')} value={ct(editing.status)} /></dl>
        <Notes value={editNotes} setValue={setEditNotes} ct={ct} />
        <div className="mt-5"><Button fullWidth={false} isLoading={replacing} leftIcon={<RefreshCw className="h-4 w-4" />} onClick={() => replaceRef.current?.click()} size="md" variant="secondary">{ct('replaceFile')}</Button><input ref={replaceRef} accept=".xlsx,.xls,.csv" className="sr-only" onChange={e => void previewReplacement(e.target.files?.[0])} type="file" /></div>
        <section className="mt-8 border-t border-mis-border pt-6"><h3 className="font-bold text-mis-navy">{ct('archive')}</h3><p className="mt-1 text-sm text-slate-500">{ct('archiveDescription')}</p><Link className="mt-4 inline-flex rounded-xl border border-slate-300 px-4 py-2.5 text-sm font-bold text-slate-700" to={`${workspaceBase}/archive?portfolioId=${editing.id}&portfolioName=${encodeURIComponent(editing.portfolioName)}`}>{ct('archivePortfolio')}</Link></section><section className="mt-8 border-t border-red-200 pt-6"><h3 className="font-bold text-red-700">{ct('dangerZone')}</h3><p className="mt-2 text-sm font-semibold">{ct('deleteImport')}</p><p className="mt-1 text-sm text-slate-500">{ct('deleteImportDescription')}</p><Button className="mt-4" fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} onClick={() => { setDeleteTarget(editing); setEditing(undefined); }} size="md" variant="danger">{ct('deleteImport')}</Button></section>
      </div> : null}
    </Modal>
    <ConfirmDialog cancelLabel={ct('cancel')} confirmLabel={ct('delete')} isConfirming={deleting} message={<><p>{ct('deleteImportPrompt')}</p><p className="my-2 break-words font-bold text-mis-navy">{deleteTarget?.originalFileName}</p><p>{ct('deleteImportCasesWarning')}</p><p className="mt-2 text-sm text-slate-500">{ct('cannotUndo')}</p></>} onCancel={() => setDeleteTarget(undefined)} onConfirm={() => void deleteImport()} open={Boolean(deleteTarget)} title={ct('deletePortfolioImport')} />
  </div>;
}

function ReadOnly({ label, value }: { label: string; value: string }) { return <div><dt className="text-xs font-semibold text-slate-500">{label}</dt><dd className="mt-1 break-words font-semibold text-mis-navy">{value}</dd></div>; }
function Notes({ value, setValue, ct }: { value: string; setValue: (value: string) => void; ct: (key: string) => string }) { return <label className="mt-6 block text-sm font-semibold text-slate-700">{ct('notes')}<textarea className="field mt-2 min-h-24 resize-y" maxLength={maximumNotesLength} onChange={e => setValue(e.target.value)} placeholder={ct('notesPlaceholder')} value={value} /><span className="mt-1 block text-end text-xs text-slate-400">{value.length}/{maximumNotesLength}</span></label>; }
function ImportSummary({ candidate, bankName, confirming, ct, date, number, notes, setNotes, stage, onBack, onConfirm }: { candidate: BankPortfolioImport; bankName: string; confirming: boolean; ct: (key: string) => string; date: (value: string) => string; number: (value: number) => string; notes: string; setNotes: (value: string) => void; stage: 'review' | 'success'; onBack: () => void; onConfirm: () => void }) {
  const success = stage === 'success'; const fields = [[ct('file'), candidate.originalFileName], [ct('bank'), bankName], [ct('records'), number(candidate.rowCount)], [ct('importDate'), `${date(candidate.uploadedAt)} · ${ct('automatic')}`]];
  return <section className="rounded-2xl border border-mis-border bg-white p-6 shadow-sm"><div className="flex items-center gap-3"><div className={`grid h-11 w-11 place-items-center rounded-xl ${success ? 'bg-emerald-50 text-emerald-700' : 'bg-mis-pale text-mis-primary'}`}>{success ? <CheckCircle2 className="h-6 w-6" /> : <FileSpreadsheet className="h-6 w-6" />}</div><h3 className="text-xl font-bold text-mis-navy">{success ? ct('portfolioImported') : ct('review')}</h3></div><dl className="mt-6 grid gap-5 sm:grid-cols-2">{fields.map(([label, value]) => <ReadOnly key={label} label={label} value={value} />)}</dl>{!success ? <Notes value={notes} setValue={setNotes} ct={ct} /> : null}<div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">{success ? <Button fullWidth={false} onClick={onBack} size="md">{ct('importAnotherFile')}</Button> : <><Button fullWidth={false} onClick={onBack} size="md" variant="outline">{ct('cancel')}</Button><Button fullWidth={false} isLoading={confirming} onClick={onConfirm} size="md">{ct('confirmImportPortfolio')}</Button></>}</div></section>;
}
