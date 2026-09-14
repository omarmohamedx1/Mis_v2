import { useEffect, useState } from 'react';
import { Button } from '../../components/common/Button';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { SelectInput } from '../../components/forms/SelectInput';
import { DataEntryBatchStatus, DataEntryRowStatus, useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryBatchDetails, DataEntryBatchListItem } from '../../features/data-entry/types/dataEntry';

const STATUS_FILTERS = ['', 'DRAFT', 'SUBMITTED', 'ACCEPTED', 'DISTRIBUTED', 'REJECTED'] as const;

export function DataEntryHistoryPage() {
  const d = useDataEntryText();
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<DataEntryBatchListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [details, setDetails] = useState<DataEntryBatchDetails | null>(null);
  const [detailsLoading, setDetailsLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);
    dataEntryService
      .myBatches(status || undefined, page, 20)
      .then((result) => {
        if (cancelled) return;
        setItems(result.items);
        setTotalCount(result.totalCount);
      })
      .catch(() => {
        if (!cancelled) setError(true);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [status, page]);

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

  const totalPages = Math.max(1, Math.ceil(totalCount / 20));

  return (
    <div className="space-y-5">
      <PageHeader
        title={d.text('سجل الرفع', 'Upload History')}
        description={d.text('دفعات الإدخال التي رفعتها أو أنشأتها.', 'Batches you uploaded or created.')}
        actions={
          <SelectInput
            containerClassName="min-w-[200px]"
            label={d.text('الحالة', 'Status')}
            value={status}
            onChange={(event) => {
              setStatus(event.target.value);
              setPage(1);
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

      {error ? (
        <ErrorState title={d.text('تعذر تحميل السجل', 'Could not load history')} onRetry={() => setPage((value) => value)} />
      ) : loading ? (
        <div className="grid min-h-[280px] place-items-center">
          <LoadingSpinner />
        </div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  {[
                    d.text('رقم الدفعة', 'Batch'),
                    d.text('الملف', 'File'),
                    d.text('الجهة', 'Organization'),
                    d.text('الصفوف', 'Rows'),
                    d.text('الحالة', 'Status'),
                    d.text('التاريخ', 'Date'),
                    d.text('إجراءات', 'Actions'),
                  ].map((label) => (
                    <th className="px-4 py-3 text-start" key={label}>
                      {label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {items.length === 0 ? (
                  <tr>
                    <td className="px-4 py-8 text-center text-slate-500" colSpan={7}>
                      {d.text('لا توجد دفعات', 'No batches')}
                    </td>
                  </tr>
                ) : (
                  items.map((item) => (
                    <tr key={item.id} className="hover:bg-slate-50">
                      <td className="px-4 py-3 font-semibold text-mis-navy" data-bidi="ltr">
                        {item.batchNumber}
                      </td>
                      <td className="px-4 py-3">{item.fileName || d.source(item.source)}</td>
                      <td className="px-4 py-3">{item.organizationName}</td>
                      <td className="px-4 py-3" data-bidi="ltr">
                        {item.validRows}/{item.totalRows}
                      </td>
                      <td className="px-4 py-3">
                        <DataEntryBatchStatus value={item.status} />
                      </td>
                      <td className="px-4 py-3">{d.dateTime(item.createdAt)}</td>
                      <td className="px-4 py-3">
                        <button className="text-sm font-bold text-mis-primary hover:underline" type="button" onClick={() => void openDetails(item.id)}>
                          {d.text('عرض', 'View')}
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          {totalCount > 20 ? (
            <div className="flex items-center justify-between border-t border-mis-border px-4 py-3 text-sm text-slate-500">
              <span>
                {page} / {totalPages}
              </span>
              <div className="flex gap-2">
                <Button disabled={page <= 1} fullWidth={false} size="sm" type="button" variant="outline" onClick={() => setPage((value) => value - 1)}>
                  {d.text('السابق', 'Previous')}
                </Button>
                <Button disabled={page >= totalPages} fullWidth={false} size="sm" type="button" variant="outline" onClick={() => setPage((value) => value + 1)}>
                  {d.text('التالي', 'Next')}
                </Button>
              </div>
            </div>
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
            <div className="grid min-h-[200px] place-items-center">
              <LoadingSpinner />
            </div>
          ) : (
            <div className="space-y-4">
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <div className="rounded-xl bg-slate-50 p-3 text-sm">
                  <p className="text-slate-500">{d.text('الحالة', 'Status')}</p>
                  <div className="mt-1">
                    <DataEntryBatchStatus value={details.summary.status} />
                  </div>
                </div>
                <div className="rounded-xl bg-slate-50 p-3 text-sm">
                  <p className="text-slate-500">{d.text('العملاء', 'Customers')}</p>
                  <p className="mt-1 font-semibold text-mis-navy">{d.number(details.summary.customerCount)}</p>
                </div>
                <div className="rounded-xl bg-slate-50 p-3 text-sm">
                  <p className="text-slate-500">{d.text('صالح / إجمالي', 'Valid / Total')}</p>
                  <p className="mt-1 font-semibold text-mis-navy" data-bidi="ltr">
                    {details.summary.validRows}/{details.summary.totalRows}
                  </p>
                </div>
                <div className="rounded-xl bg-slate-50 p-3 text-sm">
                  <p className="text-slate-500">{d.text('المصدر', 'Source')}</p>
                  <p className="mt-1 font-semibold text-mis-navy">{d.source(details.summary.source)}</p>
                </div>
              </div>
              {details.rejectionReason ? (
                <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700">
                  {d.text('سبب الرفض', 'Rejection reason')}: {details.rejectionReason}
                </div>
              ) : null}
              <div className="overflow-x-auto rounded-xl border border-mis-border">
                <table className="min-w-full text-sm">
                  <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                    <tr>
                      {[
                        d.text('رقم العميل', 'Customer Number'),
                        d.text('الاسم', 'Name'),
                        d.text('الموبايل', 'Mobile'),
                        d.text('الحالة', 'Status'),
                      ].map((label) => (
                        <th className="px-3 py-2 text-start" key={label}>
                          {label}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-mis-border">
                    {details.rows.slice(0, 100).map((row) => (
                      <tr key={row.rowNumber}>
                        <td className="px-3 py-2" data-bidi="ltr">
                          {row.customerNumber || '—'}
                        </td>
                        <td className="px-3 py-2">{row.customerName}</td>
                        <td className="px-3 py-2" data-bidi="ltr">
                          {row.mobileNumber || '—'}
                        </td>
                        <td className="px-3 py-2">
                          <DataEntryRowStatus value={row.status} />
                        </td>
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
