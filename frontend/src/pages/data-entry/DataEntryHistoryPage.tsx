import { FileUp, History } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { SelectInput } from '../../components/forms/SelectInput';
import { DataEntryDeskLabel } from '../../features/data-entry/DataEntryDeskFields';
import { DataEntryDocumentsPanel } from '../../features/data-entry/DataEntryDocumentsPanel';
import { DataEntryBatchStatus, DataEntryPipeline, DataEntryRowStatus, useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryBatchDetails, DataEntryBatchListItem } from '../../features/data-entry/types/dataEntry';

const STATUS_FILTERS = ['', 'SUBMITTED', 'ACCEPTED', 'DISTRIBUTED', 'REJECTED'] as const;

export function DataEntryHistoryPage() {
  const d = useDataEntryText();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const status = params.get('status') ?? '';
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
    dataEntryService.myBatches(status || undefined)
      .then((result) => {
        if (cancelled) return;
        setItems(result.items);
      })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [status]);

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return items;
    return items.filter((item) =>
      [item.batchNumber, item.fileName, item.organizationName, item.uploadedBy].some((value) => value?.toLowerCase().includes(term)));
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
        title={d.text('سجل الإرسال للتحصيل', 'Collections send history')}
        description={d.text('كل دفعة هنا وصلت أو في طريقها لمراجعة التحصيل. القبول ينشئ الحالات عند فرق التحصيل.', 'Every batch here is in or on its way to collections review. Acceptance creates cases for collection teams.')}
        actions={
          <SelectInput
            containerClassName="min-w-[200px]"
            label={d.text('الحالة', 'Status')}
            value={status}
            onChange={(event) => {
              const next = new URLSearchParams(params);
              if (event.target.value) next.set('status', event.target.value);
              else next.delete('status');
              setParams(next, { replace: true });
            }}
          >
            {STATUS_FILTERS.map((value) => (
              <option key={value || 'all'} value={value}>
                {value ? d.batchStatus(value) : d.text('الكل', 'All')}
              </option>
            ))}
          </SelectInput>
        }
      />
      <DataEntryPipeline current={status || (items[0]?.status ?? 'SUBMITTED')} />
      <input
        className="h-12 w-full rounded-2xl border border-mis-border bg-white px-4 text-sm focus:border-mis-blue focus:outline-none"
        placeholder={d.text('بحث برقم الدفعة أو الجهة أو الملف', 'Search batch number, organization, or file')}
        value={search}
        onChange={(event) => setSearch(event.target.value)}
      />

      {error ? (
        <ErrorState title={d.text('تعذر تحميل السجل', 'Could not load history')} onRetry={() => setParams(params)} />
      ) : loading ? (
        <div className="grid min-h-[280px] place-items-center"><LoadingSpinner /></div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="min-w-[52rem] w-full text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  {[d.text('رقم الدفعة', 'Batch'), d.text('الملف', 'File'), d.text('الجهة', 'Organization'), d.text('المكتب', 'Desk'), d.text('العملاء', 'Clients'), d.text('الحالة', 'Status'), d.text('التاريخ', 'Date'), d.text('إجراءات', 'Actions')].map((label) => (
                    <th className="px-4 py-3 text-start" key={label}>{label}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {filtered.map((item) => (
                  <tr key={item.id} className="hover:bg-slate-50">
                    <td className="px-4 py-3 font-semibold text-mis-navy" data-bidi="ltr">{item.batchNumber}</td>
                    <td className="px-4 py-3">{item.fileName || d.source(item.source)}</td>
                    <td className="px-4 py-3">{item.organizationName}</td>
                    <td className="px-4 py-3"><DataEntryDeskLabel primary={item.primaryClassification} sub={item.subClassification} /></td>
                    <td className="px-4 py-3" data-bidi="ltr">{item.validRows}/{item.totalRows}</td>
                    <td className="px-4 py-3"><DataEntryBatchStatus value={item.status} /></td>
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
              title={d.text('لا توجد دفعات', 'No batches')}
              description={d.text('ارفع ملفًا أو أضف عميلًا لإرسال دفعة للتحصيل.', 'Upload a file or add a client to send a batch to collections.')}
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
          title={details ? details.summary.batchNumber : d.text('تفاصيل الدفعة', 'Batch details')}
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
              <DataEntryPipeline current={details.summary.status} />
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <div className="rounded-xl bg-slate-50 p-3 text-sm">
                  <p className="text-slate-500">{d.text('الحالة', 'Status')}</p>
                  <div className="mt-1"><DataEntryBatchStatus value={details.summary.status} /></div>
                </div>
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
                <div className="rounded-xl bg-slate-50 p-3 text-sm sm:col-span-2 lg:col-span-4">
                  <p className="text-slate-500">{d.text('مكتب التحصيل', 'Collections desk')}</p>
                  <p className="mt-1 font-semibold text-mis-navy">
                    {details.summary.organizationName} · <DataEntryDeskLabel primary={details.summary.primaryClassification} sub={details.summary.subClassification} />
                  </p>
                </div>
              </div>
              {details.reviewedBy ? <p className="text-sm text-slate-500">{d.text('راجعها', 'Reviewed by')}: {details.reviewedBy}{details.reviewedAt ? ` · ${d.dateTime(details.reviewedAt)}` : ''}</p> : null}
              {details.rejectionReason ? (
                <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700">
                  {d.text('سبب الرفض', 'Rejection reason')}: {details.rejectionReason}
                </div>
              ) : null}
              {details.summary.status === 'ACCEPTED' || details.summary.status === 'DISTRIBUTED' ? (
                <p className="rounded-xl bg-emerald-50 p-3 text-sm text-emerald-800">
                  {d.text('البيانات وصلت للتحصيل. الحالات تظهر لفرق التحصيل بعد القبول.', 'The data reached collections. Cases appear for collection teams after acceptance.')}
                </p>
              ) : null}
              <DataEntryDocumentsPanel
                documents={details.documents ?? []}
                emptyHint={d.text('الملفات المرفوعة للحالات تظهر للمشرف عند المراجعة.', 'Files uploaded for the cases appear to the supervisor during review.')}
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
