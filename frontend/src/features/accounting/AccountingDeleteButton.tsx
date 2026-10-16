import { Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { useAccountingText } from './accountingUi';
import { BookError } from './bookUi';
import { accountingBookService } from './services/accountingBookService';
import { getApiErrorMessage } from '../../services/apiClient';

export function AccountingDeleteButton({ entryId, kind, id, amount, description, onDeleted }: { entryId?: string; kind?: string; id?: string; amount: number; description: string; onDeleted: () => void | Promise<void> }) {
  const a = useAccountingText(); const [open, setOpen] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [deleted, setDeleted] = useState(false);
  const remove = async () => {
    if (busy || deleted) return; setBusy(true); setError('');
    try {
      if (entryId) await accountingBookService.deleteEntry(entryId);
      else if (kind && id) await accountingBookService.deleteSource(kind, id);
      else throw new Error('Missing record identifier');
      setDeleted(true); setOpen(false);
      try { await onDeleted(); } catch { setError(a.text('تم الحذف. حدّث الصفحة لعرض البيانات الحالية.', 'Deleted. Refresh the page to load the current records.')); }
    } catch (e) { setError(getApiErrorMessage(e, a.text('تعذر حذف الحركة', 'Could not delete this record'))); }
    finally { setBusy(false); }
  };
  return <><Button fullWidth={false} size="sm" variant="ghost" className="text-rose-700 hover:bg-rose-50 hover:text-rose-800" disabled={deleted || busy} leftIcon={<Trash2 />} onClick={() => { setError(''); setOpen(true); }}>{deleted ? a.text('تم الحذف', 'Deleted') : a.text('حذف', 'Delete')}</Button>{!open && <BookError error={error} />}<ConfirmDialog open={open} title={a.text('تأكيد حذف الحركة', 'Confirm record deletion')} confirmLabel={a.text('نعم، احذف الحركة', 'Yes, delete record')} confirmVariant="danger" isConfirming={busy} onCancel={() => setOpen(false)} onConfirm={() => void remove()} message={<div className="space-y-3"><BookError error={error} /><p className="text-2xl font-bold text-mis-navy">{a.money(amount)}</p><p className="whitespace-pre-line break-words">{description}</p><p>{a.text('سيتم حذف الحركة من السجل وتحديث الإجماليات والأرصدة المرتبطة بها. هل تريد المتابعة؟', 'This removes the record and updates its totals and related balances. Continue?')}</p></div>} /></>;
}
