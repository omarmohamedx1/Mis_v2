import { ArrowLeft } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useOutletContext, useParams } from 'react-router-dom';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { StatusBadge } from '../../components/common/StatusBadge';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankCustomerDetails } from '../../features/collections/types/collections';
import { getApiErrorMessage } from '../../services/apiClient';
import type { BankWorkspaceContext } from './BankWorkspaceLayout';

export function BankCustomerDetailsPage() {
  const { bank, workspaceBase } = useOutletContext<BankWorkspaceContext>();
  const { customerId } = useParams();
  const { language, ct } = useCollectionsLocalization();
  const [data, setData] = useState<BankCustomerDetails>();
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const money = (value: number) => new Intl.NumberFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { style: 'currency', currency: 'EGP', maximumFractionDigits: 2 }).format(value);
  const date = (value?: string | null) => value ? new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { dateStyle: 'medium' }).format(new Date(value)) : '—';

  useEffect(() => {
    if (!customerId) return;
    let active = true;
    setLoading(true); setError('');
    void collectionsService.bankCustomer(bank.id, customerId)
      .then((value) => { if (active) setData(value); })
      .catch((reason) => { if (active) setError(getApiErrorMessage(reason, ct('loadError'))); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [bank.id, customerId, ct]);

  if (loading) return <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>;
  if (error || !data) return <ErrorState message={error} title={ct('loadError')} />;
  const addresses = splitFileAddresses(data.address, data.secondaryAddress);

  const Card = ({ title, children }: { title: string; children: React.ReactNode }) => (
    <section className="rounded-2xl border border-mis-border bg-white p-5 shadow-sm"><h3 className="mb-4 text-lg font-bold text-mis-navy">{title}</h3>{children}</section>
  );
  const Field = ({ label, value, wide = false }: { label: string; value?: string | number | null; wide?: boolean }) => (
    <div className={wide ? 'sm:col-span-2 xl:col-span-3' : ''}><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">{label}</p><p className="mt-1 font-semibold text-mis-navy" dir="auto">{value == null || value === '' ? '—' : value}</p></div>
  );

  return (
    <div className="space-y-5">
      <Link className="inline-flex items-center gap-2 text-sm font-semibold text-mis-primary" to={`${workspaceBase}/customers`}><ArrowLeft className="h-4 w-4 rtl:rotate-180" />{ct('backToCustomers')}</Link>
      <header className="rounded-2xl border border-mis-border bg-white p-6">
        <h2 className="text-2xl font-bold text-mis-navy">{data.customerName}</h2>
        <p className="mt-1 text-sm text-slate-500">{data.customerCode} · {data.organizationName}</p>
      </header>

      <Card title={ct('basicInformation')}>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          <Field label={ct('customerName')} value={data.customerName} />
          <Field label={ct('mobile')} value={data.mobile} />
          <Field label={ct('alternativeMobile')} value={data.alternativeMobile} />
          <Field label={ct('nationalId')} value={data.nationalId} />
          <Field label={ct('customerCode')} value={data.customerCode} />
          <Field label={ct('accountContract')} value={data.cases[0]?.accountReference ?? data.cases[0]?.contractReference} />
          <Field label={ct('address1')} value={addresses.address1} wide />
          <Field label={ct('secondaryAddress')} value={addresses.address2} wide />
        </div>
      </Card>

      <Card title={ct('organizationInformation')}>
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr>{[ct('caseId'), ct('portfolio'), ct('accountContract'), ct('collector'), ct('status'), ct('actions')].map((label) => <th className="px-3 py-2 text-start" key={label}>{label}</th>)}</tr></thead>
            <tbody className="divide-y divide-mis-border">
              {data.cases.map((item) => (
                <tr key={item.caseId}>
                  <td className="px-3 py-2" data-bidi="ltr">{item.caseNumber}</td>
                  <td className="px-3 py-2">{item.portfolioName}</td>
                  <td className="px-3 py-2" data-bidi="ltr">{item.accountReference}{item.contractReference ? ` / ${item.contractReference}` : ''}</td>
                  <td className="px-3 py-2">{item.assignedCollectorName ?? '—'}</td>
                  <td className="px-3 py-2"><StatusBadge>{ct(item.status as never) || item.status}</StatusBadge></td>
                  <td className="px-3 py-2"><Link className="font-bold text-mis-primary" to={`${workspaceBase}/portfolio?caseId=${item.caseId}`}>{ct('openPortfolioCase')}</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <Card title={ct('financialInformation')}>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <Field label={ct('originalAmount')} value={money(data.totalOriginalAmount)} />
          <Field label={ct('totalOutstanding')} value={money(data.totalOutstandingAmount)} />
          <Field label={ct('paidAmount')} value={money(data.totalPaidAmount)} />
          <Field label={ct('remainingAmount')} value={money(data.totalRemainingAmount)} />
          <Field label={ct('overdue')} value={money(data.totalOverdueAmount)} />
        </div>
        <div className="mt-5 overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr>{[ct('caseId'), 'DPD', ct('originalAmount'), ct('outstandingAmount'), ct('paidAmount'), ct('remainingAmount')].map((label) => <th className="px-3 py-2 text-start" key={label}>{label}</th>)}</tr></thead>
            <tbody className="divide-y divide-mis-border">
              {data.cases.map((item) => (
                <tr key={`fin-${item.caseId}`}>
                  <td className="px-3 py-2" data-bidi="ltr">{item.caseNumber}</td>
                  <td className="px-3 py-2" data-bidi="ltr">{item.daysPastDue}</td>
                  <td className="px-3 py-2" data-bidi="ltr">{money(item.originalAmount)}</td>
                  <td className="px-3 py-2" data-bidi="ltr">{money(item.outstandingAmount)}</td>
                  <td className="px-3 py-2" data-bidi="ltr">{money(item.paidAmount)}</td>
                  <td className="px-3 py-2" data-bidi="ltr">{money(item.remainingAmount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <Card title={ct('collectionHistory')}>
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {[
            [ct('payments'), `${workspaceBase}/dcr`, data.payments.length],
            ['PTP', `${workspaceBase}/ptp`, data.promises.length],
            [ct('visits'), `${workspaceBase}/visits`, data.visits.length],
            [ct('complaints'), `${workspaceBase}/complaints`, data.complaints.length],
            [ct('activity'), `${workspaceBase}/activity`, data.activities.length],
          ].map(([label, href, count]) => (
            <Link className="rounded-xl border border-mis-border p-4 hover:border-mis-primary" key={String(label)} to={String(href)}>
              <p className="font-bold text-mis-navy">{label}</p>
              <p className="mt-1 text-sm text-slate-500">{count}</p>
            </Link>
          ))}
        </div>
      </Card>

      <Card title={ct('timeline')}>
        <div className="space-y-3">
          {data.timeline.length === 0 ? <p className="text-sm text-slate-500">{ct('noActivity')}</p> : data.timeline.map((item) => (
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl bg-slate-50 px-4 py-3 text-sm" key={`${item.module}-${item.id}`}>
              <div>
                <p className="font-semibold text-mis-navy">{item.module}: {item.title}</p>
                <p className="text-xs text-slate-500">{date(item.occurredAt)}</p>
              </div>
              {item.status ? <StatusBadge>{item.status}</StatusBadge> : null}
              {item.amount != null ? <span data-bidi="ltr">{money(item.amount)}</span> : null}
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}

function splitFileAddresses(primary?: string | null, secondary?: string | null) {
  const marker = ' — ';
  const first = primary?.trim() ?? '';
  const second = secondary?.trim() ?? '';
  if (second && first.endsWith(marker + second)) return { address1: first.slice(0, first.length - (marker + second).length).trim(), address2: second };
  const index = first.indexOf(marker);
  if (index >= 0) return { address1: first.slice(0, index).trim(), address2: second || first.slice(index + marker.length).trim() };
  return { address1: first, address2: second };
}
