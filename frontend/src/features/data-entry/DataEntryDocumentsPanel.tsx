import { Download, Eye, FileText, Image as ImageIcon, Paperclip, Trash2, Upload } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Button } from '../../components/common/Button';
import { Modal } from '../../components/common/Modal';
import { useToast } from '../../components/common/Toast';
import { getApiErrorMessage } from '../../services/apiClient';
import { useDataEntryText } from './dataEntryUi';
import { dataEntryService } from './services/dataEntryService';
import type { DataEntryDocument } from './types/dataEntry';

const presets = [
  ['شهادة ميلاد', 'Birth certificate'],
  ['بطاقة رقم قومي', 'National ID'],
  ['عقد', 'Contract'],
  ['إيصال', 'Receipt'],
  ['توكيل', 'Power of attorney'],
  ['كشف حساب', 'Account statement'],
  ['صورة شخصية', 'Photo'],
] as const;

type Preview = {
  title: string;
  name: string;
  url: string;
  type: string;
  text?: string;
};

function isImage(type: string) {
  return type.startsWith('image/') && !type.includes('svg');
}

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
  const [label, setLabel] = useState('');
  const [busyId, setBusyId] = useState('');
  const [preview, setPreview] = useState<Preview | null>(null);
  const previewRef = useRef<Preview | null>(null);
  previewRef.current = preview;

  useEffect(() => () => {
    if (previewRef.current?.url) URL.revokeObjectURL(previewRef.current.url);
  }, []);

  function closePreview() {
    if (preview?.url) URL.revokeObjectURL(preview.url);
    setPreview(null);
  }

  async function takeFile(file?: File | null) {
    if (!file || !onUpload) return;
    const description = label.trim() || file.name.replace(/\.[^.]+$/, '') || file.name;
    try {
      await onUpload(file, description);
      setLabel('');
    } catch (reason) {
      toast.error(getApiErrorMessage(reason, d.text('تعذر رفع الملف', 'Could not upload the file')));
    }
  }

  async function openFile(item: DataEntryDocument) {
    setBusyId(item.id);
    try {
      const file = await dataEntryService.openDocument(item.id);
      const type = (file.contentType || item.contentType || file.blob.type || '').toLowerCase();
      if (previewRef.current?.url) URL.revokeObjectURL(previewRef.current.url);
      const url = URL.createObjectURL(file.blob);
      let text: string | undefined;
      if (type.startsWith('text/') || type.includes('csv') || type.includes('json') || type.includes('xml')) {
        text = (await file.blob.text()).slice(0, 200_000);
      }
      setPreview({
        title: item.note?.trim() || item.originalFileName,
        name: file.fileName || item.originalFileName,
        url,
        type,
        text,
      });
    } catch (reason) {
      toast.error(getApiErrorMessage(reason, d.text('تعذر عرض الملف', 'Could not open the file')));
    } finally {
      setBusyId('');
    }
  }

  const canOpen = (item: DataEntryDocument) => item.canDownload || Boolean(canDownload);

  return (
    <section className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="flex items-center gap-2 text-base font-bold text-mis-navy">
            <Paperclip className="h-4 w-4 text-mis-primary" />
            {d.text('مستندات العميل', 'Client documents')}
          </h2>
          <p className="mt-1 max-w-2xl text-sm leading-6 text-slate-500">
            {d.text(
              'كل ملف لوحده. سجّل الملف عبارة عن إيه — شهادة ميلاد أو أي ورق — وبعد الرفع يتعرض هنا.',
              'Each file stays separate. Record what it is — a birth certificate or any paper — and it opens here after upload.',
            )}
          </p>
        </div>
        <span className="rounded-full bg-mis-pale px-3 py-1 text-xs font-bold text-mis-primary">
          {d.number(documents.length)}
        </span>
      </div>

      {canUpload ? (
        <div className="mt-4 space-y-3 rounded-2xl border border-dashed border-mis-border bg-slate-50 p-4">
          <label className="block text-sm font-semibold text-mis-navy" htmlFor="client-file-label">
            {d.text('وصف الملف اختياري', 'File description, optional')}
          </label>
          <div className="flex flex-wrap gap-2">
            {presets.map(([arabic, english]) => {
              const value = d.ar ? arabic : english;
              const active = label.trim() === value;
              return (
                <button
                  className={`rounded-full px-3 py-1 text-xs font-bold ${active ? 'bg-mis-primary text-white' : 'bg-white text-slate-600 ring-1 ring-mis-border hover:text-mis-navy'}`}
                  key={arabic}
                  type="button"
                  onClick={() => setLabel(value)}
                >
                  {value}
                </button>
              );
            })}
          </div>
          <input
            className="h-11 w-full rounded-xl border border-mis-border bg-white px-3 text-sm"
            id="client-file-label"
            maxLength={500}
            placeholder={d.text('لو سبتها فاضية، اسم الملف هو الوصف', 'Leave empty and the file name is used')}
            value={label}
            onChange={(event) => setLabel(event.target.value)}
          />
          <label className="flex cursor-pointer items-center justify-center gap-2 rounded-xl border-2 border-dashed border-mis-primary bg-white px-4 py-5 text-sm font-semibold text-mis-navy hover:bg-mis-pale">
            <Upload className="h-4 w-4" />
            {uploading
              ? d.text('جارٍ الرفع...', 'Uploading...')
              : d.text('ارفع أي ملف: صورة، PDF، وورد، إكسل، أو غيره', 'Upload any file: image, PDF, Word, Excel, or anything else')}
            <input
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
        <ul className="mt-4 grid gap-3 sm:grid-cols-2">
          {documents.map((item) => {
            const title = item.note?.trim() || item.originalFileName;
            const image = isImage(item.contentType);
            return (
              <li className="flex min-w-0 flex-col rounded-2xl border border-mis-border bg-slate-50 p-4" key={item.id}>
                <div className="flex items-start gap-3">
                  <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-white text-mis-primary shadow-sm">
                    {image ? <ImageIcon className="h-4 w-4" /> : <FileText className="h-4 w-4" />}
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-bold text-mis-navy">{title}</p>
                    <p className="mt-0.5 truncate text-xs text-slate-500" dir="ltr">{item.originalFileName}</p>
                    <p className="mt-1 text-xs text-slate-500">
                      {item.uploadedBy} · {d.dateTime(item.uploadedAt)} · {(item.fileSize / 1024).toFixed(1)} KB
                    </p>
                  </div>
                </div>
                <div className="mt-3 flex flex-wrap gap-1">
                  {canOpen(item) ? (
                    <Button disabled={busyId === item.id} fullWidth={false} leftIcon={<Eye className="h-4 w-4" />} size="sm" type="button" variant="outline" onClick={() => void openFile(item)}>
                      {d.text('عرض', 'View')}
                    </Button>
                  ) : null}
                  {canOpen(item) ? (
                    <Button
                      fullWidth={false}
                      leftIcon={<Download className="h-4 w-4" />}
                      size="sm"
                      type="button"
                      variant="ghost"
                      onClick={() => void dataEntryService.downloadDocument(item.id, item.originalFileName)}
                    >
                      {d.text('تنزيل', 'Download')}
                    </Button>
                  ) : null}
                  {onDelete ? (
                    <Button
                      className="text-rose-600 hover:bg-rose-50"
                      disabled={busyId === item.id}
                      fullWidth={false}
                      leftIcon={<Trash2 className="h-4 w-4" />}
                      size="sm"
                      type="button"
                      variant="ghost"
                      onClick={() => {
                        setBusyId(item.id);
                        void onDelete(item.id).finally(() => setBusyId(''));
                      }}
                    >
                      {d.text('حذف', 'Delete')}
                    </Button>
                  ) : null}
                </div>
              </li>
            );
          })}
        </ul>
      ) : (
        <p className="mt-4 text-sm text-slate-500">{emptyHint ?? d.text('لا توجد مستندات لهذا العميل بعد.', 'No documents for this client yet.')}</p>
      )}

      <Modal
        description={<span dir="ltr">{preview?.name}</span>}
        onClose={closePreview}
        open={Boolean(preview)}
        size="xl"
        title={preview?.title ?? ''}
        footer={preview ? (
          <Button
            fullWidth={false}
            leftIcon={<Download className="h-4 w-4" />}
            type="button"
            variant="outline"
            onClick={() => {
              const link = document.createElement('a');
              link.href = preview.url;
              link.download = preview.name;
              link.click();
            }}
          >
            {d.text('تنزيل', 'Download')}
          </Button>
        ) : null}
      >
        {preview ? <FileStage preview={preview} fallback={d.text('الملف محفوظ. افتحه أو نزّله من هنا.', 'The file is stored. Open or download it from here.')} /> : null}
      </Modal>
    </section>
  );
}

function FileStage({ preview, fallback }: { preview: Preview; fallback: string }) {
  if (isImage(preview.type)) {
    return <img alt={preview.title} className="mx-auto max-h-[70vh] w-full rounded-xl object-contain" src={preview.url} />;
  }
  if (preview.type.includes('pdf')) {
    return <iframe className="h-[70vh] w-full rounded-xl border border-mis-border" src={preview.url} title={preview.title} />;
  }
  if (preview.text) {
    return <pre className="max-h-[70vh] overflow-auto whitespace-pre-wrap rounded-xl bg-slate-50 p-4 text-sm text-slate-700">{preview.text}</pre>;
  }
  return (
    <div className="grid min-h-48 place-items-center rounded-xl bg-slate-50 p-6 text-center">
      <div>
        <Paperclip className="mx-auto h-8 w-8 text-mis-primary" />
        <p className="mt-3 text-sm font-semibold text-mis-navy" dir="ltr">{preview.name}</p>
        <p className="mt-2 text-sm text-slate-500">{fallback}</p>
      </div>
    </div>
  );
}
