import { ArrowLeft, Landmark, Scale } from 'lucide-react';
import { type FormEvent, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { TextInput } from '../../components/forms/TextInput';
import { useAuth } from '../../context/AuthContext';
import { LegalPipeline, LegalStageBadge, useLegalText } from '../../features/legal/legalUi';
import { legalService } from '../../features/legal/services/legalService';
import type { LegalCaseDetails } from '../../features/legal/types/legal';
import { canAccessModule } from '../../features/modules/moduleAccess';
import { getApiErrorMessage } from '../../services/apiClient';

const OUTCOME_ACTIONS = new Set(['JUDGMENT', 'SETTLEMENT', 'RETURN']);

export function LegalCaseDetailsPage() {
  const { id = '' } = useParams();
  const l = useLegalText();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [data, setData] = useState<LegalCaseDetails>();
  const [error, setError] = useState(false);
  const [message, setMessage] = useState('');
  const [savingFile, setSavingFile] = useState(false);
  const [savingAction, setSavingAction] = useState(false);
  const [pendingAction, setPendingAction] = useState<{ actionType: string; notes: string; result: string; happenedOn: string }>();
  const canCollections = user ? canAccessModule(user, 'collections') : false;

  function load() {
    if (!id) return;
    setError(false);
    legalService.case(id).then(setData).catch(() => setError(true));
  }

  useEffect(() => { load(); }, [id]);

  if (error) {
    return <ErrorState title={l.text('تعذر تحميل الملف القانوني', 'Could not load the legal file')} onRetry={load} />;
  }
  if (!data) {
    return <div className="grid min-h-[440px] place-items-center"><LoadingSpinner /></div>;
  }

  const canManage = data.canManage;
  const inQueue = data.collectionStatus === 'LEGAL';

  async function saveFile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canManage) return;
    const form = new FormData(event.currentTarget);
    setSavingFile(true);
    setMessage('');
    try {
      const next = await legalService.saveFile(id, {
        courtName: String(form.get('courtName') || ''),
        courtCaseNumber: String(form.get('courtCaseNumber') || ''),
        lawyerName: String(form.get('lawyerName') || ''),
        nextHearingOn: String(form.get('nextHearingOn') || '') || null,
        notes: String(form.get('notes') || ''),
      });
      setData(next);
      setMessage(l.text('تم حفظ الملف القانوني.', 'Legal file saved.'));
    } catch (err) {
      setMessage(getApiErrorMessage(err, l.text('تعذر حفظ الملف.', 'Could not save the file.')));
    } finally {
      setSavingFile(false);
    }
  }

  async function submitAction(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canManage) return;
    const form = new FormData(event.currentTarget);
    const payload = {
      actionType: String(form.get('actionType') || 'NOTE'),
      notes: String(form.get('notes') || ''),
      result: String(form.get('result') || ''),
      happenedOn: String(form.get('happenedOn') || ''),
    };
    if (OUTCOME_ACTIONS.has(payload.actionType)) {
      setPendingAction(payload);
      return;
    }
    await recordAction(payload, event.currentTarget);
  }

  async function recordAction(payload: { actionType: string; notes: string; result: string; happenedOn: string }, form?: HTMLFormElement) {
    setSavingAction(true);
    setMessage('');
    try {
      const next = await legalService.recordAction(id, {
        actionType: payload.actionType,
        notes: payload.notes,
        result: payload.result || undefined,
        happenedOn: payload.happenedOn || null,
      });
      setData(next);
      form?.reset();
      setMessage(l.text('تم تسجيل الإجراء.', 'Action recorded.'));
    } catch (err) {
      setMessage(getApiErrorMessage(err, l.text('تعذر تسجيل الإجراء.', 'Could not record the action.')));
    } finally {
      setSavingAction(false);
      setPendingAction(undefined);
    }
  }

  const outcomeHint = pendingAction?.actionType === 'RETURN'
    ? l.text('ستُعاد الحالة إلى التحصيل بحالة ACTIVE.', 'The case will return to collections as ACTIVE.')
    : pendingAction?.actionType === 'SETTLEMENT'
      ? l.text('ستُغلق الحالة في التحصيل بحالة SETTLED.', 'The collection case will be marked SETTLED.')
      : l.text('ستُغلق الحالة في التحصيل بحالة CLOSED.', 'The collection case will be marked CLOSED.');

  return (
    <div className="space-y-5">
      <PageHeader
        breadcrumbs={
          <Link className="inline-flex items-center gap-2 text-sm font-semibold text-mis-primary" to="/legal/cases">
            <ArrowLeft className="h-4 w-4 rtl:rotate-180" />
            {l.text('العودة للقضايا', 'Back to cases')}
          </Link>
        }
        eyebrow={`${data.organizationName} · ${data.portfolioName}`}
        title={`${l.text('الملف القانوني', 'Legal file')} — ${data.caseNumber}`}
        description={<span>{data.customerName} · <span data-bidi="ltr">{data.accountReference}</span></span>}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <LegalStageBadge value={data.legalStage} />
            {canCollections ? (
              <Button fullWidth={false} leftIcon={<Landmark className="h-4 w-4" />} size="md" type="button" variant="outline" onClick={() => navigate(`/collections/cases/${data.collectionCaseId}`)}>
                {l.text('حالة التحصيل', 'Collections case')}
              </Button>
            ) : null}
          </div>
        }
      />

      {message ? (
        <div className={`rounded-xl border px-4 py-3 text-sm ${message.includes('تعذر') || message.toLowerCase().includes('could not') || message.toLowerCase().includes('unable') ? 'border-rose-200 bg-rose-50 text-rose-800' : 'border-emerald-200 bg-emerald-50 text-emerald-800'}`}>
          {message}
        </div>
      ) : null}

      {!inQueue ? (
        <div className="rounded-2xl border border-slate-200 bg-slate-50 px-5 py-4 text-sm text-slate-700">
          {l.text('هذه القضية خرجت من طابور LEGAL. الحالة الحالية في التحصيل: ', 'This file left the LEGAL queue. Current collections status: ')}
          <strong>{l.collectionStatus(data.collectionStatus)}</strong>
        </div>
      ) : null}

      <LegalPipeline current={data.legalStage} />

      <section className="grid gap-5 xl:grid-cols-2">
        <article className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
          <h2 className="mb-4 flex items-center gap-2 text-lg font-bold text-mis-navy"><Scale className="h-5 w-5" />{l.text('بيانات القضية', 'Case facts')}</h2>
          <dl className="grid gap-3 sm:grid-cols-2">
            <Info label={l.text('العميل', 'Customer')} value={data.customerName} />
            <Info label={l.text('الرقم القومي', 'National ID')} value={data.nationalId || '—'} bidi />
            <Info label={l.text('الموبايل', 'Mobile')} value={data.primaryPhone || '—'} bidi />
            <Info label={l.text('المبلغ الأصلي', 'Original amount')} value={l.money(data.originalAmount)} bidi />
            <Info label={l.text('المديونية', 'Outstanding')} value={l.money(data.outstandingBalance)} bidi />
            <Info label={l.text('أيام التأخر', 'Days past due')} value={l.number(data.daysPastDue)} bidi />
            <Info label={l.text('استُلم بواسطة', 'Received by')} value={data.receivedByName} />
            <Info label={l.text('تاريخ الاستلام', 'Received at')} value={l.dateTime(data.receivedAt)} />
          </dl>
        </article>

        <article className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
          <h2 className="mb-4 text-lg font-bold text-mis-navy">{l.text('ملف المحكمة', 'Court file')}</h2>
          <form className="grid gap-4 sm:grid-cols-2" key={data.updatedAt} onSubmit={(event) => void saveFile(event)}>
            <TextInput defaultValue={data.courtName ?? ''} disabled={!canManage} label={l.text('المحكمة', 'Court')} name="courtName" maxLength={160} />
            <TextInput defaultValue={data.courtCaseNumber ?? ''} disabled={!canManage} label={l.text('رقم الدعوى', 'Court case number')} name="courtCaseNumber" maxLength={80} />
            <TextInput defaultValue={data.lawyerName ?? ''} disabled={!canManage} label={l.text('المحامي', 'Lawyer')} name="lawyerName" maxLength={160} />
            <TextInput defaultValue={data.nextHearingOn ?? ''} disabled={!canManage} label={l.text('الجلسة القادمة', 'Next hearing')} name="nextHearingOn" type="date" />
            <TextAreaInput containerClassName="sm:col-span-2" defaultValue={data.notes ?? ''} disabled={!canManage} label={l.text('ملاحظات الملف', 'File notes')} name="notes" maxLength={2000} />
            {canManage ? (
              <div className="sm:col-span-2">
                <Button fullWidth={false} isLoading={savingFile} type="submit">{l.text('حفظ الملف', 'Save file')}</Button>
              </div>
            ) : null}
          </form>
        </article>
      </section>

      <section className="grid gap-5 xl:grid-cols-[minmax(0,1.4fr)_minmax(0,22rem)]">
        <article className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
          <h2 className="mb-5 text-lg font-bold text-mis-navy">{l.text('الخط الزمني القانوني', 'Legal timeline')}</h2>
          {data.actions.length ? (
            <ol className="space-y-5">
              {data.actions.map((item) => (
                <li key={item.id} className="relative border-s-2 border-mis-border ps-5">
                  <span className="absolute -start-[7px] top-1.5 h-3 w-3 rounded-full bg-amber-700" />
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <p className="font-bold text-mis-navy">{l.action(item.actionType)}</p>
                    <time className="text-xs text-slate-500" data-bidi="ltr">{l.dateTime(item.createdAt)}</time>
                  </div>
                  {item.result ? <p className="mt-1 text-sm font-semibold text-amber-800">{item.result}</p> : null}
                  {item.happenedOn ? <p className="mt-1 text-xs text-slate-500">{l.text('تاريخ الواقعة', 'Event date')}: {l.date(item.happenedOn)}</p> : null}
                  <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-slate-600">{item.notes}</p>
                  <p className="mt-2 text-xs text-slate-400">{item.createdByName}</p>
                </li>
              ))}
            </ol>
          ) : (
            <p className="text-sm text-slate-500">{l.text('لا توجد إجراءات قانونية بعد.', 'No legal actions recorded yet.')}</p>
          )}
        </article>

        {canManage && inQueue ? (
          <article className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
            <h2 className="mb-4 text-lg font-bold text-mis-navy">{l.text('تسجيل إجراء', 'Record action')}</h2>
            <form className="space-y-4" onSubmit={(event) => void submitAction(event)}>
              <label className="block">
                <span className="mb-1.5 block text-sm font-semibold text-slate-600">{l.text('نوع الإجراء', 'Action')}</span>
                <ProfessionalSelect name="actionType" required>
                  {l.actions.map((item) => <option key={item} value={item}>{l.action(item)}</option>)}
                </ProfessionalSelect>
              </label>
              <TextInput label={l.text('النتيجة', 'Result')} name="result" maxLength={120} />
              <TextInput label={l.text('تاريخ الواقعة', 'Event date')} name="happenedOn" type="date" />
              <TextAreaInput label={l.text('الملاحظات', 'Notes')} name="notes" maxLength={2000} required />
              <p className="text-xs leading-5 text-slate-500">
                {l.text('الحكم يغلق الحالة، التسوية تسويها، والإعادة ترجعها للتحصيل نشطة.', 'Judgment closes the case, settlement marks it settled, and return sends it back to collections as active.')}
              </p>
              <Button isLoading={savingAction} type="submit">{l.text('حفظ الإجراء', 'Save action')}</Button>
            </form>
          </article>
        ) : null}
      </section>

      {pendingAction ? (
        <ConfirmDialog
          open
          title={l.action(pendingAction.actionType)}
          message={outcomeHint}
          description={`${data.customerName} · ${data.caseNumber}`}
          confirmLabel={l.text('تأكيد', 'Confirm')}
          cancelLabel={l.text('إلغاء', 'Cancel')}
          isConfirming={savingAction}
          onCancel={() => setPendingAction(undefined)}
          onConfirm={() => void recordAction(pendingAction)}
        />
      ) : null}
    </div>
  );
}

function Info({ label, value, bidi = false }: { label: string; value: string; bidi?: boolean }) {
  return (
    <div>
      <dt className="text-xs font-semibold text-slate-500">{label}</dt>
      <dd className="mt-1 break-words font-semibold text-mis-navy" {...(bidi ? { 'data-bidi': 'ltr' } : {})}>{value}</dd>
    </div>
  );
}
