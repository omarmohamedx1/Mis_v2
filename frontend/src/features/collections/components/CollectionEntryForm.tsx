import { useRef, useState, type FormEvent, type ReactNode } from 'react';
import { Button } from '../../../components/common/Button';
import { ConfirmDialog } from '../../../components/common/ConfirmDialog';
import { ProfessionalDateInput } from '../../../components/forms/ProfessionalDateInput';
import { ProfessionalSelect } from '../../../components/forms/ProfessionalSelect';
import { useToast } from '../../../components/common/Toast';
import { getApiErrorMessage } from '../../../services/apiClient';
import { useCollectionsLocalization } from '../localization/collectionsTranslations';
import { collectionsService } from '../services/collectionsService';
import type { Activity, PromiseItem } from '../types/collections';
import { useCollectionFormat } from './CollectionsUi';

export type CollectionAction = 'activity' | 'promise' | 'payment';
interface Props {
  type: CollectionAction;
  caseId: string;
  context?: string;
  activity?: Activity;
  promise?: PromiseItem;
  saving: boolean;
  setSaving: (value: boolean) => void;
  onSaved: () => Promise<void>;
}

function localDateTime(value?: string) {
  if (!value) return '';
  const date = new Date(value);
  const pad = (number: number) => String(number).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function CollectionEntryForm({ type, caseId, context, activity, promise, saving, setSaving, onSaved }: Props) {
  const { ct, language } = useCollectionsLocalization();
  const format = useCollectionFormat();
  const toast = useToast();
  const [error, setError] = useState('');
  const [pending, setPending] = useState<{ values: FormData; element: HTMLFormElement }>();
  const submitting = useRef(false);
  const editing = Boolean(activity || promise);
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Cairo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());

  async function save(values: FormData, element: HTMLFormElement) {
    if (submitting.current) return;
    submitting.current = true;
    setSaving(true);
    setError('');
    const text = (name: string) => String(values.get(name) || '').trim();
    try {
      if (type === 'activity') {
        const followUp = text('nextFollowUpAt');
        const payload = { activityType: text('activityType'), result: text('result'), notes: text('notes'), channel: text('channel'), nextFollowUpAt: followUp ? new Date(followUp).toISOString() : undefined };
        if (activity) await collectionsService.updateActivity(caseId, activity.id, payload);
        else await collectionsService.createActivity(caseId, payload);
      } else if (type === 'promise') {
        const payload = { promisedAmount: Number(text('amount')), promiseDate: text('date'), channel: text('channel'), notes: text('notes') };
        if (promise) await collectionsService.updatePromise(promise.id, payload);
        else await collectionsService.createPromise(caseId, payload);
      } else {
        await collectionsService.submitPayment(caseId, { amount: Number(text('amount')), paymentDate: text('date'), method: text('method'), referenceNumber: text('reference'), currencyCode: 'EGP' });
      }
      setPending(undefined);
      if (!editing) element.reset();
      toast.success(ct('saved'));
      try { await onSaved(); }
      catch (failure) { toast.error(getApiErrorMessage(failure, ct('loadError'))); }
    } catch (failure) {
      const message = getApiErrorMessage(failure, ct('saveError'));
      setError(message);
      setPending(undefined);
      toast.error(message);
    } finally {
      submitting.current = false;
      setSaving(false);
    }
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving || submitting.current) return;
    const element = event.currentTarget;
    const values = new FormData(element);
    setError('');
    if (type !== 'activity') {
      const date = String(values.get('date') || '');
      if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || (type === 'promise' && date < today) || (type === 'payment' && date > today)) {
        setError(ct(type === 'promise' ? 'promiseDateValidation' : 'paymentDateValidation'));
        return;
      }
      const amount = Number(values.get('amount'));
      if (!Number.isFinite(amount) || amount <= 0) { setError(ct('amountValidation')); return; }
      if (type === 'payment' && !String(values.get('reference') || '').trim()) { setError(ct('referenceValidation')); return; }
      setPending({ values, element });
      return;
    }
    void save(values, element);
  }

  const review = pending?.values;
  return <>
    <form className="space-y-4" onSubmit={submit}>
      <fieldset className="space-y-4" disabled={saving}>
        {type === 'activity' ? <>
          <EntryField label={ct('activityType')}><ProfessionalSelect name="activityType" required defaultValue={activity?.type ?? 'CALL'}>
            {['CALL', 'SMS', 'EMAIL', 'NOTE', 'FOLLOW_UP'].map(value => <option key={value} value={value}>{ct(value)}</option>)}
          </ProfessionalSelect></EntryField>
          <EntryField label={ct('result')}><input name="result" defaultValue={activity?.result ?? ''} maxLength={100} className="field" /></EntryField>
          <EntryField label={ct('channel')}><input name="channel" defaultValue={activity?.channel ?? ''} maxLength={40} className="field" /></EntryField>
          <EntryField label={ct('nextFollowUp')}><ProfessionalDateInput name="nextFollowUpAt" mode="datetime" defaultValue={localDateTime(activity?.nextFollowUpAt)} disabled={saving} /></EntryField>
        </> : <>
          <EntryField label={ct('amount')}><input name="amount" type="number" min="0.01" step="0.01" required defaultValue={promise?.promisedAmount ?? ''} className="field" /></EntryField>
          <EntryField label={ct(type === 'promise' ? 'promiseDate' : 'paymentDate')}><ProfessionalDateInput name="date" required defaultValue={promise?.promiseDate ?? today} min={type === 'promise' ? today : undefined} max={type === 'payment' ? today : undefined} disabled={saving} /></EntryField>
          {type === 'promise'
            ? <EntryField label={ct('channel')}><ProfessionalSelect name="channel" required defaultValue={promise?.channel ?? 'CALL'}>
                {[...new Set(['CALL', 'VISIT', 'WHATSAPP', ...(promise?.channel ? [promise.channel] : [])])].map(value => <option key={value} value={value}>{ct(value)}</option>)}
              </ProfessionalSelect></EntryField>
            : <>
              <EntryField label={ct('method')}><ProfessionalSelect name="method" required defaultValue="CASH">{['CASH', 'BANK_TRANSFER', 'CARD'].map(value => <option key={value} value={value}>{ct(value)}</option>)}</ProfessionalSelect></EntryField>
              <EntryField label={ct('reference')}><input name="reference" required maxLength={160} className="field" /></EntryField>
            </>}
        </>}
        {type !== 'payment' && <EntryField label={ct('notes')}><textarea name="notes" rows={3} maxLength={type === 'activity' ? 4000 : 2000} defaultValue={activity?.notes ?? promise?.notes ?? ''} className="field" /></EntryField>}
        {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
        <Button type="submit" isLoading={saving}>{editing ? ct('saveChanges') : ct(type === 'activity' ? 'saveActivity' : 'reviewBeforeSave')}</Button>
      </fieldset>
    </form>
    {pending && review && <ConfirmDialog open title={ct('confirmEntry')} description={context} message={<>
      <p className="mb-4 font-semibold">{ct('confirmEntryHelp')}</p>
      <dl className="space-y-3 rounded-xl border border-mis-border bg-slate-50 p-4">
        <ReviewRow label={ct('entryOperation')} value={ct(type === 'promise' ? 'addPromise' : 'submitCollection')} />
        <ReviewRow label={ct('amount')} value={<strong className="text-xl text-mis-primary" data-bidi="ltr">{new Intl.NumberFormat(language === 'ar' ? 'ar-EG' : 'en-EG', { style: 'currency', currency: 'EGP', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(review.get('amount')))}</strong>} />
        <ReviewRow label={ct(type === 'promise' ? 'promiseDate' : 'paymentDate')} value={format.date(String(review.get('date')))} />
        <ReviewRow label={ct(type === 'promise' ? 'channel' : 'method')} value={ct(String(review.get(type === 'promise' ? 'channel' : 'method')))} />
        {type === 'payment' && <ReviewRow label={ct('reference')} value={String(review.get('reference')).trim()} />}
        {type === 'promise' && Boolean(review.get('notes')) && <ReviewRow label={ct('notes')} value={String(review.get('notes'))} />}
      </dl>
    </>} confirmVariant="primary" confirmLabel={ct('confirmAndSave')} cancelLabel={ct('backToEdit')} isConfirming={saving} onCancel={() => { if (!submitting.current) setPending(undefined); }} onConfirm={() => void save(pending.values, pending.element)} />}
  </>;
}

function EntryField({ label, children }: { label: string; children: ReactNode }) {
  return <label className="block"><span className="mb-1.5 block text-sm font-semibold text-slate-600">{label}</span>{children}</label>;
}
function ReviewRow({ label, value }: { label: string; value: ReactNode }) {
  return <div className="flex flex-wrap items-center justify-between gap-2"><dt className="text-slate-500">{label}</dt><dd className="break-words font-semibold text-mis-navy">{value}</dd></div>;
}
