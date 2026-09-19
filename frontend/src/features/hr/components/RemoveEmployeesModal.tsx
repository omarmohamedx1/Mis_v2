import { Archive, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Button } from '../../../components/common/Button';
import { Modal } from '../../../components/common/Modal';
import { TextAreaInput } from '../../../components/forms/TextAreaInput';
import { useLocalization } from '../../../context/LocalizationContext';

export function RemoveEmployeesModal({
  canDelete,
  canKeep = true,
  count,
  busy,
  names,
  onClose,
  onConfirm,
  open,
}: {
  canDelete: boolean;
  canKeep?: boolean;
  count: number;
  busy: boolean;
  names: string[];
  onClose: () => void;
  onConfirm: (keepData: boolean, reason: string) => void;
  open: boolean;
}) {
  const { t } = useLocalization();
  const [keepData, setKeepData] = useState(true);
  const [reason, setReason] = useState('');

  useEffect(() => {
    if (open) {
      setKeepData(canKeep);
      setReason('');
    }
  }, [canKeep, open]);

  if (!open || count < 1) return null;
  const selectedKeep = canKeep && (keepData || !canDelete);
  const canSubmit = selectedKeep ? reason.trim().length >= 2 : canDelete;
  const summary = names.length === 1 ? names[0] : t('employeesSelected', { count });

  return (
    <Modal
      footer={(
        <>
          <Button disabled={busy} fullWidth={false} onClick={onClose} variant="outline">{t('cancel')}</Button>
          <Button
            disabled={!canSubmit}
            fullWidth={false}
            isLoading={busy}
            onClick={() => onConfirm(selectedKeep, reason.trim())}
            variant={selectedKeep ? 'outline' : 'danger'}
          >
            {selectedKeep ? t('keepEmployeeData') : t('wipeEmployeeData')}
          </Button>
        </>
      )}
      onClose={busy ? () => undefined : onClose}
      open
      title={t('removeEmployeesPrompt')}
    >
      <p className="mb-1 font-semibold text-mis-navy" dir="auto">{summary}</p>
      <p className="mb-4 text-sm text-slate-600">{t('removeEmployeesChoiceHelp')}</p>
      <div className="grid gap-3">
        {canKeep ? (
        <button
          className={`rounded-2xl border p-4 text-start transition ${selectedKeep ? 'border-mis-primary bg-mis-pale shadow-sm' : 'border-mis-border bg-white hover:border-slate-300'}`}
          onClick={() => setKeepData(true)}
          type="button"
        >
          <span className="flex items-center gap-2 font-bold text-mis-navy"><Archive className="h-4 w-4" />{t('keepEmployeeData')}</span>
          <span className="mt-1 block text-sm text-slate-600">{t('keepEmployeeDataHelp')}</span>
        </button>
        ) : null}
        {canDelete ? (
          <button
            className={`rounded-2xl border p-4 text-start transition ${!selectedKeep ? 'border-red-300 bg-red-50 shadow-sm' : 'border-mis-border bg-white hover:border-slate-300'}`}
            onClick={() => setKeepData(false)}
            type="button"
          >
            <span className="flex items-center gap-2 font-bold text-red-700"><Trash2 className="h-4 w-4" />{t('wipeEmployeeData')}</span>
            <span className="mt-1 block text-sm text-slate-600">{t('wipeEmployeeDataHelp')}</span>
          </button>
        ) : null}
      </div>
      {selectedKeep ? (
        <div className="mt-4">
          <TextAreaInput label={t('archiveReason')} maxLength={500} onChange={(event) => setReason(event.target.value)} required rows={3} value={reason} />
        </div>
      ) : null}
    </Modal>
  );
}
