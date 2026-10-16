import { PageActions } from '../../components/common/PageActions';
import { Modal } from '../../components/common/Modal';
import { AccountingDeleteButton } from '../../features/accounting/AccountingDeleteButton';
import { AccountingExportButton, cairoToday, useAccountingMoneyConfirm } from '../../features/accounting/bookUi';
import { Car, Download, Pencil, Plus, Upload } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { DateControl } from '../../components/forms/DateControl';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { AccountingStatus, useAccountingText } from '../../features/accounting/accountingUi';
import { accountingService } from '../../features/accounting/services/accountingService';
import type { AccountingLookupEmployee, AccountingTransportation } from '../../features/accounting/types/accounting';
import { getApiErrorMessage } from '../../services/apiClient';

const blankForm = { employeeId: '', claimDate: cairoToday(), amount: 0, purpose: '', notes: '' };

export function AccountingTransportationPage() {
  const a = useAccountingText();
  const { confirmMoney, moneyDialog } = useAccountingMoneyConfirm();
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [rows, setRows] = useState<AccountingTransportation[]>([]);
  const [employees, setEmployees] = useState<AccountingLookupEmployee[]>([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState<AccountingTransportation | 'new'>();
  const [form, setForm] = useState(blankForm);

  const load = useCallback(async () => {
    const page = await accountingService.transportation({ search, status, from: from || undefined, to: to || undefined });
    setRows(page.items);
  }, [search, status, from, to]);

  useEffect(() => { load().catch((reason) => setError(getApiErrorMessage(reason, a.text('تعذر تحميل الانتقالات', 'Failed to load transportation')))); }, [load]);
  useEffect(() => { accountingService.employees().then(setEmployees).catch(() => undefined); }, []);

  const openCreate = () => { setEditing('new'); setForm(blankForm); };
  const openEdit = (row: AccountingTransportation) => {
    setEditing(row);
    setForm({ employeeId: row.employeeId, claimDate: row.claimDate.slice(0, 10), amount: row.amount, purpose: row.purpose, notes: row.notes ?? '' });
  };

  const save = async () => {
    setBusy(true); setError('');
    try {
      if (editing === 'new') await accountingService.createTransportation({ ...form, amount: Number(form.amount) });
      else if (editing) await accountingService.updateTransportation(editing.id, { claimDate: form.claimDate, amount: Number(form.amount), purpose: form.purpose, notes: form.notes || undefined });
      setEditing(undefined);
      await load();
    } catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر الحفظ', 'Could not save'))); }
    finally { setBusy(false); }
  };

  const act = async (id: string, action: string) => {
    setBusy(true); setError('');
    try { await accountingService.transportationAction(id, action); await load(); }
    catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر تنفيذ الإجراء', 'Action failed'))); }
    finally { setBusy(false); }
  };

  const upload = async (id: string, file?: File | null) => {
    if (!file) return;
    setBusy(true); setError('');
    try { await accountingService.uploadTransportationAttachment(id, file); await load(); }
    catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر رفع المرفق', 'Upload failed'))); }
    finally { setBusy(false); }
  };

  const download = async (row: AccountingTransportation) => {
    setBusy(true); setError('');
    try { await accountingService.downloadTransportationAttachment(row.id, row.attachmentFileName || 'attachment'); }
    catch (reason) { setError(getApiErrorMessage(reason, a.text('تعذر تنزيل المرفق', 'Download failed'))); }
    finally { setBusy(false); }
  };

  return (
    <div className="space-y-5">{moneyDialog}
      <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-mis-navy">{a.text('الانتقالات', 'Transportation')}</h1>
          <p className="text-sm text-slate-500">{a.text('راجع الموظف والمبلغ والتاريخ ثم أكّد لتسجيل المصروف مباشرة. يمكنك إضافة إيصال أو حذف الحركة بعد التأكيد.', 'Confirm the employee, amount and date to record the expense immediately. Attach a receipt or delete an incorrect entry after confirmation.')}</p>
        </div>
        <PageActions><Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} onClick={openCreate}>{a.text('مطالبة جديدة', 'New claim')}</Button>
      <AccountingExportButton report="transportation" filter={{ from: from || '2000-01-01', to: to || '2100-12-31', search: search || undefined, status: status || undefined }} /></PageActions></header>

      {error ? <p className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p> : null}

      <div className="flex flex-wrap gap-2">
        <input className="min-w-[200px] flex-1 rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('الموظف', 'Employee')} value={search} onChange={(event) => setSearch(event.target.value)} />
        <DateControl aria-label={a.text('من تاريخ', 'From date')} className="min-w-[10rem]" value={from} onChange={(event) => setFrom(event.target.value)} />
        <DateControl aria-label={a.text('إلى تاريخ', 'To date')} className="min-w-[10rem]" value={to} onChange={(event) => setTo(event.target.value)} />
        <ProfessionalSelect className="min-h-11 min-w-[12rem] rounded-xl border border-mis-border bg-white px-3 text-sm" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">{a.text('كل الحالات', 'All statuses')}</option>
          {['READY', 'PAID', 'REJECTED', 'CANCELLED'].map((value) => <option key={value} value={value}>{a.status(value)}</option>)}
        </ProfessionalSelect>
      </div>

      <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[48rem] text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-3 py-3 text-start">{a.text('الموظف', 'Employee')}</th>
                <th className="px-3 py-3 text-start">{a.text('التاريخ', 'Date')}</th>
                <th className="px-3 py-3 text-end">{a.text('المبلغ', 'Amount')}</th>
                <th className="px-3 py-3 text-start">{a.text('الغرض', 'Purpose')}</th>
                <th className="px-3 py-3 text-start">{a.text('الحالة', 'Status')}</th>
                <th className="px-3 py-3 text-start">{a.text('مرفق', 'File')}</th>
                <th className="px-3 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-mis-border">
              {rows.map((row) => (
                <tr key={row.id}>
                  <td className="px-3 py-2"><div className="font-semibold text-mis-navy">{row.employeeName}</div><div className="font-mono text-xs text-slate-500">{row.employeeNumber}</div></td>
                  <td className="px-3 py-2">{a.date(row.claimDate)}</td>
                  <td className="px-3 py-2 text-end font-bold" data-bidi="ltr">{a.money(row.amount)}</td>
                  <td className="px-3 py-2">{row.purpose}</td>
                  <td className="px-3 py-2"><AccountingStatus value={row.status} /></td>
                  <td className="px-3 py-2">
                    <div className="flex flex-wrap items-center gap-2 text-xs">
                      <span className="text-slate-600">{row.attachmentFileName ?? '—'}</span>
                      {row.attachmentFileName ? <Button disabled={busy} fullWidth={false} leftIcon={<Download className="h-3.5 w-3.5" />} onClick={() => void download(row)} size="sm" variant="ghost">{a.text('تنزيل', 'Download')}</Button> : null}
                      {['PENDING', 'APPROVED', 'PAID'].includes(row.status) ? (
                        <label className="inline-flex cursor-pointer items-center gap-1 font-bold text-mis-primary">
                          <input type="file" className="hidden" accept=".pdf,.jpg,.jpeg,.png" onChange={(event) => upload(row.id, event.target.files?.[0])} />
                          <Upload className="h-3.5 w-3.5" />{a.text('رفع', 'Upload')}
                        </label>
                      ) : null}
                    </div>
                  </td>
                  <td className="px-3 py-2">
                    <div className="flex flex-wrap justify-end gap-1">
                      {row.status === 'PENDING' ? (
                        <>
                          <Button disabled={busy} fullWidth={false} leftIcon={<Pencil className="h-3.5 w-3.5" />} onClick={() => openEdit(row)} size="sm" variant="ghost">{a.text('تعديل', 'Edit')}</Button>
                        </>
                      ) : null}
                      {['PENDING', 'APPROVED'].includes(row.status) ? <Button disabled={busy} fullWidth={false} onClick={() => confirmMoney(row.amount, row.employeeName + ' / ' + row.claimDate + '\n' + row.purpose, () => act(row.id, 'pay'))} size="sm" variant="outline">{a.text('صرف', 'Pay')}</Button> : null}
                      <AccountingDeleteButton kind="TRANSPORT" id={row.id} amount={row.amount} description={`${row.employeeName} / ${row.claimDate}\n${row.purpose}`} onDeleted={load} />
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!rows.length ? (
          <EmptyState
            compact
            icon={<Car className="h-5 w-5" />}
            title={a.text('لا توجد مطالبات انتقال', 'No transportation claims')}
            description={a.text('أضف مطالبة جديدة أو غيّر عوامل التصفية.', 'Add a new claim or change the filters.')}
            action={<Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} onClick={openCreate}>{a.text('مطالبة جديدة', 'New claim')}</Button>}
          />
        ) : null}
      </div>

      {editing ? (
        <Modal open onClose={() => setEditing(undefined)} title={a.text('الانتقالات', 'Transportation')} size="md" hideHeader hideCloseButton bodyClassName="p-0">
          <div className="min-w-0 p-6" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-start justify-between gap-3">
              <h2 className="text-lg font-bold text-mis-navy">{editing === 'new' ? a.text('مطالبة انتقال', 'Transportation claim') : a.text('تعديل المطالبة', 'Edit claim')}</h2>
            </div>
            <div className="mt-4 space-y-3">
              <ProfessionalSelect className="min-h-11 w-full rounded-xl border border-mis-border bg-white px-3 text-sm" disabled={editing !== 'new'} value={form.employeeId} onChange={(event) => setForm({ ...form, employeeId: event.target.value })}>
                <option value="">{a.text('اختر موظفاً', 'Select employee')}</option>
                {employees.map((employee) => <option key={employee.id} value={employee.id}>{employee.employeeNumber} — {employee.fullName}</option>)}
              </ProfessionalSelect>
              <DateControl className="w-full" value={form.claimDate} onChange={(event) => setForm({ ...form, claimDate: event.target.value })} />
              <input type="number" min="0.01" step="0.01" className="w-full rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('المبلغ', 'Amount')} value={form.amount} onChange={(event) => setForm({ ...form, amount: Number(event.target.value) })} />
              <input className="w-full rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('الغرض', 'Purpose')} value={form.purpose} onChange={(event) => setForm({ ...form, purpose: event.target.value })} />
              <textarea className="w-full rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('ملاحظات', 'Notes')} value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} />
            </div>
            <div className="mt-5 flex gap-2">
              <Button disabled={busy || !form.employeeId || !form.purpose.trim() || !form.claimDate || form.claimDate > cairoToday() || !Number.isFinite(form.amount) || form.amount <= 0 || Math.abs(form.amount * 100 - Math.round(form.amount * 100)) > 0.0001} fullWidth={false} onClick={() => confirmMoney(form.amount, (employees.find(x => x.id === form.employeeId)?.fullName ?? '') + ' / ' + form.claimDate + '\n' + form.purpose + '\n' + form.notes, save)}>{a.text('تأكيد التسجيل', 'Confirm recording')}</Button>
              <Button fullWidth={false} onClick={() => setEditing(undefined)} variant="outline">{a.text('إلغاء', 'Cancel')}</Button>
            </div>
          </div>
        </Modal>
      ) : null}
    </div>
  );
}
