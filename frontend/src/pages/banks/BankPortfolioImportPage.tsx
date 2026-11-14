import { ActionMenu } from '../../components/common/ActionMenu';
import { isAxiosError } from 'axios';
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
import type { BankPortfolioImport, BankPortfolioImportDataPreview, BankPortfolioImportPage, BankPortfolioReplacementPreview } from '../../features/collections/types/collections';
import { getApiErrorMessage, getApiErrorStatus } from '../../services/apiClient';
import type { BankWorkspaceContext } from './BankWorkspaceLayout';

const maximumBytes = 50 * 1024 * 1024;
const maximumNotesLength = 1000;
const supportedExtensions = ['.xlsx', '.xls', '.csv'];
const extension = (name: string) => name.slice(name.lastIndexOf('.')).toLowerCase();

export function BankPortfolioImportPage() {
  const context = useOutletContext<BankWorkspaceContext>();
  return <BankPortfolioImportWorkspace key={context.workspaceBase} bank={context.bank} workspaceBase={context.workspaceBase} />;
}

function BankPortfolioImportWorkspace({ bank, workspaceBase }: Pick<BankWorkspaceContext, 'bank' | 'workspaceBase'>) {
  const { language, ct } = useCollectionsLocalization();
  const toast = useToast();
  const uploadRef = useRef<HTMLInputElement>(null), replaceRef = useRef<HTMLInputElement>(null);
  const deleteInFlightRef = useRef(false), deleteToastRef = useRef<string | undefined>(undefined);
  const [stage, setStage] = useState<'upload' | 'review' | 'success'>('upload');
  const [candidate, setCandidate] = useState<BankPortfolioImport>();
  const [preview, setPreview] = useState<BankPortfolioImportDataPreview>();
  const [previewLoading, setPreviewLoading] = useState(false), [previewError, setPreviewError] = useState(false);
  const [previewPage, setPreviewPage] = useState(1);
  const [reviewNotes, setReviewNotes] = useState('');
  const [uploading, setUploading] = useState(false), [confirming, setConfirming] = useState(false);
  const [history, setHistory] = useState<BankPortfolioImportPage>(), [historyError, setHistoryError] = useState('');
  const [page, setPage] = useState(1), [searchInput, setSearchInput] = useState(''), [search, setSearch] = useState(''), [refresh, setRefresh] = useState(0);
  const [editing, setEditing] = useState<BankPortfolioImport>(), [editNotes, setEditNotes] = useState(''), [saving, setSaving] = useState(false);
  const [replacement, setReplacement] = useState<BankPortfolioReplacementPreview>(), [replacing, setReplacing] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<BankPortfolioImport>(), [deleting, setDeleting] = useState(false);
  const bankName = language === 'ar' ? bank.nameArabic : bank.nameEnglish;
  const date = (value: string) => new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG-u-nu-latn' : 'en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value));
  const number = (value: number) => new Intl.NumberFormat(language === 'ar' ? 'ar-EG-u-nu-latn' : 'en-GB').format(value);

  useEffect(() => { const timer = window.setTimeout(() => { setSearch(searchInput.trim()); setPage(1); }, 300); return () => clearTimeout(timer); }, [searchInput]);
  useEffect(() => {
    let active = true;
    setHistoryError(''); setHistory(undefined);
    void collectionsService.bankPortfolioImports(bank.id, { page, pageSize: 10, search })
      .then(value => { if (active) setHistory(value); })
      .catch(error => {
        if (!active) return;
        const responseMessage = isAxiosError<{ message?: unknown }>(error) ? error.response?.data?.message : undefined;
        const message = getApiErrorMessage(error, '') || (typeof responseMessage === 'string' ? responseMessage.trim() : '');
        if (language !== 'ar' || /[\u0600-\u06ff]/.test(message)) setHistoryError(message || ct('loadError'));
        else if (message === 'The selected classified portfolio does not exist or is inactive.')
          setHistoryError('المحفظة المختارة غير مجهزة أو غير مفعّلة. راجع إعداداتها مع مسؤول النظام.');
        else if (message === 'Bank was not found or is outside your authorized scope.')
          setHistoryError('البنك غير موجود أو ليس ضمن الجهات المسموح لك بالوصول إليها.');
        else if (getApiErrorStatus(error) === 404)
          setHistoryError('خدمة سجل الاستيراد غير متاحة حاليًا. أعد المحاولة أو تواصل مع مسؤول النظام.');
        else setHistoryError(ct('loadError'));
      });
    return () => { active = false; };
  }, [bank.id, page, refresh, search, language, ct]);

  const validateFile = useCallback((file: File) => {
    if (!supportedExtensions.includes(extension(file.name))) { toast.error(ct('unsupportedPortfolioFile')); return false; }
    if (file.size <= 0) { toast.error(ct('emptyPortfolioFile')); return false; }
    if (file.size > maximumBytes) { toast.error(ct('portfolioFileTooLarge')); return false; }
    return true;
  }, [ct, toast]);

  const loadPreview = useCallback(async (importId: string) => {
    setPreview(undefined); setPreviewError(false); setPreviewLoading(true); setPreviewPage(1);
    try { setPreview(await collectionsService.previewBankPortfolioData(bank.id, importId)); }
    catch (error) { setPreviewError(true); toast.error(getApiErrorMessage(error, ct('portfolioPreviewFailed'))); }
    finally { setPreviewLoading(false); }
  }, [bank.id, ct, toast]);

  const upload = useCallback(async (file?: File) => {
    if (!file || !validateFile(file)) return;
    setUploading(true);
    try {
      const uploaded = await collectionsService.uploadBankPortfolio(bank.id, file);
      setCandidate(uploaded); setReviewNotes(''); setStage('review');
      await loadPreview(uploaded.id);
    }
    catch (error) { toast.error(getApiErrorMessage(error, ct('portfolioUploadFailed'))); }
    finally { setUploading(false); if (uploadRef.current) uploadRef.current.value = ''; }
  }, [bank.id, ct, loadPreview, toast, validateFile]);

  async function confirm() {
    if (!candidate || !preview || previewLoading || confirming || (!preview.readyRows && !preview.existingRows)) return; setConfirming(true);
    try {
      const confirmed = await collectionsService.confirmBankPortfolio(bank.id, candidate.id, reviewNotes);
      setCandidate(confirmed.import);
      setStage('success');
      setRefresh(v => v + 1);
      const successMessage = language === 'ar' ? 'تم استلام ملف المحفظة واستيراد البيانات بنجاح' : ct('portfolioImportConfirmed');
      toast.success(`${successMessage} · ${ct('importedCount')}: ${number(confirmed.imported)}`);
    }
    catch (error) { toast.error(getApiErrorMessage(error, ct('portfolioUploadFailed'))); } finally { setConfirming(false); }
  }
  async function leaveImportReview() {
    if (confirming) return;
    if (stage === 'review' && candidate) {
      try { await collectionsService.deleteBankPortfolio(bank.id, candidate.id); }
      catch (error) { toast.error(getApiErrorMessage(error, ct('portfolioUploadFailed'))); return; }
    }
    setCandidate(undefined); setPreview(undefined); setReviewNotes(''); setStage('upload'); setRefresh(v => v + 1);
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
    {stage !== 'upload' && candidate ? <ImportSummary candidate={candidate} bankName={bankName} confirming={confirming} ct={ct} date={date} number={number} notes={reviewNotes} setNotes={setReviewNotes} stage={stage} preview={preview} previewLoading={previewLoading} previewError={previewError} previewPage={previewPage} setPreviewPage={setPreviewPage} onRetryPreview={() => void loadPreview(candidate.id)} onBack={() => void leaveImportReview()} onConfirm={() => void confirm()} /> : null}
    <section className={`${stage === 'upload' ? '' : 'mt-8 '}overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm`}>
      <div className="flex flex-col gap-4 border-b border-mis-border p-6 sm:flex-row sm:items-center sm:justify-between"><div><h2 className="text-lg font-bold text-mis-navy">{ct('bankImportHistory')}</h2><p className="mt-1 text-sm text-slate-500">{ct('importHistoryDescription')}</p></div><label className="relative block sm:w-80"><span className="sr-only">{ct('searchPortfolioImports')}</span><Search className="absolute top-3 h-4 w-4 text-mis-muted" style={{ insetInlineStart: '0.8rem' }} /><input className="field h-10 py-2 pe-3 ps-10 text-sm" value={searchInput} onChange={e => setSearchInput(e.target.value)} placeholder={ct('searchPortfolioImports')} /></label></div>
      {historyError ? <ErrorState compact title={language === 'ar' ? 'تعذر تحميل سجل الاستيراد' : 'Could not load import history'} description={historyError} onRetry={() => setRefresh(v => v + 1)} /> : !history ? <div className="flex min-h-48 items-center justify-center"><LoadingSpinner /></div> : history.items.length === 0 ? <EmptyState compact icon={<FileSpreadsheet />} title={ct('noPortfolioImports')} /> : <><div className="overflow-x-auto"><table className="w-full min-w-[68rem]"><thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500"><tr><th className="px-6 py-4 text-start">{ct('fileName')}</th><th className="px-6 py-4 text-start">{ct('importDate')}</th><th className="px-6 py-4 text-start">{ct('records')}</th><th className="px-6 py-4 text-start">{ct('importedCount')}</th><th className="px-6 py-4 text-start">{ct('updatedCount')}</th><th className="px-6 py-4 text-start">{ct('duplicateCount')}</th><th className="px-6 py-4 text-start">{ct('invalidCount')}</th><th className="px-6 py-4 text-start">{ct('uploadedBy')}</th><th className="px-6 py-4 text-start">{ct('status')}</th><th className="px-6 py-4 text-end">{ct('actions')}</th></tr></thead><tbody className="divide-y divide-mis-border">{history.items.map(item => <tr key={item.id}><td className="px-6 py-4"><p className="font-semibold text-mis-navy">{item.originalFileName}</p><p className="mt-1 text-xs text-mis-muted">{item.portfolioName}</p></td><td className="px-6 py-4 text-sm text-slate-600">{date(item.uploadedAt)}</td><td className="px-6 py-4 text-sm font-semibold">{number(item.rowCount)}</td><td className="px-6 py-4 text-sm">{number(item.importedRows)}</td><td className="px-6 py-4 text-sm">{number(item.updatedRows)}</td><td className="px-6 py-4 text-sm">{number(item.duplicateRows)}</td><td className="px-6 py-4 text-sm">{number(item.invalidRows)}</td><td className="px-6 py-4 text-sm text-slate-600">{item.uploadedBy}</td><td className="px-6 py-4"><StatusBadge dot tone="success">{ct('completed')}</StatusBadge></td><td className="px-6 py-4"><ActionMenu><Button fullWidth={false} leftIcon={<Pencil className="h-4 w-4" />} onClick={() => openEdit(item)} size="sm" variant="ghost">{ct('edit')}</Button><Button fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} onClick={() => setDeleteTarget(item)} size="sm" variant="danger">{ct('deleteImport')}</Button></ActionMenu></td></tr>)}</tbody></table></div><Pagination className="border-t border-mis-border" page={history.page} pageSize={history.pageSize} totalCount={history.totalCount} totalPages={history.totalPages} onPageChange={setPage} /></>}
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
function Notes({ value, setValue, ct }: { value: string; setValue: (value: string) => void; ct: (key: string) => string }) { return <label className="mt-6 block text-sm font-semibold text-slate-700">{ct('notes')}<textarea className="field mt-2 min-h-24 resize-y" maxLength={maximumNotesLength} onChange={e => setValue(e.target.value)} placeholder={ct('notesPlaceholder')} value={value} /><span className="mt-1 block text-end text-xs text-mis-muted">{value.length}/{maximumNotesLength}</span></label>; }
function ImportSummary({ candidate, bankName, confirming, ct, date, number, notes, setNotes, stage, preview, previewLoading, previewError, previewPage, setPreviewPage, onRetryPreview, onBack, onConfirm }: {
  candidate: BankPortfolioImport; bankName: string; confirming: boolean; ct: (key: string) => string; date: (value: string) => string;
  number: (value: number) => string; notes: string; setNotes: (value: string) => void; stage: 'review' | 'success';
  preview?: BankPortfolioImportDataPreview; previewLoading: boolean; previewError: boolean; previewPage: number; setPreviewPage: (page: number) => void;
  onRetryPreview: () => void; onBack: () => void; onConfirm: () => void;
}) {
  const success = stage === 'success';
  const fields = [[ct('file'), candidate.originalFileName], [ct('bank'), bankName], [ct('portfolio'), candidate.portfolioName], [ct('records'), number(candidate.rowCount)], [ct('importDate'), `${date(candidate.uploadedAt)} · ${ct('automatic')}`]];
  const rowsPerPage = 25;
  const totalPages = preview ? Math.max(1, Math.ceil(preview.rows.length / rowsPerPage)) : 1;
  const visibleRows = preview?.rows.slice((previewPage - 1) * rowsPerPage, previewPage * rowsPerPage) ?? [];
  const canConfirm = Boolean(preview && (preview.readyRows > 0 || preview.existingRows > 0));
  const successMessage = ct('portfolioImportSuccessExact');
  return <section className="rounded-2xl border border-mis-border bg-white p-6 shadow-sm">
    <div className="flex items-center gap-3"><div className={`grid h-11 w-11 place-items-center rounded-xl ${success ? 'bg-emerald-50 text-emerald-700' : 'bg-mis-pale text-mis-primary'}`}>{success ? <CheckCircle2 className="h-6 w-6" /> : <FileSpreadsheet className="h-6 w-6" />}</div><h3 className="text-xl font-bold text-mis-navy">{success ? ct('portfolioImported') : ct('review')}</h3></div>
    <dl className="mt-6 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">{fields.map(([label, value]) => <ReadOnly key={label} label={label} value={value} />)}</dl>
    {success ? <div className="mt-6 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-emerald-900" role="status">
      <p className="font-bold">{successMessage}</p>
      <p className="mt-2 text-sm">{ct('importedCount')}: {number(candidate.importedRows + candidate.updatedRows)} · {ct('updatedCount')}: {number(candidate.updatedRows)} · {ct('duplicateCount')}: {number(candidate.duplicateRows)} · {ct('invalidCount')}: {number(candidate.invalidRows)}</p>
    </div> : <>
      {previewLoading ? <div className="mt-6 flex min-h-32 items-center justify-center"><LoadingSpinner /></div> : previewError ? <div className="mt-6 rounded-xl border border-rose-200 bg-rose-50 p-4"><p className="text-sm text-rose-800">{ct('portfolioPreviewFailed')}</p><Button className="mt-3" fullWidth={false} onClick={onRetryPreview} size="sm" variant="outline">{ct('portfolioPreviewRetry')}</Button></div> : preview ? <div className="mt-6 space-y-5" dir={document.documentElement.dir}>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <CountCard label={ct('records')} value={preview.totalRows} number={number} />
          <CountCard label={ct('validRows')} value={preview.readyRows} number={number} />
          <CountCard label={ct('existingRows')} value={preview.existingRows} number={number} />
          <CountCard label={ct('duplicateCount')} value={preview.duplicateRows} number={number} />
          <CountCard label={ct('invalidCount')} value={preview.invalidRows} number={number} />
        </div>
        <div className="rounded-xl border border-mis-border p-4">
          <h4 className="font-bold text-mis-navy">{ct('detectedColumnMappings')}</h4>
          {preview.detectedColumns?.length ? <div className="mt-3 flex flex-wrap gap-2">{preview.detectedColumns.map(mapping => <span key={`${mapping.field}:${mapping.sourceColumn}`} className="rounded-lg bg-slate-100 px-3 py-1.5 text-xs text-slate-700"><strong>{ct(mapping.field)}</strong>: {mapping.sourceColumn}</span>)}</div> : <p className="mt-2 text-sm text-slate-500">{ct('noDetectedColumns')}</p>}
        </div>
        <div className="overflow-hidden rounded-xl border border-mis-border">
          <div className="flex flex-col gap-2 border-b border-mis-border bg-slate-50 p-4 sm:flex-row sm:items-center sm:justify-between"><h4 className="font-bold text-mis-navy">{ct('rowValidation')}</h4><span className="text-xs text-slate-500">{number(visibleRows.length)} / {number(preview.rows.length)}</span></div>
          <div className="max-h-[34rem] overflow-auto"><table className="w-full min-w-[58rem] text-start text-sm"><thead className="sticky top-0 bg-white text-xs text-slate-500 shadow-sm"><tr><th className="px-3 py-3 text-start">{ct('rowNumber')}</th><th className="px-3 py-3 text-start">{ct('accountNumber')}</th><th className="px-3 py-3 text-start">{ct('customerName')}</th><th className="px-3 py-3 text-start">{ct('outstandingAmount')}</th><th className="px-3 py-3 text-start">{ct('bucket')}</th><th className="px-3 py-3 text-start">{ct('status')}</th><th className="px-3 py-3 text-start">{ct('validationErrors')}</th></tr></thead><tbody className="divide-y divide-mis-border">{visibleRows.map(row => <tr key={row.rowNumber}><td className="px-3 py-3">{number(row.rowNumber)}</td><td className="px-3 py-3 font-medium text-mis-navy">{row.accountReference ?? '—'}</td><td className="px-3 py-3">{row.customerName ?? '—'}</td><td className="px-3 py-3">{row.outstandingBalance == null ? '—' : number(row.outstandingBalance)}</td><td className="px-3 py-3">{row.bucket ?? '—'}</td><td className="px-3 py-3"><StatusBadge tone={row.status === 'Ready' ? 'success' : row.status === 'Existing' || row.status === 'Duplicate' ? 'warning' : 'danger'}>{ct(row.status)}</StatusBadge></td><td className="px-3 py-3 text-xs text-rose-700">{row.errors.length ? row.errors.join(' · ') : '—'}</td></tr>)}</tbody></table></div>
          {totalPages > 1 ? <div className="flex items-center justify-between border-t border-mis-border px-4 py-3"><Button fullWidth={false} disabled={previewPage <= 1} onClick={() => setPreviewPage(previewPage - 1)} size="sm" variant="outline">{ct('previousPage')}</Button><span className="text-xs text-slate-600">{number(previewPage)} / {number(totalPages)}</span><Button fullWidth={false} disabled={previewPage >= totalPages} onClick={() => setPreviewPage(previewPage + 1)} size="sm" variant="outline">{ct('nextPage')}</Button></div> : null}
        </div>
      </div> : null}
      <Notes value={notes} setValue={setNotes} ct={ct} />
    </>}
    <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">{success ? <Button fullWidth={false} onClick={onBack} size="md">{ct('importAnotherFile')}</Button> : <><Button fullWidth={false} onClick={onBack} size="md" variant="outline">{ct('cancel')}</Button><Button disabled={!canConfirm || previewLoading || previewError} fullWidth={false} isLoading={confirming} onClick={onConfirm} size="md">{ct('confirmImportPortfolio')}</Button></>}</div>
  </section>;
}

function CountCard({ label, value, number }: { label: string; value: number; number: (value: number) => string }) {
  return <div className="rounded-xl border border-mis-border bg-slate-50 p-3"><p className="text-xs font-medium text-slate-500">{label}</p><p className="mt-1 text-xl font-bold text-mis-navy">{number(value)}</p></div>;
}
