import { Bell, ClipboardList } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { DataEntryDeskLabel } from '../../features/data-entry/DataEntryDeskFields';
import { DataEntryDocumentsPanel } from '../../features/data-entry/DataEntryDocumentsPanel';
import { DataEntryBatchStatus, DataEntryPipeline, DataEntryRowStatus, useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryBatchDetails, DataEntryBatchListItem, DataEntryNotification } from '../../features/data-entry/types/dataEntry';
import { getApiErrorMessage } from '../../services/apiClient';

const STATUS_FILTERS = ['', 'SUBMITTED', 'ACCEPTED', 'DISTRIBUTED', 'REJECTED'] as const;

export function CollectionsDataBatchesPage() {
  const d = useDataEntryText();
  const navigate = useNavigate();
  const [status, setStatus] = useState('SUBMITTED');
  const [search, setSearch] = useState('');
  const [items, setItems] = useState<DataEntryBatchListItem[]>([]);
  const [notifications, setNotifications] = useState<DataEntryNotification[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [details, setDetails] = useState<DataEntryBatchDetails | null>(null);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [rejectTarget, setRejectTarget] = useState<DataEntryBatchListItem | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [busyId, setBusyId] = useState('');
  const [actionError, setActionError] = useState('');
  const [distributionHint, setDistributionHint] = useState(false);

  async function reload() {
    setLoading(true);
    setError(false);
    try {
      const [batches, notes] = await Promise.all([
        dataEntryService.supervisorBatches(status || undefined),
        dataEntryService.notifications().catch(() => [] as DataEntryNotification[]),
      ]);
      setItems(batches.items);
      setNotifications(notes);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void reload(); }, [status]);

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return items;
    return items.filter((item) =>
      [item.batchNumber, item.organizationName, item.uploadedBy, item.fileName].some((value) => value?.toLowerCase().includes(term)));
  }, [items, search]);

  async function openDetails(batchId: string, notificationId?: string) {
    setDetailsLoading(true);
    setActionError('');
    try {
      if (notificationId) {
        await dataEntryService.markNotificationRead(notificationId).catch(() => undefined);
        setNotifications((current) => current.map((item) => (item.id === notificationId ? { ...item, isRead: true } : item)));
      }
      setDetails(await dataEntryService.supervisorBatch(batchId));
    } catch (reason) {
      setActionError(getApiErrorMessage(reason, d.text('تعذر فتح الدفعة', 'Could not open batch')));
      setDetails(null);
    } finally {
      setDetailsLoading(false);
    }
  }

  async function runAction(batchId: string, action: () => Promise<void>) {
    setBusyId(batchId);
    setActionError('');
    try {
      await action();
      await reload();
      if (details?.summary.id === batchId) setDetails(await dataEntryService.supervisorBatch(batchId));
    } catch (reason) {
      setActionError(getApiErrorMessage(reason, d.text('تعذر تنفيذ الإجراء', 'Could not complete the action')));
    } finally {
      setBusyId('');
    }
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title={d.text('دفعات إدخال البيانات', 'Data entry batches')}
        description={d.text('اقبل الدفعة لإنشاء حالات التحصيل من العملاء المُدخلين. بعد القبول تظهر البيانات في شاشات الحالات والتوزيع.', 'Accept a batch to create collection cases from entered clients. After acceptance, the data appears on cases and distribution screens.')}
        actions={
          <SelectInput
            containerClassName="min-w-[200px]"
            label={d.text('الحالة', 'Status')}
            value={status}
            onChange={(event) => setStatus(event.target.value)}
          >
            {STATUS_FILTERS.map((value) => (
              <option key={value || 'all'} value={value}>{value ? d.batchStatus(value) : d.text('الكل', 'All')}</option>
            ))}
          </SelectInput>
        }
      />
      <DataEntryPipeline current={status || 'SUBMITTED'} />

      {notifications.length > 0 ? (
        <section className="rounded-2xl border border-mis-border bg-white p-4 shadow-sm">
          <h2 className="flex items-center gap-2 text-sm font-bold text-mis-navy"><Bell className="h-4 w-4 text-mis-primary" />{d.text('وصل من إدخال البيانات', 'Arrived from data entry')}</h2>
          <ul className="mt-3 space-y-2">
            {notifications.slice(0, 8).map((note) => (
              <li key={note.id} className={`flex flex-wrap items-center justify-between gap-2 rounded-xl px-3 py-2 text-sm ${note.isRead ? 'bg-slate-50 text-slate-600' : 'bg-amber-50 text-amber-900'}`}>
                <span>{d.ar ? note.messageArabic : note.messageEnglish}</span>
                <Button fullWidth={false} size="sm" type="button" variant="ghost" onClick={() => void openDetails(note.batchId, note.id)}>
                  {d.text('مراجعة', 'Review')}
                </Button>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {distributionHint ? (
        <div className="rounded-2xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
          {d.text('الدفعة جاهزة للتوزيع. أكمل تعيين المحصلين من شاشة التوزيع.', 'The batch is ready for distribution. Finish collector assignment from the distribution screen.')}{' '}
          <Link className="font-bold underline" to="/collections/assignments">{d.text('فتح التوزيع', 'Open assignment')}</Link>
        </div>
      ) : null}

      {actionError ? <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{actionError}</div> : null}

      <input
        className="h-12 w-full rounded-2xl border border-mis-border bg-white px-4 text-sm focus:border-mis-blue focus:outline-none"
        placeholder={d.text('بحث برقم الدفعة أو الجهة أو الرافع', 'Search batch number, organization, or uploader')}
        value={search}
        onChange={(event) => setSearch(event.target.value)}
      />

      {error ? (
        <ErrorState title={d.text('تعذر تحميل الدفعات', 'Could not load batches')} onRetry={() => void reload()} />
      ) : loading ? (
        <div className="grid min-h-[280px] place-items-center"><LoadingSpinner /></div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="min-w-[56rem] w-full text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  {[d.text('رقم الدفعة', 'Batch'), d.text('الجهة', 'Organization'), d.text('المكتب', 'Desk'), d.text('رفع بواسطة', 'Uploaded by'), d.text('الصفوف', 'Rows'), d.text('الحالة', 'Status'), d.text('التاريخ', 'Date'), d.text('إجراءات', 'Actions')].map((label) => (
                    <th className="px-4 py-3 text-start" key={label}>{label}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {filtered.map((item) => (
                  <tr key={item.id} className="hover:bg-slate-50">
                    <td className="px-4 py-3 font-semibold text-mis-navy" data-bidi="ltr">{item.batchNumber}</td>
                    <td className="px-4 py-3">{item.organizationName}</td>
                    <td className="px-4 py-3"><DataEntryDeskLabel primary={item.primaryClassification} sub={item.subClassification} /></td>
                    <td className="px-4 py-3">{item.uploadedBy}</td>
                    <td className="px-4 py-3" data-bidi="ltr">{item.validRows}/{item.totalRows}</td>
                    <td className="px-4 py-3"><DataEntryBatchStatus value={item.status} /></td>
                    <td className="px-4 py-3">{d.dateTime(item.createdAt)}</td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-2">
                        <Button fullWidth={false} size="sm" type="button" variant="ghost" onClick={() => void openDetails(item.id)}>{d.text('عرض', 'View')}</Button>
                        {item.status === 'SUBMITTED' ? (
                          <>
                            <Button disabled={busyId === item.id} fullWidth={false} size="sm" type="button" variant="outline" onClick={() => void runAction(item.id, async () => { await dataEntryService.acceptBatch(item.id); })}>{d.text('قبول وإنشاء حالات', 'Accept & create cases')}</Button>
                            <Button disabled={busyId === item.id} fullWidth={false} size="sm" type="button" variant="danger" onClick={() => { setRejectTarget(item); setRejectReason(''); }}>{d.text('رفض', 'Reject')}</Button>
                          </>
                        ) : null}
                        {item.status === 'ACCEPTED' ? (
                          <Button disabled={busyId === item.id} fullWidth={false} size="sm" type="button" variant="outline" onClick={() => void runAction(item.id, async () => { await dataEntryService.sendToDistribution(item.id); setDistributionHint(true); })}>{d.text('إرسال للتوزيع', 'Send to distribution')}</Button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!filtered.length ? (
            <EmptyState
              compact
              icon={<ClipboardList className="h-5 w-5" />}
              title={d.text('لا توجد دفعات في هذه الحالة', 'No batches in this status')}
              description={d.text('عندما يرسل إدخال البيانات عملاء أو ملفًا، تظهر هنا للمراجعة.', 'When data entry sends clients or a file, they appear here for review.')}
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
            <div className="flex flex-wrap gap-2">
              {details?.summary.status === 'SUBMITTED' ? (
                <>
                  <Button disabled={busyId === details.summary.id} fullWidth={false} size="md" type="button" onClick={() => void runAction(details.summary.id, async () => { await dataEntryService.acceptBatch(details.summary.id); })}>
                    {d.text('قبول وإنشاء حالات', 'Accept & create cases')}
                  </Button>
                  <Button disabled={busyId === details.summary.id} fullWidth={false} size="md" type="button" variant="danger" onClick={() => { setRejectTarget(details.summary); setRejectReason(''); }}>
                    {d.text('رفض', 'Reject')}
                  </Button>
                </>
              ) : null}
              {details?.summary.status === 'ACCEPTED' ? (
                <Button disabled={busyId === details.summary.id} fullWidth={false} size="md" type="button" onClick={() => void runAction(details.summary.id, async () => { await dataEntryService.sendToDistribution(details.summary.id); setDistributionHint(true); })}>
                  {d.text('إرسال للتوزيع', 'Send to distribution')}
                </Button>
              ) : null}
              {details?.summary.status === 'ACCEPTED' || details?.summary.status === 'DISTRIBUTED' ? (
                <Button fullWidth={false} size="md" type="button" variant="outline" onClick={() => navigate('/collections/cases')}>
                  {d.text('فتح الحالات', 'Open cases')}
                </Button>
              ) : null}
              <Button fullWidth={false} size="md" type="button" variant="outline" onClick={() => setDetails(null)}>{d.text('إغلاق', 'Close')}</Button>
            </div>
          }
        >
          {detailsLoading || !details ? (
            <div className="grid min-h-[200px] place-items-center"><LoadingSpinner /></div>
          ) : (
            <div className="space-y-4">
              <DataEntryPipeline current={details.summary.status} />
              <div className="flex flex-wrap gap-3 text-sm text-slate-600">
                <span>{details.summary.organizationName}</span>
                <span><DataEntryDeskLabel primary={details.summary.primaryClassification} sub={details.summary.subClassification} /></span>
                <DataEntryBatchStatus value={details.summary.status} />
                <span data-bidi="ltr">{details.summary.validRows}/{details.summary.totalRows}</span>
              </div>
              {details.rejectionReason ? <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{details.rejectionReason}</div> : null}
              <DataEntryDocumentsPanel
                canDownload
                documents={details.documents ?? []}
                emptyHint={d.text('لا توجد ملفات مرفقة من إدخال البيانات.', 'No supporting files from data entry.')}
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
                          {row.collectionCaseId ? (
                            <Link className="font-bold text-mis-primary hover:underline" to={`/collections/cases/${row.collectionCaseId}`}>{row.customerNumber || d.text('الحالة', 'Case')}</Link>
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

      {rejectTarget ? (
        <Modal
          closeOnBackdrop={!busyId}
          closeOnEscape={!busyId}
          hideCloseButton={Boolean(busyId)}
          onClose={() => setRejectTarget(null)}
          open
          title={d.text('رفض الدفعة', 'Reject batch')}
          footer={
            <>
              <Button disabled={Boolean(busyId)} fullWidth={false} size="md" type="button" variant="outline" onClick={() => setRejectTarget(null)}>{d.text('إلغاء', 'Cancel')}</Button>
              <Button
                disabled={rejectReason.trim().length < 2}
                fullWidth={false}
                isLoading={busyId === rejectTarget.id}
                size="md"
                type="button"
                variant="danger"
                onClick={() => void runAction(rejectTarget.id, async () => {
                  await dataEntryService.rejectBatch(rejectTarget.id, rejectReason.trim());
                  setRejectTarget(null);
                })}
              >
                {d.text('تأكيد الرفض', 'Confirm reject')}
              </Button>
            </>
          }
        >
          <TextAreaInput label={d.text('سبب الرفض', 'Rejection reason')} required rows={4} value={rejectReason} onChange={(event) => setRejectReason(event.target.value)} />
        </Modal>
      ) : null}
    </div>
  );
}
