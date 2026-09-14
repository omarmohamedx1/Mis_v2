import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { DataEntryBatchStatus, useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryClientDetails } from '../../features/data-entry/types/dataEntry';

function hasValue(value: unknown) {
  if (value === null || value === undefined) return false;
  if (typeof value === 'string') return value.trim().length > 0;
  return true;
}

function DetailSection({ title, rows }: { title: string; rows: [string, string][] }) {
  const visible = rows.filter(([, value]) => value && value !== '—');
  if (!visible.length) return null;
  return (
    <section className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
      <h2 className="text-base font-bold text-mis-navy">{title}</h2>
      <dl className="mt-4 grid gap-3 sm:grid-cols-2">
        {visible.map(([label, value]) => (
          <div className="rounded-xl bg-slate-50 p-3" key={label}>
            <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</dt>
            <dd className="mt-1 break-words text-sm font-semibold text-mis-navy">{value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

export function DataEntryClientDetailsPage() {
  const { id = '' } = useParams();
  const d = useDataEntryText();
  const [data, setData] = useState<DataEntryClientDetails>();
  const [error, setError] = useState(false);

  useEffect(() => {
    if (!id) return;
    setError(false);
    dataEntryService.client(id).then(setData).catch(() => setError(true));
  }, [id]);

  if (error) {
    return <ErrorState title={d.text('تعذر تحميل بيانات العميل', 'Could not load client details')} onRetry={() => location.reload()} />;
  }
  if (!data) {
    return (
      <div className="grid min-h-[440px] place-items-center">
        <LoadingSpinner />
      </div>
    );
  }

  const displayName =
    (d.ar ? data.customerNameArabic : data.customerNameEnglish) ||
    data.customerNameArabic ||
    data.customerNameEnglish ||
    data.customerNumber;

  const moneyOrDash = (value?: number | null) => (hasValue(value) ? d.money(Number(value)) : '—');

  return (
    <div className="space-y-5">
      <PageHeader
        breadcrumbs={
          <Link className="text-sm font-semibold text-mis-primary hover:underline" to="/data-entry/clients">
            {d.text('← العملاء', '← Clients')}
          </Link>
        }
        title={displayName}
        description={
          <span data-bidi="ltr">
            {d.text('رقم العميل', 'Customer Number')}: {data.customerNumber}
          </span>
        }
      />

      <div className="space-y-4">
        <DetailSection
          title={d.text('أساسي', 'Basic')}
          rows={[
            [d.text('رقم العميل', 'Customer Number'), data.customerNumber],
            [d.text('الاسم بالعربية', 'Arabic name'), data.customerNameArabic || '—'],
            [d.text('الاسم بالإنجليزية', 'English name'), data.customerNameEnglish || '—'],
            [d.text('الرقم القومي', 'National ID'), data.nationalId || '—'],
            [d.text('المصدر', 'Source'), data.source || '—'],
            [d.text('أنشئ بواسطة', 'Created by'), data.createdBy || '—'],
            [d.text('تاريخ الإنشاء', 'Created at'), d.dateTime(data.createdAt)],
          ]}
        />
        <DetailSection
          title={d.text('الاتصال', 'Contact')}
          rows={[
            [d.text('الموبايل', 'Mobile'), data.mobileNumber || '—'],
            [d.text('موبايل بديل', 'Alternate mobile'), data.alternateMobile || '—'],
          ]}
        />
        <DetailSection title={d.text('العنوان', 'Address')} rows={[[d.text('العنوان', 'Address'), data.address || '—']]} />
        <DetailSection title={d.text('فيدباك', 'Feedback')} rows={[[d.text('فيدباك', 'Feedback'), data.feedback || '—']]} />
        <DetailSection title={d.text('ملاحظات', 'Notes')} rows={[[d.text('ملاحظات', 'Notes'), data.notes || '—']]} />
        <DetailSection
          title={d.text('الجهة', 'Organization')}
          rows={[
            [d.text('الجهة', 'Organization'), data.organizationName],
            [d.text('نوع الجهة', 'Organization type'), data.organizationType || '—'],
            [d.text('كود المحفظة', 'Portfolio code'), data.portfolioCode || '—'],
            [d.text('المحفظة', 'Portfolio'), data.portfolioName || '—'],
            [d.text('التصنيف الرئيسي', 'Primary classification'), data.primaryClassification || '—'],
            [d.text('التصنيف الفرعي', 'Sub classification'), data.subClassification || '—'],
          ]}
        />
        <DetailSection
          title={d.text('الحساب / الحالة', 'Account / Case')}
          rows={[
            [d.text('رقم الحالة', 'Case number'), data.caseNumber || '—'],
            [d.text('رقم الحساب', 'Account number'), data.accountNumber || '—'],
            [d.text('رقم العقد', 'Contract number'), data.contractNumber || '—'],
            [d.text('حالة الحالة', 'Case status'), data.caseStatus || '—'],
          ]}
        />
        {(hasValue(data.outstandingBalance) || hasValue(data.overdueBalance) || hasValue(data.daysPastDue)) && (
          <DetailSection
            title={d.text('مالي', 'Financial')}
            rows={[
              [d.text('المديونية', 'Outstanding'), moneyOrDash(data.outstandingBalance)],
              [d.text('المتأخر', 'Overdue'), moneyOrDash(data.overdueBalance)],
              [d.text('أيام التأخر', 'Days past due'), hasValue(data.daysPastDue) ? d.number(Number(data.daysPastDue)) : '—'],
            ]}
          />
        )}
        {(hasValue(data.batchNumber) || hasValue(data.batchStatus)) && (
          <section className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
            <h2 className="text-base font-bold text-mis-navy">{d.text('معلومات الرفع', 'Import Information')}</h2>
            <dl className="mt-4 grid gap-3 sm:grid-cols-2">
              {hasValue(data.batchNumber) ? (
                <div className="rounded-xl bg-slate-50 p-3">
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{d.text('رقم الدفعة', 'Batch number')}</dt>
                  <dd className="mt-1 text-sm font-semibold text-mis-navy" data-bidi="ltr">
                    {data.batchNumber}
                  </dd>
                </div>
              ) : null}
              {hasValue(data.batchStatus) ? (
                <div className="rounded-xl bg-slate-50 p-3">
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{d.text('حالة الدفعة', 'Batch status')}</dt>
                  <dd className="mt-1">
                    <DataEntryBatchStatus value={data.batchStatus!} />
                  </dd>
                </div>
              ) : null}
            </dl>
          </section>
        )}
      </div>
    </div>
  );
}
