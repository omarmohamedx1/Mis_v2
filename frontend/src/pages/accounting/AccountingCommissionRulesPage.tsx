import { Percent, Plus, PowerOff, Scale, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { EmptyState } from '../../components/common/EmptyState';
import { DateControl } from '../../components/forms/DateControl';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { AccountingStatus, useAccountingText } from '../../features/accounting/accountingUi';
import { accountingService } from '../../features/accounting/services/accountingService';
import type { AccountingCommissionRule } from '../../features/accounting/types/accounting';
import { getApiErrorMessage } from '../../services/apiClient';

const emptyForm = {
  code: '',
  nameArabic: '',
  nameEnglish: '',
  scope: 'COLLECTOR',
  basis: 'PERCENT_OF_COLLECTED',
  percentage: 5,
  fixedAmount: 0,
  effectiveFrom: new Date().toISOString().slice(0, 10),
};

export function AccountingCommissionRulesPage() {
  const a = useAccountingText();
  const [scope, setScope] = useState('');
  const [rows, setRows] = useState<AccountingCommissionRule[]>([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [deleteTarget, setDeleteTarget] = useState<AccountingCommissionRule | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<AccountingCommissionRule | null>(null);

  const load = useCallback(async () => {
    setRows(await accountingService.commissionRules(scope || undefined));
  }, [scope]);

  useEffect(() => {
    load().catch((reason) => setError(getApiErrorMessage(reason, a.text('تعذر تحميل قواعد العمولة', 'Could not load commission rules'))));
  }, [load]);

  const create = async () => {
    setBusy(true); setError('');
    try {
      await accountingService.createCommissionRule({
        code: form.code,
        nameArabic: form.nameArabic,
        nameEnglish: form.nameEnglish,
        scope: form.scope,
        basis: form.basis,
        percentage: form.basis === 'PERCENT_OF_COLLECTED' ? Number(form.percentage) : null,
        fixedAmount: form.basis === 'FIXED_AMOUNT' ? Number(form.fixedAmount) : null,
        effectiveFrom: form.effectiveFrom,
      });
      setShowCreate(false);
      setForm(emptyForm);
      await load();
    } catch (reason) {
      setError(getApiErrorMessage(reason, a.text('تعذر إنشاء القاعدة', 'Could not create the rule')));
    } finally {
      setBusy(false);
    }
  };

  const deactivate = async () => {
    if (!deactivateTarget) return;
    setBusy(true); setError('');
    try {
      await accountingService.setCommissionRuleActive(deactivateTarget.id, false);
      setDeactivateTarget(null);
      await load();
    } catch (reason) {
      setError(getApiErrorMessage(reason, a.text('تعذر إيقاف القاعدة', 'Could not deactivate the rule')));
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!deleteTarget) return;
    setBusy(true); setError('');
    try {
      await accountingService.deleteCommissionRule(deleteTarget.id);
      setDeleteTarget(null);
      await load();
    } catch (reason) {
      setError(getApiErrorMessage(reason, a.text('تعذر حذف القاعدة', 'Could not delete the rule')));
    } finally {
      setBusy(false);
    }
  };

  const scopeLabel = (value: string) => value === 'SUPERVISOR' ? a.text('مشرف', 'Supervisor') : a.text('محصل', 'Collector');
  const basisLabel = (value: string) => value === 'FIXED_AMOUNT' ? a.text('مبلغ ثابت', 'Fixed amount') : a.text('نسبة من التحصيل', 'Percent of collected');
  const ruleValue = (row: AccountingCommissionRule) => row.basis === 'FIXED_AMOUNT' ? a.money(row.fixedAmount ?? 0) : `${a.number(row.percentage ?? 0)}%`;

  return (
    <div className="space-y-5">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-mis-navy">{a.text('قواعد العمولة', 'Commission rules')}</h1>
          <p className="mt-1 max-w-3xl text-sm leading-6 text-slate-500">{a.text('القواعد النشطة تُستخدم عند احتساب عمولات المحصلين والمشرفين من المدفوعات المعتمدة فقط.', 'Active rules are used when collector and supervisor commissions are calculated from approved payments only.')}</p>
        </div>
        <Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} onClick={() => setShowCreate(true)}>{a.text('قاعدة جديدة', 'New rule')}</Button>
      </header>

      {error ? <p className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p> : null}

      <ProfessionalSelect aria-label={a.text('النطاق', 'Scope')} className="min-h-11 w-full max-w-xs rounded-xl border border-mis-border bg-white px-3 text-sm" value={scope} onChange={(event) => setScope(event.target.value)}>
        <option value="">{a.text('كل النطاقات', 'All scopes')}</option>
        <option value="COLLECTOR">{a.text('محصل', 'Collector')}</option>
        <option value="SUPERVISOR">{a.text('مشرف', 'Supervisor')}</option>
      </ProfessionalSelect>

      <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[48rem] text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-3 py-3 text-start">{a.text('الكود', 'Code')}</th>
                <th className="px-3 py-3 text-start">{a.text('الاسم', 'Name')}</th>
                <th className="px-3 py-3 text-start">{a.text('النطاق', 'Scope')}</th>
                <th className="px-3 py-3 text-start">{a.text('الأساس', 'Basis')}</th>
                <th className="px-3 py-3 text-end">{a.text('القيمة', 'Value')}</th>
                <th className="px-3 py-3 text-start">{a.text('سارية من', 'Effective from')}</th>
                <th className="px-3 py-3 text-start">{a.text('الحالة', 'Status')}</th>
                <th className="px-3 py-3 text-end">{a.text('الإجراء', 'Action')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-mis-border">
              {rows.map((row) => (
                <tr key={row.id}>
                  <td className="px-3 py-2 font-mono text-xs font-bold text-mis-navy">{row.code}</td>
                  <td className="px-3 py-2 font-semibold text-mis-navy">{a.ar ? row.nameArabic : row.nameEnglish}</td>
                  <td className="px-3 py-2">{scopeLabel(row.scope)}</td>
                  <td className="px-3 py-2">{basisLabel(row.basis)}</td>
                  <td className="px-3 py-2 text-end font-bold" data-bidi="ltr">{ruleValue(row)}</td>
                  <td className="px-3 py-2">{a.date(row.effectiveFrom)}</td>
                  <td className="px-3 py-2"><AccountingStatus value={row.isActive ? 'ACTIVE' : 'INACTIVE'} /></td>
                  <td className="px-3 py-2">
                    <div className="flex justify-end gap-1">
                      {row.isActive ? (
                        <Button fullWidth={false} leftIcon={<PowerOff className="h-4 w-4" />} onClick={() => setDeactivateTarget(row)} size="sm" variant="ghost">{a.text('إيقاف', 'Deactivate')}</Button>
                      ) : null}
                      <Button className="text-red-600 hover:bg-red-50 hover:text-red-700" fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} onClick={() => setDeleteTarget(row)} size="sm" variant="ghost">{a.text('حذف', 'Delete')}</Button>
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
            icon={<Percent className="h-5 w-5" />}
            title={a.text('لا توجد قواعد عمولة', 'No commission rules')}
            description={a.text('أضف قاعدة للمحصلين والمشرفين قبل احتساب عمولات الشهر.', 'Add collector and supervisor rules before calculating monthly commissions.')}
            action={<Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} onClick={() => setShowCreate(true)}>{a.text('قاعدة جديدة', 'New rule')}</Button>}
          />
        ) : null}
      </div>

      {showCreate ? (
        <div className="fixed inset-0 z-40 grid place-items-center bg-slate-900/40 p-4" onClick={() => setShowCreate(false)}>
          <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-6 shadow-xl" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-center gap-3">
              <span className="grid h-11 w-11 place-items-center rounded-xl bg-emerald-50 text-emerald-700"><Scale className="h-5 w-5" /></span>
              <div>
                <h2 className="text-lg font-bold text-mis-navy">{a.text('قاعدة عمولة جديدة', 'New commission rule')}</h2>
                <p className="text-sm text-slate-500">{a.text('تُطبَّق على الاحتساب من تاريخ السريان.', 'Applied to calculations from the effective date.')}</p>
              </div>
            </div>
            <div className="mt-4 grid gap-3">
              <input className="w-full rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('الكود', 'Code')} value={form.code} onChange={(event) => setForm({ ...form, code: event.target.value })} />
              <input className="w-full rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('الاسم بالعربية', 'Arabic name')} value={form.nameArabic} onChange={(event) => setForm({ ...form, nameArabic: event.target.value })} />
              <input className="w-full rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('الاسم بالإنجليزية', 'English name')} value={form.nameEnglish} onChange={(event) => setForm({ ...form, nameEnglish: event.target.value })} />
              <ProfessionalSelect className="min-h-11 w-full rounded-xl border border-mis-border bg-white px-3 text-sm" value={form.scope} onChange={(event) => setForm({ ...form, scope: event.target.value })}>
                <option value="COLLECTOR">{a.text('محصل', 'Collector')}</option>
                <option value="SUPERVISOR">{a.text('مشرف', 'Supervisor')}</option>
              </ProfessionalSelect>
              <ProfessionalSelect className="min-h-11 w-full rounded-xl border border-mis-border bg-white px-3 text-sm" value={form.basis} onChange={(event) => setForm({ ...form, basis: event.target.value })}>
                <option value="PERCENT_OF_COLLECTED">{a.text('نسبة من التحصيل', 'Percent of collected')}</option>
                <option value="FIXED_AMOUNT">{a.text('مبلغ ثابت', 'Fixed amount')}</option>
              </ProfessionalSelect>
              {form.basis === 'PERCENT_OF_COLLECTED' ? (
                <input type="number" min={0} max={100} className="w-full rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('النسبة %', 'Percentage %')} value={form.percentage} onChange={(event) => setForm({ ...form, percentage: Number(event.target.value) })} />
              ) : (
                <input type="number" min={0} className="w-full rounded-xl border border-mis-border px-3 py-2 text-sm" placeholder={a.text('المبلغ الثابت', 'Fixed amount')} value={form.fixedAmount} onChange={(event) => setForm({ ...form, fixedAmount: Number(event.target.value) })} />
              )}
              <DateControl className="w-full" value={form.effectiveFrom} onChange={(event) => setForm({ ...form, effectiveFrom: event.target.value })} />
            </div>
            <div className="mt-5 flex flex-wrap gap-2">
              <Button disabled={busy || !form.code || !form.nameArabic || !form.nameEnglish} fullWidth={false} onClick={() => void create()}>{a.text('حفظ', 'Save')}</Button>
              <Button fullWidth={false} onClick={() => setShowCreate(false)} variant="outline">{a.text('إلغاء', 'Cancel')}</Button>
            </div>
          </div>
        </div>
      ) : null}

      <ConfirmDialog
        confirmLabel={a.text('إيقاف', 'Deactivate')}
        isConfirming={busy}
        message={a.text(`إيقاف قاعدة ${deactivateTarget?.code ?? ''}؟ القواعد المستخدمة في الاحتساب تبقى للأرشيف.`, `Deactivate rule ${deactivateTarget?.code ?? ''}? Rules already used in calculations stay for history.`)}
        onCancel={() => setDeactivateTarget(null)}
        onConfirm={() => void deactivate()}
        open={Boolean(deactivateTarget)}
        title={a.text('إيقاف قاعدة العمولة', 'Deactivate commission rule')}
      />
      <ConfirmDialog
        confirmLabel={a.text('حذف', 'Delete')}
        isConfirming={busy}
        message={a.text(`حذف قاعدة ${deleteTarget?.code ?? ''} نهائيًا؟ الحذف متاح فقط إذا لم تُستخدم في احتساب عمولة.`, `Permanently delete rule ${deleteTarget?.code ?? ''}? Allowed only if it was never used in a commission calculation.`)}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => void remove()}
        open={Boolean(deleteTarget)}
        title={a.text('حذف قاعدة العمولة', 'Delete commission rule')}
      />
    </div>
  );
}
