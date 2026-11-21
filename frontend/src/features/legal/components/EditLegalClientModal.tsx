import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '../../../components/common/Button';
import { ErrorState } from '../../../components/common/ErrorState';
import { LoadingSpinner } from '../../../components/common/LoadingSpinner';
import { Modal } from '../../../components/common/Modal';
import { useToast } from '../../../components/common/Toast';
import { ProfessionalSelect } from '../../../components/forms/ProfessionalSelect';
import { TextAreaInput } from '../../../components/forms/TextAreaInput';
import { legalClientError } from '../legalClientError';
import { useLegalText } from '../legalUi';
import { legalService } from '../services/legalService';
import type { LegalClientDetails } from '../types/legal';

export function EditLegalClientModal({ customerId, onClose, onSaved }: {
  customerId: string; onClose: () => void; onSaved: () => void;
}) {
  const l = useLegalText();
  const toast = useToast();
  const [details, setDetails] = useState<LegalClientDetails>();
  const [status, setStatus] = useState('');
  const [notes, setNotes] = useState('');
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<{ reason: unknown }>();
  const [reloadKey, setReloadKey] = useState(0);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    setLoading(true);
    setLoadError(undefined);
    setError('');
    setDetails(undefined);
    legalService.client(customerId, true).then((client) => {
      if (!active) return;
      setDetails(client);
      setStatus(client.client.status);
      setNotes(client.notes ?? '');
    }).catch((reason: unknown) => { if (active) setLoadError({ reason }); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [customerId, reloadKey]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (saving || loading || loadError || !details?.canManage || details.client.customerId.toLowerCase() !== customerId.toLowerCase()) return;
    setSaving(true);
    setError('');
    try {
      await legalService.updateClient(customerId, { status, notes }, true);
      toast.success(l.text('تم حفظ بيانات الملف.', 'Client file saved.'));
      onSaved();
    } catch (reason) {
      setError(legalClientError(reason, l.text, true));
    } finally {
      setSaving(false);
    }
  }

  return <Modal open title={l.text('تعديل العميل', 'Edit client')} onClose={onClose}
    closeOnBackdrop={!saving} closeOnEscape={!saving} hideCloseButton={saving}
    footer={<>
      <Button disabled={saving} fullWidth={false} variant="outline" onClick={onClose}>{l.text('إلغاء', 'Cancel')}</Button>
      <Button disabled={loading || Boolean(loadError) || !details?.canManage || saving} isLoading={saving} fullWidth={false} form="edit-legal-client" type="submit">{l.text('حفظ الملف', 'Save Client File')}</Button>
    </>}>
    {loading ? <LoadingSpinner /> : loadError ? (
      <ErrorState title={l.text('تعذر تحميل ملف العميل', 'Could not load the client file')} message={legalClientError(loadError.reason, l.text)} onRetry={() => setReloadKey((value) => value + 1)} />
    ) : details ? <form id="edit-legal-client" className="space-y-4" onSubmit={submit}>
      <p className="font-semibold text-mis-navy">{details.client.name}</p>
      <p className="text-sm text-slate-500">{l.text('الحالة وملاحظات الشؤون القانونية.', 'Legal status and notes.')}</p>
      {!details.canManage ? <p role="status">{l.text('ليس لديك صلاحية تعديل بيانات هذا العميل.', 'You do not have permission to edit this client.')}</p> : null}
      {error ? <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p> : null}
      <label className="block text-sm font-semibold text-slate-700">
        <span className="mb-1.5 block">{l.text('حالة العميل', 'Client Status')}</span>
        <ProfessionalSelect disabled={!details.canManage || saving} className="h-11 w-full rounded-xl border border-mis-control bg-white px-3" value={status} onChange={(event) => setStatus(event.target.value)}>
          {['ACTIVE', 'LEGAL', 'ON_HOLD', 'CLOSED'].map((value) => <option key={value} value={value}>{l.clientStatus(value)}</option>)}
        </ProfessionalSelect>
      </label>
      <TextAreaInput disabled={!details.canManage || saving} maxLength={2000} label={l.text('ملاحظات الشؤون القانونية', 'Legal Notes')} value={notes} onChange={(event) => setNotes(event.target.value)} />
    </form> : null}
  </Modal>;
}
