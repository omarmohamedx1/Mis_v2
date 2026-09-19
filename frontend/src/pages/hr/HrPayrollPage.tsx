import { Link } from 'react-router-dom';
import { RefreshCw, Wallet, X } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge, type StatusTone } from '../../components/common/StatusBadge';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextInput } from '../../components/forms/TextInput';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import { AccountingStatus } from '../../features/accounting/accountingUi';
import { hrPayrollService } from '../../features/hr/services/hrPayrollService';
import type { HrPayrollEmployee, HrPayrollSheet } from '../../features/hr/types/payroll';
import { canAccessModule } from '../../features/modules/moduleAccess';
import { getApiErrorMessage } from '../../services/apiClient';

function money(value: number, language: string) {
  return new Intl.NumberFormat(language === 'ar' ? 'ar-EG' : 'en-EG', { style: 'currency', currency: 'EGP', maximumFractionDigits: 2, minimumFractionDigits: 2 }).format(value);
}

function monthName(month: number, language: string) {
  return new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { month: 'long' }).format(new Date(2000, month - 1, 1));
}

function deductionTone(code: string): StatusTone {
  return code === 'FRIDAY_OFFSET' ? 'success' : code === 'LATE' ? 'warning' : 'danger';
}

function deductionLabel(code: string, ar: boolean) {
  return code === 'FRIDAY_OFFSET' ? (ar ? 'مقاصة الجمعة' : 'Friday offset') : code === 'LATE' ? (ar ? 'تأخير' : 'Late') : (ar ? 'غياب' : 'Absence');
}

export function HrPayrollPage() {
  const { language, t } = useLocalization();
  const { user } = useAuth();
  const ar = language === 'ar';
  const now = useMemo(() => new Date(), []);
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [search, setSearch] = useState('');
  const [sheet, setSheet] = useState<HrPayrollSheet | null>(null);
  const [selected, setSelected] = useState<HrPayrollEmployee | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const canOpenAccounting = user ? canAccessModule(user, 'finance') : false;

  const load = useCallback(async () => {
    setLoading(true); setError('');
    try {
      const data = await hrPayrollService.sheet(year, month);
      setSheet(data);
      setSelected((current) => current ? data.employees.find((item) => item.employeeId === current.employeeId) ?? null : null);
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, ar ? 'تعذر تحميل صافي المرتبات.' : 'Unable to load net salaries.'));
    } finally { setLoading(false); }
  }, [ar, month, year]);

  useEffect(() => { void load(); }, [load]);

  const rows = (sheet?.employees ?? []).filter((item) => {
    const needle = search.trim().toLowerCase();
    if (!needle) return true;
    return item.employeeName.toLowerCase().includes(needle) || item.employeeNumber.toLowerCase().includes(needle) || (item.departmentName ?? '').toLowerCase().includes(needle);
  });

  return (
    <div className="space-y-5">
      <PageHeader
        title={t('netSalaries')}
        description={t('netSalariesSubtitle')}
        actions={<Button fullWidth={false} onClick={() => void load()} size="md" type="button" variant="outline" leftIcon={<RefreshCw className="h-4 w-4" />}>{ar ? 'تحديث' : 'Refresh'}</Button>}
      />

      <Card padding="lg">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <SelectInput label={ar ? 'الشهر' : 'Month'} onChange={(event) => setMonth(Number(event.target.value))} value={String(month)}>
            {Array.from({ length: 12 }, (_, index) => index + 1).map((value) => <option key={value} value={value}>{monthName(value, language)}</option>)}
          </SelectInput>
          <TextInput inputMode="numeric" label={ar ? 'السنة' : 'Year'} onChange={(event) => setYear(Number(event.target.value) || year)} type="number" value={year} />
          <TextInput label={ar ? 'بحث' : 'Search'} onChange={(event) => setSearch(event.target.value)} value={search} />
          {canOpenAccounting ? (
            <div className="flex items-end">
              <Link className="inline-flex h-12 items-center font-semibold text-mis-primary" to="/finance/salaries">{ar ? 'فتح مسير الحسابات' : 'Open accounting payroll'}</Link>
            </div>
          ) : null}
        </div>
        <p className="mt-4 text-sm leading-6 text-slate-500">
          {ar
            ? `أيام العمل الأحد–الخميس. سماح التأخير ${sheet?.lateGraceMinutes ?? 15} دقيقة. كل يوم تأخير ${sheet?.lateMinutesPerDeductedDay ?? 60} دقيقة أو أكثر يخصم يومًا واحدًا لذلك اليوم، وساعتان في نفس اليوم متخصمش يومين. زيارة الجمعة في نفس الشهر تسقط يوم غياب واحد. يوم الغياب = الراتب ÷ ${sheet?.monthDivisor ?? 30}.`
            : `Working days are Sunday–Thursday. Late grace is ${sheet?.lateGraceMinutes ?? 15} minutes. Each day with ${sheet?.lateMinutesPerDeductedDay ?? 60} or more late minutes deducts one day for that day; two hours on the same day still deduct one day. A Friday visit in the same month waives one absence. Daily rate is salary ÷ ${sheet?.monthDivisor ?? 30}.`}
        </p>
      </Card>

      {error ? <ErrorState compact message={error} onRetry={() => void load()} title={ar ? 'تعذر التحميل' : 'Unable to load'} /> : null}

      {loading ? <div className="flex min-h-64 items-center justify-center"><LoadingSpinner className="h-8 w-8 text-mis-primary" /></div> : sheet ? (
        <>
          <div className="grid gap-4 sm:grid-cols-3">
            <Card padding="sm"><p className="text-sm text-slate-500">{ar ? 'الإجمالي' : 'Gross'}</p><p className="mt-2 text-2xl font-bold text-mis-navy" data-bidi="ltr">{money(sheet.totalGross, language)}</p></Card>
            <Card padding="sm"><p className="text-sm text-slate-500">{ar ? 'الخصومات' : 'Deductions'}</p><p className="mt-2 text-2xl font-bold text-rose-700" data-bidi="ltr">{money(sheet.totalDeductions, language)}</p></Card>
            <Card padding="sm"><p className="text-sm text-slate-500">{ar ? 'صافي المرتبات' : 'Net salaries'}</p><p className="mt-2 text-2xl font-bold text-emerald-700" data-bidi="ltr">{money(sheet.totalNet, language)}</p></Card>
          </div>

          {!rows.length ? (
            <EmptyState
              icon={<Wallet className="h-5 w-5" />}
              title={ar ? 'لا توجد مرتبات لهذا الشهر' : 'No salaries for this month'}
              description={ar ? 'أضف راتبًا أساسيًا للموظفين أو غيّر الشهر.' : 'Add a basic salary to employees or change the month.'}
            />
          ) : (
            <Card padding="none">
              <div className="overflow-auto">
                <table className="w-full min-w-[1100px] text-sm">
                  <thead className="bg-mis-surface text-start">
                    <tr>
                      {[ar ? 'الموظف' : 'Employee', ar ? 'القسم' : 'Department', ar ? 'الأساسي' : 'Basic', ar ? 'البدلات' : 'Allowances', ar ? 'غياب' : 'Absence', ar ? 'جمعة' : 'Friday', ar ? 'تأخير' : 'Late', ar ? 'الخصم' : 'Deduction', ar ? 'الصافي' : 'Net', ''].map((label, index) => <th className="px-4 py-3 font-bold text-mis-navy" key={`${label}-${index}`}>{label}</th>)}
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((item) => (
                      <tr className="cursor-pointer border-t border-mis-border hover:bg-mis-pale/40" key={item.employeeId} onClick={() => setSelected(item)}>
                        <td className="px-4 py-3"><p className="font-semibold text-mis-navy">{item.employeeName}</p><p className="text-xs text-slate-400">{item.employeeNumber}</p></td>
                        <td className="px-4 py-3">{item.departmentName || '—'}</td>
                        <td className="px-4 py-3" data-bidi="ltr">{money(item.basicSalary, language)}</td>
                        <td className="px-4 py-3" data-bidi="ltr">{money(item.allowances, language)}</td>
                        <td className="px-4 py-3">{item.chargeableAbsenceDays}/{item.absenceDays}</td>
                        <td className="px-4 py-3">{item.fridayVisitDays}</td>
                        <td className="px-4 py-3">{item.lateMinutes}′ / {item.lateDeductionDays}</td>
                        <td className="px-4 py-3 font-semibold text-rose-700" data-bidi="ltr">{money(item.totalDeductions, language)}</td>
                        <td className="px-4 py-3 font-bold text-mis-navy" data-bidi="ltr">{money(item.netSalary, language)}</td>
                        <td className="px-4 py-3 text-end">
                          <Button fullWidth={false} onClick={(event) => { event.stopPropagation(); setSelected(item); }} size="sm" type="button" variant="ghost">{ar ? 'تفاصيل' : 'Details'}</Button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Card>
          )}
        </>
      ) : null}

      <Modal
        description={selected ? `${selected.employeeNumber} · ${selected.positionName || t('notAssigned')}` : undefined}
        footer={<Button fullWidth={false} leftIcon={<X className="h-4 w-4" />} onClick={() => setSelected(null)} size="md" type="button" variant="outline">{ar ? 'إغلاق' : 'Close'}</Button>}
        onClose={() => setSelected(null)}
        open={Boolean(selected)}
        size="lg"
        title={selected?.employeeName ?? t('netSalaries')}
      >
        {selected ? (
          <div className="space-y-5">
            <div className="flex flex-wrap items-start justify-between gap-3 rounded-2xl bg-mis-surface px-5 py-4">
              <div>
                <p className="text-sm text-slate-500">{ar ? 'صافي المرتب' : 'Net salary'}</p>
                <p className="mt-1 text-2xl font-bold text-mis-navy" data-bidi="ltr">{money(selected.netSalary, language)}</p>
              </div>
              {selected.accountingStatus ? <AccountingStatus value={selected.accountingStatus} /> : null}
            </div>
            <dl className="grid gap-4 sm:grid-cols-2">
              <div className="rounded-xl border border-mis-border px-4 py-3"><dt className="text-xs font-semibold text-slate-400">{ar ? 'أجر اليوم' : 'Daily rate'}</dt><dd className="mt-1 font-semibold" data-bidi="ltr">{money(selected.dailyRate, language)}</dd></div>
              <div className="rounded-xl border border-mis-border px-4 py-3"><dt className="text-xs font-semibold text-slate-400">{ar ? 'أيام الغياب' : 'Absence days'}</dt><dd className="mt-1 font-semibold">{selected.absenceDays}</dd></div>
              <div className="rounded-xl border border-mis-border px-4 py-3"><dt className="text-xs font-semibold text-slate-400">{ar ? 'زيارات الجمعة المسقطة' : 'Friday visits waived'}</dt><dd className="mt-1 font-semibold">{selected.offsetAbsenceDays}</dd></div>
              <div className="rounded-xl border border-mis-border px-4 py-3"><dt className="text-xs font-semibold text-slate-400">{ar ? 'دقائق التأخير المتبقية' : 'Late remainder'}</dt><dd className="mt-1 font-semibold">{selected.lateRemainderMinutes}</dd></div>
            </dl>
            <div className="space-y-2">
              <h3 className="text-sm font-bold text-mis-navy">{ar ? 'الخصومات' : 'Deductions'}</h3>
              {selected.deductions.length ? selected.deductions.map((item) => (
                <div className="flex items-start justify-between gap-4 rounded-xl border border-mis-border bg-mis-surface px-4 py-3" key={`${item.code}-${item.reason}`}>
                  <div className="min-w-0">
                    <StatusBadge tone={deductionTone(item.code)}>{deductionLabel(item.code, ar)}</StatusBadge>
                    <p className="mt-2 text-sm leading-6 text-slate-700">{item.reason}</p>
                  </div>
                  <p className="shrink-0 font-bold text-mis-navy" data-bidi="ltr">{money(item.amount, language)}</p>
                </div>
              )) : <p className="text-sm text-slate-500">{ar ? 'لا توجد خصومات هذا الشهر.' : 'No deductions this month.'}</p>}
            </div>
            {!selected.hasCompensation ? <p className="rounded-xl bg-amber-50 px-4 py-3 text-sm text-amber-800">{ar ? 'لا يوجد راتب أساسي مسجل لهذا الموظف.' : 'No basic salary is recorded for this employee.'}</p> : null}
          </div>
        ) : null}
      </Modal>
    </div>
  );
}
