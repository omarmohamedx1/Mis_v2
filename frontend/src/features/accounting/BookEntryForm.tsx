import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { Modal } from '../../components/common/Modal';
import { DateControl } from '../../components/forms/DateControl';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { financeService } from '../finance/services/financeService';
import type { FinanceAccount } from '../finance/types/finance';
import { accountingService } from './services/accountingService';
import { accountingBookService } from './services/accountingBookService';
import { useAccountingText } from './accountingUi';
import { BookError, BookField, cairoToday, categories, manualCategories } from './bookUi';
import type { AccountingLookupEmployee } from './types/accounting';
import type { Advance, BookCreate } from './types/book';
import { getApiErrorMessage } from '../../services/apiClient';

export function BookEntryForm({ open, category = 'PETTY', advance, onClose, onSaved }: { open: boolean; category?: string; advance?: Advance; onClose: () => void; onSaved: () => void }) {
  const a = useAccountingText(); const [accounts, setAccounts] = useState<FinanceAccount[]>([]); const [employees, setEmployees] = useState<AccountingLookupEmployee[]>([]);
  const [form, setForm] = useState<BookCreate>({ category, amount: 0, transactionDate: cairoToday(), accountId: '', counterAccountId: '', description: '' });
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false); const [review, setReview] = useState(false); const [loaded, setLoaded] = useState(false);
  const name = (x: FinanceAccount) => `${x.code} · ${a.ar ? x.nameArabic : x.nameEnglish}`;
  useEffect(() => {
    if (!open) return; let active = true; setError(''); setLoaded(false); setReview(false);
    Promise.all([financeService.accounts(), accountingService.employees()]).then(([acc, emp]) => {
      if (!active) return; setAccounts(acc); setEmployees(emp);
      const cat = advance ? 'ADVANCE_REPAYMENT' : manualCategories.includes(category) ? category : 'PETTY';
      setForm({ category: cat, transactionDate: cairoToday(), amount: 0, accountId: advance?.accountId ?? acc.find(x => x.code === categories[cat][2])?.id ?? '', counterAccountId: acc.find(x => x.code === (cat === 'CARRIED_ADVANCE' ? '310100' : '110300'))?.id ?? '', description: advance ? `${a.text('سداد سلفة', 'Repayment')} ${advance.documentNumber}` : '', employeeId: advance?.employeeId, relatedAdvanceId: advance?.id, requestId: crypto.randomUUID() }); setLoaded(true);
    }).catch(e => { if (active) setError(getApiErrorMessage(e, a.text('تعذر تحميل الحسابات والموظفين', 'Could not load accounts and employees'))); });
    return () => { active = false; };
  }, [open, category, advance]);
  const cash = form.category !== 'CARRIED_ADVANCE'; const isAdvance = ['ADVANCE', 'CARRIED_ADVANCE', 'ADVANCE_REPAYMENT'].includes(form.category);
  const accountType = isAdvance ? 'ASSET' : form.category === 'INCOME' ? 'REVENUE' : 'EXPENSE';
  const usable = accounts.filter(x => x.isActive && x.postingAllowed && !x.controlAccountType);
  const changeCategory = (cat: string) => setForm({ ...form, category: cat, accountId: accounts.find(x => x.code === categories[cat][2])?.id ?? '', counterAccountId: accounts.find(x => x.code === (cat === 'CARRIED_ADVANCE' ? '310100' : '110300'))?.id ?? '', employeeId: undefined, relatedAdvanceId: undefined });
  const save = async () => {
    if (busy) return; setBusy(true); setError('');
    try { await accountingBookService.create(form); setReview(false); onClose(); onSaved(); }
    catch (e) { setError(getApiErrorMessage(e, a.text('تعذر حفظ الحركة', 'Could not save entry'))); }
    finally { setBusy(false); }
  };
  const submit = (e: FormEvent) => { e.preventDefault(); setError(''); setReview(true); };
  return <>
    <Modal open={open && !review} onClose={onClose} title={a.text('تسجيل حركة محاسبية', 'New accounting entry')} description={a.text('تُحفظ كمسودة للمراجعة والاعتماد ثم الترحيل.', 'Save a draft, then review, approve and post.')} size="lg">
      <BookError error={error} />
      {!loaded ? <p className="p-5 text-slate-500">{a.text('جار تحميل البيانات…', 'Loading…')}</p> : <form onSubmit={submit} className="mt-4 grid gap-4 sm:grid-cols-2">
        <BookField label={a.text('البند', 'Category')}><ProfessionalSelect className="field" disabled={!!advance} value={form.category} onChange={e => changeCategory(e.target.value)}>{manualCategories.filter(x => x !== 'ADVANCE_REPAYMENT' || !!advance).map(x => <option key={x} value={x}>{categories[x][a.ar ? 0 : 1]}</option>)}</ProfessionalSelect></BookField>
        <BookField label={a.text('تاريخ الحركة', 'Transaction date')}><DateControl required max={cairoToday()} className="w-full" value={form.transactionDate} onChange={e => setForm({ ...form, transactionDate: e.target.value })} /></BookField>
        <BookField label={a.text('المبلغ بالجنيه', 'Amount EGP')}><input autoComplete="off" className="field" type="number" min="0.01" step="0.01" max={advance?.remaining ?? '9999999999999.99'} required value={form.amount || ''} onChange={e => setForm({ ...form, amount: Number(e.target.value) })} /></BookField>
        <BookField label={a.text(isAdvance ? 'الموظف' : 'الموظف (اختياري)', 'Employee')}><ProfessionalSelect required={isAdvance} disabled={!!advance} className="field" value={form.employeeId ?? ''} onChange={e => setForm({ ...form, employeeId: e.target.value || undefined })}><option value="">{a.text('اختر الموظف', 'Select employee')}</option>{employees.map(x => <option value={x.id} key={x.id}>{x.employeeNumber} · {x.fullName}</option>)}</ProfessionalSelect></BookField>
        <BookField label={a.text('الحساب المحاسبي', 'Posting account')}><ProfessionalSelect required disabled={!!advance} className="field" value={form.accountId} onChange={e => setForm({ ...form, accountId: e.target.value })}><option value="">{a.text('اختر الحساب', 'Select account')}</option>{usable.filter(x => x.accountType === accountType && !x.isCashAccount).map(x => <option value={x.id} key={x.id}>{name(x)}</option>)}</ProfessionalSelect></BookField>
        <BookField label={cash ? a.text('الخزينة / البنك', 'Cash / bank') : a.text('حساب الأرصدة الافتتاحية', 'Opening equity account')}><ProfessionalSelect required className="field" value={form.counterAccountId} onChange={e => setForm({ ...form, counterAccountId: e.target.value })}><option value="">{a.text('اختر الحساب المقابل', 'Select counter account')}</option>{usable.filter(x => cash ? x.isCashAccount : x.accountType === 'EQUITY').map(x => <option key={x.id} value={x.id}>{name(x)}</option>)}</ProfessionalSelect></BookField>
        <div className="sm:col-span-2"><BookField label={a.text('البيان', 'Description')}><textarea required maxLength={1000} className="field min-h-24" value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} /></BookField></div>
        <BookField label={a.text('المرجع / رقم المستند (اختياري)', 'Reference (optional)')}><input className="field" maxLength={160} value={form.reference ?? ''} onChange={e => setForm({ ...form, reference: e.target.value })} /></BookField>
        {advance && <p className="self-center text-sm text-mis-primary">{advance.documentNumber} · {a.text('المتبقي', 'Outstanding')}: {a.money(advance.remaining)}</p>}
        {form.category === 'CARRIED_ADVANCE' && <p className="rounded-xl bg-amber-50 p-3 text-sm text-amber-900 sm:col-span-2">{a.text('استخدمها لإدخال سلفة سابقة غير مسجلة فقط. السلف المسجلة تُرحّل تلقائياً في كشف الأرصدة دون تسجيلها مرة أخرى.', 'Only enter a previously unrecorded advance here. Existing advances carry forward automatically. Only the opening balance is posted; no new cash payment is recorded.')}</p>}
        <div className="flex gap-2 sm:col-span-2"><Button type="submit" fullWidth={false}>{a.text('مراجعة البيانات', 'Review details')}</Button><Button type="button" variant="outline" fullWidth={false} onClick={onClose}>{a.text('إلغاء', 'Cancel')}</Button></div>
      </form>}
    </Modal>
    <ConfirmDialog open={open && review} onCancel={() => setReview(false)} onConfirm={() => void save()} isConfirming={busy} confirmVariant="primary" confirmLabel={a.text('تأكيد وحفظ المسودة', 'Confirm and save draft')} title={a.text('هل المبلغ والبيانات صحيحة؟', 'Confirm amount and details')} message={<div className="space-y-3"><BookError error={error} /><p className="text-2xl font-bold text-mis-navy">{a.money(form.amount)}</p><p>{categories[form.category]?.[a.ar ? 0 : 1]} · {form.transactionDate}</p><p className="break-words">{form.description}</p><p>{employees.find(x => x.id === form.employeeId)?.fullName}</p><p>{accounts.find(x => x.id === form.accountId) && name(accounts.find(x => x.id === form.accountId)!)}</p><p>{accounts.find(x => x.id === form.counterAccountId) && name(accounts.find(x => x.id === form.counterAccountId)!)}</p>{form.reference && <p>{a.text('المرجع', 'Reference')}: {form.reference}</p>}</div>} />
  </>;
}
