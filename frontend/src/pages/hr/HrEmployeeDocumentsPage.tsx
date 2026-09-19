import { CheckCircle2, Download, Eye, FileCheck2, FileText, FolderOpen, RefreshCw, Search, Trash2, Upload, XCircle } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge } from '../../components/common/StatusBadge';
import { useToast } from '../../components/common/Toast';
import { FileInput } from '../../components/forms/FileInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { useLocalization } from '../../context/LocalizationContext';
import { lookupLabel, organizationLabel } from '../../features/hr/employeeDisplay';
import { HrEmployeeDocumentBulkUploadModal } from '../../features/hr/components/HrEmployeeDocumentBulkUploadModal';
import { hrEmployeeDocumentService } from '../../features/hr/services/hrEmployeeDocumentService';
import { hrEmployeeService } from '../../features/hr/services/hrEmployeeService';
import { hrMasterDataService } from '../../features/hr/services/hrMasterDataService';
import type { EmployeePersonnelFile, PagedEmployeePersonnelFiles, PersonnelDocumentChecklistItem, PersonnelFileQuery, PersonnelFileSummary, RequiredDocumentCode } from '../../features/hr/types/document';
import type { EmployeeOrganizationAssignment } from '../../features/hr/types/employee';
import type { MasterDataLookup } from '../../features/hr/types/masterData';
import { getApiErrorMessage } from '../../services/apiClient';

const emptyPage: PagedEmployeePersonnelFiles = { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 };
const emptySummary: PersonnelFileSummary = { totalEmployees: 0, completeFiles: 0, incompleteFiles: 0, missingDocuments: 0 };
const copy = {
  en: { title: 'Employee Documents', subtitle: 'Personnel-file completion and required-document checklist', total: 'Total Employees', completeFiles: 'Complete Files', incompleteFiles: 'Incomplete Files', missingDocuments: 'Missing Documents', search: 'Search employee name, code, or national ID', allDepartments: 'All departments', allOrganizations: 'All banks / companies', allPositions: 'All positions', allGenders: 'All genders', male: 'Male', female: 'Female', allStatuses: 'All completion statuses', complete: 'Complete', incomplete: 'Incomplete', allMissing: 'All missing-document types', employee: 'Employee', department: 'Department', organization: 'Assigned bank / company', position: 'Position', gender: 'Gender', completed: 'Documents Completed', missing: 'Documents Missing', completion: 'Completion', status: 'Status', actions: 'Actions', manage: 'Open file', archived: 'Archived', fileTitle: 'Employee Personnel File', nationalId: 'National ID', uploaded: 'Uploaded', notUploaded: 'Missing', upload: 'Upload document', view: 'View', download: 'Download', replace: 'Replace', remove: 'Delete', uploadedAt: 'Uploaded', uploadedBy: 'Uploaded by', uploadFile: 'Upload {document}', replaceFile: 'Replace {document}', drag: 'Drag the file here or choose a file', formats: 'PDF / JPG / JPEG / PNG · maximum 10 MB', choose: 'Choose File', cancel: 'Cancel', submitUpload: 'Upload Document', deleteTitle: 'Delete Document', deleteConfirm: 'Are you sure you want to delete this document?', deleted: 'Document deleted.', saved: 'Document uploaded.', replaced: 'Document replaced.', loadError: 'Unable to load employee personnel files.', fileError: 'Unable to load this employee personnel file.', uploadError: 'Unable to upload the document.', deleteError: 'Unable to delete the document.', previewError: 'Unable to preview the document.', downloadError: 'Unable to download the document.', invalidFile: 'Choose a PDF, JPG, JPEG, or PNG file no larger than 10 MB.', noEmployees: 'No employees match these filters.', refresh: 'Refresh', bulkUpload: 'Bulk upload', fileComplete: 'Complete File', fileIncomplete: 'Incomplete File', unknown: 'Not specified', birth: 'Birth Certificate', graduation: 'Graduation Certificate', idCopy: 'National ID Copy', military: 'Military Exemption / Status', criminal: 'Criminal Record', appointment: 'Employment / Appointment Paper', labor: 'Labor Office Registration (Kaab El Amal)' },
  ar: { title: 'مستندات الموظفين', subtitle: 'متابعة اكتمال ملف الموظف وقائمة المستندات المطلوبة', total: 'إجمالي الموظفين', completeFiles: 'ملفات مكتملة', incompleteFiles: 'ملفات غير مكتملة', missingDocuments: 'مستندات ناقصة', search: 'ابحث باسم الموظف أو الكود أو الرقم القومي', allDepartments: 'كل الأقسام', allOrganizations: 'كل البنوك / الشركات', allPositions: 'كل المسميات الوظيفية', allGenders: 'كل الأنواع', male: 'ذكر', female: 'أنثى', allStatuses: 'كل حالات الاكتمال', complete: 'مكتمل', incomplete: 'غير مكتمل', allMissing: 'كل المستندات الناقصة', employee: 'الموظف', department: 'القسم', organization: 'البنك / الشركة', position: 'المسمى الوظيفي', gender: 'النوع', completed: 'المستندات المكتملة', missing: 'المستندات الناقصة', completion: 'نسبة الاكتمال', status: 'الحالة', actions: 'الإجراءات', manage: 'فتح الملف', archived: 'مؤرشف', fileTitle: 'ملف مستندات الموظف', nationalId: 'الرقم القومي', uploaded: 'مرفوع', notUploaded: 'غير مرفوع', upload: 'رفع المستند', view: 'عرض', download: 'تحميل', replace: 'استبدال', remove: 'حذف', uploadedAt: 'تاريخ الرفع', uploadedBy: 'رفع بواسطة', uploadFile: 'رفع {document}', replaceFile: 'استبدال {document}', drag: 'اسحب الملف هنا أو اختر ملفًا', formats: 'PDF / JPG / JPEG / PNG · بحد أقصى 10 ميجابايت', choose: 'اختيار ملف', cancel: 'إلغاء', submitUpload: 'رفع المستند', deleteTitle: 'حذف المستند', deleteConfirm: 'هل أنت متأكد من حذف هذا المستند؟', deleted: 'تم حذف المستند.', saved: 'تم رفع المستند.', replaced: 'تم استبدال المستند.', loadError: 'تعذر تحميل ملفات مستندات الموظفين.', fileError: 'تعذر تحميل ملف مستندات الموظف.', uploadError: 'تعذر رفع المستند.', deleteError: 'تعذر حذف المستند.', previewError: 'تعذر عرض المستند.', downloadError: 'تعذر تحميل المستند.', invalidFile: 'اختر ملف PDF أو JPG أو JPEG أو PNG لا يتجاوز 10 ميجابايت.', noEmployees: 'لا يوجد موظفون مطابقون لعوامل التصفية.', refresh: 'تحديث', bulkUpload: 'رفع دفعة واحدة', fileComplete: 'ملف مكتمل', fileIncomplete: 'ملف غير مكتمل', unknown: 'غير محدد', birth: 'شهادة الميلاد', graduation: 'شهادة التخرج', idCopy: 'صورة البطاقة', military: 'ورق الإعفاء / موقف التجنيد', criminal: 'الفيش الجنائي', appointment: 'ورقة التعيين', labor: 'كعب العمل' },
} as const;

type Text = typeof copy.en | typeof copy.ar;
function docName(code: RequiredDocumentCode, text: Text) { const names: Record<RequiredDocumentCode, string> = { BIRTH_CERTIFICATE: text.birth, GRADUATION_CERTIFICATE: text.graduation, NATIONAL_ID_COPY: text.idCopy, MILITARY_STATUS: text.military, CRIMINAL_RECORD: text.criminal, EMPLOYMENT_APPOINTMENT_PAPER: text.appointment, LABOR_OFFICE_REGISTRATION: text.labor }; return names[code]; }

function DocumentSlot({ document, locale, onDelete, onDownload, onPreview, onUpload, text }: {
  document: PersonnelDocumentChecklistItem;
  locale: string;
  onDelete: () => void;
  onDownload: () => void;
  onPreview: () => void;
  onUpload: () => void;
  text: Text;
}) {
  const name = docName(document.code, text);
  return (
    <article className="flex min-h-[15.5rem] flex-col rounded-2xl border border-mis-border bg-white p-5">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h4 className="break-words text-base font-semibold leading-6 text-mis-navy">{name}</h4>
          <div className="mt-2"><StatusBadge dot tone={document.isUploaded ? 'success' : 'warning'}>{document.isUploaded ? text.uploaded : text.notUploaded}</StatusBadge></div>
          {document.uploadedAt ? <p className="mt-2 break-words text-xs leading-5 text-slate-500">{text.uploadedAt}: {new Intl.DateTimeFormat(locale, { dateStyle: 'medium' }).format(new Date(document.uploadedAt))}{document.uploadedBy ? ` · ${text.uploadedBy}: ${document.uploadedBy}` : ''}</p> : null}
        </div>
        {document.isUploaded ? <CheckCircle2 className="h-6 w-6 shrink-0 text-emerald-600" /> : <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-amber-50 text-amber-600"><Upload className="h-4 w-4" /></span>}
      </div>
      {document.isUploaded ? (
        <div className="mt-auto grid grid-cols-2 gap-2 border-t border-slate-100 pt-4">
          <Button leftIcon={<Eye />} onClick={onPreview} size="sm" variant="outline">{text.view}</Button>
          <Button leftIcon={<Download />} onClick={onDownload} size="sm" variant="outline">{text.download}</Button>
          <Button leftIcon={<RefreshCw />} onClick={onUpload} size="sm" variant="outline">{text.replace}</Button>
          <Button leftIcon={<Trash2 />} onClick={onDelete} size="sm" variant="danger">{text.remove}</Button>
        </div>
      ) : (
        <button className="mt-5 flex flex-1 flex-col items-center justify-center rounded-xl border border-dashed border-slate-300 bg-slate-50 px-4 py-6 text-center transition hover:border-mis-primary hover:bg-sky-50/80" onClick={onUpload} type="button">
          <span className="flex h-11 w-11 items-center justify-center rounded-full bg-white text-mis-primary shadow-sm ring-1 ring-slate-200"><Upload className="h-5 w-5" /></span>
          <span className="mt-3 text-sm font-semibold text-mis-navy">{text.upload}</span>
          <span className="mt-1 text-xs leading-5 text-slate-500">{text.formats}</span>
        </button>
      )}
    </article>
  );
}

function UploadModal({ employee, document, onClose, onSaved }: { employee: EmployeePersonnelFile; document: PersonnelDocumentChecklistItem; onClose: () => void; onSaved: () => void }) {
  const { language } = useLocalization(); const text = copy[language]; const toast = useToast();
  const [file, setFile] = useState<File | null>(null); const [saving, setSaving] = useState(false); const [error, setError] = useState(''); const name = docName(document.code, text);
  async function submit(event: React.FormEvent) { event.preventDefault(); setError(''); if (!file || file.size > 10 * 1024 * 1024 || !/\.(pdf|jpe?g|png)$/i.test(file.name)) { setError(text.invalidFile); return; } setSaving(true); try { await hrEmployeeDocumentService.uploadRequired(employee.employeeId, document.code, file); toast.success(document.isUploaded ? text.replaced : text.saved); onSaved(); } catch (requestError) { setError(getApiErrorMessage(requestError, text.uploadError)); } finally { setSaving(false); } }
  return <Modal closeOnBackdrop={!saving} onClose={onClose} open size="md" title={(document.isUploaded ? text.replaceFile : text.uploadFile).replace('{document}', name)}><form className="space-y-5" onSubmit={submit}><div className="rounded-xl bg-mis-surface p-4 text-sm"><p className="font-bold text-mis-navy">{employee.employeeName} — {employee.employeeNumber}</p><p className="mt-1 text-slate-500">{name}</p></div>{error ? <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</div> : null}<div className="rounded-2xl border-2 border-dashed border-mis-border bg-slate-50 p-7 text-center"><Upload className="mx-auto h-9 w-9 text-mis-primary" /><p className="mt-3 font-semibold text-mis-navy">{text.drag}</p><p className="mt-1 text-xs text-slate-500">{text.formats}</p><div className="mx-auto mt-4 max-w-sm"><FileInput accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png" label={text.choose} onChange={(event) => setFile(event.target.files?.[0] ?? null)} required /></div></div><div className="flex justify-end gap-2"><Button disabled={saving} fullWidth={false} onClick={onClose} type="button" variant="outline">{text.cancel}</Button><Button fullWidth={false} isLoading={saving} type="submit">{text.submitUpload}</Button></div></form></Modal>;
}

function PersonnelFileModal({ employeeId, onClose, onChanged }: { employeeId: string; onClose: () => void; onChanged: () => void }) {
  const { language } = useLocalization(); const text = copy[language]; const locale = language === 'ar' ? 'ar-EG' : 'en-GB'; const toast = useToast();
  const [employee, setEmployee] = useState<EmployeePersonnelFile | null>(null); const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [uploading, setUploading] = useState<PersonnelDocumentChecklistItem | null>(null); const [deleting, setDeleting] = useState<PersonnelDocumentChecklistItem | null>(null);
  const load = useCallback(async () => { setLoading(true); setError(''); try { setEmployee(await hrEmployeeDocumentService.getPersonnelFile(employeeId)); } catch (requestError) { setError(getApiErrorMessage(requestError, text.fileError)); } finally { setLoading(false); } }, [employeeId, text.fileError]); useEffect(() => { void load(); }, [load]);
  async function preview(document: PersonnelDocumentChecklistItem) { if (!document.documentId) return; try { const file = await hrEmployeeDocumentService.preview(document.documentId); const url = URL.createObjectURL(file.blob); window.open(url, '_blank', 'noopener,noreferrer'); window.setTimeout(() => URL.revokeObjectURL(url), 60_000); } catch (e) { toast.error(getApiErrorMessage(e, text.previewError)); } }
  async function download(document: PersonnelDocumentChecklistItem) { if (!document.documentId || !document.fileName) return; try { await hrEmployeeDocumentService.download(document.documentId, document.fileName); } catch (e) { toast.error(getApiErrorMessage(e, text.downloadError)); } }
  async function remove() { if (!deleting?.documentId) return; try { await hrEmployeeDocumentService.delete(deleting.documentId, null); toast.success(text.deleted); setDeleting(null); await load(); onChanged(); } catch (e) { toast.error(getApiErrorMessage(e, text.deleteError)); } }
  return <Modal bodyClassName="p-0" onClose={onClose} open size="xl" title={text.fileTitle}>{loading ? <div className="flex min-h-80 items-center justify-center"><LoadingSpinner /></div> : error || !employee ? <div className="p-6"><ErrorState compact message={error} onRetry={() => void load()} title={text.fileError} /></div> : <><div className="border-b border-mis-border bg-mis-surface p-5 sm:p-6"><div className="flex flex-wrap justify-between gap-4"><div className="min-w-0"><h3 className="break-words text-xl font-bold text-mis-navy">{employee.employeeName}</h3><p className="mt-1 break-words text-sm text-slate-500">{employee.employeeNumber} · {employee.departmentName} · {employee.positionName ?? text.unknown}</p><p className="mt-1 break-words text-sm text-slate-500">{text.gender}: {employee.gender === 'Female' ? text.female : employee.gender === 'Male' ? text.male : text.unknown}{employee.nationalId ? ` · ${text.nationalId}: ${employee.nationalId}` : ''}</p></div><div className="text-end"><StatusBadge dot tone={employee.missingDocuments === 0 ? 'success' : 'warning'}>{employee.missingDocuments === 0 ? text.fileComplete : text.fileIncomplete}</StatusBadge><p className="mt-2 text-lg font-bold text-mis-navy">{employee.completedDocuments}/{employee.requiredDocuments} · {employee.completionPercentage}%</p></div></div></div><div className="grid gap-4 bg-slate-50 p-4 sm:grid-cols-2 sm:p-6">{employee.documents.map((document) => <DocumentSlot document={document} key={document.code} locale={locale} onDelete={() => setDeleting(document)} onDownload={() => void download(document)} onPreview={() => void preview(document)} onUpload={() => setUploading(document)} text={text} />)}</div>{uploading ? <UploadModal document={uploading} employee={employee} onClose={() => setUploading(null)} onSaved={() => { setUploading(null); void load(); onChanged(); }} /> : null}{deleting ? <Modal dialogRole="alertdialog" footer={<><Button fullWidth={false} onClick={() => setDeleting(null)} variant="outline">{text.cancel}</Button><Button fullWidth={false} onClick={() => void remove()} variant="danger">{text.remove}</Button></>} onClose={() => setDeleting(null)} open size="sm" title={text.deleteTitle}><p>{text.deleteConfirm}</p></Modal> : null}</>}</Modal>;
}


export function HrEmployeeDocumentsPage() {
  const { language, t } = useLocalization();
  const text = copy[language];
  const toast = useToast();
  const [searchParams] = useSearchParams();
  const requestedStatus = searchParams.get('completionStatus');
  const [query, setQuery] = useState<PersonnelFileQuery>({
    page: 1,
    pageSize: 200,
    search: searchParams.get('employee') ?? '',
    employeeId: searchParams.get('employeeId') ?? '',
    departmentId: '',
    organizationId: '',
    positionId: '',
    gender: '',
    completionStatus: requestedStatus === 'Incomplete' || requestedStatus === 'Complete' ? requestedStatus : 'All',
    missingDocumentCode: '',
  });
  const [search, setSearch] = useState(searchParams.get('employee') ?? '');
  const [data, setData] = useState(emptyPage);
  const [summary, setSummary] = useState(emptySummary);
  const [departments, setDepartments] = useState<MasterDataLookup[]>([]);
  const [organizations, setOrganizations] = useState<EmployeeOrganizationAssignment[]>([]);
  const [positions, setPositions] = useState<MasterDataLookup[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selected, setSelected] = useState<string | null>(query.employeeId || null);
  const [bulkOpen, setBulkOpen] = useState(false);

  useEffect(() => {
    const timer = window.setTimeout(() => setQuery((value) => ({
      ...value,
      page: 1,
      search: search.trim(),
      employeeId: search.trim() === (searchParams.get('employee') ?? '') ? value.employeeId : '',
    })), 300);
    return () => window.clearTimeout(timer);
  }, [search, searchParams]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [page, totals] = await Promise.all([
        hrEmployeeDocumentService.getPersonnelFiles(query),
        hrEmployeeDocumentService.getPersonnelFileSummary(),
      ]);
      setData(page);
      setSummary(totals);
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, text.loadError));
    } finally {
      setLoading(false);
    }
  }, [query, text.loadError]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    void Promise.all([
      hrMasterDataService.getLookup('departments', true),
      hrMasterDataService.getLookup('positions', true),
      hrEmployeeService.getOrganizations(),
    ]).then(([departmentItems, positionItems, organizationItems]) => {
      setDepartments(departmentItems);
      setPositions(positionItems);
      setOrganizations(organizationItems);
    }).catch((reason) => toast.error(getApiErrorMessage(reason, t('loadLookupsError'))));
  }, [t, toast]);

  const filter = (value: Partial<PersonnelFileQuery>) => setQuery((current) => ({ ...current, ...value, page: 1 }));
  const cards = [
    { label: text.total, value: summary.totalEmployees, icon: <FolderOpen />, onClick: () => filter({ completionStatus: 'All', missingDocumentCode: '' }) },
    { label: text.completeFiles, value: summary.completeFiles, icon: <FileCheck2 />, onClick: () => filter({ completionStatus: 'Complete', missingDocumentCode: '' }) },
    { label: text.incompleteFiles, value: summary.incompleteFiles, icon: <FileText />, onClick: () => filter({ completionStatus: 'Incomplete', missingDocumentCode: '' }) },
    { label: text.missingDocuments, value: summary.missingDocuments, icon: <XCircle />, onClick: () => filter({ completionStatus: 'Incomplete' }) },
  ];

  return <div className="space-y-6">
    <PageHeader
      actions={<div className="flex flex-wrap items-center gap-2"><Button fullWidth={false} leftIcon={<Upload />} onClick={() => setBulkOpen(true)}>{text.bulkUpload}</Button><Button fullWidth={false} leftIcon={<RefreshCw />} onClick={() => void load()} variant="outline">{text.refresh}</Button></div>}
      description={text.subtitle}
      title={text.title}
    />
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {cards.map((card) => <button className="text-start" key={card.label} onClick={card.onClick} type="button"><Card className="h-full p-5 transition hover:border-mis-primary hover:shadow-sm"><div className="flex items-center justify-between gap-4"><div className="min-w-0"><p className="break-words text-sm font-semibold leading-5 text-slate-500">{card.label}</p><p className="mt-2 text-3xl font-bold text-mis-navy">{card.value}</p></div><div className="shrink-0 rounded-xl bg-mis-pale p-3 text-mis-primary">{card.icon}</div></div></Card></button>)}
    </div>
    <Card className="p-4 sm:p-5">
      <div className="module-filter-grid">
        <label className="relative self-end"><Search className="absolute start-3 top-3 h-5 w-5 text-slate-400" /><input className="h-11 w-full rounded-xl border border-mis-border pe-3 ps-10 text-sm outline-none focus:border-mis-primary focus:ring-2 focus:ring-sky-100" onChange={(event) => setSearch(event.target.value)} placeholder={text.search} value={search} /></label>
        <SelectInput label={text.department} onChange={(event) => filter({ departmentId: event.target.value })} value={query.departmentId}><option value="">{text.allDepartments}</option>{departments.map((item) => <option key={item.id} value={item.id}>{lookupLabel(item, language)}</option>)}</SelectInput>
        <SelectInput label={text.organization} onChange={(event) => filter({ organizationId: event.target.value })} value={query.organizationId}><option value="">{text.allOrganizations}</option>{organizations.map((item) => <option key={item.id} value={item.id}>{organizationLabel(item, language)}</option>)}</SelectInput>
        <SelectInput label={text.position} onChange={(event) => filter({ positionId: event.target.value })} value={query.positionId}><option value="">{text.allPositions}</option>{positions.map((item) => <option key={item.id} value={item.id}>{lookupLabel(item, language)}</option>)}</SelectInput>
        <SelectInput label={text.gender} onChange={(event) => filter({ gender: event.target.value })} value={query.gender}><option value="">{text.allGenders}</option><option value="Male">{text.male}</option><option value="Female">{text.female}</option></SelectInput>
        <SelectInput label={text.status} onChange={(event) => filter({ completionStatus: event.target.value as PersonnelFileQuery['completionStatus'] })} value={query.completionStatus}><option value="All">{text.allStatuses}</option><option value="Complete">{text.complete}</option><option value="Incomplete">{text.incomplete}</option></SelectInput>
        <SelectInput label={text.missingDocuments} onChange={(event) => filter({ missingDocumentCode: event.target.value as PersonnelFileQuery['missingDocumentCode'] })} value={query.missingDocumentCode}><option value="">{text.allMissing}</option><option value="BIRTH_CERTIFICATE">{text.birth}</option><option value="GRADUATION_CERTIFICATE">{text.graduation}</option><option value="NATIONAL_ID_COPY">{text.idCopy}</option><option value="MILITARY_STATUS">{text.military}</option><option value="CRIMINAL_RECORD">{text.criminal}</option><option value="EMPLOYMENT_APPOINTMENT_PAPER">{text.appointment}</option><option value="LABOR_OFFICE_REGISTRATION">{text.labor}</option></SelectInput>
      </div>
    </Card>
    <section aria-live="polite">
      {loading ? <Card className="flex min-h-72 items-center justify-center"><LoadingSpinner /></Card> : error ? <Card><ErrorState message={error} onRetry={() => void load()} title={text.loadError} /></Card> : data.items.length === 0 ? <Card><EmptyState description={text.noEmployees} icon={<FileText className="h-6 w-6" />} title={text.noEmployees} /></Card> : <div className="grid gap-4 lg:grid-cols-2 2xl:grid-cols-3">
        {data.items.map((employee) => <Card className="flex min-w-0 flex-col p-5" key={employee.employeeId}>
          <div className="flex min-w-0 items-start justify-between gap-3"><div className="min-w-0"><h3 className="break-words font-bold leading-6 text-mis-navy">{employee.employeeName}</h3><p className="mt-1 text-xs font-semibold text-slate-500"><bdi>{employee.employeeNumber}</bdi>{employee.isArchived ? ` · ${text.archived}` : ''}</p></div><StatusBadge dot tone={employee.missingDocuments === 0 ? 'success' : 'warning'}>{employee.missingDocuments === 0 ? text.complete : text.incomplete}</StatusBadge></div>
          <dl className="mt-4 grid grid-cols-2 gap-x-4 gap-y-3 text-sm"><div className="min-w-0"><dt className="text-xs text-slate-400">{text.department}</dt><dd className="mt-1 break-words font-semibold text-slate-700">{employee.departmentName || text.unknown}</dd></div><div className="min-w-0"><dt className="text-xs text-slate-400">{text.position}</dt><dd className="mt-1 break-words font-semibold text-slate-700">{employee.positionName ?? text.unknown}</dd></div><div><dt className="text-xs text-slate-400">{text.gender}</dt><dd className="mt-1 font-semibold text-slate-700">{employee.gender === 'Female' ? text.female : employee.gender === 'Male' ? text.male : text.unknown}</dd></div><div><dt className="text-xs text-slate-400">{text.missing}</dt><dd className="mt-1 font-semibold text-slate-700">{employee.missingDocuments}</dd></div></dl>
          <div className="mt-4"><div className="flex items-center justify-between gap-3 text-xs"><span className="font-semibold text-slate-500">{text.completion}</span><strong className="text-mis-navy">{employee.completedDocuments}/{employee.requiredDocuments} · {employee.completionPercentage}%</strong></div><div className="mt-2 h-2.5 overflow-hidden rounded-full bg-slate-100"><div className="h-full rounded-full bg-mis-primary" style={{ width: `${employee.completionPercentage}%` }} /></div></div>
          <Button className="mt-5" fullWidth leftIcon={<Eye />} onClick={() => setSelected(employee.employeeId)} variant="outline">{text.manage}</Button>
        </Card>)}
      </div>}
    </section>
    {selected ? <PersonnelFileModal employeeId={selected} onChanged={() => void load()} onClose={() => setSelected(null)} /> : null}
    {bulkOpen ? <HrEmployeeDocumentBulkUploadModal onClose={() => setBulkOpen(false)} onCompleted={() => { void load(); }} /> : null}
  </div>;
}
