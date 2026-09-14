import { Files, Upload } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Button } from '../../../components/common/Button';
import { Modal } from '../../../components/common/Modal';
import { StatusBadge } from '../../../components/common/StatusBadge';
import { useToast } from '../../../components/common/Toast';
import { SelectInput } from '../../../components/forms/SelectInput';
import { useLocalization } from '../../../context/LocalizationContext';
import { EmployeeSearchSelect } from '../../hr/components/EmployeeSearchSelect';
import {
  hrEmployeeDocumentService,
  type EmployeeDocumentBulkCorrection,
  type EmployeeDocumentBulkItem,
  type EmployeeDocumentBulkResult,
  type EmployeeDocumentBulkUpload,
} from '../../hr/services/hrEmployeeDocumentService';
import type { RequiredDocumentCode } from '../../hr/types/document';
import { getApiErrorMessage } from '../../../services/apiClient';

const documentCodes: RequiredDocumentCode[] = [
  'BIRTH_CERTIFICATE',
  'GRADUATION_CERTIFICATE',
  'NATIONAL_ID_COPY',
  'MILITARY_STATUS',
  'CRIMINAL_RECORD',
  'EMPLOYMENT_APPOINTMENT_PAPER',
  'LABOR_OFFICE_REGISTRATION',
];

const copy = {
  en: {
    title: 'Bulk Upload Employee Documents',
    guideTitle: 'Recommended file name format',
    guideFormat: 'EmployeeNumber_EmployeeName_DocumentType.ext',
    guideExamples: '1001_Nada_BirthCertificate.pdf · 1001_Nada_NationalID.jpg · 1001_ندى_شهادة_الميلاد.pdf',
    drag: 'Drag files here',
    or: 'or',
    choose: 'Choose files',
    formats: 'PDF / JPG / JPEG / PNG · maximum 10 MB each · up to 50 files',
    preview: 'Preview',
    fileName: 'File Name',
    employee: 'Matched Employee',
    employeeNumber: 'Employee Number',
    documentType: 'Detected Document Type',
    status: 'Status',
    action: 'Action',
    selectEmployee: 'Select Employee',
    selectType: 'Select Document Type',
    skip: 'Skip',
    replace: 'Replace Existing',
    confirm: 'Confirm Upload',
    cancel: 'Cancel',
    ready: 'Ready',
    notFound: 'Employee Not Found',
    ambiguous: 'Ambiguous Employee',
    typeUnknown: 'Document Type Not Recognized',
    exists: 'Document Already Exists',
    error: 'Error',
    invalid: 'Choose PDF, JPG, JPEG, or PNG files no larger than 10 MB.',
    uploadError: 'Unable to prepare the bulk upload.',
    confirmError: 'Unable to complete the bulk upload.',
    resultTitle: 'Bulk upload complete',
    uploaded: 'Uploaded',
    replaced: 'Replaced',
    skipped: 'Skipped',
    failed: 'Failed',
    done: 'Done',
    birth: 'Birth Certificate',
    graduation: 'Graduation Certificate',
    idCopy: 'National ID Copy',
    military: 'Military Exemption / Status',
    criminal: 'Criminal Record',
    appointment: 'Employment / Appointment Paper',
    labor: 'Labor Office Registration (Kaab El Amal)',
  },
  ar: {
    title: 'رفع مستندات الموظفين دفعة واحدة',
    guideTitle: 'صيغة اسم الملف الموصى بها',
    guideFormat: 'رقم الموظف_اسم الموظف_نوع المستند',
    guideExamples: '1001_Nada_BirthCertificate.pdf · 1001_ندى_شهادة_الميلاد.pdf · 1001_ندى_صورة_البطاقة.jpg',
    drag: 'اسحب الملفات هنا',
    or: 'أو',
    choose: 'اختر الملفات',
    formats: 'PDF / JPG / JPEG / PNG · بحد أقصى 10 ميجابايت لكل ملف · حتى 50 ملفًا',
    preview: 'المعاينة',
    fileName: 'اسم الملف',
    employee: 'الموظف المطابق',
    employeeNumber: 'رقم الموظف',
    documentType: 'نوع المستند المكتشف',
    status: 'الحالة',
    action: 'الإجراء',
    selectEmployee: 'اختر الموظف',
    selectType: 'اختر نوع المستند',
    skip: 'تخطي',
    replace: 'استبدال المستند الحالي',
    confirm: 'تأكيد الرفع',
    cancel: 'إلغاء',
    ready: 'جاهز',
    notFound: 'الموظف غير موجود',
    ambiguous: 'أكثر من موظف مطابق',
    typeUnknown: 'نوع المستند غير معروف',
    exists: 'المستند موجود بالفعل',
    error: 'خطأ',
    invalid: 'اختر ملفات PDF أو JPG أو JPEG أو PNG لا تتجاوز 10 ميجابايت.',
    uploadError: 'تعذر تجهيز الرفع الجماعي.',
    confirmError: 'تعذر إكمال الرفع الجماعي.',
    resultTitle: 'اكتمل الرفع الجماعي',
    uploaded: 'تم الرفع',
    replaced: 'تم الاستبدال',
    skipped: 'تم التخطي',
    failed: 'فشل',
    done: 'تم',
    birth: 'شهادة الميلاد',
    graduation: 'شهادة التخرج',
    idCopy: 'صورة البطاقة',
    military: 'ورق الإعفاء / موقف التجنيد',
    criminal: 'الفيش الجنائي',
    appointment: 'ورقة التعيين',
    labor: 'كعب العمل',
  },
} as const;

type BulkText = (typeof copy)[keyof typeof copy];

function docLabel(code: RequiredDocumentCode, text: BulkText) {
  const names: Record<RequiredDocumentCode, string> = {
    BIRTH_CERTIFICATE: text.birth,
    GRADUATION_CERTIFICATE: text.graduation,
    NATIONAL_ID_COPY: text.idCopy,
    MILITARY_STATUS: text.military,
    CRIMINAL_RECORD: text.criminal,
    EMPLOYMENT_APPOINTMENT_PAPER: text.appointment,
    LABOR_OFFICE_REGISTRATION: text.labor,
  };
  return names[code];
}

function statusLabel(status: string, text: BulkText) {
  switch (status) {
    case 'Ready': return text.ready;
    case 'EmployeeNotFound': return text.notFound;
    case 'AmbiguousEmployee': return text.ambiguous;
    case 'DocumentTypeNotRecognized': return text.typeUnknown;
    case 'DocumentAlreadyExists': return text.exists;
    default: return text.error;
  }
}

function statusTone(status: string): 'success' | 'warning' | 'danger' | 'neutral' {
  if (status === 'Ready') return 'success';
  if (status === 'DocumentAlreadyExists') return 'warning';
  if (status === 'Error') return 'danger';
  return 'neutral';
}

function correctionsFrom(items: EmployeeDocumentBulkItem[]): EmployeeDocumentBulkCorrection[] {
  return items.map((item) => ({
    fileId: item.fileId,
    employeeId: item.employeeId,
    documentCode: item.documentCode,
    duplicateAction: item.duplicateAction === 'Replace' ? 'Replace' : 'Skip',
  }));
}

export function HrEmployeeDocumentBulkUploadModal({ onClose, onCompleted }: { onClose: () => void; onCompleted: () => void }) {
  const { language } = useLocalization();
  const text = copy[language];
  const toast = useToast();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [batch, setBatch] = useState<EmployeeDocumentBulkUpload | null>(null);
  const [result, setResult] = useState<EmployeeDocumentBulkResult | null>(null);
  const [dragOver, setDragOver] = useState(false);

  const readyCount = useMemo(
    () => batch?.items.filter((item) => item.status === 'Ready' || (item.status === 'DocumentAlreadyExists' && item.duplicateAction === 'Replace')).length ?? 0,
    [batch],
  );

  async function acceptFiles(list: FileList | File[] | null) {
    if (!list) return;
    const files = Array.from(list);
    if (files.length === 0) return;
    if (files.some((file) => file.size > 10 * 1024 * 1024 || !/\.(pdf|jpe?g|png)$/i.test(file.name))) {
      setError(text.invalid);
      return;
    }
    setBusy(true);
    setError('');
    try {
      setBatch(await hrEmployeeDocumentService.bulkUpload(files));
      setResult(null);
    } catch (reason) {
      setError(getApiErrorMessage(reason, text.uploadError));
    } finally {
      setBusy(false);
    }
  }

  async function patchItem(fileId: string, patch: Partial<EmployeeDocumentBulkItem>) {
    if (!batch) return;
    const nextItems = batch.items.map((item) => (item.fileId === fileId ? { ...item, ...patch } : item));
    setBatch({ ...batch, items: nextItems });
    setBusy(true);
    setError('');
    try {
      setBatch(await hrEmployeeDocumentService.bulkPreview(batch.id, correctionsFrom(nextItems)));
    } catch (reason) {
      setError(getApiErrorMessage(reason, text.uploadError));
    } finally {
      setBusy(false);
    }
  }

  async function confirm() {
    if (!batch) return;
    setBusy(true);
    setError('');
    try {
      const outcome = await hrEmployeeDocumentService.bulkConfirm(batch.id, correctionsFrom(batch.items));
      setResult(outcome);
      toast.success(`${text.uploaded}: ${outcome.uploaded} · ${text.replaced}: ${outcome.replaced}`);
      onCompleted();
    } catch (reason) {
      setError(getApiErrorMessage(reason, text.confirmError));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      closeOnBackdrop={!busy}
      onClose={onClose}
      open
      size="xl"
      title={result ? text.resultTitle : text.title}
      footer={
        result ? (
          <Button fullWidth={false} onClick={onClose}>{text.done}</Button>
        ) : (
          <>
            <Button disabled={busy} fullWidth={false} onClick={onClose} variant="outline">{text.cancel}</Button>
            {batch ? <Button disabled={busy || readyCount === 0} fullWidth={false} isLoading={busy} onClick={() => void confirm()}>{text.confirm}</Button> : null}
          </>
        )
      }
    >
      {error ? <div className="mb-4 rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</div> : null}

      {result ? (
        <div className="grid gap-3 sm:grid-cols-4">
          {[
            [text.uploaded, result.uploaded],
            [text.replaced, result.replaced],
            [text.skipped, result.skipped],
            [text.failed, result.failed],
          ].map(([label, value]) => (
            <div className="rounded-xl bg-mis-surface p-4" key={String(label)}>
              <p className="text-sm text-slate-500">{label}</p>
              <p className="mt-1 text-2xl font-bold text-mis-navy">{value}</p>
            </div>
          ))}
        </div>
      ) : !batch ? (
        <div className="space-y-4">
          <div className="rounded-xl border border-mis-border bg-mis-surface p-4 text-sm">
            <p className="font-semibold text-mis-navy">{text.guideTitle}</p>
            <p className="mt-1 font-mono text-xs text-slate-600" dir="ltr">{text.guideFormat}</p>
            <p className="mt-2 text-xs text-slate-500" dir="auto">{text.guideExamples}</p>
          </div>
          <div
            className={`rounded-2xl border-2 border-dashed p-8 text-center transition ${dragOver ? 'border-mis-primary bg-mis-pale' : 'border-mis-border bg-slate-50'}`}
            onDragEnter={(event) => { event.preventDefault(); setDragOver(true); }}
            onDragLeave={(event) => { event.preventDefault(); setDragOver(false); }}
            onDragOver={(event) => event.preventDefault()}
            onDrop={(event) => { event.preventDefault(); setDragOver(false); void acceptFiles(event.dataTransfer.files); }}
          >
            <Files className="mx-auto h-10 w-10 text-mis-primary" />
            <p className="mt-3 font-semibold text-mis-navy">{text.drag}</p>
            <p className="mt-1 text-sm text-slate-500">{text.or}</p>
            <label className="mt-4 inline-flex cursor-pointer items-center gap-2 rounded-xl bg-mis-primary px-4 py-2 text-sm font-semibold text-white">
              <Upload className="h-4 w-4" />
              {text.choose}
              <input
                accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
                className="hidden"
                disabled={busy}
                multiple
                onChange={(event) => void acceptFiles(event.target.files)}
                type="file"
              />
            </label>
            <p className="mt-3 text-xs text-slate-500">{text.formats}</p>
          </div>
        </div>
      ) : (
        <div className="space-y-4">
          <div className="rounded-xl border border-mis-border bg-mis-surface p-3 text-sm text-slate-600">
            {text.preview}: {batch.items.length} · {text.ready}: {readyCount}
          </div>
          <div className="max-h-[55vh] overflow-auto rounded-xl border border-mis-border">
            <table className="min-w-full text-sm">
              <thead className="sticky top-0 bg-mis-surface text-xs uppercase text-slate-500">
                <tr>
                  {[text.fileName, text.employee, text.employeeNumber, text.documentType, text.status, text.action].map((label) => (
                    <th className="px-3 py-3 text-start" key={label}>{label}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {batch.items.map((item) => (
                  <tr key={item.fileId}>
                    <td className="max-w-48 px-3 py-3 align-top">
                      <p className="truncate font-medium text-mis-navy" dir="auto" title={item.fileName}>{item.fileName}</p>
                      {item.errors.length ? <p className="mt-1 text-xs text-amber-700" dir="auto">{item.errors[0]}</p> : null}
                    </td>
                    <td className="min-w-52 px-3 py-3 align-top">
                      <EmployeeSearchSelect
                        initialSelection={item.employeeId && item.employeeNumber && item.employeeName
                          ? { id: item.employeeId, employeeNumber: item.employeeNumber, fullName: item.employeeName }
                          : null}
                        label=""
                        onChange={(employeeId) => void patchItem(item.fileId, { employeeId: employeeId || null })}
                        value={item.employeeId ?? ''}
                      />
                    </td>
                    <td className="px-3 py-3 align-top text-slate-600">{item.employeeNumber ?? '—'}</td>
                    <td className="min-w-48 px-3 py-3 align-top">
                      <SelectInput
                        label=""
                        onChange={(event) => void patchItem(item.fileId, { documentCode: (event.target.value || null) as RequiredDocumentCode | null })}
                        value={item.documentCode ?? ''}
                      >
                        <option value="">{text.selectType}</option>
                        {documentCodes.map((code) => (
                          <option key={code} value={code}>{docLabel(code, text)}</option>
                        ))}
                      </SelectInput>
                    </td>
                    <td className="px-3 py-3 align-top">
                      <StatusBadge dot tone={statusTone(item.status)}>{statusLabel(item.status, text)}</StatusBadge>
                    </td>
                    <td className="min-w-40 px-3 py-3 align-top">
                      {item.status === 'DocumentAlreadyExists' ? (
                        <SelectInput
                          label=""
                          onChange={(event) => void patchItem(item.fileId, { duplicateAction: event.target.value as 'Skip' | 'Replace' })}
                          value={item.duplicateAction === 'Replace' ? 'Replace' : 'Skip'}
                        >
                          <option value="Skip">{text.skip}</option>
                          <option value="Replace">{text.replace}</option>
                        </SelectInput>
                      ) : (
                        <span className="text-slate-400">—</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </Modal>
  );
}
