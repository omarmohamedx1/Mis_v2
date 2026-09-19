import { Download, FileBarChart2, FileSpreadsheet, Filter, RefreshCw, Search } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { Pagination } from '../../components/common/Pagination';
import { Section } from '../../components/common/Section';
import { useToast } from '../../components/common/Toast';
import { DateInput } from '../../components/forms/DateInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextInput } from '../../components/forms/TextInput';
import { useLocalization } from '../../context/LocalizationContext';
import { EmployeeSearchSelect } from '../../features/hr/components/EmployeeSearchSelect';
import { hrMasterDataService } from '../../features/hr/services/hrMasterDataService';
import { hrReportService } from '../../features/hr/services/hrReportService';
import type { MasterDataLookup } from '../../features/hr/types/masterData';
import { emptyHrReportFilter, type HrReportCatalogItem, type HrReportExportFormat, type HrReportFilter, type HrReportPreview } from '../../features/hr/types/report';
import { getApiErrorMessage } from '../../services/apiClient';

const copyByLanguage = {
  en: {
    title: 'HR Reports', subtitle: 'Filter, preview, and export operational HR data', catalog: 'Report Catalog', selectReport: 'Select a report to configure its filters.', filters: 'Report Filters',
    search: 'Search', dateFrom: 'Date From', dateTo: 'Date To', employee: 'Employee', department: 'Department', status: 'Status', type: 'Type', source: 'Source', allDepartments: 'All departments', allTypes: 'All types', allSources: 'All sources', allStatuses: 'All statuses', inactive: 'Inactive',
    run: 'Run Report', reset: 'Reset Filters', preview: 'Report Preview', generated: 'Generated {date}', rows: '{count} rows', noRows: 'No matching report rows', noRowsHelp: 'Adjust the filters and run the report again.', choose: 'Choose a report from the catalog to begin.',
    excel: 'Export Excel', pdf: 'Export PDF', exporting: 'Preparing export…', exported: 'Report downloaded successfully.', exportError: 'Unable to export the report.', loadCatalogError: 'Unable to load the report catalog.', loadPreviewError: 'Unable to generate the report preview.',
    dateValidation: 'Date To cannot be before Date From.', appliedFilters: 'Applied filters', noFilters: 'No filters applied', clearEmployee: 'Clear selection', retry: 'Try again', page: 'Page {page} of {total}', showing: 'Showing {from}–{to} of {total}', previous: 'Previous page', next: 'Next page',
  },
  ar: {
    title: 'تقارير الموارد البشرية', subtitle: 'فلترة ومعاينة وتصدير بيانات الموارد البشرية التشغيلية', catalog: 'دليل التقارير', selectReport: 'اختر تقريرًا لضبط عوامل التصفية.', filters: 'عوامل تصفية التقرير',
    search: 'بحث', dateFrom: 'من تاريخ', dateTo: 'إلى تاريخ', employee: 'الموظف', department: 'القسم', status: 'الحالة', type: 'النوع', source: 'المصدر', allDepartments: 'كل الأقسام', allTypes: 'كل الأنواع', allSources: 'كل المصادر', allStatuses: 'كل الحالات', inactive: 'غير نشط',
    run: 'تشغيل التقرير', reset: 'مسح الفلاتر', preview: 'معاينة التقرير', generated: 'تم الإنشاء {date}', rows: '{count} سجل', noRows: 'لا توجد نتائج مطابقة', noRowsHelp: 'عدّل عوامل التصفية ثم شغّل التقرير مرة أخرى.', choose: 'اختر تقريرًا من الدليل للبدء.',
    excel: 'تصدير Excel', pdf: 'تصدير PDF', exporting: 'جارٍ تجهيز الملف…', exported: 'تم تنزيل التقرير بنجاح.', exportError: 'تعذر تصدير التقرير.', loadCatalogError: 'تعذر تحميل دليل التقارير.', loadPreviewError: 'تعذر إنشاء معاينة التقرير.',
    dateValidation: 'لا يمكن أن يسبق تاريخ النهاية تاريخ البداية.', appliedFilters: 'عوامل التصفية المطبقة', noFilters: 'لم تُطبق عوامل تصفية', clearEmployee: 'مسح الاختيار', retry: 'إعادة المحاولة', page: 'صفحة {page} من {total}', showing: 'عرض {from}–{to} من {total}', previous: 'الصفحة السابقة', next: 'الصفحة التالية',
  },
} as const;

const arabicReportNames: Record<string, { name: string; description: string }> = {
  'employee-list': { name: 'قائمة الموظفين', description: 'دليل الموظفين الحالي والهيكل التنظيمي.' },
  'employee-details': { name: 'تفاصيل الموظفين', description: 'ملفات الموظفين دون بيانات الرواتب أو البنوك.' },
  attendance: { name: 'تقرير الحضور', description: 'سجلات الحضور وساعات العمل المحسوبة.' },
  absence: { name: 'تقرير الغياب', description: 'حالات غياب الموظفين المسجلة.' },
  leave: { name: 'تقرير الإجازات', description: 'طلبات الإجازة والقرارات والأرصدة.' },
  'late-employees': { name: 'الموظفون المتأخرون', description: 'سجلات الحضور التي تحتوي على دقائق تأخير.' },
  overtime: { name: 'تقرير الوقت الإضافي', description: 'ساعات العمل الإضافية المحسوبة.' },
  'expiring-contracts': { name: 'العقود القريبة من الانتهاء', description: 'العقود المنتهية أو القريبة من تاريخ النهاية.' },
  'expiring-documents': { name: 'المستندات القريبة من الانتهاء', description: 'المستندات المنتهية أو المطلوب تجديدها قريبًا.' },
  'employees-by-department': { name: 'الموظفون حسب القسم', description: 'إجمالي الموظفين مجمعًا حسب القسم.' },
  delegations: { name: 'تقرير التفويضات', description: 'التفويضات الإدارية وفترات سريانها.' },
};

function reportTypeCategory(code: string): 'leave-types' | 'document-types' | 'contract-types' | 'delegation-types' | null {
  if (code === 'leave') return 'leave-types';
  if (code === 'expiring-documents') return 'document-types';
  if (code === 'expiring-contracts') return 'contract-types';
  if (code === 'delegations') return 'delegation-types';
  return null;
}

function selectName(item: MasterDataLookup, language: 'en' | 'ar') {
  return language === 'ar' && item.nameArabic ? item.nameArabic : item.nameEnglish;
}

const statusLabels = {
  en: { Active: 'Active', Inactive: 'Inactive', OnLeave: 'On Leave', Suspended: 'Suspended', Terminated: 'Terminated', Present: 'Present', Absent: 'Absent', Late: 'Late', Leave: 'On Leave', Holiday: 'Holiday', Weekend: 'Weekend', Pending: 'Pending', PendingReview: 'Needs Review', NotApplicable: 'No Deduction', Excluded: 'Excluded from Payroll', Excused: 'Excused', Unexcused: 'Unexcused', Approved: 'Approved', Rejected: 'Rejected', Cancelled: 'Cancelled', Draft: 'Draft', Expired: 'Expired', ExpiringSoon: 'Expiring Soon', Valid: 'Valid', ExcelImport: 'Excel import', Manual: 'Manual', DeviceIntegration: 'Device integration', SystemProcessing: 'System processing' },
  ar: { Active: 'نشط', Inactive: 'غير نشط', OnLeave: 'في إجازة', Suspended: 'موقوف', Terminated: 'منتهي الخدمة', Present: 'حاضر', Absent: 'غائب', Late: 'متأخر', Leave: 'في إجازة', Holiday: 'عطلة', Weekend: 'إجازة أسبوعية', Pending: 'قيد الانتظار', PendingReview: 'يحتاج مراجعة', NotApplicable: 'بدون خصم', Excluded: 'مستبعد من الخصم', Excused: 'بعذر', Unexcused: 'بدون عذر', Approved: 'مقبول', Rejected: 'مرفوض', Cancelled: 'ملغي', Draft: 'مسودة', Expired: 'منتهي', ExpiringSoon: 'ينتهي قريبًا', Valid: 'ساري', ExcelImport: 'استيراد Excel', Manual: 'يدوي', DeviceIntegration: 'ربط جهاز البصمة', SystemProcessing: 'معالجة النظام' },
} as const;

function statusOptions(reportCode: string): string[] {
  if (['employee-list', 'employee-details', 'employees-by-department'].includes(reportCode)) return ['Active', 'Inactive', 'OnLeave', 'Suspended', 'Terminated'];
  if (['attendance', 'late-employees', 'overtime'].includes(reportCode)) return ['Present', 'Absent', 'Late', 'Leave', 'Holiday', 'Weekend'];
  if (reportCode === 'absence') return ['Pending', 'Excused', 'Unexcused'];
  if (reportCode === 'leave') return ['Pending', 'Approved', 'Rejected', 'Cancelled'];
  if (reportCode === 'expiring-contracts') return ['Draft', 'Active', 'Expired', 'Terminated'];
  if (reportCode === 'expiring-documents') return ['Expired', 'ExpiringSoon', 'Valid'];
  if (reportCode === 'delegations') return ['Draft', 'Active', 'Expired', 'Cancelled'];
  return [];
}

function genericTypeOptions(reportCode: string): string[] {
  if (['attendance', 'late-employees', 'overtime'].includes(reportCode)) return ['ExcelImport', 'Manual', 'DeviceIntegration', 'SystemProcessing'];
  if (reportCode === 'absence') return ['Absent'];
  return [];
}

const arabicReportLabels: Record<string, string> = {
  employeeNumber: 'رقم الموظف', employeeName: 'اسم الموظف', nameArabic: 'الاسم بالعربية', nameEnglish: 'الاسم بالإنجليزية', nationalId: 'الرقم القومي', dateOfBirth: 'تاريخ الميلاد', gender: 'النوع', maritalStatus: 'الحالة الاجتماعية', mobile: 'رقم الهاتف', email: 'البريد الإلكتروني', city: 'المدينة', department: 'القسم', departmentCode: 'كود القسم', position: 'المسمى الوظيفي', manager: 'المدير المباشر', employmentType: 'نوع التوظيف', hireDate: 'تاريخ التعيين', status: 'الحالة', contractType: 'نوع العقد', contractStart: 'بداية العقد', contractEnd: 'نهاية العقد', probationEnd: 'نهاية فترة الاختبار', date: 'التاريخ', checkIn: 'وقت الحضور', checkOut: 'وقت الانصراف', workingHours: 'ساعات العمل', lateMinutes: 'دقائق التأخير', earlyLeaveMinutes: 'دقائق الانصراف المبكر', overtimeMinutes: 'دقائق الوقت الإضافي', source: 'المصدر', type: 'النوع', payrollImpact: 'التأثير على المرتب', reason: 'السبب', leaveType: 'نوع الإجازة', startDate: 'تاريخ البداية', endDate: 'تاريخ النهاية', days: 'عدد الأيام', requestDate: 'تاريخ الطلب', decision: 'ملاحظات القرار', daysRemaining: 'الأيام المتبقية', documentType: 'نوع المستند', fileName: 'اسم الملف', issueDate: 'تاريخ الإصدار', expiryDate: 'تاريخ الانتهاء', total: 'إجمالي الموظفين', active: 'نشط', inactive: 'غير نشط', delegationNumber: 'رقم التفويض', delegationType: 'نوع التفويض', subject: 'الموضوع', authorizedEntity: 'جهة التفويض', createdAt: 'تاريخ الإنشاء', Search: 'بحث', 'Date From': 'من تاريخ', 'Date To': 'إلى تاريخ', Employee: 'الموظف', Department: 'القسم', Status: 'الحالة', Type: 'النوع',
};

function reportLabel(key: string, fallback: string, language: 'en' | 'ar'): string {
  return language === 'ar' ? arabicReportLabels[key] ?? arabicReportLabels[fallback] ?? fallback : fallback;
}

function reportValue(key: string, value: string | null, language: 'en' | 'ar', locale: string): string {
  if (!value) return '—';
  const compactStatus = value.replace(/\s+/g, '');
  const translatedStatus = statusLabels[language][compactStatus as keyof typeof statusLabels.en];
  if (translatedStatus) return translatedStatus;
  if (/^\d{4}-\d{2}-\d{2}/.test(value) || key.toLowerCase().includes('date') || key === 'createdAt' || key === 'checkIn' || key === 'checkOut') {
    const normalized = value.includes('T') || /\d{2}:\d{2}/.test(value) ? value.replace(' ', 'T') : `${value}T12:00:00`;
    const parsed = new Date(normalized);
    if (!Number.isNaN(parsed.getTime())) return new Intl.DateTimeFormat(locale, key === 'createdAt' || key === 'checkIn' || key === 'checkOut' ? { dateStyle: 'medium', timeStyle: 'short' } : { dateStyle: 'medium' }).format(parsed);
  }
  return value;
}

export function HrReportsPage() {
  const { language, t } = useLocalization();
  const toast = useToast();
  const copy = copyByLanguage[language];
  const locale = language === 'ar' ? 'ar-EG' : 'en-GB';
  const [catalog, setCatalog] = useState<HrReportCatalogItem[]>([]);
  const [selectedCode, setSelectedCode] = useState('');
  const [filter, setFilter] = useState<HrReportFilter>(emptyHrReportFilter);
  const [preview, setPreview] = useState<HrReportPreview | null>(null);
  const [departments, setDepartments] = useState<MasterDataLookup[]>([]);
  const [types, setTypes] = useState<MasterDataLookup[]>([]);
  const [catalogLoading, setCatalogLoading] = useState(true);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [exporting, setExporting] = useState<HrReportExportFormat | null>(null);
  const [catalogError, setCatalogError] = useState('');
  const [previewError, setPreviewError] = useState('');
  const [validationError, setValidationError] = useState('');

  const selected = useMemo(() => catalog.find((item) => item.code === selectedCode) ?? null, [catalog, selectedCode]);
  const supports = useCallback((name: string) => selected?.supportedFilters.includes(name) ?? false, [selected]);
  const genericTypes = useMemo(() => genericTypeOptions(selectedCode), [selectedCode]);

  const loadCatalog = useCallback(async () => {
    setCatalogLoading(true);
    setCatalogError('');
    try {
      const [items, departmentItems] = await Promise.all([
        hrReportService.getCatalog(), hrMasterDataService.getLookup('departments'),
      ]);
      setCatalog(items); setDepartments(departmentItems);
      setSelectedCode((current) => current || items[0]?.code || '');
    } catch (error) { setCatalogError(getApiErrorMessage(error, copy.loadCatalogError)); }
    finally { setCatalogLoading(false); }
  }, [copy.loadCatalogError]);

  useEffect(() => { void loadCatalog(); }, [loadCatalog]);

  useEffect(() => {
    const category = reportTypeCategory(selectedCode);
    setTypes([]);
    setFilter((current) => ({ ...current, page: 1, typeId: '', type: '' }));
    if (!category) return;
    let active = true;
    void hrMasterDataService.getLookup(category, true).then((items) => { if (active) setTypes(items); }).catch((reason) => { if (active) { setTypes([]); toast.error(getApiErrorMessage(reason, t('loadLookupsError'))); } });
    return () => { active = false; };
  }, [selectedCode, t, toast]);

  const runReport = useCallback(async (nextFilter: HrReportFilter = filter) => {
    if (!selectedCode) return;
    if (nextFilter.dateFrom && nextFilter.dateTo && nextFilter.dateTo < nextFilter.dateFrom) { setValidationError(copy.dateValidation); return; }
    setValidationError(''); setPreviewError(''); setPreviewLoading(true);
    try { setPreview(await hrReportService.getPreview(selectedCode, nextFilter)); }
    catch (error) { setPreview(null); setPreviewError(getApiErrorMessage(error, copy.loadPreviewError)); }
    finally { setPreviewLoading(false); }
  }, [copy.dateValidation, copy.loadPreviewError, filter, selectedCode]);

  function selectReport(code: string) {
    setSelectedCode(code); setFilter(emptyHrReportFilter()); setPreview(null); setPreviewError(''); setValidationError('');
  }

  function changeFilter<K extends keyof HrReportFilter>(key: K, value: HrReportFilter[K]) {
    setFilter((current) => ({ ...current, [key]: value, page: 1 }));
  }

  async function exportReport(format: HrReportExportFormat) {
    if (!selectedCode) return;
    if (filter.dateFrom && filter.dateTo && filter.dateTo < filter.dateFrom) { setValidationError(copy.dateValidation); return; }
    setExporting(format);
    try { await hrReportService.export(selectedCode, format, filter); toast.success(copy.exported); }
    catch (error) { toast.error(getApiErrorMessage(error, copy.exportError)); }
    finally { setExporting(null); }
  }

  function displayReport(item: HrReportCatalogItem) {
    return language === 'ar' ? arabicReportNames[item.code] ?? { name: item.name, description: item.description } : { name: item.name, description: item.description };
  }

  if (catalogLoading && !catalog.length) return <div className="flex min-h-[420px] items-center justify-center"><LoadingSpinner /></div>;
  if (catalogError && !catalog.length) return <ErrorState message={catalogError} onRetry={() => void loadCatalog()} retryLabel={copy.retry} title={copy.loadCatalogError} />;

  return (
    <div>
      <PageHeader description={copy.subtitle} eyebrow={t('hrDepartment')} title={copy.title} />
      <div className="grid gap-6 xl:grid-cols-[310px_minmax(0,1fr)]">
        <Section bodyClassName="p-2" description={copy.selectReport} title={copy.catalog}>
          <nav aria-label={copy.catalog} className="space-y-1">
            {catalog.map((item) => { const text = displayReport(item); return <button aria-current={selectedCode === item.code ? 'page' : undefined} className={`w-full rounded-xl px-4 py-3 text-start transition ${selectedCode === item.code ? 'bg-mis-pale text-mis-deep' : 'text-slate-600 hover:bg-slate-50'}`} key={item.code} onClick={() => selectReport(item.code)} type="button"><span className="block text-sm font-bold">{text.name}</span><span className="mt-1 block text-xs leading-5 opacity-75">{text.description}</span></button>; })}
          </nav>
        </Section>

        <div className="min-w-0 space-y-6">
          {selected ? <Section description={displayReport(selected).description} title={displayReport(selected).name}>
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {supports('search') ? <TextInput label={copy.search} onChange={(event) => changeFilter('search', event.target.value)} value={filter.search} /> : null}
              {supports('dateFrom') ? <DateInput label={copy.dateFrom} onChange={(event) => changeFilter('dateFrom', event.target.value)} value={filter.dateFrom} /> : null}
              {supports('dateTo') ? <DateInput error={validationError || undefined} label={copy.dateTo} min={filter.dateFrom || undefined} onChange={(event) => changeFilter('dateTo', event.target.value)} value={filter.dateTo} /> : null}
              {supports('employee') ? <EmployeeSearchSelect includeInactive label={copy.employee} onChange={(employeeId) => changeFilter('employeeId', employeeId)} value={filter.employeeId} /> : null}
              {supports('department') ? <SelectInput label={copy.department} onChange={(event) => changeFilter('departmentId', event.target.value)} value={filter.departmentId}><option value="">{copy.allDepartments}</option>{departments.map((item) => <option key={item.id} value={item.id}>{selectName(item, language)}</option>)}</SelectInput> : null}
              {supports('status') ? <SelectInput label={copy.status} onChange={(event) => changeFilter('status', event.target.value)} value={filter.status}><option value="">{copy.allStatuses}</option>{statusOptions(selectedCode).map((status) => <option key={status} value={status}>{statusLabels[language][status as keyof typeof statusLabels.en]}</option>)}</SelectInput> : null}
              {supports('typeId') && types.length ? <SelectInput label={copy.type} onChange={(event) => changeFilter('typeId', event.target.value)} value={filter.typeId}><option value="">{copy.allTypes}</option>{types.map((item) => <option key={item.id} value={item.id}>{selectName(item, language)}{!item.isActive ? ` (${copy.inactive})` : ''}</option>)}</SelectInput> : null}
              {supports('type') && !supports('typeId') && genericTypes.length ? <SelectInput label={['attendance', 'late-employees', 'overtime'].includes(selectedCode) ? copy.source : copy.type} onChange={(event) => changeFilter('type', event.target.value)} value={filter.type}><option value="">{['attendance', 'late-employees', 'overtime'].includes(selectedCode) ? copy.allSources : copy.allTypes}</option>{genericTypes.map((item) => <option key={item} value={item}>{statusLabels[language][item as keyof typeof statusLabels.en]}</option>)}</SelectInput> : null}
            </div>
            <div className="mt-5 flex flex-wrap justify-end gap-2 border-t border-mis-border pt-5">
              <Button fullWidth={false} leftIcon={<RefreshCw className="h-4 w-4" />} onClick={() => { const reset = emptyHrReportFilter(); setFilter(reset); setPreview(null); setValidationError(''); }} size="md" variant="outline">{copy.reset}</Button>
              <Button fullWidth={false} isLoading={previewLoading} leftIcon={<Filter className="h-4 w-4" />} onClick={() => void runReport()} size="md">{copy.run}</Button>
            </div>
          </Section> : <Card><EmptyState description={copy.selectReport} icon={<FileBarChart2 />} title={copy.choose} /></Card>}

          {previewError ? <ErrorState compact message={previewError} onRetry={() => void runReport()} retryLabel={copy.retry} title={copy.loadPreviewError} /> : null}
          {previewLoading && !preview ? <Card className="flex min-h-64 items-center justify-center"><LoadingSpinner /></Card> : null}
          {preview ? <Section
            action={<div className="flex flex-wrap gap-2"><Button disabled={preview.totalCount === 0 || exporting !== null} fullWidth={false} isLoading={exporting === 'excel'} leftIcon={<FileSpreadsheet className="h-4 w-4" />} onClick={() => void exportReport('excel')} size="sm" variant="outline">{copy.excel}</Button><Button disabled={preview.totalCount === 0 || exporting !== null} fullWidth={false} isLoading={exporting === 'pdf'} leftIcon={<Download className="h-4 w-4" />} onClick={() => void exportReport('pdf')} size="sm">{copy.pdf}</Button></div>}
            bodyClassName="p-0"
            description={`${copy.rows.replace('{count}', String(preview.totalCount))} · ${copy.generated.replace('{date}', new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(preview.generatedAt)))}`}
            title={copy.preview}
          >
            <div className="border-b border-mis-border px-5 py-4"><p className="text-xs font-bold uppercase tracking-wide text-slate-500">{copy.appliedFilters}</p><div className="mt-2 flex flex-wrap gap-2">{Object.entries(preview.appliedFilters).length ? Object.entries(preview.appliedFilters).map(([key, value]) => <span className="rounded-full bg-slate-100 px-3 py-1 text-xs text-slate-600" key={key}><strong>{reportLabel(key, key, language)}:</strong> {reportValue(key, value, language, locale)}</span>) : <span className="text-sm text-slate-500">{copy.noFilters}</span>}</div></div>
            {preview.rows.length ? <><div className="overflow-x-auto"><table className="w-full min-w-max text-start"><thead className="bg-mis-surface text-xs uppercase tracking-wide text-slate-500"><tr>{preview.columns.map((column) => <th className="whitespace-nowrap px-4 py-3 text-start" key={column.key}>{reportLabel(column.key, column.header, language)}</th>)}</tr></thead><tbody className="divide-y divide-mis-border">{preview.rows.map((row, index) => <tr className="hover:bg-slate-50/70" key={`${preview.page}-${index}`}>{preview.columns.map((column) => { const value = reportValue(column.key, row.values[column.key], language, locale); return <td dir="auto" className="max-w-xs whitespace-nowrap px-4 py-3 text-sm text-slate-700" key={column.key} title={value === '—' ? undefined : value}>{value}</td>; })}</tr>)}</tbody></table></div><Pagination labels={{ nextPage: copy.next, pageOf: (page, total) => copy.page.replace('{page}', String(page)).replace('{total}', String(total)), previousPage: copy.previous, showing: (from, to, total) => copy.showing.replace('{from}', String(from)).replace('{to}', String(to)).replace('{total}', String(total)) }} onPageChange={(page) => { const next = { ...filter, page }; setFilter(next); void runReport(next); }} page={preview.page} pageSize={preview.pageSize} totalCount={preview.totalCount} totalPages={preview.totalPages} /></> : <EmptyState description={copy.noRowsHelp} icon={<Search />} title={copy.noRows} />}
          </Section> : null}
        </div>
      </div>
    </div>
  );
}
