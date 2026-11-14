import { Eye, RotateCcw } from 'lucide-react';
import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useLocation } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { Modal } from '../../components/common/Modal';
import { DateControl } from '../../components/forms/DateControl';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import { AccountingKpi, AccountingStatus, useAccountingText } from '../../features/accounting/accountingUi';
import { AccountingExportButton, AddButton, BookEmpty, BookError, BookField, BookHeader, cairoToday, cell, ClearButton, panel, table, useDebounced } from '../../features/accounting/bookUi';
import { accountingBookService } from '../../features/accounting/services/accountingBookService';
import { accountingService } from '../../features/accounting/services/accountingService';
import type { AccountingLookupEmployee } from '../../features/accounting/types/accounting';
import type { Advance, AdvanceDetails, AdvanceFilter, BookCreate } from '../../features/accounting/types/book';
import type { TranslationKey } from '../../localization/translations';
import { financeService } from '../../features/finance/services/financeService';
import type { FinanceAccount } from '../../features/finance/types/finance';
import { getApiErrorMessage } from '../../services/apiClient';

type AdvanceForm = BookCreate;
type ReverseTarget = { id: string; isPayment: boolean; label: string };

const formDefaults = (category: string): AdvanceForm => ({
  category,
  transactionDate: cairoToday(),
  amount: 0,
  accountId: '',
  counterAccountId: '',
  description: '',
  requestId: crypto.randomUUID(),
});

export function AccountingAdvancesPage() {
  const a = useAccountingText();
  const isCarriedPage = useLocation().pathname === '/finance/carried-advances';
  const { isRtl, language, t } = useLocalization();
  const tx = (key: TranslationKey) => t(key);
  const { user } = useAuth();
  const canCreateAdvance = Boolean(user && (user.roles.includes('Admin') || user.permissions.includes('*') || user.permissions.includes('finance.journal.manual.create') || user.permissions.includes('accounting.transaction.manage')));
  const canReverseAdvance = Boolean(user && (user.roles.includes('Admin') || user.permissions.includes('*') || user.permissions.includes('finance.transaction.reverse')));
  const [rows, setRows] = useState<Advance[]>([]);
  const [summary, setSummary] = useState({ totalAmount: 0, totalPaid: 0, totalRemaining: 0, openCount: 0 });
  const [employees, setEmployees] = useState<AccountingLookupEmployee[]>([]);
  const [departments, setDepartments] = useState<{ id: string; name: string }[]>([]);
  const [error, setError] = useState('');
  const [employeeError, setEmployeeError] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [loading, setLoading] = useState(true);
  const [employeeName, setEmployeeName] = useState('');
  const [employeeNumber, setEmployeeNumber] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [status, setStatus] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [asOf, setAsOf] = useState(cairoToday());
  const [employeeOrDocument, setEmployeeOrDocument] = useState('');
  const nameQuery = useDebounced(employeeName);
  const numberQuery = useDebounced(employeeNumber);
  const searchQuery = useDebounced(employeeOrDocument);
  const [formMode, setFormMode] = useState<'advance' | 'payment'>();
  const [selectedAdvance, setSelectedAdvance] = useState<Advance>();
  const [form, setForm] = useState<AdvanceForm>(formDefaults('ADVANCE'));
  const [accounts, setAccounts] = useState<FinanceAccount[]>([]);
  const [formLoading, setFormLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');
  const [details, setDetails] = useState<AdvanceDetails>();
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [reverseTarget, setReverseTarget] = useState<ReverseTarget>();
  const [reverseReason, setReverseReason] = useState('');
  const [reversing, setReversing] = useState(false);

  const filter = useMemo<AdvanceFilter>(() => ({
    ...(isCarriedPage
      ? { asOf, search: searchQuery || undefined, status: status || undefined, carriedOnly: true }
      : {
          from: from || undefined,
          to: to || undefined,
          employeeName: nameQuery || undefined,
          employeeNumber: numberQuery || undefined,
          departmentId: departmentId || undefined,
          status: status || undefined,
        }),
  }), [asOf, departmentId, from, isCarriedPage, nameQuery, numberQuery, searchQuery, status, to]);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError('');
    setRows([]);
    setSummary({ totalAmount: 0, totalPaid: 0, totalRemaining: 0, openCount: 0 });
    void accountingBookService.advances(filter).then(result => {
      if (!active) return;
      setRows(result.items);
      setSummary({ totalAmount: result.totalAmount, totalPaid: result.totalPaid, totalRemaining: result.totalRemaining, openCount: result.openCount });
    }).catch(e => {
      if (active) setError(getApiErrorMessage(e, tx('advancesLoadError')));
    }).finally(() => {
      if (active) setLoading(false);
    });
    return () => { active = false; };
  }, [filter, refresh, language]);

  useEffect(() => {
    let active = true;
    setEmployeeError('');
    void accountingService.employees().then(employeeOptions => {
      if (!active) return;
      setEmployees(employeeOptions);
      setDepartments([...new Map(employeeOptions.filter(x => x.departmentId && x.departmentName).map(x => [x.departmentId!, { id: x.departmentId!, name: x.departmentName! }])).values()].sort((x, y) => x.name.localeCompare(y.name)));
    }).catch(e => {
      if (active) setEmployeeError(getApiErrorMessage(e, tx('advancesEmployeesLoadError')));
    });
    return () => { active = false; };
  }, [language]);

  const loadDetails = async (id: string) => {
    setDetails(undefined);
    setDetailsLoading(true);
    setError('');
    try {
      setDetails(await accountingBookService.advanceDetails(id));
    } catch (e) {
      setError(getApiErrorMessage(e, tx('advancesDetailsLoadError')));
    } finally {
      setDetailsLoading(false);
    }
  };

  const openAdvanceForm = async (mode: 'advance' | 'payment', advance?: Advance) => {
    const category = mode === 'payment' ? 'ADVANCE_REPAYMENT' : isCarriedPage ? 'CARRIED_ADVANCE' : 'ADVANCE';
    setFormMode(mode);
    setSelectedAdvance(advance);
    setFormError('');
    setFormLoading(true);
    setForm(formDefaults(category));
    try {
      const accountOptions = await financeService.accounts();
      setAccounts(accountOptions);
      const usable = accountOptions.filter(x => x.isActive && x.postingAllowed && !x.controlAccountType);
      const counterAccount = category === 'CARRIED_ADVANCE'
        ? usable.find(x => x.code === '310100' && !x.isCashAccount) ?? usable.find(x => x.accountType === 'EQUITY')
        : usable.find(x => x.isCashAccount);
      setForm({
        ...formDefaults(category),
        accountId: advance?.accountId ?? usable.find(x => x.code === '113100' && !x.isCashAccount)?.id ?? '',
        counterAccountId: counterAccount?.id ?? '',
        employeeId: advance?.employeeId,
        relatedAdvanceId: advance?.id,
        description: mode === 'payment' && advance ? t('advancesRepaymentReason', { documentNumber: advance.documentNumber }) : '',
      });
    } catch (e) {
      setFormError(getApiErrorMessage(e, tx('advancesAccountsLoadError')));
    } finally {
      setFormLoading(false);
    }
  };

  const closeForm = () => { setFormMode(undefined); setFormError(''); };
  const usableAccounts = accounts.filter(x => x.isActive && x.postingAllowed && !x.controlAccountType);
  const employee = employees.find(x => x.id === form.employeeId);
  const accountName = (account: FinanceAccount) => `${account.code} · ${a.ar ? account.nameArabic : account.nameEnglish}`;

  const saveForm = async (event: FormEvent) => {
    event.preventDefault();
    if (saving) return;
    setFormError('');
    if (!form.employeeId) { setFormError(tx('advancesEmployeeRequired')); return; }
    if (!form.amount || form.amount <= 0) { setFormError(tx('advancesPositiveAmount')); return; }
    if (!form.transactionDate) { setFormError(tx('advancesDateRequired')); return; }
    if (formMode === 'advance' && !form.description.trim()) { setFormError(tx('advancesAdvanceReasonRequired')); return; }
    if (form.category !== 'CARRIED_ADVANCE' && !form.paymentMethod) { setFormError(tx('advancesPaymentMethodRequired')); return; }
    if (!form.accountId || !form.counterAccountId) { setFormError(tx('advancesAccountsRequired')); return; }
    if (form.category === 'CARRIED_ADVANCE' && !form.reference?.trim()) { setFormError(tx('carriedAdvancesReferenceRequired')); return; }
    if (formMode === 'payment' && selectedAdvance && form.amount > selectedAdvance.remaining) {
      setFormError(tx('advancesOverpayment'));
      return;
    }
    setSaving(true);
    try {
      await accountingBookService.create(form);
      closeForm();
      setRefresh(x => x + 1);
      if (selectedAdvance) await loadDetails(selectedAdvance.id);
    } catch (e) {
      setFormError(getApiErrorMessage(e, tx('advancesSaveError')));
    } finally {
      setSaving(false);
    }
  };

  const reverse = async () => {
    if (!reverseTarget || reversing) return;
    if (reverseReason.trim().length < 3) {
      setError(tx('advancesReversalReasonRequired'));
      return;
    }
    setReversing(true);
    setError('');
    const target = reverseTarget;
    try {
      await accountingBookService.action(reverseTarget.id, 'reverse', reverseReason.trim());
      const detailId = details?.advance.id;
      setReverseTarget(undefined);
      setReverseReason('');
      setRefresh(x => x + 1);
      if (target.isPayment && detailId) await loadDetails(detailId);
      else { setSelectedAdvance(undefined); setDetails(undefined); }
    } catch (e) {
      setError(getApiErrorMessage(e, tx('advancesReverseError')));
    } finally {
      setReversing(false);
    }
  };

  const clearFilters = () => {
    setEmployeeName('');
    setEmployeeNumber('');
    setEmployeeOrDocument('');
    setDepartmentId('');
    setStatus('');
    setFrom('');
    setTo('');
  };

  const methodOptions = [
    ['CASH', tx('advancesCash')],
    ['BANK_TRANSFER', tx('advancesBankTransfer')],
    ['CHECK', tx('advancesCheque')],
    ['OTHER', tx('advancesOtherMethod')],
  ];
  const methodName = (value?: string | null) => ({
    CASH: tx('advancesCash'),
    BANK_TRANSFER: tx('advancesBankTransfer'),
    CHECK: tx('advancesCheque'),
    OTHER: tx('advancesOtherMethod'),
  }[value ?? ''] ?? value ?? tx('advancesMissingValue'));
  const dateTime = (value: string) => new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG-u-nu-latn' : 'en-GB', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));

  const statusBadge = (value: Advance['status']) => {
    const styles = value === 'SETTLED' ? 'bg-emerald-50 text-emerald-700' : value === 'PARTIAL' ? 'bg-amber-50 text-amber-700' : 'bg-sky-50 text-sky-700';
    const label = value === 'SETTLED' ? tx('advancesFullySettled') : value === 'PARTIAL' ? tx('advancesPartiallySettled') : tx('advancesOpen');
    return <span className={`inline-flex whitespace-nowrap rounded-full px-3 py-1 text-xs font-bold ${styles}`}>{label}</span>;
  };

  const exportFilter = {
    ...(isCarriedPage
      ? { asOf, search: searchQuery || undefined, status: status || undefined, carriedOnly: true }
      : {
          from: from || '2000-01-01',
          to: to || cairoToday(),
          employeeName: nameQuery || undefined,
          employeeNumber: numberQuery || undefined,
          departmentId: departmentId || undefined,
          status: status || undefined,
        }),
  };

  return <div className="space-y-6" dir={isRtl ? 'rtl' : 'ltr'} lang={language}>
    <BookHeader
      eyebrow={tx('advancesPageEyebrow')}
      title={isCarriedPage ? tx('carriedAdvancesTitle') : tx('advancesTitle')}
      description={isCarriedPage ? tx('carriedAdvancesDescription') : tx('advancesDescription')}
    >
      <AccountingExportButton report="advances" filter={exportFilter} label={tx('advancesExportExcel')} errorMessage={tx('advancesExportError')} />
      {canCreateAdvance ? <AddButton onClick={() => void openAdvanceForm('advance')}>{isCarriedPage ? tx('carriedAdvancesAddBalance') : tx('advancesDisburse')}</AddButton> : null}
    </BookHeader>
    <BookError error={error || employeeError} />
    <div className={`grid gap-4 sm:grid-cols-2 ${isCarriedPage ? 'xl:grid-cols-3' : 'xl:grid-cols-4'}`}>
      <AccountingKpi label={isCarriedPage ? tx('carriedAdvancesOriginal') : tx('advancesTotalAdvances')} value={a.money(summary.totalAmount)} />
      <AccountingKpi label={isCarriedPage ? tx('carriedAdvancesSettled') : tx('advancesTotalPaid')} value={a.money(summary.totalPaid)} />
      <AccountingKpi label={isCarriedPage ? tx('carriedAdvancesRemaining') : tx('advancesTotalRemaining')} value={a.money(summary.totalRemaining)} />
      {!isCarriedPage ? <AccountingKpi label={tx('advancesOpenCount')} value={a.number(summary.openCount)} /> : null}
    </div>
    {isCarriedPage ? (
      <section className={`${panel} grid gap-4 sm:grid-cols-3`}>
        <BookField label={tx('carriedAdvancesAsOf')}><DateControl dir="ltr" className="w-full" value={asOf} onChange={e => setAsOf(e.target.value)} /></BookField>
        <BookField label={tx('carriedAdvancesSearch')}><input className="field" value={employeeOrDocument} onChange={e => setEmployeeOrDocument(e.target.value)} /></BookField>
        <div className="flex items-end gap-2">
          <BookField label={tx('advancesStatus')}><ProfessionalSelect className="field" value={status} onChange={e => setStatus(e.target.value)}><option value="">{tx('advancesAllStatuses')}</option><option value="OPEN">{tx('advancesOpen')}</option><option value="PARTIAL">{tx('advancesPartiallySettled')}</option><option value="SETTLED">{tx('advancesFullySettled')}</option></ProfessionalSelect></BookField>
          <ClearButton label={tx('advancesClearFilters')} onClick={() => { setEmployeeOrDocument(''); setStatus(''); }} />
        </div>
      </section>
    ) : (
      <section className={`${panel} grid gap-4 sm:grid-cols-2 xl:grid-cols-6`}>
        <BookField label={tx('advancesEmployeeName')}><input className="field" value={employeeName} onChange={e => setEmployeeName(e.target.value)} /></BookField>
        <BookField label={tx('advancesEmployeeCode')}><input dir="ltr" className="field" value={employeeNumber} onChange={e => setEmployeeNumber(e.target.value)} /></BookField>
        <BookField label={tx('advancesDepartment')}><ProfessionalSelect className="field" value={departmentId} onChange={e => setDepartmentId(e.target.value)}><option value="">{tx('advancesAllDepartments')}</option>{departments.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</ProfessionalSelect></BookField>
        <BookField label={tx('advancesStatus')}><ProfessionalSelect className="field" value={status} onChange={e => setStatus(e.target.value)}><option value="">{tx('advancesAllStatuses')}</option><option value="OPEN">{tx('advancesOpen')}</option><option value="PARTIAL">{tx('advancesPartiallySettled')}</option><option value="SETTLED">{tx('advancesFullySettled')}</option></ProfessionalSelect></BookField>
        <BookField label={tx('advancesFromDate')}><DateControl dir="ltr" className="w-full" value={from} onChange={e => setFrom(e.target.value)} /></BookField>
        <div className="flex items-end gap-2"><BookField label={tx('advancesToDate')}><DateControl dir="ltr" className="w-full" value={to} onChange={e => setTo(e.target.value)} /></BookField><ClearButton label={tx('advancesClearFilters')} onClick={clearFilters} /></div>
      </section>
    )}
    <div className="overflow-hidden rounded-2xl border border-mis-border bg-white">
      <div className="overflow-x-auto"><table className={table}>
        {isCarriedPage ? <>
          <thead><tr>{[
            tx('advancesEmployeeName'), tx('advancesAdvanceDate'), tx('advancesOriginalAmount'),
            tx('advancesPaid'), tx('advancesRemaining'), tx('carriedAdvancesType'), tx('advancesActions'),
          ].map((label, i) => <th key={i} className={cell}>{label}</th>)}</tr></thead>
          <tbody>{rows.map(row => <tr key={row.id}>
            <td className={cell}><p className="font-bold text-mis-navy">{row.employeeName}</p><p dir="ltr" className="font-mono text-xs">{row.employeeNumber} · {row.documentNumber}</p>{row.reference ? <p className="text-xs text-slate-500">{row.reference}</p> : null}</td>
            <td className={cell}>{a.date(row.date)}</td>
            <td className={cell} data-bidi="ltr">{a.money(row.amount)}</td>
            <td className={cell} data-bidi="ltr">{a.money(row.repaid)}</td>
            <td className={`${cell} font-bold text-mis-primary`} data-bidi="ltr">{a.money(row.remaining)}</td>
            <td className={cell}>{tx('carriedAdvancesCarriedType')}</td>
            <td className={cell}><Button fullWidth={false} variant="outline" size="sm" leftIcon={<Eye className="h-4 w-4" />} onClick={() => { setSelectedAdvance(row); void loadDetails(row.id); }}>{tx('carriedAdvancesHistory')}</Button></td>
          </tr>)}</tbody>
        </> : <>
          <thead><tr>{[
            tx('advancesEmployeeCode'), tx('advancesEmployeeName'), tx('advancesDepartment'), tx('advancesAdvanceDate'),
            tx('advancesOriginalAmount'), tx('advancesPaid'), tx('advancesRemaining'), tx('advancesReason'), tx('advancesStatusLabel'), tx('advancesActions'),
          ].map((label, i) => <th key={i} className={cell}>{label}</th>)}</tr></thead>
          <tbody>{rows.map(row => <tr key={row.id} className="cursor-pointer" onClick={() => { setSelectedAdvance(row); void loadDetails(row.id); }}>
            <td dir="ltr" className={`${cell} font-mono text-xs`}>{row.employeeNumber}</td>
            <td className={`${cell} font-bold text-mis-navy`}>{row.employeeName}</td>
            <td className={cell}>{row.departmentName || tx('advancesMissingValue')}</td>
            <td className={cell}>{a.date(row.date)}</td>
            <td className={cell} data-bidi="ltr">{a.money(row.amount)}</td>
            <td className={cell} data-bidi="ltr">{a.money(row.repaid)}</td>
            <td className={`${cell} font-bold text-mis-primary`} data-bidi="ltr">{a.money(row.remaining)}</td>
            <td className={`${cell} max-w-64 truncate`} data-tooltip={row.reason}>{row.reason}</td>
            <td className={cell}>{statusBadge(row.status)}</td>
            <td className={cell}><Button fullWidth={false} variant="outline" size="sm" leftIcon={<Eye className="h-4 w-4" />} onClick={e => { e.stopPropagation(); setSelectedAdvance(row); void loadDetails(row.id); }}>{tx('advancesViewDetails')}</Button></td>
          </tr>)}</tbody>
        </>}
      </table></div>
      {loading ? <p className="p-8 text-center text-sm text-slate-500">{tx('advancesLoading')}</p> : !rows.length && !error ? <BookEmpty title={tx('advancesNoAdvances')} /> : null}
    </div>

    <Modal
      open={!!formMode}
      onClose={closeForm}
      title={formMode === 'payment' ? tx('advancesRegisterPayment') : isCarriedPage ? tx('carriedAdvancesAddBalance') : tx('advancesNewAdvance')}
      size="lg"
    >
      <BookError error={formError} />
      {formLoading ? <p className="p-5 text-slate-500">{tx('advancesLoadingAccounts')}</p> : <form noValidate onSubmit={saveForm} className="grid gap-4 sm:grid-cols-2">
        {formMode === 'advance' ? <>
          <BookField label={tx('advancesEmployee')}><ProfessionalSelect required className="field" value={form.employeeId ?? ''} onChange={e => setForm({ ...form, employeeId: e.target.value || undefined })}><option value="">{tx('advancesSelectEmployee')}</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNumber} · {x.fullName}</option>)}</ProfessionalSelect></BookField>
          <BookField label={tx('advancesEmployeeCode')}><input dir="ltr" className="field bg-slate-50" value={employee?.employeeNumber ?? ''} readOnly /></BookField>
          <BookField label={tx('advancesDepartment')}><input className="field bg-slate-50" value={employee?.departmentName ?? ''} readOnly /></BookField>
          <BookField label={isCarriedPage ? tx('carriedAdvancesOriginalAmount') : tx('advancesAdvanceAmount')}><input required className="field" type="number" min="0.01" step="0.01" value={form.amount || ''} onChange={e => setForm({ ...form, amount: Number(e.target.value) })} /></BookField>
          <BookField label={isCarriedPage ? tx('carriedAdvancesBalanceDate') : tx('advancesAdvanceDate')}><DateControl dir="ltr" required max={cairoToday()} className="w-full" value={form.transactionDate} onChange={e => setForm({ ...form, transactionDate: e.target.value })} /></BookField>
          {isCarriedPage ? null : <BookField label={tx('advancesPaymentMethod')}><ProfessionalSelect required className="field" value={form.paymentMethod ?? ''} onChange={e => setForm({ ...form, paymentMethod: e.target.value })}><option value="">{tx('advancesSelectDisbursementMethod')}</option>{methodOptions.map(x => <option key={x[0]} value={x[0]}>{x[1]}</option>)}</ProfessionalSelect></BookField>}
          <BookField label={isCarriedPage ? tx('carriedAdvancesOpeningAccount') : tx('advancesCashBankAccount')}><ProfessionalSelect required className="field" value={form.counterAccountId} onChange={e => setForm({ ...form, counterAccountId: e.target.value })}><option value="">{tx('advancesSelectAccount')}</option>{isCarriedPage ? usableAccounts.filter(x => x.accountType === 'EQUITY').map(x => <option key={x.id} value={x.id}>{accountName(x)}</option>) : usableAccounts.filter(x => x.isCashAccount).map(x => <option key={x.id} value={x.id}>{accountName(x)}</option>)}</ProfessionalSelect></BookField>
          <BookField label={tx('advancesAccountingAccount')}><ProfessionalSelect required className="field" value={form.accountId} onChange={e => setForm({ ...form, accountId: e.target.value })}><option value="">{tx('advancesSelectAccount')}</option>{usableAccounts.filter(x => x.accountType === 'ASSET' && !x.isCashAccount).map(x => <option key={x.id} value={x.id}>{accountName(x)}</option>)}</ProfessionalSelect></BookField>
          <BookField label={isCarriedPage ? tx('carriedAdvancesReference') : `${tx('advancesReferenceNumber')} ${tx('advancesOptional')}`}><input dir="ltr" required={isCarriedPage} className="field" maxLength={160} value={form.reference ?? ''} onChange={e => setForm({ ...form, reference: e.target.value })} /></BookField>
          <div className="sm:col-span-2"><BookField label={tx('advancesReason')}><textarea required maxLength={1000} className="field min-h-20" value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} /></BookField></div>
          <div className="sm:col-span-2"><BookField label={`${tx('advancesNotes')} ${tx('advancesOptional')}`}><textarea maxLength={1000} className="field min-h-20" value={form.notes ?? ''} onChange={e => setForm({ ...form, notes: e.target.value })} /></BookField></div>
        </> : <>
          <div className="rounded-xl bg-slate-50 p-4 sm:col-span-2"><p className="font-bold text-mis-navy">{selectedAdvance?.employeeName} · {selectedAdvance?.employeeNumber}</p><p className="mt-1 text-sm text-slate-600">{tx('advancesCurrentBalance')} <strong>{a.money(selectedAdvance?.remaining ?? 0)}</strong></p></div>
          <BookField label={tx('advancesPaymentAmount')}><input required className="field" type="number" min="0.01" step="0.01" max={selectedAdvance?.remaining ?? 0} value={form.amount || ''} onChange={e => setForm({ ...form, amount: Number(e.target.value) })} /></BookField>
          <BookField label={tx('advancesPaymentDate')}><DateControl dir="ltr" required min={selectedAdvance?.date} max={cairoToday()} className="w-full" value={form.transactionDate} onChange={e => setForm({ ...form, transactionDate: e.target.value })} /></BookField>
          <BookField label={tx('advancesSettlementMethod')}><ProfessionalSelect required className="field" value={form.paymentMethod ?? ''} onChange={e => setForm({ ...form, paymentMethod: e.target.value })}><option value="">{tx('advancesSelectSettlementMethod')}</option>{methodOptions.map(x => <option key={x[0]} value={x[0]}>{x[1]}</option>)}</ProfessionalSelect></BookField>
          <BookField label={tx('advancesCashBankAccount')}><ProfessionalSelect required className="field" value={form.counterAccountId} onChange={e => setForm({ ...form, counterAccountId: e.target.value })}><option value="">{tx('advancesSelectAccount')}</option>{usableAccounts.filter(x => x.isCashAccount).map(x => <option key={x.id} value={x.id}>{accountName(x)}</option>)}</ProfessionalSelect></BookField>
          <BookField label={`${tx('advancesReferenceNumber')} ${tx('advancesOptional')}`}><input dir="ltr" className="field" maxLength={160} value={form.reference ?? ''} onChange={e => setForm({ ...form, reference: e.target.value })} /></BookField>
          <div className="sm:col-span-2"><BookField label={`${tx('advancesNotes')} ${tx('advancesOptional')}`}><textarea maxLength={1000} className="field min-h-20" value={form.notes ?? ''} onChange={e => setForm({ ...form, notes: e.target.value })} /></BookField></div>
        </>}
        <div className="flex gap-2 sm:col-span-2"><Button type="submit" fullWidth={false} isLoading={saving}>{formMode === 'payment' ? tx('advancesSavePayment') : isCarriedPage ? tx('carriedAdvancesSaveBalance') : tx('advancesSaveAdvance')}</Button><Button type="button" variant="outline" fullWidth={false} onClick={closeForm}>{tx('advancesCancel')}</Button></div>
      </form>}
    </Modal>

    <Modal open={!!selectedAdvance && !formMode} onClose={() => { setSelectedAdvance(undefined); setDetails(undefined); }} title={isCarriedPage ? tx('carriedAdvancesHistory') : tx('advancesDetails')} size="xl">
      {detailsLoading || !details ? <p className="p-5 text-slate-500">{tx('advancesLoadingDetails')}</p> : <div className="space-y-5">
        <BookError error={error} />
        <div className="flex flex-wrap items-start justify-between gap-3"><div><h3 className="text-lg font-bold text-mis-navy">{details.advance.employeeName}</h3><p className="text-sm text-slate-500"><span dir="ltr">{details.advance.employeeNumber}</span> · {details.advance.departmentName || tx('advancesMissingValue')}</p></div><div className="flex flex-wrap gap-2">{canCreateAdvance && details.advance.remaining > 0 && <Button fullWidth={false} onClick={() => void openAdvanceForm('payment', details.advance)}>{tx('advancesRegisterPayment')}</Button>}{canReverseAdvance ? <Button fullWidth={false} variant="outline" leftIcon={<RotateCcw className="h-4 w-4" />} disabled={details.payments.some(payment => payment.status === 'POSTED')} onClick={() => { setReverseReason(''); setReverseTarget({ id: details.advance.id, isPayment: false, label: details.advance.documentNumber }); }}>{tx('advancesVoid')}</Button> : null}</div></div>
        {details.payments.some(payment => payment.status === 'POSTED') ? <p className="rounded-xl bg-amber-50 p-3 text-sm text-amber-800">{tx('advancesVoidWarning')}</p> : null}
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">{[
          [tx('advancesOriginalAmount'), a.money(details.advance.amount)],
          [tx('advancesAdvanceDate'), a.date(details.advance.date)],
          [tx('advancesTotalPaid'), a.money(details.advance.repaid)],
          [tx('advancesRemaining'), a.money(details.advance.remaining)],
          [tx('advancesEmployeeOutstanding'), a.money(details.employeeTotalOutstanding)],
          [tx('advancesStatusLabel'), statusBadge(details.advance.status)],
          [tx('advancesPaymentMethod'), methodName(details.advance.paymentMethod)],
          [tx('advancesReferenceNumber'), details.advance.reference || tx('advancesMissingValue')],
        ].map(([label, value]) => <div key={String(label)} className="rounded-xl border border-mis-border p-3"><p className="text-xs text-slate-500">{label}</p><div className="mt-1 font-semibold text-mis-navy" data-bidi="ltr">{value}</div></div>)}</div>
        <div className="grid gap-3 rounded-xl bg-slate-50 p-4 sm:grid-cols-2"><div><p className="text-xs text-slate-500">{tx('advancesReason')}</p><p className="mt-1 font-medium">{details.advance.reason}</p></div><div><p className="text-xs text-slate-500">{tx('advancesNotes')}</p><p className="mt-1">{details.advance.notes || tx('advancesMissingValue')}</p></div><div className="text-xs text-slate-500">{tx('advancesCreatedBy')} <strong className="text-slate-700">{details.advance.createdBy}</strong> · {tx('advancesCreatedAt')} {dateTime(details.advance.createdAt)}</div></div>
        <div><h4 className="mb-3 font-bold text-mis-navy">{tx('advancesPaymentHistory')}</h4><div className="overflow-x-auto rounded-xl border border-mis-border"><table className={table}><thead><tr>{[tx('advancesPaymentDate'), tx('advancesPaymentAmount'), a.text('الحالة', 'Status'), tx('advancesSettlementMethod'), tx('advancesReferenceNumber'), tx('advancesNotes'), tx('advancesHistoryCreatedByAt'), tx('advancesBalanceAfterPayment'), ...(canReverseAdvance ? [tx('advancesActions')] : [])].map((label, i) => <th key={i} className={cell}>{label}</th>)}</tr></thead><tbody>{details.payments.map(payment => <tr key={payment.id}><td className={cell}>{a.date(payment.date)}</td><td className={cell} data-bidi="ltr">{a.money(payment.amount)}</td><td className={cell}><AccountingStatus value={payment.status} /></td><td className={cell}>{methodName(payment.paymentMethod)}</td><td className={cell} dir="ltr">{payment.reference || tx('advancesMissingValue')}</td><td className={cell}>{payment.notes || tx('advancesMissingValue')}</td><td className={cell}><p>{payment.createdBy}</p><p className="text-xs text-slate-500">{dateTime(payment.createdAt)}</p></td><td className={cell} data-bidi="ltr">{a.money(payment.remainingAfter)}</td>{canReverseAdvance ? <td className={cell}>{payment.status === 'POSTED' ? <Button fullWidth={false} size="sm" variant="ghost" onClick={() => { setReverseReason(''); setReverseTarget({ id: payment.id, isPayment: true, label: a.money(payment.amount) }); }}>{tx('advancesReversePayment')}</Button> : null}</td> : null}</tr>)}</tbody></table>{!details.payments.length ? <BookEmpty title={tx('advancesNoPayments')}><span>{tx('advancesNoPaymentsHelp')}</span></BookEmpty> : null}</div></div>
      </div>}
    </Modal>

    <ConfirmDialog open={!!reverseTarget} title={reverseTarget?.isPayment ? tx('advancesConfirmPaymentReversal') : tx('advancesConfirmVoid')} confirmLabel={tx('advancesConfirmReversal')} confirmVariant="danger" isConfirming={reversing} onCancel={() => setReverseTarget(undefined)} onConfirm={() => void reverse()} message={<div className="space-y-3"><p>{reverseTarget?.label}</p><p>{tx('advancesReversalHelp')}</p><BookField label={tx('advancesReversalReason')}><textarea required minLength={3} maxLength={1000} className="field min-h-20" value={reverseReason} onChange={e => setReverseReason(e.target.value)} /></BookField><BookError error={error} /></div>} />
  </div>;
}
