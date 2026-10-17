import { Eye, Pencil } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ActionMenu } from '../src/components/common/ActionMenu';
import { Button } from '../src/components/common/Button';
import { Modal } from '../src/components/common/Modal';
import { AccountingDeleteButton } from '../src/features/accounting/AccountingDeleteButton';
import { useLocalization } from '../src/context/LocalizationContext';

export function ActionMenuLab() {
  const { language } = useLocalization();
  const ar = language === 'ar';
  const [result, setResult] = useState('');
  const [rowClicks, setRowClicks] = useState(0);
  const [dialog, setDialog] = useState(false);
  return <section data-qa="action-menus" className="space-y-3 rounded-2xl border border-mis-border bg-white p-4">
    <div className="overflow-hidden rounded-xl border border-mis-border">
      <div className="overflow-x-auto">
        <table className="w-full min-w-[600px] text-sm"><thead className="bg-slate-50"><tr><th className="p-3 text-start">{ar ? 'السجل' : 'Record'}</th><th className="p-3 text-end">{ar ? 'الإجراءات' : 'Actions'}</th></tr></thead><tbody>
          <tr onClick={() => setRowClicks(x => x + 1)}><td className="p-3">{ar ? 'سجل تجريبي' : 'Sample record'}</td><td className="p-3 text-end">
            <ActionMenu label="Row actions">
              <button aria-label={ar ? 'عرض السجل' : 'View record'} title={ar ? 'عرض' : 'View'} onClick={() => setResult('view')}><Eye /></button>
              <Button leftIcon={<Pencil />} onClick={event => { event.stopPropagation(); setResult('edit'); setDialog(true); }}>{ar ? 'تعديل' : 'Edit'}</Button>
              <Button disabled>{ar ? 'غير متاح' : 'Unavailable'}</Button>
              <Link to="#menu-link" onClick={() => setResult('link')}>{ar ? 'السجل الأصلي' : 'Source record'}</Link>
              <button onClick={() => setResult('long')}>{(ar ? 'إجراء باسم طويل لاختبار ظهور النص كاملًا ' : 'An action with a long label that must remain readable ').repeat(3)}</button>
              <AccountingDeleteButton menuItem entryId="qa-only-never-delete" amount={1200} description="Synthetic menu record" onDeleted={() => { throw new Error('QA must never delete'); }} />
            </ActionMenu>
          </td></tr>
          <tr><td className="p-3">{ar ? 'سجل ثانٍ' : 'Second record'}</td><td className="p-3 text-end"><ActionMenu label="Second row actions"><button onClick={() => setResult('second')}>{ar ? 'عرض' : 'View'}</button></ActionMenu></td></tr>
        </tbody></table>
      </div>
    </div>
    <div data-qa="empty-actions"><ActionMenu>{false}{null}</ActionMenu></div>
    <div dir="ltr" data-qa="ltr-actions"><ActionMenu label="LTR actions"><button>Local direction</button></ActionMenu></div>
    <Button fullWidth={false} onClick={() => setResult('outside')}>Outside menu</Button>
    <output data-qa="action-result">{result}</output><output data-qa="row-clicks">{rowClicks}</output>
    <Modal open={dialog} title="Action dialog" onClose={() => setDialog(false)}>
      <ActionMenu label="Dialog actions"><button onClick={() => setResult('dialog')}>Dialog action</button></ActionMenu>
    </Modal>
  </section>;
}
