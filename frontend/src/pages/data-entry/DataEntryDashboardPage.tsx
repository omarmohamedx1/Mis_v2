import { Bell, FileUp, UsersRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { DataEntryKpi, useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryDashboard } from '../../features/data-entry/types/dataEntry';

export function DataEntryDashboardPage() {
  const d = useDataEntryText();
  const [data, setData] = useState<DataEntryDashboard>();
  const [error, setError] = useState(false);

  useEffect(() => {
    dataEntryService.dashboard().then(setData).catch(() => setError(true));
  }, []);

  if (error) {
    return <ErrorState title={d.text('تعذر تحميل لوحة إدخال البيانات', 'Could not load the data entry dashboard')} onRetry={() => location.reload()} />;
  }
  if (!data) {
    return (
      <div className="grid min-h-[440px] place-items-center">
        <LoadingSpinner />
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <header>
        <p className="text-xs font-bold uppercase tracking-[.18em] text-mis-primary">{d.text('إدخال البيانات', 'DATA ENTRY')}</p>
        <h1 className="mt-2 text-3xl font-bold text-mis-navy">{d.text('لوحة التحكم', 'Dashboard')}</h1>
        <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500">
          {d.text('متابعة العملاء والدفعات المرسلة للمراجعة.', 'Track clients and batches sent for review.')}
        </p>
      </header>

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <DataEntryKpi label={d.text('عملائي', 'My clients')} value={d.number(data.myClients)} tone="blue" />
        <DataEntryKpi label={d.text('مُرسل للمراجعة', 'Submitted')} value={d.number(data.submittedBatches)} tone="amber" />
        <DataEntryKpi label={d.text('مقبول', 'Accepted')} value={d.number(data.acceptedBatches)} tone="green" />
        <DataEntryKpi label={d.text('مرفوض', 'Rejected')} value={d.number(data.rejectedBatches)} tone="red" />
      </section>

      {data.unreadNotifications > 0 ? (
        <div className="flex items-center gap-3 rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          <Bell className="h-4 w-4 shrink-0" />
          <span>
            {d.text(`لديك ${data.unreadNotifications} إشعار غير مقروء`, `You have ${data.unreadNotifications} unread notification(s)`)}
          </span>
        </div>
      ) : null}

      <section className="grid gap-4 md:grid-cols-2">
        <Link
          to="/data-entry/clients"
          className="group flex items-center gap-4 rounded-2xl border border-mis-border bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-mis-blue"
        >
          <span className="grid h-11 w-11 place-items-center rounded-xl bg-mis-pale text-mis-primary">
            <UsersRound className="h-5 w-5" />
          </span>
          <div>
            <p className="font-bold text-mis-navy">{d.text('العملاء', 'Clients')}</p>
            <p className="text-sm text-slate-500">{d.text('عرض وإضافة العملاء', 'View and add clients')}</p>
          </div>
        </Link>
        <Link
          to="/data-entry/import"
          className="group flex items-center gap-4 rounded-2xl border border-mis-border bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-mis-blue"
        >
          <span className="grid h-11 w-11 place-items-center rounded-xl bg-mis-pale text-mis-primary">
            <FileUp className="h-5 w-5" />
          </span>
          <div>
            <p className="font-bold text-mis-navy">{d.text('رفع البيانات', 'Import Data')}</p>
            <p className="text-sm text-slate-500">{d.text('رفع ملف عملاء', 'Upload a client file')}</p>
          </div>
        </Link>
      </section>
    </div>
  );
}
