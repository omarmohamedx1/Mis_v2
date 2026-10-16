import { Pencil, Trash2 } from 'lucide-react';
import { useRef, useState } from 'react';
import { ConfirmDialog } from '../../../components/common/ConfirmDialog';
import { Modal } from '../../../components/common/Modal';
import { useToast } from '../../../components/common/Toast';
import { useAuth } from '../../../context/AuthContext';
import { getApiErrorMessage } from '../../../services/apiClient';
import { useCollectionsLocalization } from '../localization/collectionsTranslations';
import { collectionsService } from '../services/collectionsService';
import type { Activity, PromiseItem } from '../types/collections';
import { CollectionEntryForm } from './CollectionEntryForm';

type Props = { caseId: string; context: string; onSaved: () => Promise<void> } & ({ activity: Activity; promise?: never } | { promise: PromiseItem; activity?: never });
export function CollectionRecordActions({ activity, promise, caseId, context, onSaved }: Props) {
  const { user } = useAuth();
  const { ct } = useCollectionsLocalization();
  const toast = useToast();
  const [editing, setEditing] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [saving, setSaving] = useState(false);
  const submitting = useRef(false);
  const manager = user?.roles.some(role => ['Admin', 'CollectionsOperationsManager', 'CollectionsSupervisor'].includes(role)) || user?.permissions.includes('collections.assignment.manage');
  const operational = manager || user?.roles.includes('CollectionsCollector') || user?.permissions.some(permission => ['*', 'collections.activity.manage', 'collections.ptp.manage'].includes(permission));
  const ownerId = activity?.createdById ?? promise?.collectorId;
  const manual = !activity || ['CALL', 'SMS', 'EMAIL', 'NOTE', 'FOLLOW_UP'].includes(activity.type);
  if (!operational || (!manager && ownerId !== user?.id) || !manual || (promise && promise.actualPaidAmount > 0)) return null;

  async function remove() {
    if (submitting.current) return;
    submitting.current = true;
    setSaving(true);
    try {
      if (activity) await collectionsService.deleteActivity(caseId, activity.id);
      else if (promise) await collectionsService.deletePromise(promise.id);
      setDeleting(false);
      toast.success(ct('recordDeleted'));
      try { await onSaved(); } catch (failure) { toast.error(getApiErrorMessage(failure, ct('loadError'))); }
    } catch (failure) {
      toast.error(getApiErrorMessage(failure, ct('saveError')));
    } finally { submitting.current = false; setSaving(false); }
  }

  return <>
    <div className="mt-2 flex items-center gap-2">
      <button type="button" disabled={saving} onClick={() => setEditing(true)} className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-semibold text-mis-primary hover:bg-mis-pale"><Pencil className="h-3.5 w-3.5" />{ct('editRecord')}</button>
      <button type="button" disabled={saving} onClick={() => setDeleting(true)} className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-semibold text-rose-600 hover:bg-rose-50"><Trash2 className="h-3.5 w-3.5" />{ct('delete')}</button>
    </div>
    {editing && <Modal open title={ct(activity ? 'editActivity' : 'editPromise')} description={context} onClose={() => { if (!saving) setEditing(false); }} closeOnBackdrop={!saving} closeOnEscape={!saving} hideCloseButton={saving}>
      <CollectionEntryForm type={activity ? 'activity' : 'promise'} caseId={caseId} context={context} activity={activity} promise={promise} saving={saving} setSaving={setSaving} onSaved={async () => { setEditing(false); await onSaved(); }} />
    </Modal>}
    {deleting && <ConfirmDialog open title={ct(activity ? 'deleteActivity' : 'deletePromise')} description={context} message={ct('deleteRecordConfirm')} confirmLabel={ct('delete')} cancelLabel={ct('cancel')} isConfirming={saving} onCancel={() => { if (!saving) setDeleting(false); }} onConfirm={() => void remove()} />}
  </>;
}
