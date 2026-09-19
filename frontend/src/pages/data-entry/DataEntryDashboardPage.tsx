import { ArrowUpRight, FileUp, History, UsersRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { DataEntryBatchStatus, DataEntryKpi, DataEntryPipeline, useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryBatchListItem, DataEntryDashboard } from '../../features/data-entry/types/dataEntry';

export function DataEntryDashboardPage() {
  const d = useDataEntryText();
  const navigate = useNavigate();
  const [data, setData] = useState<DataEntryDashboard>();
  const [batches, setBatches] = useState<DataEntryBatchListItem[]>([]);
  const [error, setError] = useState(false);

  function load() {
    setError(false);
    Promise.all([dataEntryService.dashboard(), dataEntryService.myBatches()])
      .then(([dashboard, page]) => {
        setData(dashboard);
        setBatches(page.items.slice(0, 8));
      })
      .catch(() => setError(true));
  }

  useEffect(() => { load(); }, []);

  if (error) {
    return <ErrorState title={d.text('تعذر تحميل لوحة إدخال البيانات', 'Could not load the data entry dashboard')} onRetry={load} />;
  }
  if (!data) {
    return <div className="grid min-h-[440px] place-items-center"><LoadingSpinner /></div>;
  }

  const kpis = [
    { to: '/data-entry/clients', label: d.text('عملائي', 'My clients'), value: data.myClients, tone: 'blue' as const, hint: d.text('موجودون في التحصيل بعد الإرسال', 'Available in collections after submit') },
    { to: '/data-entry/history?status=SUBMITTED', label: d.text('بانتظار التحصيل', 'Awaiting collections'), value: data.submittedBatches, tone: 'amber' as const, hint: d.text('مراجعة المشرف', 'Supervisor review') },
    { to: '/data-entry/history?status=ACCEPTED', label: d.text('مقبول في التحصيل', 'Accepted in collections'), value: data.acceptedBatches + data.distributedBatches, tone: 'green' as const, hint: d.text('حالات جاهزة للعمل', 'Cases ready to work') },
    { to: '/data-entry/history?status=REJECTED', label: d.text('مرفوض', 'Rejected'), value: data.rejectedBatches, tone: 'red' as const, hint: d.text('راجع السبب وأعد الإرسال', 'Check the reason and send again') },
  ];

  return (
    <div className="space-y-8">
      <header className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p className="text-xs font-bold uppercase tracking-[.18em] text-mis-primary">{d.text('إدخال البيانات', 'DATA ENTRY')}</p>
          <h1 className="mt-2 text-3xl font-bold text-mis-navy">{d.text('مكتب الإدخال إلى التحصيل', 'Entry desk to collections')}</h1>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-500">
            {d.text('أدخل العملاء أو ارفع الملف، ثم تُرسل الدفعة لمراجعة التحصيل. بعد القبول تظهر الحالات عند فرق التحصيل.', 'Enter clients or upload a file, then the batch goes to collections review. After acceptance, cases appear for collection teams.')}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button fullWidth={false} leftIcon={<UsersRound className="h-4 w-4" />} onClick={() => navigate('/data-entry/clients')}>{d.text('إضافة عميل', 'Add client')}</Button>
          <Button fullWidth={false} leftIcon={<FileUp className="h-4 w-4" />} variant="outline" onClick={() => navigate('/data-entry/import')}>{d.text('رفع ملف', 'Upload file')}</Button>
        </div>
      </header>

      <DataEntryPipeline current={data.submittedBatches ? 'SUBMITTED' : data.acceptedBatches ? 'ACCEPTED' : data.distributedBatches ? 'DISTRIBUTED' : 'enter'} />

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {kpis.map((kpi) => (
          <Link key={kpi.to} className="block transition hover:-translate-y-0.5" to={kpi.to}>
            <DataEntryKpi hint={kpi.hint} label={kpi.label} tone={kpi.tone} value={d.number(kpi.value)} />
          </Link>
        ))}
      </section>

      <section className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="flex items-center justify-between border-b border-mis-border px-5 py-4">
          <h2 className="text-lg font-bold text-mis-navy">{d.text('آخر الدفعات المرسلة للتحصيل', 'Latest batches sent to collections')}</h2>
          <Link className="inline-flex items-center gap-1 text-sm font-bold text-mis-primary" to="/data-entry/history">
            {d.text('كل السجل', 'Full history')}
            <ArrowUpRight className="h-4 w-4" />
          </Link>
        </div>
        {batches.length ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[44rem] text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  <th className="px-4 py-3 text-start">{d.text('الدفعة', 'Batch')}</th>
                  <th className="px-4 py-3 text-start">{d.text('الجهة', 'Organization')}</th>
                  <th className="px-4 py-3 text-end">{d.text('العملاء', 'Clients')}</th>
                  <th className="px-4 py-3 text-start">{d.text('الحالة', 'Status')}</th>
                  <th className="px-4 py-3 text-start">{d.text('التاريخ', 'Date')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {batches.map((batch) => (
                  <tr key={batch.id}>
                    <td className="px-4 py-3 font-semibold text-mis-navy" data-bidi="ltr">{batch.batchNumber}</td>
                    <td className="px-4 py-3">{batch.organizationName}</td>
                    <td className="px-4 py-3 text-end" data-bidi="ltr">{d.number(batch.customerCount || batch.validRows)}</td>
                    <td className="px-4 py-3"><DataEntryBatchStatus value={batch.status} /></td>
                    <td className="px-4 py-3">{d.dateTime(batch.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <EmptyState
            compact
            icon={<History className="h-5 w-5" />}
            title={d.text('لم تُرسل دفعات بعد', 'No batches sent yet')}
            description={d.text('ابدأ بإضافة عميل أو رفع ملف ليصل للتحصيل.', 'Start by adding a client or uploading a file so it can reach collections.')}
            action={<Button fullWidth={false} leftIcon={<FileUp className="h-4 w-4" />} onClick={() => navigate('/data-entry/import')}>{d.text('رفع ملف', 'Upload file')}</Button>}
          />
        )}
      </section>
    </div>
  );
}
