import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Search } from 'lucide-react';
import { Button } from '../../components/common/Button';
import { Card } from '../../components/common/Card';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { Pagination } from '../../components/common/Pagination';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { TextInput } from '../../components/forms/TextInput';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { DateInput } from '../../components/forms/DateInput';
import { Checkbox } from '../../components/forms/Checkbox';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { EmployeeSearchSelect } from '../../features/hr/components/EmployeeSearchSelect';
import { hrEmployeeService } from '../../features/hr/services/hrEmployeeService';
import {
  formatExcuseTime,
  hrExcuseService as service,
  type ExcuseItem,
  type ExcusePage,
  type ExcuseTypeOption,
  type ManualExcuse,
} from '../../features/hr/services/hrExcuseService';
import type { EmployeeListItem } from '../../features/hr/types/employee';
import { getApiErrorMessage } from '../../services/apiClient';

const words = {
  en: {
    title: 'Excuses & Field Duties',
    add: '+ Add Manual Excuse',
    pending: 'Pending Visit Approvals',
    searchPlaceholder: 'Search by employee name or mobile number...',
    employee: 'Employee',
    number: 'Employee Number',
    department: 'Department',
    position: 'Position',
    type: 'Excuse Type',
    source: 'Source',
    date: 'Date',
    from: 'From Time',
    to: 'To Time',
    fullDay: 'Full Day',
    reason: 'Reason',
    status: 'Status',
    attachment: 'Attachment',
    actions: 'Actions',
    review: 'Review Request',
    approve: 'Approve',
    reject: 'Reject',
    rejection: 'Rejection reason (required to reject)',
    cancelReason: 'Cancellation reason',
    notes: 'Notes',
    original: 'View Original Visit',
    customer: 'Customer',
    organization: 'Bank / Installment Company',
    address: 'Address / Location',
    case: 'Case Number',
    visitStatus: 'Visit Status',
    result: 'Visit Result',
    visitNotes: 'Visit Notes',
    creator: 'Created By',
    upload: 'Upload Attachment',
    view: 'View',
    download: 'Download',
    replace: 'Replace',
    delete: 'Delete',
    edit: 'Edit',
    cancel: 'Cancel Excuse',
    savePending: 'Save as Pending',
    saveApprove: 'Approve Immediately',
    save: 'Save Changes',
    empty: 'No excuses found.',
    error: 'Unable to complete this action.',
    link: 'Link to Existing Employee',
    missing: 'This collector needs a link to an existing employee before approval.',
    changed: 'The original visit has changed. See HR audit history for prior decisions.',
    history: 'Audit History',
    fileHint: 'PDF, JPG, JPEG or PNG · maximum 10 MB',
    timeOrder: 'To Time must be after From Time.',
    timesRequired: 'From Time and To Time are required unless Full Day is selected.',
    PendingApproval: 'Pending Approval',
    Approved: 'Approved',
    Rejected: 'Rejected',
    Cancelled: 'Cancelled',
    FieldVisit: 'Field Visit',
    Manual: 'Manual',
    FieldVisitMission: 'Mission / Field Visit',
    MedicalExcuse: 'Medical Excuse',
    PersonalExcuse: 'Personal Permission',
    OfficialMission: 'Official Mission',
    LateArrivalExcuse: 'Late Arrival Excuse',
    EarlyLeaveExcuse: 'Early Leave Excuse',
    Other: 'Other',
  },
  ar: {
    title: 'الأعذار والمأموريات',
    add: '+ إضافة عذر يدوي',
    pending: 'زيارات تحتاج موافقة',
    searchPlaceholder: 'ابحث باسم الموظف أو رقم الموبايل...',
    employee: 'الموظف',
    number: 'رقم الموظف',
    department: 'القسم',
    position: 'الوظيفة',
    type: 'نوع العذر',
    source: 'المصدر',
    date: 'التاريخ',
    from: 'من الساعة',
    to: 'إلى الساعة',
    fullDay: 'يوم كامل',
    reason: 'السبب',
    status: 'الحالة',
    attachment: 'المرفق',
    actions: 'الإجراءات',
    review: 'مراجعة الطلب',
    approve: 'موافقة',
    reject: 'رفض',
    rejection: 'سبب الرفض (مطلوب للرفض)',
    cancelReason: 'سبب الإلغاء',
    notes: 'ملاحظات',
    original: 'عرض الزيارة الأصلية',
    customer: 'العميل',
    organization: 'البنك / شركة التقسيط',
    address: 'العنوان / الموقع',
    case: 'رقم القضية',
    visitStatus: 'حالة الزيارة',
    result: 'نتيجة الزيارة',
    visitNotes: 'ملاحظات الزيارة',
    creator: 'أنشئ بواسطة',
    upload: 'رفع مرفق',
    view: 'عرض',
    download: 'تنزيل',
    replace: 'استبدال',
    delete: 'حذف',
    edit: 'تعديل',
    cancel: 'إلغاء العذر',
    savePending: 'حفظ بانتظار الموافقة',
    saveApprove: 'اعتماد فوري',
    save: 'حفظ التعديلات',
    empty: 'لا توجد أعذار.',
    error: 'تعذر إتمام الإجراء.',
    link: 'ربط بموظف موجود',
    missing: 'يجب ربط المحصل بموظف موجود قبل الموافقة.',
    changed: 'تم تغيير الزيارة الأصلية. راجع سجل التدقيق للاطلاع على القرارات السابقة.',
    history: 'سجل التدقيق',
    fileHint: 'PDF، JPG، JPEG أو PNG · بحد أقصى 10 ميجابايت',
    timeOrder: 'يجب أن يكون وقت الانتهاء بعد وقت البداية.',
    timesRequired: 'من الساعة وإلى الساعة مطلوبان ما لم يتم اختيار يوم كامل.',
    PendingApproval: 'بانتظار الموافقة',
    Approved: 'تمت الموافقة',
    Rejected: 'مرفوض',
    Cancelled: 'ملغي',
    FieldVisit: 'زيارة ميدانية',
    Manual: 'يدوي',
    FieldVisitMission: 'مأمورية / زيارة ميدانية',
    MedicalExcuse: 'عذر طبي',
    PersonalExcuse: 'إذن شخصي',
    OfficialMission: 'مأمورية عمل',
    LateArrivalExcuse: 'عذر تأخير',
    EarlyLeaveExcuse: 'عذر انصراف مبكر',
    Other: 'أخرى',
  },
};

const blank = (): ManualExcuse => ({
  employeeId: '',
  type: 'PersonalExcuse',
  date: '',
  fromTime: null,
  toTime: null,
  fullDay: false,
  approveImmediately: false,
  reason: '',
  notes: null,
});

export function HrExcusesPage({ employeeId: profileEmployeeId }: { employeeId?: string }) {
  const { language, isRtl, ct } = useCollectionsLocalization();
  const w = words[language];
  const label = (key: string) => w[key as keyof typeof w] ?? key;
  const [params, setParams] = useSearchParams();
  const [data, setData] = useState<ExcusePage>();
  const [types, setTypes] = useState<ExcuseTypeOption[]>([]);
  const [search, setSearch] = useState('');
  const [pendingOnly, setPendingOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const version = useRef(0);
  const [item, setItem] = useState<ExcuseItem>();
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState(blank());
  const [selectedEmployee, setSelectedEmployee] = useState<EmployeeListItem | null>(null);
  const [notes, setNotes] = useState('');
  const [reason, setReason] = useState('');
  const [cancelReason, setCancelReason] = useState('');
  const [end, setEnd] = useState('');
  const [linkId, setLinkId] = useState('');
  const [file, setFile] = useState<File>();
  const [replacement, setReplacement] = useState<string>();
  const [formError, setFormError] = useState('');

  const load = useCallback(async () => {
    const v = ++version.current;
    setLoading(true);
    setError('');
    try {
      const result = await service.list({
        search: pendingOnly ? undefined : search,
        employeeId: profileEmployeeId || undefined,
        source: pendingOnly ? 'FieldVisit' : undefined,
        status: pendingOnly ? 'PendingApproval' : undefined,
        page,
      });
      if (v === version.current) setData(result);
    } catch (e) {
      if (v === version.current) setError(getApiErrorMessage(e, w.error));
    } finally {
      if (v === version.current) setLoading(false);
    }
  }, [search, pendingOnly, profileEmployeeId, page, w.error]);

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 250);
    return () => clearTimeout(timer);
  }, [load]);

  useEffect(() => {
    void service.types().then(setTypes).catch(() => setTypes([]));
  }, [language]);

  useEffect(() => {
    if (!form.employeeId) {
      setSelectedEmployee(null);
      return;
    }
    let active = true;
    void hrEmployeeService
      .getEmployee(form.employeeId)
      .then((employee) => {
        if (active) {
          setSelectedEmployee({
            id: employee.id,
            employeeNumber: employee.employeeNumber,
            fullName: employee.fullName,
            departmentId: employee.departmentId,
            departmentName: employee.departmentName,
            departmentCode: employee.departmentCode,
            positionId: employee.positionId,
            positionName: employee.positionName,
            isActive: employee.isActive,
            status: employee.status,
          });
        }
      })
      .catch(() => {
        if (active) setSelectedEmployee(null);
      });
    return () => {
      active = false;
    };
  }, [form.employeeId]);

  async function open(id: string) {
    setFormError('');
    setBusy(true);
    setCreating(false);
    setEditing(false);
    try {
      const r = await service.details(id);
      setItem(r);
      setNotes(r.notes || '');
      setEnd(r.toTime || '');
      setReason('');
      setCancelReason('');
      setFile(undefined);
      setReplacement(undefined);
      setLinkId('');
    } catch (e) {
      setError(getApiErrorMessage(e, w.error));
    } finally {
      setBusy(false);
    }
  }

  const requestId = params.get('requestId');
  useEffect(() => {
    if (requestId) void open(requestId);
  }, [requestId, language]);

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setFormError('');
    try {
      await action();
      await load();
      window.dispatchEvent(new Event('hr-excuses-changed'));
    } catch (e) {
      setFormError(getApiErrorMessage(e, w.error));
    } finally {
      setBusy(false);
    }
  }

  function validateManual(current: ManualExcuse) {
    if (!current.fullDay) {
      if (!current.fromTime || !current.toTime) return w.timesRequired;
      if (current.toTime <= current.fromTime) return w.timeOrder;
    }
    return '';
  }

  async function submitManual(event: FormEvent, approveImmediately: boolean) {
    event.preventDefault();
    const payload = { ...form, approveImmediately, fromTime: form.fullDay ? null : form.fromTime, toTime: form.fullDay ? null : form.toTime };
    const validation = validateManual(payload);
    if (validation) {
      setFormError(validation);
      return;
    }
    await run(async () => {
      const id = editing && item ? await service.edit(item.id, { ...payload, expectedUpdatedAt: item.updatedAt }) : await service.create(payload);
      setCreating(false);
      setEditing(false);
      await open(id);
      if (file && !editing) {
        await service.upload(id, file);
        await open(id);
      }
    });
  }

  function startEdit(current: ExcuseItem) {
    setEditing(true);
    setCreating(true);
    setForm({
      employeeId: current.employeeId || '',
      type: current.type,
      date: current.date,
      fromTime: current.fromTime,
      toTime: current.toTime,
      fullDay: Boolean(current.fullDay),
      approveImmediately: false,
      reason: current.reason || '',
      notes: current.notes,
      expectedUpdatedAt: current.updatedAt,
    });
    setFile(undefined);
    setFormError('');
  }

  function closeReview() {
    setCreating(false);
    setEditing(false);
    setItem(undefined);
    setParams((current) => {
      const next = new URLSearchParams(current);
      next.delete('requestId');
      return next;
    }, { replace: true });
  }

  async function decide(approve: boolean) {
    if (!item) return;
    if (!approve && !reason.trim()) {
      setFormError(w.rejection);
      return;
    }
    await run(async () => {
      await service.decide(item, approve, reason, notes, end);
      closeReview();
    });
  }

  const typeOptions = types.length
    ? types
    : [
        { code: 'PersonalExcuse', name: label('PersonalExcuse'), reasonRequired: true },
        { code: 'MedicalExcuse', name: label('MedicalExcuse'), reasonRequired: true },
        { code: 'OfficialMission', name: label('OfficialMission'), reasonRequired: true },
        { code: 'LateArrivalExcuse', name: label('LateArrivalExcuse'), reasonRequired: true },
        { code: 'EarlyLeaveExcuse', name: label('EarlyLeaveExcuse'), reasonRequired: true },
        { code: 'Other', name: label('Other'), reasonRequired: true },
      ];

  const fields = item
    ? [
        [w.employee, item.employeeName],
        [w.number, item.employeeNumber],
        [w.department, item.department],
        [w.position, item.position],
        [w.type, label(item.type)],
        [w.source, label(item.source)],
        [w.date, item.date],
        [w.fullDay, item.fullDay ? (language === 'ar' ? 'نعم' : 'Yes') : language === 'ar' ? 'لا' : 'No'],
        [w.from, formatExcuseTime(item.fromTime, language)],
        [w.to, formatExcuseTime(item.toTime, language)],
        [w.status, label(item.status)],
        [w.customer, item.customer],
        [w.case, item.caseReference],
        [w.organization, item.organization],
        [w.address, item.address],
        [w.visitStatus, item.visitStatus ? ct(item.visitStatus) : null],
        [w.result, item.visitResult ? ct(item.visitResult) : null],
        [w.visitNotes, item.visitNotes],
        [w.creator, item.visitCreatedBy],
        [w.reason, item.reason],
        [w.notes, item.notes],
        [w.rejection, item.rejectionReason],
      ]
    : [];

  const canEditManual = !!item && item.source === 'Manual' && item.status === 'PendingApproval' && !!data?.canManage;
  const canCancel = !!item && (item.status === 'PendingApproval' || item.status === 'Approved') && !!data?.canManage && (item.status !== 'Approved' || !!data.canApprove);

  return (
    <div dir={isRtl ? 'rtl' : 'ltr'} className="space-y-5">
      <PageHeader
        title={w.title}
        actions={
          <>
            {data?.canManage ? (
              <Button
                fullWidth={false}
                onClick={() => {
                  setForm({ ...blank(), employeeId: profileEmployeeId || '' });
                  setFile(undefined);
                  setFormError('');
                  setEditing(false);
                  setItem(undefined);
                  setCreating(true);
                }}
              >
                {w.add}
              </Button>
            ) : null}
            <button
              type="button"
              className={`rounded-xl border px-5 py-2.5 font-semibold ${pendingOnly ? 'border-sky-400 bg-sky-100 text-sky-950' : 'border-sky-200 bg-sky-50 text-sky-900'}`}
              onClick={() => {
                setPendingOnly(true);
                setSearch('');
                setPage(1);
              }}
            >
              {w.pending}: {data?.pendingApproval ?? '—'}
            </button>
          </>
        }
      />

      <div className="relative max-w-3xl">
        <Search className={`pointer-events-none absolute top-1/2 h-5 w-5 -translate-y-1/2 text-slate-400 ${isRtl ? 'end-3' : 'start-3'}`} aria-hidden />
        <input
          name="excuse-search"
          type="search"
          value={search}
          placeholder={w.searchPlaceholder}
          aria-label={w.searchPlaceholder}
          className={`h-12 w-full rounded-xl border border-mis-border bg-white text-sm text-mis-navy shadow-sm outline-none ring-mis-primary/30 placeholder:text-slate-400 focus:border-mis-primary focus:ring-2 ${isRtl ? 'pe-4 ps-11' : 'ps-11 pe-4'}`}
          onChange={(e) => {
            setSearch(e.target.value);
            setPendingOnly(false);
            setPage(1);
          }}
        />
      </div>

      {error && (
        <p role="alert" className="text-red-700">
          {error}
        </p>
      )}

      {loading ? (
        <LoadingSpinner />
      ) : (
        data && (
          <Card className="overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-slate-50">
                  <tr>
                    {[w.employee, w.number, w.type, w.source, w.date, w.from, w.to, w.status, w.attachment, w.actions].map((h) => (
                      <th key={h} className="whitespace-nowrap px-4 py-3 text-start">
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((r) => (
                    <tr key={r.id} className="border-t border-slate-100">
                      <td className="px-4 py-3">{r.employeeName || '—'}</td>
                      <td className="px-4 py-3">{r.employeeNumber || '—'}</td>
                      <td className="px-4 py-3">{label(r.type)}</td>
                      <td className="px-4 py-3">{label(r.source)}</td>
                      <td className="px-4 py-3">{r.date}</td>
                      <td className="px-4 py-3">{r.fullDay ? label('fullDay') : formatExcuseTime(r.fromTime, language) || '—'}</td>
                      <td className="px-4 py-3">{r.fullDay ? '—' : formatExcuseTime(r.toTime, language) || '—'}</td>
                      <td className="px-4 py-3">{label(r.status)}</td>
                      <td className="px-4 py-3">{r.attachments.length || '—'}</td>
                      <td className="px-4 py-3">
                        <Button disabled={busy} variant="secondary" onClick={() => void open(r.id)}>
                          {r.status === 'PendingApproval' ? w.review : w.view}
                        </Button>
                      </td>
                    </tr>
                  ))}
                  {!data.items.length && (
                    <tr>
                      <td className="p-8 text-center" colSpan={10}>
                        {w.empty}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
            <Pagination page={page} pageSize={data.pageSize} totalCount={data.totalCount} totalPages={data.totalPages} onPageChange={setPage} />
          </Card>
        )
      )}

      <Modal open={creating || !!item} onClose={() => { if (!busy) closeReview(); }} title={creating ? (editing ? w.edit : w.add) : w.review} size="lg">
        {formError && (
          <p role="alert" className="mb-4 text-red-700">
            {formError}
          </p>
        )}

        {creating ? (
          <form className="space-y-4">
            <EmployeeSearchSelect
              required
              label={w.employee}
              value={form.employeeId}
              onChange={(id) => setForm({ ...form, employeeId: id })}
              disabled={editing}
            />
            {selectedEmployee && (
              <dl className="grid gap-3 rounded-xl bg-slate-50 p-4 sm:grid-cols-3">
                <div>
                  <dt className="text-sm text-slate-500">{w.number}</dt>
                  <dd className="font-medium">{selectedEmployee.employeeNumber}</dd>
                </div>
                <div>
                  <dt className="text-sm text-slate-500">{w.department}</dt>
                  <dd className="font-medium">{selectedEmployee.departmentName || '—'}</dd>
                </div>
                <div>
                  <dt className="text-sm text-slate-500">{w.position}</dt>
                  <dd className="font-medium">{selectedEmployee.positionName || '—'}</dd>
                </div>
              </dl>
            )}
            <SelectInput label={w.type} value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })}>
              {typeOptions.map((t) => (
                <option key={t.code} value={t.code}>
                  {t.name}
                </option>
              ))}
            </SelectInput>
            <DateInput required label={w.date} value={form.date} onChange={(e) => setForm({ ...form, date: e.target.value })} />
            <Checkbox
              checked={form.fullDay}
              label={w.fullDay}
              name="fullDay"
              onChange={(e) => setForm({ ...form, fullDay: e.target.checked, fromTime: e.target.checked ? null : form.fromTime, toTime: e.target.checked ? null : form.toTime })}
            />
            {!form.fullDay && (
              <div className="grid grid-cols-2 gap-4">
                <TextInput name="from" type="time" label={w.from} value={form.fromTime || ''} onChange={(e) => setForm({ ...form, fromTime: e.target.value || null })} required />
                <TextInput name="to" type="time" label={w.to} value={form.toTime || ''} onChange={(e) => setForm({ ...form, toTime: e.target.value || null })} required />
              </div>
            )}
            <TextAreaInput required label={w.reason} maxLength={1000} value={form.reason} onChange={(e) => setForm({ ...form, reason: e.target.value })} />
            <TextAreaInput label={w.notes} maxLength={3000} value={form.notes || ''} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
            {!editing && (
              <>
                <label className="block text-sm">
                  {w.upload}
                  <input className="mt-2 block" type="file" accept=".pdf,.jpg,.jpeg,.png" onChange={(e) => setFile(e.target.files?.[0])} />
                </label>
                <p className="text-xs text-slate-500">{w.fileHint}</p>
              </>
            )}
            <div className="flex flex-wrap gap-3">
              <Button type="button" disabled={busy} onClick={(e) => void submitManual(e as unknown as FormEvent, false)}>
                {editing ? w.save : w.savePending}
              </Button>
              {!editing && data?.canApprove && (
                <Button type="button" disabled={busy} variant="secondary" onClick={(e) => void submitManual(e as unknown as FormEvent, true)}>
                  {w.saveApprove}
                </Button>
              )}
            </div>
          </form>
        ) : (
          item && (
            <div className="space-y-5">
              {item.sourceChanged && <p className="rounded-lg bg-amber-50 p-3 text-amber-900">{w.changed}</p>}
              <dl className="grid gap-3 sm:grid-cols-2">
                {fields.map(([k, v]) => (
                  <div key={String(k)}>
                    <dt className="text-sm text-slate-500">{k}</dt>
                    <dd className="whitespace-pre-wrap break-words font-medium">{v || '—'}</dd>
                  </div>
                ))}
              </dl>
              <div className="flex flex-wrap gap-4">
                {item.visitId && (
                  <Link className="text-mis-primary underline" to={`/hr/excuses/visits/${item.visitId}`}>
                    {w.original}
                  </Link>
                )}
                <Link className="text-mis-primary underline" to={`/hr/audit?employeeId=${item.employeeId || ''}`}>
                  {w.history}
                </Link>
                {canEditManual && (
                  <Button fullWidth={false} variant="secondary" disabled={busy} onClick={() => startEdit(item)}>
                    {w.edit}
                  </Button>
                )}
              </div>
              {!item.employeeId && data?.canApprove && item.collectorUserId && (
                <div className="space-y-3 rounded-lg bg-amber-50 p-4">
                  <p>{w.missing}</p>
                  <EmployeeSearchSelect label={w.employee} value={linkId} onChange={setLinkId} includeInactive />
                  <Button
                    disabled={busy || !linkId}
                    onClick={() =>
                      void run(async () => {
                        await service.link(item.collectorUserId!, linkId);
                        await open(item.id);
                      })
                    }
                  >
                    {w.link}
                  </Button>
                </div>
              )}
              <div className="space-y-3">
                {item.attachments.map((a) => (
                  <div key={a.id} className="flex flex-wrap items-center gap-2 rounded-lg border p-3">
                    <span className="break-all">{a.fileName}</span>
                    <Button disabled={busy} variant="secondary" onClick={() => void run(() => service.file(item.id, a, false))}>
                      {w.view}
                    </Button>
                    <Button disabled={busy} variant="secondary" onClick={() => void run(() => service.file(item.id, a, true))}>
                      {w.download}
                    </Button>
                    {data?.canManage && (
                      <>
                        <Button
                          disabled={busy}
                          variant="secondary"
                          onClick={() => {
                            setReplacement(a.id);
                            setFile(undefined);
                          }}
                        >
                          {w.replace}
                        </Button>
                        <Button
                          disabled={busy}
                          variant="secondary"
                          onClick={() =>
                            void run(async () => {
                              await service.remove(item.id, a.id);
                              await open(item.id);
                            })
                          }
                        >
                          {w.delete}
                        </Button>
                      </>
                    )}
                  </div>
                ))}
              </div>
              {data?.canManage && (
                <div className="space-y-2">
                  <label className="block text-sm">
                    {replacement ? w.replace : w.upload}
                    <input key={replacement || item.updatedAt} disabled={busy} className="mt-2 block" type="file" accept=".pdf,.jpg,.jpeg,.png" onChange={(e) => setFile(e.target.files?.[0])} />
                  </label>
                  <p className="text-xs text-slate-500">{w.fileHint}</p>
                  <Button
                    disabled={busy || !file}
                    onClick={() =>
                      void run(async () => {
                        await service.upload(item.id, file!, replacement);
                        await open(item.id);
                      })
                    }
                  >
                    {replacement ? w.replace : w.upload}
                  </Button>
                </div>
              )}
              {item.status === 'PendingApproval' && data?.canApprove && (
                <div className="space-y-4 border-t pt-4">
                  {!item.fullDay && <TextInput name="confirm-end" type="time" label={w.to} value={end.slice(0, 5)} onChange={(e) => setEnd(e.target.value)} />}
                  <TextAreaInput label={w.notes} maxLength={3000} value={notes} onChange={(e) => setNotes(e.target.value)} />
                  <TextAreaInput label={w.rejection} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
                  <div className="flex gap-3">
                    <Button disabled={busy || !item.employeeId} onClick={() => void decide(true)}>
                      {w.approve}
                    </Button>
                    <Button variant="danger" disabled={busy || !reason.trim()} onClick={() => void decide(false)}>
                      {w.reject}
                    </Button>
                  </div>
                </div>
              )}
              {canCancel && (
                <div className="space-y-3 border-t pt-4">
                  <TextAreaInput label={w.cancelReason} maxLength={1000} value={cancelReason} onChange={(e) => setCancelReason(e.target.value)} />
                  <Button
                    variant="danger"
                    disabled={busy || !cancelReason.trim()}
                    onClick={() =>
                      void run(async () => {
                        await service.cancel(item, cancelReason);
                        closeReview();
                      })
                    }
                  >
                    {w.cancel}
                  </Button>
                </div>
              )}
            </div>
          )
        )}
      </Modal>
    </div>
  );
}
