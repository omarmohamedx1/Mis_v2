import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { Archive, ArrowLeft, ArrowRightLeft, Download, Eye, FileCheck2, HandCoins, MessageSquarePlus, Paperclip, Phone, Scale, ShieldCheck, Trash2 } from 'lucide-react';
import { type FormEvent, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { useToast } from '../../components/common/Toast';
import { CollectionStatus, KpiCard, useCollectionFormat } from '../../features/collections/components/CollectionsUi';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { AssignmentPreview, CaseDetails, CollectionAttachment, CollectorLookup } from '../../features/collections/types/collections';
import { useAuth } from '../../context/AuthContext';
import { DataEntryDocumentsPanel } from '../../features/data-entry/DataEntryDocumentsPanel';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryDocument } from '../../features/data-entry/types/dataEntry';
import { canAccessModule } from '../../features/modules/moduleAccess';
import { ProfessionalDateInput } from '../../components/forms/ProfessionalDateInput';
import { FileInput } from '../../components/forms/FileInput';
import { ExtraFieldsPanel } from '../../features/import';
import { getApiErrorMessage } from '../../services/apiClient';

type Action = 'activity' | 'promise' | 'payment';
export function CollectionCaseDetailsPage() {
  const { id = '' } = useParams(); const { language, ct } = useCollectionsLocalization(); const f = useCollectionFormat(); const { user } = useAuth(); const navigate = useNavigate();   const [data, setData] = useState<CaseDetails>(); const [attachments, setAttachments] = useState<CollectionAttachment[]>([]); const [entryDocs, setEntryDocs] = useState<DataEntryDocument[]>([]); const [error, setError] = useState(false); const [action, setAction] = useState<Action>('activity'); const [saving, setSaving] = useState(false); const [message, setMessage] = useState(''); const [attachmentToDelete, setAttachmentToDelete] = useState<CollectionAttachment>(); const [deleting, setDeleting] = useState(false); const [archiveOpen, setArchiveOpen] = useState(false); const [archiving, setArchiving] = useState(false); const [transferOpen, setTransferOpen] = useState(false);
  const revealAllowed = user?.roles.some(role => ['Admin', 'CollectionsOperationsManager', 'CollectionsSupervisor', 'CollectionsAuditor'].includes(role));
  const manageAllowed = user?.roles.some(role => ['Admin', 'CollectionsOperationsManager', 'CollectionsSupervisor'].includes(role));
  const load = () => { setError(false); Promise.all([collectionsService.caseDetails(id), collectionsService.attachments(id), manageAllowed ? dataEntryService.caseDocuments(id).catch(() => [] as DataEntryDocument[]) : Promise.resolve([] as DataEntryDocument[])]).then(([details, files, docs]) => { setData(details); setAttachments(files); setEntryDocs(docs); }).catch(() => setError(true)); }; useEffect(load, [id, manageAllowed]);
  const legalAllowed = user ? canAccessModule(user, 'legal') : false;
  const reveal = async () => { setSaving(true); setMessage(''); try { setData(await collectionsService.revealSensitive(id)); setMessage(ct('revealed')); } catch { setMessage(ct('saveError')); } finally { setSaving(false); } };
  const confirmDeleteAttachment = async () => { if (!attachmentToDelete) return; setDeleting(true); try { await collectionsService.deleteAttachment(attachmentToDelete.id); setAttachmentToDelete(undefined); setAttachments(await collectionsService.attachments(id)); } catch { setMessage(ct('deleteFailed')); setAttachmentToDelete(undefined); } finally { setDeleting(false); } };
  const confirmArchive = async () => { setArchiving(true); try { await collectionsService.archiveCollectionCase(id, 'CASE_CLOSED'); navigate('/collections/cases'); } catch { setMessage(ct('saveError')); setArchiveOpen(false); } finally { setArchiving(false); } };
  if (error) return <ErrorState title={ct('loadError')} onRetry={load} />; if (!data) return <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>;
  return <div><PageHeader breadcrumbs={<Link to="/collections/cases" className="inline-flex items-center gap-2 text-sm font-semibold text-mis-primary"><ArrowLeft className="h-4 w-4 rtl:rotate-180" />{ct('backToCases')}</Link>} eyebrow={`${data.clientName} · ${data.portfolioName}`} title={`${ct('case360')} — ${data.caseNumber}`} description={<span>{data.customerName} · <span data-bidi="ltr">{data.accountReference}</span></span>} actions={<div className="flex flex-wrap items-center gap-2"><CollectionStatus value={data.priority} />{data.status === 'LEGAL' && legalAllowed ? <Button variant="outline" size="md" fullWidth={false} leftIcon={<Scale className="h-4 w-4" />} onClick={() => navigate(`/legal/cases/${data.id}`)}>{ct('openLegalFile')}</Button> : null}{revealAllowed && !data.sensitiveValuesRevealed ? <Button variant="outline" size="md" fullWidth={false} onClick={reveal} isLoading={saving} leftIcon={<Eye className="h-4 w-4" />}>{ct('reveal')}</Button> : null}{manageAllowed ? <Button variant="secondary" size="md" fullWidth={false} onClick={() => setTransferOpen(true)} leftIcon={<ArrowRightLeft className="h-4 w-4" />}>{data.assignedCollectorId ? ct('transferCase') : ct('assignCollector')}</Button> : null}{manageAllowed ? <Button variant="danger" size="md" fullWidth={false} onClick={() => setArchiveOpen(true)} leftIcon={<Archive className="h-4 w-4" />}>{ct('archiveCase')}</Button> : null}</div>} />
    {message && <div className={`mb-5 rounded-xl border px-4 py-3 text-sm ${message === ct('revealed') || message === ct('transferComplete') ? 'border-emerald-200 bg-emerald-50 text-emerald-800' : 'border-rose-200 bg-rose-50 text-rose-800'}`}>{message}</div>}
    <section className="mb-5 rounded-2xl border border-blue-200 bg-gradient-to-r from-blue-50 to-white p-5 shadow-sm"><p className="text-xs font-bold uppercase tracking-wide text-mis-primary">{ct('nextBestAction')}</p><div className="mt-2 flex flex-wrap items-start justify-between gap-3"><div><h2 className="text-lg font-bold text-mis-navy">{ct(data.recommendedActionCode)}</h2><p className="mt-1 text-sm leading-6 text-slate-600">{data.recommendedActionReason}</p></div><span className="rounded-full bg-white px-3 py-1 text-xs font-bold text-mis-primary shadow-sm" data-bidi="ltr">{data.recommendedActionCode}</span></div></section>
    {(data.relatedCreditorCases ?? []).length > 0 ? (
      <section className="mb-5 rounded-2xl border border-mis-sky bg-white p-5 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="text-xs font-bold uppercase tracking-wide text-mis-primary">{ct('relatedCreditors')}</p>
            <p className="mt-1 text-sm text-slate-600">{ct('relatedCreditorsHelp')}</p>
          </div>
          <p className="rounded-full bg-mis-pale px-3 py-1 text-xs font-bold text-mis-primary">
            {new Set((data.relatedCreditorCases ?? []).map((item) => item.organizationId)).size} · {(data.relatedCreditorCases ?? []).length} · <span data-bidi="ltr">{f.money((data.relatedCreditorCases ?? []).reduce((sum, item) => sum + item.outstandingBalance, 0))}</span>
          </p>
        </div>
        <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {(data.relatedCreditorCases ?? []).map((item) => (
            <Link className={`rounded-xl border px-4 py-3 ${item.caseId === data.id ? 'border-mis-primary bg-mis-pale' : 'border-mis-border hover:border-mis-primary'}`} key={item.caseId} to={`/collections/cases/${item.caseId}`}>
              <p className="font-bold text-mis-navy">{item.organizationName}</p>
              <p className="mt-1 text-xs text-slate-500">{ct(item.organizationType)} · {item.portfolioName}</p>
              <p className="mt-1 text-xs text-slate-500" data-bidi="ltr">{item.accountReference}</p>
              <p className="mt-2 text-sm font-semibold" data-bidi="ltr">{f.money(item.outstandingBalance)}</p>
            </Link>
          ))}
        </div>
      </section>
    ) : null}
    <section className="grid gap-5 xl:grid-cols-2"><Panel title={ct('customerIdentity')} icon={<ShieldCheck className="h-5 w-5" />}><dl className="grid gap-4 sm:grid-cols-2"><Info label={ct('customer')} value={`${data.customerName} (${data.customerCode})`} /><Info label={ct('nationalId')} value={data.nationalId} bidi /><Info label={ct('mobile1')} value={data.primaryPhone} bidi /><Info label={ct('alternatePhone')} value={data.alternatePhone || '—'} bidi /><Info label={ct('mobile3')} value={data.tertiaryPhone || '—'} bidi /><Info label={ct('region')} value={[data.area, data.governorate, data.city].filter(Boolean).join(' · ') || '—'} /><Info label={ct('employer')} value={data.employer || '—'} /><Info label={ct('jobTitle')} value={data.jobTitle || '—'} /><Info label={ct('address1')} value={data.address || '—'} wide /><Info label={ct('secondaryAddress')} value={data.secondaryAddress || '—'} wide /><Info label={ct('collector')} value={<CollectorStatus details={data} />} /><Info label={ct('fileCollector')} value={collectorLabel(data.fileCollectorUserName, data.fileCollectorName)} /><Info label={ct('previousCollector')} value={collectorLabel(data.previousCollectorUserName, data.previousCollectorName)} /></dl></Panel><Panel title={ct('financialPosition')} icon={<HandCoins className="h-5 w-5" />}><div className="grid gap-3 sm:grid-cols-2"><KpiCard label={ct('currentBalance')} value={f.money(data.outstandingBalance)} /><KpiCard label={ct('totalDues')} value={f.money(data.overdueBalance)} accent="red" /><KpiCard label={ct('creditLimit')} value={data.creditLimit == null ? '—' : f.money(data.creditLimit)} /><KpiCard label={ct('dpdBucket')} value={`${data.importBucketLabel || data.bucket}`} accent="amber" /></div><dl className="mt-5 grid gap-4 sm:grid-cols-2"><Info label={ct('caseRef')} value={data.accountReference} bidi /><Info label={ct('cardNumber')} value={data.cardNumber || '—'} bidi /><Info label={ct('fileStatus')} value={data.importStatusText || ct(data.status)} /><Info label={ct('product')} value={data.productType || '—'} /><Info label={ct('availableLimit')} value={data.purchaseAvailableLimit == null ? '—' : f.money(data.purchaseAvailableLimit)} bidi /><Info label={ct('activationDate')} value={data.activationDate ? f.date(data.activationDate) : '—'} /><Info label={ct('lastPayment')} value={data.lastPaymentAmount == null ? '—' : `${f.money(data.lastPaymentAmount)}${data.lastPaymentAt ? ` · ${f.date(data.lastPaymentAt)}` : ''}`} bidi /><Info label={ct('lastTransaction')} value={data.lastTransactionAmount == null ? '—' : `${f.money(data.lastTransactionAmount)}${data.lastTransactionDate ? ` · ${f.date(data.lastTransactionDate)}` : ''}`} bidi /></dl></Panel></section>
    {data.fileSnapshot ? <section className="mt-5"><Panel title={ct('fileSnapshot')} icon={<FileCheck2 className="h-5 w-5" />}><dl className="grid gap-4 sm:grid-cols-3"><Info label={ct('fileAction')} value={data.fileSnapshot.action || '—'} /><Info label={ct('ptpDateFile')} value={data.fileSnapshot.ptpDate || '—'} /><Info label={ct('ptpAmount')} value={data.fileSnapshot.ptpAmount || '—'} /><Info label={ct('filePayment')} value={data.fileSnapshot.payment || '—'} /><Info label={ct('fileUpdate')} value={data.fileSnapshot.update || '—'} /><Info label={ct('keepFlag')} value={data.fileSnapshot.keep || '—'} /><Info label={ct('feedback')} value={data.fileSnapshot.feedback || data.feedback || '—'} wide /></dl></Panel></section> : null}
    <div className="mt-5"><ExtraFieldsPanel arabic={language === 'ar'} fields={Object.entries(data.fileSnapshot?.extraFields ?? {})} /></div>
    <section className="mt-6 grid min-w-0 gap-5 xl:grid-cols-[minmax(0,1.4fr)_minmax(0,22rem)]"><Panel title={ct('timeline')} icon={<Phone className="h-5 w-5" />}>{data.timeline.length ? <ol className="space-y-5">{data.timeline.map(item => <li key={item.id} className="relative border-s-2 border-mis-border ps-5"><span className="absolute -start-[7px] top-1.5 h-3 w-3 rounded-full bg-mis-blue" /><div className="flex flex-wrap items-start justify-between gap-2"><p className="font-bold text-mis-navy">{item.type}</p><time className="text-xs text-slate-500" data-bidi="ltr">{f.dateTime(item.createdAt)}</time></div>{item.result && <p className="mt-1 text-sm font-semibold text-mis-primary">{item.result}</p>}{item.notes && <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-slate-600">{item.notes}</p>}<p className="mt-2 text-xs text-slate-400">{item.createdBy}</p></li>)}</ol> : <p className="text-sm text-slate-500">{ct('noActivity')}</p>}</Panel><Panel title={action === 'activity' ? ct('addActivity') : action === 'promise' ? ct('addPromise') : ct('submitCollection')} icon={action === 'activity' ? <MessageSquarePlus className="h-5 w-5" /> : <FileCheck2 className="h-5 w-5" />}><div className="mb-5 grid grid-cols-3 rounded-xl bg-slate-100 p-1"><ActionTab active={action === 'activity'} onClick={() => setAction('activity')}>{ct('addActivity')}</ActionTab><ActionTab active={action === 'promise'} onClick={() => setAction('promise')}>{ct('addPromise')}</ActionTab><ActionTab active={action === 'payment'} onClick={() => setAction('payment')}>{ct('submitCollection')}</ActionTab></div><ActionForm type={action} caseId={id} onSaved={async () => { setMessage(ct('saved')); setData(await collectionsService.caseDetails(id)); }} setSaving={setSaving} saving={saving} /></Panel></section>
    <section className="mt-6"><Panel title={ct('attachments')} icon={<Paperclip className="h-5 w-5" />}><AttachmentForm caseId={id} payments={data.payments} saving={saving} setSaving={setSaving} onSaved={async () => setAttachments(await collectionsService.attachments(id))} />{attachments.length ? <div className="mt-5 divide-y divide-mis-border rounded-xl border border-mis-border">{attachments.map(file => <div key={file.id} className="flex items-center gap-4 px-4 py-3"><Paperclip className="h-5 w-5 text-mis-primary" /><div className="min-w-0 flex-1"><p className="truncate font-semibold text-mis-navy">{file.originalFileName}</p><p className="mt-1 text-xs text-slate-500">{ct(file.category)} · {(file.fileSize / 1024).toFixed(1)} KB · {file.uploadedBy}</p></div><button type="button" onClick={() => collectionsService.downloadAttachment(file.id, file.originalFileName)} aria-label={ct('downloadErrors')} title={ct('view')} className="rounded-lg p-2 text-mis-primary hover:bg-mis-pale"><Download className="h-5 w-5" /></button><button type="button" onClick={() => setAttachmentToDelete(file)} aria-label={ct('deleteAttachment')} title={ct('deleteAttachment')} className="rounded-lg p-2 text-rose-600 hover:bg-rose-50"><Trash2 className="h-5 w-5" /></button></div>)}</div> : <p className="mt-5 text-sm text-slate-500">{ct('noAttachments')}</p>}</Panel></section>
    {manageAllowed ? <section className="mt-6"><DataEntryDocumentsPanel canDownload documents={entryDocs} /></section> : null}
    {attachmentToDelete && <ConfirmDialog open title={ct('deleteAttachment')} message={ct('deleteAttachmentConfirm')} description={attachmentToDelete.originalFileName} confirmLabel={ct('delete')} cancelLabel={ct('cancel')} isConfirming={deleting} onCancel={() => setAttachmentToDelete(undefined)} onConfirm={() => void confirmDeleteAttachment()} />}
    {archiveOpen && <ConfirmDialog open title={ct('archiveCase')} message={ct('archiveCaseConfirm')} description={`${data.customerName} · ${data.caseNumber}`} confirmLabel={ct('archiveCase')} cancelLabel={ct('cancel')} isConfirming={archiving} onCancel={() => setArchiveOpen(false)} onConfirm={() => void confirmArchive()} />}
    {transferOpen ? <TransferCaseModal details={data} onClose={() => setTransferOpen(false)} onTransferred={async () => { setTransferOpen(false); setMessage(ct('transferComplete')); setData(await collectionsService.caseDetails(id)); }} /> : null}
  </div>;
}

function ActionForm({ type, caseId, onSaved, saving, setSaving }: { type: Action; caseId: string; onSaved: () => Promise<void>; saving: boolean; setSaving: (v: boolean) => void }) {
  const { ct } = useCollectionsLocalization(); const [error, setError] = useState(''); const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); setSaving(true); setError(''); const form = new FormData(event.currentTarget); try { if (type === 'activity') await collectionsService.createActivity(caseId, { activityType: String(form.get('activityType')), result: String(form.get('result') || ''), notes: String(form.get('notes') || ''), channel: String(form.get('channel') || ''), nextFollowUpAt: String(form.get('nextFollowUpAt') || '') || undefined }); else if (type === 'promise') await collectionsService.createPromise(caseId, { promisedAmount: Number(form.get('amount')), promiseDate: String(form.get('date')), channel: String(form.get('channel')), notes: String(form.get('notes') || '') }); else await collectionsService.submitPayment(caseId, { amount: Number(form.get('amount')), paymentDate: String(form.get('date')), method: String(form.get('method')), referenceNumber: String(form.get('reference')) }); event.currentTarget.reset(); await onSaved(); } catch { setError(ct('saveError')); } finally { setSaving(false); } };
  return <form className="space-y-4" onSubmit={submit}>{type === 'activity' ? <><Field label={ct('activityType')}><ProfessionalSelect name="activityType" required className="field"><option value="CALL">{ct('CALL')}</option><option value="SMS">{ct('SMS')}</option><option value="EMAIL">{ct('EMAIL')}</option><option value="NOTE">{ct('NOTE')}</option></ProfessionalSelect></Field><Field label={ct('result')}><input name="result" maxLength={100} className="field" /></Field><Field label={ct('channel')}><input name="channel" maxLength={40} className="field" /></Field><Field label={ct('nextFollowUp')}><ProfessionalDateInput name="nextFollowUpAt" mode="datetime" /></Field></> : type === 'promise' ? <><Field label={ct('amount')}><input name="amount" type="number" min="0.01" step="0.01" required className="field" /></Field><Field label={ct('promiseDate')}><ProfessionalDateInput name="date" required /></Field><Field label={ct('channel')}><ProfessionalSelect name="channel" required className="field"><option value="CALL">{ct('CALL')}</option><option value="VISIT">{ct('VISIT')}</option><option value="WHATSAPP">{ct('WHATSAPP')}</option></ProfessionalSelect></Field></> : <><Field label={ct('amount')}><input name="amount" type="number" min="0.01" step="0.01" required className="field" /></Field><Field label={ct('paymentDate')}><ProfessionalDateInput name="date" required /></Field><Field label={ct('method')}><ProfessionalSelect name="method" required className="field"><option value="CASH">{ct('CASH')}</option><option value="BANK_TRANSFER">{ct('BANK_TRANSFER')}</option><option value="CARD">{ct('CARD')}</option></ProfessionalSelect></Field><Field label={ct('reference')}><input name="reference" required maxLength={160} className="field" /></Field></>} {type !== 'payment' && <Field label={ct('notes')}><textarea name="notes" rows={3} maxLength={2000} className="field" /></Field>}{error && <p className="text-sm text-rose-700">{error}</p>}<button disabled={saving} className="w-full rounded-xl bg-mis-primary px-4 py-3 font-bold text-white hover:bg-mis-deep disabled:opacity-50">{saving ? ct('loading') : type === 'activity' ? ct('saveActivity') : type === 'promise' ? ct('submitPromise') : ct('submit')}</button></form>;
}
function Panel({ title, icon, children }: { title: string; icon: React.ReactNode; children: React.ReactNode }) { return <article className="min-w-0 overflow-hidden rounded-2xl border border-mis-border bg-white p-5 shadow-sm sm:p-6"><h2 className="mb-5 flex items-center gap-2 text-lg font-bold text-mis-navy"><span className="shrink-0">{icon}</span><span className="min-w-0 [overflow-wrap:anywhere]">{title}</span></h2>{children}</article>; }
function CollectorStatus({ details }: { details: CaseDetails }) {
  const { ct } = useCollectionsLocalization();
  const name = details.assignedCollectorName || collectorLabel(details.fileCollectorUserName, details.fileCollectorName);
  if (!name || name === '—') return '—';
  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      <span>{name}</span>
      {!details.assignedCollectorId ? <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-amber-800">{ct('unassignedFileCollector')}</span> : null}
    </span>
  );
}

function TransferCaseModal({ details, onClose, onTransferred }: { details: CaseDetails; onClose: () => void; onTransferred: () => Promise<void> }) {
  const { ct } = useCollectionsLocalization();
  const format = useCollectionFormat();
  const toast = useToast();
  const [collectors, setCollectors] = useState<CollectorLookup[]>([]);
  const [collectorId, setCollectorId] = useState('');
  const [reason, setReason] = useState(ct('transferReasonDefault'));
  const [preview, setPreview] = useState<AssignmentPreview>();
  const [saving, setSaving] = useState(false);
  const currentName = details.assignedCollectorName || collectorLabel(details.fileCollectorUserName, details.fileCollectorName);
  const chosen = collectors.find((item) => item.id === collectorId);

  useEffect(() => {
    let active = true;
    void collectionsService.collectors()
      .then((rows) => { if (active) setCollectors(rows); })
      .catch((failure) => { if (active) toast.error(getApiErrorMessage(failure, ct('saveError'))); });
    return () => { active = false; };
    // Collectors are loaded once when the transfer dialog opens.
  }, [ct, toast]);

  async function continueTransfer() {
    if (!collectorId || reason.trim().length < 2) return;
    if (collectorId === details.assignedCollectorId) { toast.error(ct('sameCollector')); return; }
    setSaving(true);
    try {
      const payload = { caseIds: [details.id], collectorId, teamId: chosen?.teamId, reason: reason.trim(), confirmed: Boolean(preview) };
      if (!preview) { setPreview(await collectionsService.previewAssignment({ ...payload, confirmed: false })); return; }
      await collectionsService.assign({ ...payload, confirmed: true });
      await onTransferred();
    } catch (failure) {
      toast.error(getApiErrorMessage(failure, ct('saveError')));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      footer={(
        <>
          <Button disabled={saving} fullWidth={false} onClick={() => preview ? setPreview(undefined) : onClose()} variant="outline">{ct('back')}</Button>
          <Button disabled={!collectorId || reason.trim().length < 2 || collectorId === details.assignedCollectorId} fullWidth={false} isLoading={saving} onClick={() => void continueTransfer()}>
            {preview ? ct('confirmTransfer') : ct('previewAssignment')}
          </Button>
        </>
      )}
      onClose={() => { if (!saving) onClose(); }}
      open
      size="md"
      title={details.assignedCollectorId ? ct('transferCase') : ct('assignCollector')}
    >
      <div className="space-y-4">
        <p className="text-sm leading-6 text-slate-600">{ct('transferCaseHelp')}</p>
        <div className="rounded-xl border border-mis-border bg-slate-50 p-4">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">{details.caseNumber}</p>
          <p className="mt-1 font-bold text-mis-navy">{details.customerName}</p>
          <p className="mt-1 text-sm text-slate-500">{details.clientName}</p>
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="rounded-xl border border-mis-border p-4">
            <p className="text-xs font-semibold text-slate-500">{ct('transferFrom')}</p>
            <p className="mt-1 font-bold text-mis-navy">{currentName}</p>
            {!details.assignedCollectorId ? <p className="mt-1 text-xs font-semibold text-amber-700">{ct('unassignedFileCollector')}</p> : null}
          </div>
          <div className="rounded-xl border border-mis-border p-4">
            <p className="text-xs font-semibold text-slate-500">{ct('transferTo')}</p>
            <p className="mt-1 font-bold text-mis-navy">{chosen?.name || '—'}</p>
            {chosen ? <p className="mt-1 text-xs text-slate-500">{format.number(chosen.activeWorkload)} · {chosen.teamName || ct('collectorTeam')}</p> : null}
          </div>
        </div>
        {preview ? (
          <div>
            <p className="mb-2 text-sm font-semibold text-slate-600">{ct('transferPreview')}</p>
            {preview.collectors.map((item) => (
              <div className="rounded-xl bg-slate-50 p-4" key={item.collectorId}>
                <p className="font-bold text-mis-navy">{item.collectorName}</p>
                <div className="mt-3 grid grid-cols-3 gap-3 text-sm">
                  <div><p className="text-slate-500">{ct('currentWorkload')}</p><p className="mt-1 text-lg font-bold">{format.number(item.currentWorkload)}</p></div>
                  <div><p className="text-slate-500">{ct('proposedCases')}</p><p className="mt-1 text-lg font-bold">{format.number(item.proposedAdditionalCases)}</p></div>
                  <div><p className="text-slate-500">{ct('resultingWorkload')}</p><p className="mt-1 text-lg font-bold text-mis-primary">{format.number(item.resultingWorkload)}</p></div>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <>
            <label className="block text-sm font-semibold">{ct('selectTransferCollector')}
              <ProfessionalSelect className="field mt-2" onChange={(event) => { setCollectorId(event.target.value); setPreview(undefined); }} value={collectorId}>
                <option value="">{ct('selectPortfolioCollector')}</option>
                {collectors.filter((item) => item.id !== details.assignedCollectorId).map((item) => (
                  <option key={item.id} value={item.id}>{item.name} — {format.number(item.activeWorkload)}</option>
                ))}
              </ProfessionalSelect>
            </label>
            <label className="block text-sm font-semibold">{ct('transferReason')}
              <textarea className="field mt-2" maxLength={500} onChange={(event) => { setReason(event.target.value); setPreview(undefined); }} rows={3} value={reason} />
            </label>
          </>
        )}
      </div>
    </Modal>
  );
}

function collectorLabel(systemName?: string, fileName?: string) {
  if (systemName && fileName && systemName !== fileName) return `${systemName} (${fileName})`;
  return systemName || fileName || '—';
}
function Info({ label, value, wide = false, bidi = false }: { label: string; value: React.ReactNode; wide?: boolean; bidi?: boolean }) { return <div className={wide ? 'sm:col-span-2' : ''}><dt className="text-xs font-semibold text-slate-500">{label}</dt><dd className="mt-1 break-words font-semibold text-mis-navy" {...(bidi ? { 'data-bidi': 'ltr' } : {})}>{value}</dd></div>; }
function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="block"><span className="mb-1.5 block text-sm font-semibold text-slate-600">{label}</span>{children}</label>; }
function ActionTab({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) { return <button type="button" onClick={onClick} className={`min-w-0 rounded-lg px-1 py-2 text-[11px] font-bold leading-4 sm:text-xs ${active ? 'bg-white text-mis-primary shadow-sm' : 'text-slate-500'}`}>{children}</button>; }
function AttachmentForm({ caseId, payments, saving, setSaving, onSaved }: { caseId: string; payments: CaseDetails['payments']; saving: boolean; setSaving: (value: boolean) => void; onSaved: () => Promise<void> }) { const { ct } = useCollectionsLocalization(); const [category, setCategory] = useState('CASE_DOCUMENT'); const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); const formElement = event.currentTarget; const form = new FormData(formElement); const file = form.get('file'); if (!(file instanceof File) || !file.size) return; setSaving(true); try { await collectionsService.uploadAttachment(caseId, category, file, category === 'PAYMENT_PROOF' ? String(form.get('paymentId')) : undefined); formElement.reset(); setCategory('CASE_DOCUMENT'); await onSaved(); } finally { setSaving(false); } }; return <form onSubmit={submit} className="grid min-w-0 items-end gap-3 md:grid-cols-2 xl:grid-cols-[minmax(0,14rem)_minmax(0,1fr)_auto]"><label className="text-sm font-semibold text-slate-700">{ct('category')}<ProfessionalSelect name="category" value={category} onChange={event => setCategory(event.target.value)} className="field mt-2"><option value="CASE_DOCUMENT">{ct('CASE_DOCUMENT')}</option>{payments.length > 0 && <option value="PAYMENT_PROOF">{ct('PAYMENT_PROOF')}</option>}<option value="VISIT_EVIDENCE">{ct('VISIT_EVIDENCE')}</option><option value="COMPLAINT_DOCUMENT">{ct('COMPLAINT_DOCUMENT')}</option><option value="SETTLEMENT_DOCUMENT">{ct('SETTLEMENT_DOCUMENT')}</option></ProfessionalSelect></label>{category === 'PAYMENT_PROOF' && <label className="text-sm font-semibold text-slate-700">{ct('relatedPayment')}<ProfessionalSelect required name="paymentId" aria-label={ct('relatedPayment')} className="field mt-2"><option value="">{ct('relatedPayment')}</option>{payments.map(payment => <option key={payment.id} value={payment.id}>{payment.referenceNumber} · {payment.amount}</option>)}</ProfessionalSelect></label>}<FileInput required name="file" accept=".pdf,.jpg,.jpeg,.png" label={ct('file')} /><Button disabled={saving} fullWidth={false} size="md" type="submit">{ct('uploadAttachment')}</Button></form>; }
