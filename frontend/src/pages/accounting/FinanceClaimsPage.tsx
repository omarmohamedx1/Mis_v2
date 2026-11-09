import { Download, FileUp, Plus, Trash2 } from 'lucide-react';
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { Button } from '../../components/common/Button';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { ActionMenu } from '../../components/common/ActionMenu';
import { DateControl } from '../../components/forms/DateControl';
import { useAuth } from '../../context/AuthContext';
import { useAccountingText } from '../../features/accounting/accountingUi';
import { BookError, BookField, BookHeader, cell, table } from '../../features/accounting/bookUi';
import { financeClaimsService } from '../../features/accounting/services/financeClaimsService';
import type { Claim, ClaimDetails, ClaimImportPreview, ClaimImportResult, ClaimInput, ClaimOrganization } from '../../features/accounting/types/claims';
import { getApiErrorMessage } from '../../services/apiClient';

const today = () => new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Africa/Cairo', year: 'numeric', month: '2-digit', day: '2-digit',
}).format(new Date());

type ClaimOrganizationCategory = 'BANK' | 'INSTALLMENT';

const emptyInput = (): ClaimInput => ({
  bankName: '', claimType: '', month: today().slice(0, 7), claimDate: today(),
  amountWithoutVat: 0, vatPercent: 14, taxAtSourcePercent: 0,
});

function FieldInput(props: React.InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={`field ${props.className ?? ''}`} />;
}

export function FinanceClaimsPage() {
  const a = useAccountingText();
  const { user } = useAuth();
  const can = (permission: string) => Boolean(
    user?.roles.includes('Admin') || user?.permissions.includes('*') || user?.permissions.includes(permission),
  );
  const [claims, setClaims] = useState<Claim[]>([]);
  const [organizations, setOrganizations] = useState<ClaimOrganization[]>([]);
  const [organizationsLoading, setOrganizationsLoading] = useState(false);
  const [organizationsError, setOrganizationsError] = useState('');
  const [organizationCategory, setOrganizationCategory] = useState<ClaimOrganizationCategory>('BANK');
  const [filterOrganizationId, setFilterOrganizationId] = useState('');
  const [statusFilter, setStatusFilter] = useState<'ACTIVE' | 'CANCELLED' | 'ALL'>('ACTIVE');
  const [search, setSearch] = useState('');
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [refresh, setRefresh] = useState(0);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [statusMessage, setStatusMessage] = useState('');
  const [editing, setEditing] = useState<Claim>();
  const [formOpen, setFormOpen] = useState(false);
  const [form, setForm] = useState<ClaimInput>(emptyInput);
  const [details, setDetails] = useState<ClaimDetails>();
  const [receiptOpen, setReceiptOpen] = useState(false);
  const [receiptDate, setReceiptDate] = useState('');
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);
  const [cancelConfirmOpen, setCancelConfirmOpen] = useState(false);
  const [reactivateConfirmOpen, setReactivateConfirmOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<Claim>();
  const [preview, setPreview] = useState<ClaimImportPreview>();
  const [importResult, setImportResult] = useState<ClaimImportResult>();
  const [exporting, setExporting] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  const calculated = useMemo(() => {
    const base = Number(form.amountWithoutVat) || 0;
    const vat = Math.round((base * (Number(form.vatPercent) || 0) + Number.EPSILON) * 100) / 100;
    const gross = Math.round((base + vat + Number.EPSILON) * 100) / 100;
    const withholding = Math.round((base * (Number(form.taxAtSourcePercent) || 0) + Number.EPSILON) * 100) / 100;
    const net = Math.round((gross - withholding + Number.EPSILON) * 100) / 100;
    return { vat, gross, withholding, net };
  }, [form.amountWithoutVat, form.vatPercent, form.taxAtSourcePercent]);

  const errorMessage = (exception: unknown, arabic: string, english: string) => {
    const message = getApiErrorMessage(exception, a.text(arabic, english));
    return a.ar && !/[\u0600-\u06ff]/i.test(message) ? arabic : message;
  };

  useEffect(() => {
    let active = true;
    setLoading(true);
    financeClaimsService.list({ page, pageSize: 200, bankOrganizationId: filterOrganizationId || undefined, status: statusFilter, search: search.trim() || undefined })
      .then(result => {
        if (!active) return;
        setClaims(result.items);
        setTotalCount(result.totalCount);
      })
      .catch(exception => {
        if (active) {
          setClaims([]);
          setError(errorMessage(exception, 'تعذر تحميل المطالبات', 'Could not load claims'));
        }
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [a.ar, filterOrganizationId, page, refresh, search, statusFilter]);

  useEffect(() => {
    let active = true;
    setOrganizationsLoading(true);
    setOrganizationsError('');
    financeClaimsService.organizations()
      .then(result => {
        if (!active) return;
        if (!Array.isArray(result)) throw new Error('The organization directory response is invalid.');
        const valid = result.filter(organization =>
          typeof organization.id === 'string'
          && typeof organization.nameArabic === 'string'
          && typeof organization.nameEnglish === 'string'
          && typeof organization.organizationType === 'string'
          && claimOrganizationCategory(organization.organizationType) !== null);
        if (valid.length !== result.length) throw new Error('The organization directory response contains unsupported organization types.');
        setOrganizations(valid);
      })
      .catch(exception => {
        if (!active) return;
        setOrganizations([]);
        setOrganizationsError(exception instanceof Error && exception.message.startsWith('The organization directory')
          ? a.text('استجابة دليل الجهات غير صالحة. أعد تحميل الصفحة أو تواصل مع الدعم.', 'The organization directory response is invalid. Reload the page or contact support.')
          : errorMessage(exception, 'تعذر تحميل دليل البنوك وشركات التقسيط', 'Could not load banks and installment companies'));
      })
      .finally(() => { if (active) setOrganizationsLoading(false); });
    return () => { active = false; };
  }, [a.ar]);

  const reload = () => setRefresh(value => value + 1);
  const startCreate = () => {
    setEditing(undefined);
    setOrganizationCategory('BANK');
    setForm(emptyInput());
    setError('');
    setFormOpen(true);
  };
  const startEdit = (claim: Claim) => {
    setEditing(claim);
    const organization = organizations.find(item => item.id === claim.bankOrganizationId);
    setOrganizationCategory(organization && claimOrganizationCategory(organization.organizationType) === 'INSTALLMENT' ? 'INSTALLMENT' : 'BANK');
    setForm({
      serial: claim.serial, bankOrganizationId: claim.bankOrganizationId, bankName: claim.bankName,
      claimType: claim.claimType, month: claim.month, claimDate: claim.claimDate,
      amountWithoutVat: claim.amountWithoutVat, vatPercent: claim.vatPercent,
      taxAtSourcePercent: claim.taxAtSourcePercent, dateInBank: claim.dateInBank,
    });
    setDetails(undefined);
    setError('');
    setFormOpen(true);
  };

  useEffect(() => {
    if (!formOpen || !editing?.bankOrganizationId) return;
    const organization = organizations.find(item => item.id === editing.bankOrganizationId);
    if (organization) setOrganizationCategory(claimOrganizationCategory(organization.organizationType) ?? 'BANK');
  }, [editing, formOpen, organizations]);

  const submitForm = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      if (editing) await financeClaimsService.update(editing.id, form);
      else await financeClaimsService.create(form);
      setFormOpen(false);
      setEditing(undefined);
      reload();
    } catch (exception) {
      setError(errorMessage(exception, 'تعذر حفظ المطالبة', 'Could not save claim'));
    } finally {
      setBusy(false);
    }
  };

  const showDetails = async (claim: Claim) => {
    setBusy(true);
    setError('');
    try {
      setDetails(await financeClaimsService.details(claim.id));
    } catch (exception) {
      setError(errorMessage(exception, 'تعذر تحميل المطالبة', 'Could not load claim'));
    } finally {
      setBusy(false);
    }
  };

  const confirmReceipt = async (claim: Claim, historical: boolean) => {
    setBusy(true);
    setError('');
    try {
      if (historical) await financeClaimsService.postHistoricalReceipt(claim.id);
      else await financeClaimsService.confirmReceipt(claim.id, receiptDate);
      setReceiptOpen(false);
      setDetails(await financeClaimsService.details(claim.id));
      reload();
    } catch (exception) {
      setError(errorMessage(exception, 'تعذر تأكيد التحصيل', 'Could not confirm receipt'));
    } finally {
      setBusy(false);
    }
  };

  const deleteClaim = async () => {
    if (!deleteTarget) return;
    setBusy(true);
    setError('');
    try {
      await financeClaimsService.delete(deleteTarget.id);
      setDeleteConfirmOpen(false);
      setDeleteTarget(undefined);
      setDetails(undefined);
      reload();
      setStatusMessage(a.text('تم حذف المطالبة من القائمة النشطة مع الاحتفاظ بسجلها المالي.', 'Claim removed from the active list; its financial history was retained.'));
    } catch (exception) {
      setError(errorMessage(exception, 'تعذر حذف المطالبة. إذا كان لها تحصيل مرحل، اعكس التحصيل أولاً.', 'Could not delete the claim. Reverse any posted receipt first.'));
    } finally {
      setBusy(false);
    }
  };

  const cancelClaim = async () => {
    if (!selectedClaim) return;
    setBusy(true);
    setError('');
    try {
      await financeClaimsService.cancel(selectedClaim.id);
      setCancelConfirmOpen(false);
      setDetails(undefined);
      setPage(1);
      setStatusMessage(a.text('تم إلغاء المطالبة دون تعديل أي قيود مالية.', 'Claim cancelled without changing any financial entries.'));
      reload();
    } catch (exception) {
      setError(errorMessage(exception, 'تعذر إلغاء المطالبة. إذا كان لها تحصيل مرحل، اعكسه من إجراء التحصيل أولاً.', 'Could not cancel the claim. Reverse any posted receipt through the receipt workflow first.'));
    } finally {
      setBusy(false);
    }
  };

  const reactivateClaim = async () => {
    if (!selectedClaim) return;
    setBusy(true);
    setError('');
    try {
      await financeClaimsService.reactivate(selectedClaim.id);
      setReactivateConfirmOpen(false);
      setDetails(undefined);
      setPage(1);
      setStatusMessage(a.text('تمت إعادة تفعيل المطالبة.', 'Claim reactivated.'));
      reload();
    } catch (exception) {
      setError(errorMessage(exception, 'تعذر إعادة تفعيل المطالبة. راجع القيود المالية المرتبطة بها.', 'Could not reactivate the claim. Review its related financial entries.'));
    } finally {
      setBusy(false);
    }
  };

  const chooseFile = async (file?: File) => {
    if (!file) return;
    setBusy(true);
    setError('');
    setImportResult(undefined);
    try {
      setPreview(await financeClaimsService.previewImport(file));
    } catch (exception) {
      setError(errorMessage(exception, 'تعذر قراءة ملف Excel', 'Could not read the Excel file'));
    } finally {
      setBusy(false);
      if (fileInput.current) fileInput.current.value = '';
    }
  };

  const confirmImport = async () => {
    if (!preview) return;
    setBusy(true);
    setError('');
    try {
      setImportResult(await financeClaimsService.confirmImport(preview.previewId));
      setPreview(undefined);
      setPage(1);
      reload();
    } catch (exception) {
      setError(errorMessage(exception, 'تعذر استيراد المطالبات', 'Could not import claims'));
    } finally {
      setBusy(false);
    }
  };

  const exportRows = async () => {
    setExporting(true);
    setError('');
    try {
      await financeClaimsService.export({ bankOrganizationId: filterOrganizationId || undefined, status: statusFilter, search: search.trim() || undefined });
    } catch (exception) {
      setError(errorMessage(exception, 'تعذر تصدير المطالبات', 'Could not export claims'));
    } finally {
      setExporting(false);
    }
  };

  const formatDate = (value?: string) => value ? a.date(value) : '—';
  const organizationLabel = (organization: ClaimOrganization) => a.ar ? organization.nameArabic : organization.nameEnglish;
  const banks = organizations.filter(organization => claimOrganizationCategory(organization.organizationType) === 'BANK');
  const installmentCompanies = organizations.filter(organization => claimOrganizationCategory(organization.organizationType) === 'INSTALLMENT');
  const formOrganizations = organizationCategory === 'BANK' ? banks : installmentCompanies;
  const localizedImportMessage = (message: string) => {
    if (!a.ar) return message;
    if (message.includes('Possible duplicate claim')) return 'قد تكون المطالبة مكررة؛ ستُتخطى للمراجعة.';
    if (message.includes('differs from the recalculated amount')) return 'تختلف قيمة Excel عن المبلغ المعاد حسابه؛ ستُستخدم القيمة المعاد حسابها.';
    const translations: Record<string, string> = {
      'Bank Name is required.': 'اسم البنك / شركة التقسيط مطلوب.',
      'Bank / Installment Company is required.': 'اسم البنك / شركة التقسيط مطلوب.',
      'The type of Claim is required.': 'نوع المطالبة مطلوب.',
      'Month is required.': 'الشهر مطلوب.',
      'Date of claim is missing or invalid.': 'تاريخ المطالبة مفقود أو غير صالح.',
      'Total claim without VAT must be positive.': 'إجمالي المطالبة بدون ضريبة يجب أن يكون موجباً.',
      'VAT % must be between 0 and 100.': 'نسبة ضريبة القيمة المضافة يجب أن تكون بين 0 و100.',
      'Tax at source % must be between 0 and 100.': 'نسبة ضريبة المنبع يجب أن تكون بين 0 و100.',
      'Date of claim is outside the supported date range.': 'تاريخ المطالبة خارج النطاق المسموح.',
      'Total claim without VAT must have at most two decimal places.': 'يُسمح بمنزلتين عشريتين كحد أقصى لمبلغ المطالبة.',
      'Tax percentages must have at most four decimal places.': 'يُسمح بأربع منازل عشرية كحد أقصى للنسب.',
      'Date In Bank is outside the supported date range.': 'تاريخ دخول البنك خارج النطاق المسموح.',
      'Date In Bank cannot be in the future.': 'لا يمكن أن يكون تاريخ دخول البنك في المستقبل.',
    };
    return translations[message] ?? message;
  };

  const columns = [
    ['مسلسل', 'S / Serial'],
    ['البنك / شركة التقسيط', 'Bank / Installment Company'],
    ['نوع المطالبة', 'Claim Type'],
    ['الشهر', 'Month'],
    ['تاريخ المطالبة', 'Claim Date'],
    ['إجمالي المطالبة بدون ضريبة القيمة المضافة', 'Total Claim Without VAT'],
    ['نسبة ضريبة القيمة المضافة', 'VAT %'],
    ['قيمة ضريبة القيمة المضافة', 'VAT'],
    ['إجمالي المطالبة شامل ضريبة القيمة المضافة', 'Total Claim With VAT'],
    ['نسبة ضريبة المنبع', 'Tax at Source %'],
    ['قيمة ضريبة المنبع', 'Tax at Source'],
    ['الإجمالي', 'Total'],
    ['صافي المبلغ بالبنك', 'Total in Bank'],
    ['تاريخ دخول المبلغ البنك', 'Date In Bank'],
  ] as const;

  const claimValues = (claim: Claim) => [
    claim.serial || '—', claim.bankName, claim.claimType, claim.month, formatDate(claim.claimDate),
    a.money(claim.amountWithoutVat), `${a.number(claim.vatPercent)}%`, a.money(claim.vat),
    a.money(claim.totalWithVat), `${a.number(claim.taxAtSourcePercent)}%`, a.money(claim.taxAtSource),
    a.money(claim.total), a.money(claim.totalInBank), formatDate(claim.dateInBank),
  ];

  const previewValues = (row: ClaimImportPreview['rows'][number]) => [
    row.serial || '—', row.bankName || '—', row.claimType || '—', row.month || '—', formatDate(row.claimDate),
    row.amountWithoutVat == null ? '—' : a.money(row.amountWithoutVat),
    row.vatPercent == null ? '—' : `${a.number(row.vatPercent)}%`,
    row.calculatedVat == null ? '—' : a.money(row.calculatedVat),
    row.calculatedTotalWithVat == null ? '—' : a.money(row.calculatedTotalWithVat),
    row.taxAtSourcePercent == null ? '—' : `${a.number(row.taxAtSourcePercent)}%`,
    row.calculatedTaxAtSource == null ? '—' : a.money(row.calculatedTaxAtSource),
    row.calculatedTotal == null ? '—' : a.money(row.calculatedTotal),
    row.calculatedTotalInBank == null ? '—' : a.money(row.calculatedTotalInBank),
    formatDate(row.dateInBank),
  ];

  const selectedClaim = details?.claim;
  const isHistoricalUnposted = Boolean(selectedClaim?.isHistoricalImport && selectedClaim.status === 'RECEIVED' && !selectedClaim.hasAccountingPosting);
  const canConfirmReceipt = Boolean(can('finance.claims.receipt.confirm') && selectedClaim?.status === 'PENDING');

  return <div dir={a.ar ? 'rtl' : 'ltr'} className="space-y-5">
    <BookHeader title={a.text('المطالبات', 'Claims')} eyebrow={a.text('المحاسبة والمالية', 'ACCOUNTING & FINANCE')} description={a.text('سجل المطالبات وفق أعمدة Excel.', 'Claims register matching the Excel columns.')}>
      {can('finance.claims.export') && <Button fullWidth={false} variant="outline" leftIcon={<Download />} isLoading={exporting} onClick={() => void exportRows()}>{a.text('تصدير Excel', 'Export Excel')}</Button>}
      {can('finance.claims.import') && <Button fullWidth={false} variant="outline" leftIcon={<FileUp />} onClick={() => fileInput.current?.click()}>{a.text('استيراد Excel', 'Import Excel')}</Button>}
      {can('finance.claims.create') && <Button fullWidth={false} leftIcon={<Plus />} onClick={startCreate}>{a.text('مطالبة جديدة', 'New Claim')}</Button>}
      <input ref={fileInput} className="hidden" type="file" accept=".xlsx,.xls" onChange={event => void chooseFile(event.target.files?.[0])} />
    </BookHeader>
    <BookError error={error} />
    {statusMessage && <p role="status" className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">{statusMessage}</p>}

    <div className="grid max-w-5xl gap-4 md:grid-cols-3">
      <BookField label={a.text('حالة المطالبات', 'Claim status')}>
        <select className="field" value={statusFilter} onChange={event => { setStatusFilter(event.target.value as 'ACTIVE' | 'CANCELLED' | 'ALL'); setPage(1); }}>
          <option value="ACTIVE">{a.text('المطالبات النشطة', 'Active Claims')}</option>
          <option value="CANCELLED">{a.text('المطالبات الملغاة', 'Cancelled Claims')}</option>
          <option value="ALL">{a.text('كل المطالبات', 'All Claims')}</option>
        </select>
      </BookField>
      <BookField label={a.text('بحث', 'Search claims')}>
        <FieldInput value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} placeholder={a.text('البنك أو نوع المطالبة أو الشهر أو المسلسل', 'Bank, type, month, or serial')} />
      </BookField>
      <BookField label={a.text('البنك / شركة التقسيط', 'Bank / Installment Company')}>
        <select className="field" onChange={event => { setFilterOrganizationId(event.target.value); setPage(1); }} value={filterOrganizationId}>
          <option value="">{a.text('كل الجهات', 'All organizations')}</option>
          <optgroup label={a.text('البنوك', 'Banks')}>
            {banks.map(organization => <option key={organization.id} value={organization.id}>{organizationLabel(organization)}</option>)}
          </optgroup>
          <optgroup label={a.text('شركات التقسيط', 'Installment Companies')}>
            {installmentCompanies.map(organization => <option key={organization.id} value={organization.id}>{organizationLabel(organization)}</option>)}
          </optgroup>
        </select>
      </BookField>
    </div>

    <section className="overflow-hidden rounded-2xl border border-mis-border bg-white">
      {loading && claims.length === 0 ? <div className="grid min-h-48 place-items-center"><LoadingSpinner /></div> : <div className="overflow-x-auto">
        <table className={`${table} min-w-[1880px]`}>
          <thead><tr>{columns.map(([arabic, english]) => <th className={cell} key={english}>{a.text(arabic, english)}</th>)}<th className={cell}>{a.text('الحالة', 'Status')}</th><th className={cell}>{a.text('الإجراءات', 'Actions')}</th></tr></thead>
          <tbody>
            {claims.map(claim => <tr key={claim.id} tabIndex={0} className="cursor-pointer hover:bg-slate-50" onClick={() => void showDetails(claim)} onKeyDown={event => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                void showDetails(claim);
              }
            }}>
              {claimValues(claim).map((value, index) => <td className={`${cell} whitespace-nowrap ${index >= 5 && index <= 12 ? 'tabular-nums' : ''}`} data-bidi={index >= 5 && index <= 12 ? 'ltr' : undefined} key={index}>{value}</td>)}
              <td className={cell}><span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${claim.status === 'CANCELLED' ? 'bg-slate-100 text-slate-700' : claim.status === 'RECEIVED' ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-800'}`}>{claim.status === 'CANCELLED' ? a.text('ملغاة', 'Cancelled') : claim.status === 'RECEIVED' ? a.text('تم التحصيل', 'Received') : a.text('نشطة', 'Active')}</span></td>
              <td className={cell} onClick={event => event.stopPropagation()}>
                <ActionMenu label={a.text('إجراءات المطالبة', 'Claim actions')}>
                  <button type="button" onClick={() => void showDetails(claim)}>{a.text('عرض التفاصيل', 'View details')}</button>
                  {can('finance.claims.delete') && !claim.hasAccountingPosting && <button type="button" className="text-rose-700" onClick={() => { setDeleteTarget(claim); setDeleteConfirmOpen(true); }}>{a.text('حذف المطالبة', 'Delete Claim')}</button>}
                </ActionMenu>
              </td>
            </tr>)}
            {!loading && claims.length === 0 && <tr><td className={`${cell} py-12 text-center text-slate-500`} colSpan={16}>{a.text('لا توجد مطالبات', 'No claims')}</td></tr>}
          </tbody>
        </table>
      </div>}
      <div className="flex items-center justify-between border-t border-mis-border p-3 text-sm">
        <span className="text-slate-500">{a.number(totalCount)}</span>
        <div className="flex gap-2">
          <Button fullWidth={false} size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(value => value - 1)}>{a.text('السابق', 'Previous')}</Button>
          <span className="self-center">{a.number(page)}</span>
          <Button fullWidth={false} size="sm" variant="outline" disabled={page * 200 >= totalCount} onClick={() => setPage(value => value + 1)}>{a.text('التالي', 'Next')}</Button>
        </div>
      </div>
    </section>
    {importResult && <p role="status" className="text-sm text-slate-600">
      {a.text(
        `تم الاستيراد: ${a.number(importResult.imported)}، تم التخطي: ${a.number(importResult.skipped)}، أخطاء: ${a.number(importResult.failed)}.`,
        `Imported: ${a.number(importResult.imported)}, skipped: ${a.number(importResult.skipped)}, failed: ${a.number(importResult.failed)}.`,
      )}
    </p>}

    <Modal open={formOpen} onClose={() => setFormOpen(false)} title={editing ? a.text('تعديل مطالبة', 'Edit Claim') : a.text('مطالبة جديدة', 'New Claim')} size="xl"
      footer={<><Button fullWidth={false} variant="outline" onClick={() => setFormOpen(false)}>{a.text('إغلاق', 'Close')}</Button><Button fullWidth={false} isLoading={busy} form="claim-form" type="submit">{a.text('حفظ', 'Save')}</Button></>}>
      <form id="claim-form" className="grid gap-4 sm:grid-cols-2" onSubmit={event => void submitForm(event)}>
        <BookField label={a.text('مسلسل', 'S / Serial')}><FieldInput maxLength={40} value={form.serial ?? ''} onChange={event => setForm(value => ({ ...value, serial: event.target.value || undefined }))} /></BookField>
        <BookField label={a.text('نوع الجهة', 'Organization Type')}>
          <select
            className="field"
            disabled={organizationsLoading || Boolean(organizationsError)}
            value={organizationCategory}
            onChange={event => {
              const category: ClaimOrganizationCategory = event.target.value === 'INSTALLMENT' ? 'INSTALLMENT' : 'BANK';
              setOrganizationCategory(category);
              setForm(value => ({ ...value, bankOrganizationId: undefined, bankName: '' }));
            }}
          >
            <option value="BANK">{a.text('البنوك', 'Banks')}</option>
            <option value="INSTALLMENT">{a.text('شركات التقسيط', 'Installment Companies')}</option>
          </select>
        </BookField>
        <BookField label={a.text('البنك / شركة التقسيط', 'Bank / Installment Company')}>
          <select
            className="field"
            disabled={organizationsLoading || Boolean(organizationsError) || formOrganizations.length === 0}
            value={form.bankOrganizationId ?? ''}
            onChange={event => {
              const organization = formOrganizations.find(item => item.id === event.target.value);
              setForm(value => ({
                ...value,
                bankOrganizationId: organization?.id,
                bankName: organization ? organizationLabel(organization) : '',
              }));
            }}
            required
          >
            <option value="">{a.text('اختر البنك / شركة التقسيط', 'Select a bank / installment company')}</option>
            {editing?.bankOrganizationId && !organizations.some(item => item.id === editing.bankOrganizationId) && (
              <option value={editing.bankOrganizationId}>{editing.bankName}</option>
            )}
            {formOrganizations.map(organization => (
              <option key={organization.id} value={organization.id}>{organizationLabel(organization)}</option>
            ))}
          </select>
          {organizationsLoading ? <p role="status" className="mt-1 text-xs text-slate-500">{a.text('جارٍ تحميل الجهات...', 'Loading organizations...')}</p> : null}
          {!organizationsLoading && !organizationsError && formOrganizations.length === 0
            ? <p role="status" className="mt-1 text-xs text-amber-700">{a.text('لا توجد جهات نشطة من هذا النوع في دليل التحصيل.', 'No active organizations of this type were found in Collections.')}</p>
            : null}
          {organizationsError ? <p role="alert" className="mt-1 text-xs text-rose-700">{organizationsError}</p> : null}
        </BookField>
        <BookField label={a.text('نوع المطالبة', 'Claim Type')}><FieldInput value={form.claimType} onChange={event => setForm(value => ({ ...value, claimType: event.target.value }))} required /></BookField>
        <BookField label={a.text('الشهر', 'Month')}><FieldInput value={form.month} onChange={event => setForm(value => ({ ...value, month: event.target.value }))} required /></BookField>
        <BookField label={a.text('تاريخ المطالبة', 'Claim Date')}><DateControl value={form.claimDate} onChange={event => setForm(value => ({ ...value, claimDate: event.target.value }))} required /></BookField>
        <BookField label={a.text('إجمالي المطالبة بدون ضريبة القيمة المضافة', 'Total Claim Without VAT')}><FieldInput type="number" min="0.01" step="0.01" value={form.amountWithoutVat} onChange={event => setForm(value => ({ ...value, amountWithoutVat: Number(event.target.value) }))} required /></BookField>
        <BookField label={a.text('نسبة ضريبة القيمة المضافة', 'VAT %')}><FieldInput type="number" min="0" max="100" step="0.0001" value={form.vatPercent} onChange={event => setForm(value => ({ ...value, vatPercent: Number(event.target.value) }))} required /></BookField>
        <BookField label={a.text('قيمة ضريبة القيمة المضافة', 'VAT')}><FieldInput readOnly value={calculated.vat.toFixed(2)} /></BookField>
        <BookField label={a.text('إجمالي المطالبة شامل ضريبة القيمة المضافة', 'Total Claim With VAT')}><FieldInput readOnly value={calculated.gross.toFixed(2)} /></BookField>
        <BookField label={a.text('نسبة ضريبة المنبع', 'Tax at Source %')}><FieldInput type="number" min="0" max="100" step="0.0001" value={form.taxAtSourcePercent} onChange={event => setForm(value => ({ ...value, taxAtSourcePercent: Number(event.target.value) }))} required /></BookField>
        <BookField label={a.text('قيمة ضريبة المنبع', 'Tax at Source')}><FieldInput readOnly value={calculated.withholding.toFixed(2)} /></BookField>
        <BookField label={a.text('الإجمالي', 'Total')}><FieldInput readOnly value={calculated.net.toFixed(2)} /></BookField>
        <BookField label={a.text('صافي المبلغ بالبنك', 'Total in Bank')}><FieldInput readOnly value={calculated.net.toFixed(2)} /></BookField>
        <BookField label={a.text('تاريخ دخول المبلغ البنك', 'Date In Bank')}><DateControl value={form.dateInBank ?? ''} onChange={event => setForm(value => ({ ...value, dateInBank: event.target.value || undefined }))} /></BookField>
      </form>
    </Modal>

    <Modal open={!!details} onClose={() => setDetails(undefined)} title={a.text('المطالبة', 'Claim')} size="xl"
      footer={<div className="flex flex-wrap justify-end gap-2">
        {can('finance.claims.edit') && selectedClaim?.status !== 'CANCELLED' && <Button fullWidth={false} variant="outline" disabled={busy} onClick={() => selectedClaim && startEdit(selectedClaim)}>{a.text('تعديل', 'Edit')}</Button>}
        {can('finance.claims.cancel') && selectedClaim?.status !== 'CANCELLED' && <Button fullWidth={false} variant="danger" disabled={busy} onClick={() => setCancelConfirmOpen(true)}>{a.text('إلغاء المطالبة', 'Cancel Claim')}</Button>}
        {can('finance.claims.cancel') && selectedClaim?.status === 'CANCELLED' && <Button fullWidth={false} disabled={busy} onClick={() => setReactivateConfirmOpen(true)}>{a.text('إعادة تفعيل المطالبة', 'Reactivate Claim')}</Button>}
        {can('finance.claims.delete') && selectedClaim && !selectedClaim.hasAccountingPosting && <Button fullWidth={false} variant="danger" leftIcon={<Trash2 size={16} />} disabled={busy} onClick={() => { setDeleteTarget(selectedClaim); setDeleteConfirmOpen(true); }}>{a.text('حذف المطالبة', 'Delete Claim')}</Button>}
        {canConfirmReceipt && <Button fullWidth={false} onClick={() => {
          if (!selectedClaim) return;
          setReceiptDate(selectedClaim.dateInBank ?? today());
          setReceiptOpen(true);
        }}>{a.text('تأكيد التحصيل', 'Confirm Receipt')}</Button>}
        {isHistoricalUnposted && can('finance.claims.receipt.confirm') && <Button fullWidth={false} onClick={() => selectedClaim && void confirmReceipt(selectedClaim, true)}>{a.text('ترحيل التحصيل التاريخي', 'Post Historical Receipt')}</Button>}
        <Button fullWidth={false} variant="outline" onClick={() => setDetails(undefined)}>{a.text('إغلاق', 'Close')}</Button>
      </div>}>
      {details && <div className="space-y-4">
        {selectedClaim?.status === 'RECEIVED' && <p className="rounded-xl bg-slate-50 p-3 text-sm text-slate-600">{isHistoricalUnposted
          ? a.text('هذا سجل تاريخي؛ لم يُرحّل إلى وارد الشركة. استخدم الإجراء الصريح للترحيل عند التحقق.', 'This historical receipt has not been posted to Company Income. Use the explicit action to post it after review.')
          : a.text('تم تأكيد التحصيل وترحيل صافي المبلغ إلى وارد الشركة.', 'Receipt confirmed; the net amount was posted to Company Income.')}</p>}
        <div className="grid gap-4 rounded-xl bg-slate-50 p-4 sm:grid-cols-2">
        {claimValues(details.claim).map((value, index) => {
          const [arabic, english] = columns[index];
          return <div key={english} className="min-w-0">
            <p className="text-xs font-medium text-slate-500">{a.text(arabic, english)}</p>
            <p className="mt-1 break-words font-semibold text-slate-800" data-bidi={index >= 5 && index <= 12 ? 'ltr' : undefined}>{value}</p>
          </div>;
        })}
        </div>
      </div>}
    </Modal>

    <Modal open={cancelConfirmOpen} onClose={() => !busy && setCancelConfirmOpen(false)} title={a.text('تأكيد إلغاء المطالبة', 'Confirm Cancel Claim')} size="sm"
      footer={<div className="flex justify-end gap-2">
        <Button fullWidth={false} variant="outline" disabled={busy} onClick={() => setCancelConfirmOpen(false)}>{a.text('رجوع', 'Keep Claim')}</Button>
        <Button fullWidth={false} variant="danger" isLoading={busy} onClick={() => void cancelClaim()}>{a.text('إلغاء المطالبة', 'Cancel Claim')}</Button>
      </div>}>
      <BookError error={error} />
      <p className="text-sm text-slate-600">{a.text(
        'سيتم إخفاء المطالبة من القائمة النشطة مع الاحتفاظ بسجلها. لن يتم تعديل أي قيد مالي. لا يمكن الإلغاء ما دام هناك تحصيل مرحل غير معكوس.',
        'The claim will leave the active list while its history is retained. No financial entries will be changed. Cancellation is blocked while a receipt posting remains active.',
      )}</p>
    </Modal>

    <Modal open={reactivateConfirmOpen} onClose={() => !busy && setReactivateConfirmOpen(false)} title={a.text('تأكيد إعادة التفعيل', 'Confirm Reactivation')} size="sm"
      footer={<div className="flex justify-end gap-2">
        <Button fullWidth={false} variant="outline" disabled={busy} onClick={() => setReactivateConfirmOpen(false)}>{a.text('رجوع', 'Cancel')}</Button>
        <Button fullWidth={false} isLoading={busy} onClick={() => void reactivateClaim()}>{a.text('إعادة تفعيل المطالبة', 'Reactivate Claim')}</Button>
      </div>}>
      <BookError error={error} />
      <p className="text-sm text-slate-600">{a.text(
        'ستعود المطالبة إلى قائمة المطالبات النشطة. سيبقى سجل الإلغاء والتدقيق محفوظًا، ولن يتم تعديل أي قيد مالي.',
        'This claim will return to the active list. Cancellation and audit history remain intact, and no financial entries will be changed.',
      )}</p>
    </Modal>

    <Modal open={deleteConfirmOpen} onClose={() => !busy && setDeleteConfirmOpen(false)} title={a.text('تأكيد حذف المطالبة', 'Confirm Delete Claim')} size="sm"
      footer={<div className="flex justify-end gap-2">
        <Button fullWidth={false} variant="outline" disabled={busy} onClick={() => setDeleteConfirmOpen(false)}>{a.text('إلغاء', 'Cancel')}</Button>
        <Button fullWidth={false} variant="danger" leftIcon={<Trash2 size={16} />} isLoading={busy} onClick={() => void deleteClaim()}>{a.text('حذف المطالبة', 'Delete Claim')}</Button>
      </div>}>
      <p className="text-sm text-slate-600">{a.text(
        'ستختفي المطالبة من القائمة النشطة والتقارير، مع الاحتفاظ ببياناتها وسجلها المالي للتدقيق. لا يمكن حذف مطالبة لها تحصيل مرحل قبل عكسه.',
        'This claim will leave the active list and reports while its data and financial history remain available for audit. Reverse any posted receipt before deleting.',
      )}</p>
    </Modal>

    <Modal open={receiptOpen} onClose={() => setReceiptOpen(false)} title={a.text('تأكيد التحصيل', 'Confirm Receipt')} size="md"
      footer={<div className="flex justify-end gap-2">
        <Button fullWidth={false} variant="outline" onClick={() => setReceiptOpen(false)}>{a.text('إلغاء', 'Cancel')}</Button>
        <Button fullWidth={false} isLoading={busy} onClick={() => selectedClaim && void confirmReceipt(selectedClaim, false)}>{a.text('تأكيد التحصيل', 'Confirm Receipt')}</Button>
      </div>}>
      <div className="space-y-4">
        <p className="text-sm text-slate-600">{a.text('لن يؤثر تاريخ البنك وحده على وارد الشركة. سيُرحّل صافي المبلغ مرة واحدة بعد تأكيدك.', 'A bank date alone does not affect Company Income. The net amount posts once after you confirm.')}</p>
        <BookField label={a.text('تاريخ دخول المبلغ البنك', 'Date In Bank')}><DateControl value={receiptDate} onChange={event => setReceiptDate(event.target.value)} required /></BookField>
      </div>
    </Modal>

    <Modal open={!!preview} onClose={() => setPreview(undefined)} title={a.text('استيراد Excel', 'Import Excel')} size="full"
      footer={<div className="flex justify-end gap-2">
        <Button fullWidth={false} variant="outline" onClick={() => setPreview(undefined)}>{a.text('إغلاق', 'Close')}</Button>
        <Button fullWidth={false} isLoading={busy} disabled={!preview || preview.validRows === 0} onClick={() => void confirmImport()}>{a.text('استيراد الصفوف الصالحة', 'Import Valid Rows')}</Button>
      </div>}>
      {preview && <div className="space-y-3">
        <p className="text-sm text-slate-600">
          {preview.fileName} · {a.text(`الصفوف: ${a.number(preview.totalRows)}، صالحة: ${a.number(preview.validRows)}، أخطاء: ${a.number(preview.invalidRows)}`,
            `Rows: ${a.number(preview.totalRows)}, valid: ${a.number(preview.validRows)}, invalid: ${a.number(preview.invalidRows)}`)}
        </p>
        <div className="max-h-[65vh] overflow-auto rounded-xl border border-mis-border">
          <table className={`${table} min-w-[1800px]`}>
            <thead><tr>{columns.map(([arabic, english]) => <th className={cell} key={english}>{a.text(arabic, english)}</th>)}</tr></thead>
            <tbody>{preview.rows.map(row => <tr key={row.rowNumber}>
              {previewValues(row).map((value, index) => <td className={`${cell} whitespace-nowrap ${index === 13 ? 'whitespace-normal' : ''}`} data-bidi={index >= 5 && index <= 12 ? 'ltr' : undefined} key={index}>
                {value}
                {index === 13 && <div className="mt-1 max-w-80">
                  {(row.errors.length > 0 || row.warnings.length > 0) && <p className="mb-1 text-xs font-semibold">{a.text(`صف الملف ${a.number(row.rowNumber)}`, `Workbook row ${a.number(row.rowNumber)}`)}</p>}
                  {[...row.errors, ...row.warnings].map(message => <p key={message} className={`text-xs ${row.errors.includes(message) ? 'text-rose-700' : 'text-amber-700'}`}>{localizedImportMessage(message)}</p>)}
                </div>}
              </td>)}
            </tr>)}</tbody>
          </table>
        </div>
      </div>}
    </Modal>
  </div>;
}

function claimOrganizationCategory(value: string): ClaimOrganizationCategory | null {
  const normalized = value.trim().toUpperCase().replace(/[\s-]+/g, '_');
  if (normalized === 'BANK') return 'BANK';
  if (['CONSUMER_FINANCE', 'FINANCIAL_INSTITUTION', 'INSTALLMENT_COMPANY', 'FINANCE_COMPANY'].includes(normalized)) {
    return 'INSTALLMENT';
  }
  return null;
}
