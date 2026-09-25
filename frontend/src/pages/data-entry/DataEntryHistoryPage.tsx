import { FileUp, History } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { DataEntryDocumentsPanel } from '../../features/data-entry/DataEntryDocumentsPanel';
import { DataEntryRowStatus, useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryBatchDetails, DataEntryBatchListItem } from '../../features/data-entry/types/dataEntry';

export function DataEntryHistoryPage() {
  const d = useDataEntryText();
  const navigate = useNavigate();
  const [reloadKey, setReloadKey] = useState(0);
  const [search, setSearch] = useState('');
  const [items, setItems] = useState<DataEntryBatchListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [details, setDetails] = useState<DataEntryBatchDetails | null>(null);
  const [detailsLoading, setDetailsLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);
    dataEntryService.myBatches()
      .then((result) => {
        if (cancelled) return;
        setItems(result.items);
      })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [reloadKey]);

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return items;
    return items.filter((item) =>
      [item.batchNumber, item.fileName, item.uploadedBy].some((value) => value?.toLowerCase().includes(term)));
  }, [items, search]);

  async function openDetails(batchId: string) {
    setDetailsLoading(true);
    try {
      setDetails(await dataEntryService.batch(batchId));
    } catch {
      setDetails(null);
    } finally {
      setDetailsLoading(false);
    }
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title={d.text('سجل الرفع', 'Upload history')}
        description={d.text('الملفات التي رُفعت من إدخال البيانات، مع حالة كل ملف.', 'Files uploaded from data entry, with the status of each file.')}
      />
      <input
        className="h-12 w-full rounded-2xl border border-mis-border bg-white px-4 text-sm focus:border-mis-blue focus:outline-none"
        placeholder={d.text('بحث باسم الملف', 'Search by file name')}
        value={search}
        onChange={(event) => setSearch(event.target.value)}
      />

      {error ? (
        <ErrorState title={d.text('تعذر تحميل السجل', 'Could not load history')} onRetry={() => setReloadKey((value) => value + 1)} />
      ) : loading ? (
        <div className="grid min-h-[280px] place-items-center"><LoadingSpinner /></div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="min-w-[52rem] w-full text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  {[d.text('الملف', 'File'), d.text('العملاء', 'Clients'), d.text('التاريخ', 'Date'), d.text('إجراءات', 'Actions')].map((label) => (
                    <th className="px-4 py-3 text-start" key={label}>{label}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {filtered.map((item) => (
                  <tr key={item.id} className="hover:bg-slate-50">
                    <td className="px-4 py-3 font-semibold text-mis-navy">{item.fileName || d.source(item.source)}</td>
                    <td className="px-4 py-3" data-bidi="ltr">{item.validRows}/{item.totalRows}</td>
                    <td className="px-4 py-3">{d.dateTime(item.createdAt)}</td>
                    <td className="px-4 py-3">
                      <Button fullWidth={false} size="sm" type="button" variant="ghost" onClick={() => void openDetails(item.id)}>
                        {d.text('تفاصيل', 'Details')}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!filtered.length ? (
            <EmptyState
              compact
              icon={<History className="h-5 w-5" />}
              title={d.text('لا توجد ملفات', 'No files')}
              description={d.text('ارفع ملف العملاء أو أضف عميلًا ليظهر هنا.', 'Upload a client file or add a client to see it here.')}
              action={<Button fullWidth={false} leftIcon={<FileUp className="h-4 w-4" />} onClick={() => navigate('/data-entry/import')}>{d.text('رفع ملف', 'Upload file')}</Button>}
            />
          ) : null}
        </div>
      )}

      {(details || detailsLoading) && (
        <Modal
          onClose={() => setDetails(null)}
          open
          size="xl"
          title={details ? (details.summary.fileName || details.summary.batchNumber) : d.text('تفاصيل الملف', 'File details')}
          footer={
            <Button fullWidth={false} size="md" type="button" variant="outline" onClick={() => setDetails(null)}>
              {d.text('إغلاق', 'Close')}
            </Button>
          }
        >
          {detailsLoading || !details ? (
            <div className="grid min-h-[200px] place-items-center"><LoadingSpinner /></div>
          ) : (
            <div className="space-y-4">
              <div className="grid gap-3 sm:grid-cols-3">
                <div className="rounded-xl bg-slate-50 p-3 text-sm">
                  <p className="text-slate-500">{d.text('العملاء', 'Customers')}</p>
                  <p className="mt-1 font-semibold text-mis-navy">{d.number(details.summary.customerCount)}</p>
                </div>
                <div className="rounded-xl bg-slate-50 p-3 text-sm">
                  <p className="text-slate-500">{d.text('صالح / إجمالي', 'Valid / total')}</p>
                  <p className="mt-1 font-semibold text-mis-navy" data-bidi="ltr">{details.summary.validRows}/{details.summary.totalRows}</p>
                </div>
                <div className="rounded-xl bg-slate-50 p-3 text-sm">
                  <p className="text-slate-500">{d.text('المصدر', 'Source')}</p>
                  <p className="mt-1 font-semibold text-mis-navy">{d.source(details.summary.source)}</p>
                </div>
              </div>
              <DataEntryDocumentsPanel
                documents={details.documents ?? []}
                emptyHint={d.text('مستندات العملاء بتترفع من صفحة كل عميل.', 'Client papers are uploaded from each client’s page.')}
              />
              <div className="overflow-x-auto rounded-xl border border-mis-border">
                <table className="min-w-full text-sm">
                  <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                    <tr>
                      {[d.text('رقم العميل', 'Customer number'), d.text('الاسم', 'Name'), d.text('الموبايل', 'Mobile'), d.text('الحالة', 'Status')].map((label) => (
                        <th className="px-3 py-2 text-start" key={label}>{label}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-mis-border">
                    {details.rows.map((row) => (
                      <tr key={row.rowNumber}>
                        <td className="px-3 py-2" data-bidi="ltr">
                          {row.collectionCustomerId ? (
                            <Link className="font-bold text-mis-primary hover:underline" to={`/data-entry/clients/${row.collectionCustomerId}`}>{row.customerNumber || d.text('فتح', 'Open')}</Link>
                          ) : (row.customerNumber || '—')}
                        </td>
                        <td className="px-3 py-2">{row.customerName}</td>
                        <td className="px-3 py-2" data-bidi="ltr">{row.mobileNumber || '—'}</td>
                        <td className="px-3 py-2"><DataEntryRowStatus value={row.status} /></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </Modal>
      )}
    </div>
  );
}
