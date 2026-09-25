import { Paperclip, Search } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { useToast } from '../../components/common/Toast';
import { useAuth } from '../../context/AuthContext';
import { DataEntryDocumentsPanel } from '../../features/data-entry/DataEntryDocumentsPanel';
import { useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryClientListItem, DataEntryDocument } from '../../features/data-entry/types/dataEntry';

export function DataEntryFilesPage() {
  const d = useDataEntryText();
  const toast = useToast();
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const selectedId = params.get('client') ?? '';
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [clients, setClients] = useState<DataEntryClientListItem[]>([]);
  const [documents, setDocuments] = useState<DataEntryDocument[]>([]);
  const [loadingClients, setLoadingClients] = useState(true);
  const [loadingFiles, setLoadingFiles] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState(false);
  const canUpload = Boolean(user?.roles.some((role) => ['Admin', 'DataEntry'].includes(role)) || user?.permissions.includes('data_entry.manage') || user?.permissions.includes('data_entry.access'));
  const selected = clients.find((item) => item.id === selectedId) ?? null;

  useEffect(() => {
    const timer = setTimeout(() => setQuery(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let cancelled = false;
    setLoadingClients(true);
    setError(false);
    dataEntryService.clients(query || undefined)
      .then((result) => { if (!cancelled) setClients(result.items); })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoadingClients(false); });
    return () => { cancelled = true; };
  }, [query]);

  useEffect(() => {
    if (!selectedId) {
      setDocuments([]);
      return;
    }
    let cancelled = false;
    setLoadingFiles(true);
    dataEntryService.clientDocuments(selectedId)
      .then((files) => { if (!cancelled) setDocuments(files); })
      .catch(() => { if (!cancelled) setDocuments([]); })
      .finally(() => { if (!cancelled) setLoadingFiles(false); });
    return () => { cancelled = true; };
  }, [selectedId]);

  function choose(id: string) {
    const next = new URLSearchParams(params);
    next.set('client', id);
    setParams(next);
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title={d.text('ملفات العملاء', 'Client files')}
        description={d.text('اختار العميل، سجّل الملف عبارة عن إيه، وارفع أي ورق: شهادة ميلاد، بطاقة، عقد، أو أي ملف تاني. كل ملف لوحده ويتفتح بعد الرفع.', 'Pick a client, record what the file is, and upload any paper: birth certificate, ID, contract, or any other file. Each file stays separate and opens after upload.')}
      />
      {error ? (
        <ErrorState title={d.text('تعذر تحميل العملاء', 'Could not load clients')} onRetry={() => setQuery((value) => value)} />
      ) : (
        <div className="grid gap-4 lg:grid-cols-[20rem_minmax(0,1fr)]">
          <section className="rounded-2xl border border-mis-border bg-white shadow-sm">
            <div className="border-b border-mis-border p-4">
              <div className="relative">
                <Search className="pointer-events-none absolute top-3.5 h-4 w-4 text-slate-400" style={{ insetInlineStart: '0.85rem' }} />
                <input
                  className="h-11 w-full rounded-xl border border-mis-border bg-white pe-3 ps-10 text-sm"
                  placeholder={d.text('ابحث عن عميل', 'Search for a client')}
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                />
              </div>
            </div>
            {loadingClients ? (
              <div className="grid min-h-40 place-items-center"><LoadingSpinner /></div>
            ) : clients.length ? (
              <ul className="max-h-[36rem] divide-y divide-mis-border overflow-auto">
                {clients.map((item) => {
                  const active = item.id === selectedId;
                  return (
                    <li key={item.id}>
                      <button
                        className={`w-full px-4 py-3 text-start ${active ? 'bg-mis-pale' : 'hover:bg-slate-50'}`}
                        type="button"
                        onClick={() => choose(item.id)}
                      >
                        <p className="font-semibold text-mis-navy">{item.customerName}</p>
                        <p className="font-mono text-xs text-slate-500" data-bidi="ltr">{item.customerNumber}</p>
                      </button>
                    </li>
                  );
                })}
              </ul>
            ) : (
              <EmptyState
                compact
                icon={<Paperclip className="h-5 w-5" />}
                title={d.text('لا يوجد عملاء', 'No clients')}
                description={d.text('ارفع ملف العملاء أولًا، وبعدين ارجع هنا لملفات كل عميل.', 'Upload the client file first, then come back here for each client’s papers.')}
              />
            )}
          </section>

          <div>
            {!selectedId ? (
              <EmptyState
                icon={<Paperclip className="h-5 w-5" />}
                title={d.text('اختار عميل', 'Choose a client')}
                description={d.text('ملفات كل عميل منفصلة عن ملف الإكسل. اختار العميل من القائمة.', 'Each client’s files are separate from the spreadsheet. Choose a client from the list.')}
              />
            ) : loadingFiles ? (
              <div className="grid min-h-64 place-items-center rounded-2xl border border-mis-border bg-white"><LoadingSpinner /></div>
            ) : (
              <div className="space-y-3">
                {selected ? (
                  <p className="text-sm text-slate-500">
                    {selected.customerName}
                    <span className="mx-2 text-slate-300">·</span>
                    <span data-bidi="ltr">{selected.customerNumber}</span>
                  </p>
                ) : null}
                <DataEntryDocumentsPanel
                  canDownload
                  canUpload={canUpload}
                  documents={documents}
                  uploading={uploading}
                  onDelete={canUpload ? async (documentId) => {
                    await dataEntryService.deleteDocument(documentId);
                    setDocuments((current) => current.filter((item) => item.id !== documentId));
                    toast.success(d.text('تم حذف الملف.', 'File removed.'));
                  } : undefined}
                  onUpload={canUpload ? async (file, note) => {
                    setUploading(true);
                    try {
                      const created = await dataEntryService.uploadClientDocument(selectedId, file, note);
                      setDocuments((current) => [created, ...current]);
                      toast.success(d.text('تم رفع الملف ويمكن فتحه الآن.', 'File uploaded and ready to open.'));
                    } finally {
                      setUploading(false);
                    }
                  } : undefined}
                />
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
