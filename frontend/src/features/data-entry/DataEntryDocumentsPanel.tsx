import { Download, Paperclip, Trash2, Upload } from 'lucide-react';
import { useState } from 'react';
import { Button } from '../../components/common/Button';
import { useToast } from '../../components/common/Toast';
import { getApiErrorMessage } from '../../services/apiClient';
import { useDataEntryText } from './dataEntryUi';
import { dataEntryService } from './services/dataEntryService';
import type { DataEntryDocument } from './types/dataEntry';

export function DataEntryDocumentsPanel({
  documents,
  canUpload,
  canDownload,
  uploading = false,
  onUpload,
  onDelete,
  emptyHint,
}: {
  documents: DataEntryDocument[];
  canUpload?: boolean;
  canDownload?: boolean;
  uploading?: boolean;
  onUpload?: (file: File, note: string) => Promise<void>;
  onDelete?: (id: string) => Promise<void>;
  emptyHint?: string;
}) {
  const d = useDataEntryText();
  const toast = useToast();
  const [note, setNote] = useState('');
  const [busyId, setBusyId] = useState('');

  async function takeFile(file?: File | null) {
    if (!file || !onUpload) return;
    try {
      await onUpload(file, note);
      setNote('');
    } catch (reason) {
      toast.error(getApiErrorMessage(reason, d.text('تعذر رفع الملف', 'Could not upload the file')));
    }
  }

  return (
    <section className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="flex items-center gap-2 text-base font-bold text-mis-navy">
            <Paperclip className="h-4 w-4 text-mis-primary" />
            {d.text('ملفات وبيانات الحالة', 'Case files and data')}
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            {canDownload
              ? d.text('ملفات إدخال البيانات الخاصة بهذه الحالة. تظهر للمشرف فقط.', 'Data-entry files for this case. Visible to supervisors only.')
              : d.text('ارفع المستندات أو ملفات البيانات للحالة. المشرف وحده يفتحها بعد الإرسال.', 'Upload supporting documents or data files for the case. Only the supervisor can open them after send.')}
          </p>
        </div>
        <span className="rounded-full bg-mis-pale px-3 py-1 text-xs font-bold text-mis-primary">
          {d.number(documents.length)}
        </span>
      </div>

      {canUpload ? (
        <div className="mt-4 space-y-3">
          <input
            className="h-11 w-full rounded-xl border border-mis-border px-3 text-sm"
            maxLength={500}
            placeholder={d.text('ملاحظة للمشرف (اختياري)', 'Note for the supervisor (optional)')}
            value={note}
            onChange={(event) => setNote(event.target.value)}
          />
          <label className="flex cursor-pointer items-center justify-center gap-2 rounded-xl border-2 border-dashed border-mis-border bg-slate-50 px-4 py-4 text-sm font-semibold text-mis-navy hover:border-mis-primary hover:bg-mis-pale">
            <Upload className="h-4 w-4" />
            {uploading ? d.text('جارٍ الرفع...', 'Uploading...') : d.text('رفع ملف أو بيانات Excel/PDF', 'Upload a file or Excel/PDF data')}
            <input
              accept=".pdf,.jpg,.jpeg,.png,.csv,.xlsx,.xls"
              className="hidden"
              disabled={uploading}
              type="file"
              onChange={(event) => {
                const file = event.target.files?.[0];
                event.target.value = '';
                void takeFile(file);
              }}
            />
          </label>
        </div>
      ) : null}

      {documents.length ? (
        <ul className="mt-4 divide-y divide-mis-border rounded-xl border border-mis-border">
          {documents.map((item) => (
            <li className="flex items-center gap-3 px-4 py-3" key={item.id}>
              <Paperclip className="h-4 w-4 shrink-0 text-mis-primary" />
              <div className="min-w-0 flex-1">
                <p className="truncate font-semibold text-mis-navy">{item.originalFileName}</p>
                <p className="mt-0.5 text-xs text-slate-500">
                  {item.uploadedBy} · {d.dateTime(item.uploadedAt)} · {(item.fileSize / 1024).toFixed(1)} KB
                  {canDownload ? '' : ` · ${d.text('ظاهر للمشرف فقط', 'Supervisor only')}`}
                </p>
                {item.note ? <p className="mt-1 text-sm text-slate-600">{item.note}</p> : null}
              </div>
              {item.canDownload || canDownload ? (
                <Button
                  fullWidth={false}
                  size="sm"
                  type="button"
                  variant="ghost"
                  onClick={() => void dataEntryService.downloadDocument(item.id, item.originalFileName)}
                >
                  <Download className="h-4 w-4" />
                </Button>
              ) : null}
              {onDelete ? (
                <Button
                  className="text-rose-600 hover:bg-rose-50"
                  disabled={busyId === item.id}
                  fullWidth={false}
                  size="sm"
                  type="button"
                  variant="ghost"
                  onClick={() => {
                    setBusyId(item.id);
                    void onDelete(item.id).finally(() => setBusyId(''));
                  }}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              ) : null}
            </li>
          ))}
        </ul>
      ) : (
        <p className="mt-4 text-sm text-slate-500">{emptyHint ?? d.text('لا توجد ملفات بعد.', 'No files yet.')}</p>
      )}
    </section>
  );
}
