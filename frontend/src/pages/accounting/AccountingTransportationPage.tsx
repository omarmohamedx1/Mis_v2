import { useCallback, useEffect, useState } from 'react';
import { getApiErrorMessage } from '../../services/apiClient';
import { AccountingStatus, useAccountingText } from '../../features/accounting/accountingUi';
import { accountingService } from '../../features/accounting/services/accountingService';
import type { AccountingLookupEmployee, AccountingTransportation } from '../../features/accounting/types/accounting';

export function AccountingTransportationPage() {
  const a = useAccountingText();
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [rows, setRows] = useState<AccountingTransportation[]>([]);
  const [employees, setEmployees] = useState<AccountingLookupEmployee[]>([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState({ employeeId: '', claimDate: new Date().toISOString().slice(0, 10), amount: 0, purpose: '', notes: '' });

  const load = useCallback(async () => {
    const page = await accountingService.transportation({ search, status, from: from || undefined, to: to || undefined });
    setRows(page.items);
  }, [search, status, from, to]);

  useEffect(() => { load().catch((e) => setError(getApiErrorMessage(e, 'Failed to load transportation'))); }, [load]);
  useEffect(() => { accountingService.employees().then(setEmployees).catch(() => undefined); }, []);

  const create = async () => {
    setBusy(true); setError('');
    try {
      await accountingService.createTransportation({ ...form, amount: Number(form.amount) });
      setShowCreate(false);
      await load();
    } catch (e) { setError(getApiErrorMessage(e, a.text('تعذر الإنشاء', 'Could not create'))); }
    finally { setBusy(false); }
  };

  const act = async (id: string, action: string) => {
    setBusy(true); setError('');
    try { await accountingService.transportationAction(id, action); await load(); }
    catch (e) { setError(getApiErrorMessage(e, a.text('تعذر تنفيذ الإجراء', 'Action failed'))); }
    finally { setBusy(false); }
  };

  const upload = async (id: string, file?: File | null) => {
    if (!file) return;
    setBusy(true); setError('');
    try { await accountingService.uploadTransportationAttachment(id, file); await load(); }
    catch (e) { setError(getApiErrorMessage(e, a.text('تعذر رفع المرفق', 'Upload failed'))); }
    finally { setBusy(false); }
  };

  return (
    <div className="space-y-5">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-mis-navy">{a.text('الانتقالات', 'Transportation')}</h1>
          <p className="text-sm text-slate-500">{a.text('مطالبات انتقال مرتبطة بالموظفين والزيارات الميدانية عند التوفر', 'Claims linked to employees and field visits when available')}</p>
        </div>
        <button onClick={() => setShowCreate(true)} className="rounded-xl bg-mis-primary px-4 py-2 text-sm font-bold text-white">{a.text('مطالبة جديدة', 'New claim')}</button>
      </header>

      {error ? <p className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p> : null}

      <div className="flex flex-wrap gap-2">
        <input className="min-w-[200px] flex-1 rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('الموظف', 'Employee')} value={search} onChange={(e) => setSearch(e.target.value)} />
        <input type="date" className="rounded-xl border border-mis-border px-3 py-2 text-sm" value={from} onChange={(e) => setFrom(e.target.value)} />
        <input type="date" className="rounded-xl border border-mis-border px-3 py-2 text-sm" value={to} onChange={(e) => setTo(e.target.value)} />
        <select className="rounded-xl border border-mis-border px-3 py-2 text-sm" value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="">{a.text('كل الحالات', 'All statuses')}</option>
          {['PENDING', 'APPROVED', 'REJECTED', 'PAID', 'CANCELLED'].map((s) => <option key={s} value={s}>{a.status(s)}</option>)}
        </select>
      </div>

      <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[1000px] text-sm">
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
                  <td className="px-3 py-2">{row.claimDate}</td>
                  <td className="px-3 py-2 text-end font-bold" data-bidi="ltr">{a.money(row.amount)}</td>
                  <td className="px-3 py-2">{row.purpose}</td>
                  <td className="px-3 py-2"><AccountingStatus value={row.status} /></td>
                  <td className="px-3 py-2 text-xs">{row.attachmentFileName ?? '—'}{row.status === 'PENDING' ? <label className="ms-2 cursor-pointer font-bold text-mis-primary"><input type="file" className="hidden" accept=".pdf,.jpg,.jpeg,.png" onChange={(e) => upload(row.id, e.target.files?.[0])} />{a.text('رفع', 'Upload')}</label> : null}</td>
                  <td className="px-3 py-2 text-end space-x-2 rtl:space-x-reverse">
                    {row.status === 'PENDING' ? <>
                      <button disabled={busy} className="text-xs font-bold text-emerald-700" onClick={() => act(row.id, 'approve')}>{a.text('اعتماد', 'Approve')}</button>
                      <button disabled={busy} className="text-xs font-bold text-rose-700" onClick={() => act(row.id, 'reject')}>{a.text('رفض', 'Reject')}</button>
                    </> : null}
                    {row.status === 'APPROVED' ? <button disabled={busy} className="text-xs font-bold text-mis-primary" onClick={() => act(row.id, 'pay')}>{a.text('صرف', 'Pay')}</button> : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {showCreate ? (
        <div className="fixed inset-0 z-40 grid place-items-center bg-slate-900/40 p-4" onClick={() => setShowCreate(false)}>
          <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl" onClick={(e) => e.stopPropagation()}>
            <h2 className="text-lg font-bold text-mis-navy">{a.text('مطالبة انتقال', 'Transportation claim')}</h2>
            <div className="mt-4 space-y-3">
              <select className="w-full rounded-xl border px-3 py-2 text-sm" value={form.employeeId} onChange={(e) => setForm({ ...form, employeeId: e.target.value })}>
                <option value="">{a.text('اختر موظفاً', 'Select employee')}</option>
                {employees.map((e) => <option key={e.id} value={e.id}>{e.employeeNumber} — {e.fullName}</option>)}
              </select>
              <input type="date" className="w-full rounded-xl border px-3 py-2 text-sm" value={form.claimDate} onChange={(e) => setForm({ ...form, claimDate: e.target.value })} />
              <input type="number" className="w-full rounded-xl border px-3 py-2 text-sm" placeholder={a.text('المبلغ', 'Amount')} value={form.amount} onChange={(e) => setForm({ ...form, amount: Number(e.target.value) })} />
              <input className="w-full rounded-xl border px-3 py-2 text-sm" placeholder={a.text('الغرض', 'Purpose')} value={form.purpose} onChange={(e) => setForm({ ...form, purpose: e.target.value })} />
              <textarea className="w-full rounded-xl border px-3 py-2 text-sm" placeholder={a.text('ملاحظات', 'Notes')} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
            </div>
            <div className="mt-5 flex gap-2">
              <button disabled={busy || !form.employeeId || !form.purpose} onClick={create} className="rounded-xl bg-mis-primary px-4 py-2 text-sm font-bold text-white disabled:opacity-50">{a.text('حفظ', 'Save')}</button>
              <button onClick={() => setShowCreate(false)} className="rounded-xl border px-4 py-2 text-sm font-bold">{a.text('إلغاء', 'Cancel')}</button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
