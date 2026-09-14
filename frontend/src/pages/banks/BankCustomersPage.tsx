import { Eye, FileUp, RefreshCw, Search } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useOutletContext } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Pagination } from '../../components/common/Pagination';
import { StatusBadge } from '../../components/common/StatusBadge';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankCustomerPage } from '../../features/collections/types/collections';
import { getApiErrorMessage } from '../../services/apiClient';
import type { BankWorkspaceContext } from './BankWorkspaceLayout';

export function BankCustomersPage() {
  const { bank, organizationKind } = useOutletContext<BankWorkspaceContext>();
  const base = organizationKind === 'installment' ? '/installment-companies' : '/banks';
  const { language, ct } = useCollectionsLocalization();
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);
  const [data, setData] = useState<BankCustomerPage>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const money = (value: number) => new Intl.NumberFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { style: 'currency', currency: 'EGP', maximumFractionDigits: 2 }).format(value);

  useEffect(() => {
    const timer = window.setTimeout(() => { setSearch(searchInput.trim()); setPage(1); }, 300);
    return () => clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    let active = true;
    setLoading(true); setError('');
    void collectionsService.bankCustomers(bank.id, { page, pageSize: 20, search })
      .then((value) => { if (active) setData(value); })
      .catch((reason) => { if (active) setError(getApiErrorMessage(reason, ct('loadError'))); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [bank.id, page, search, ct, reload]);

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-xl font-bold text-mis-navy">{ct('customers')}</h2>
          <p className="mt-1 text-sm text-slate-500">{ct('customersDescription')}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button fullWidth={false} onClick={() => setReload((value) => value + 1)} variant="outline"><RefreshCw className="h-4 w-4" />{ct('refresh')}</Button>
          <Link className="inline-flex items-center gap-2 rounded-xl bg-mis-primary px-4 py-2.5 text-sm font-bold text-white" to={`${base}/${bank.id}/customers/import`}><FileUp className="h-4 w-4" />{ct('importCustomersFile')}</Link>
        </div>
      </div>
      <label className="relative block">
        <Search className="absolute start-3 top-3 h-5 w-5 text-slate-400" />
        <input className="h-11 w-full rounded-xl border border-mis-border pe-3 ps-10 text-sm outline-none" onChange={(event) => setSearchInput(event.target.value)} placeholder={ct('searchCustomers')} value={searchInput} />
      </label>
      <div className="overflow-hidden rounded-2xl border border-mis-border bg-white">
        {loading ? <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>
          : error ? <div className="p-6"><ErrorState message={error} title={ct('loadError')} /></div>
            : !data?.items.length ? <EmptyState title={ct('noCustomers')} />
              : <>
                <div className="overflow-x-auto">
                  <table className="min-w-full text-sm">
                    <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                      <tr>
                        {[ct('customerName'), ct('mobile'), ct('nationalId'), ct('accountContract'), ct('totalOutstanding'), ct('paidAmount'), ct('remainingAmount'), ct('status'), ct('collector'), ct('actions')].map((label) => <th className="px-4 py-3 text-start" key={label}>{label}</th>)}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-mis-border">
                      {data.items.map((item) => (
                        <tr className="hover:bg-slate-50" key={item.id}>
                          <td className="px-4 py-3 font-semibold text-mis-navy">{item.customerName}</td>
                          <td className="px-4 py-3" data-bidi="ltr">{item.mobile ?? '—'}</td>
                          <td className="px-4 py-3" data-bidi="ltr">{item.nationalId ?? '—'}</td>
                          <td className="px-4 py-3" data-bidi="ltr">{item.accountReference ?? item.contractReference ?? '—'}</td>
                          <td className="px-4 py-3" data-bidi="ltr">{money(item.outstandingAmount)}</td>
                          <td className="px-4 py-3" data-bidi="ltr">{money(item.paidAmount)}</td>
                          <td className="px-4 py-3" data-bidi="ltr">{money(item.remainingAmount)}</td>
                          <td className="px-4 py-3">{item.status ? <StatusBadge>{ct(item.status as never) || item.status}</StatusBadge> : '—'}</td>
                          <td className="px-4 py-3">{item.assignedCollectorName ?? '—'}</td>
                          <td className="px-4 py-3"><Link className="inline-flex items-center gap-1 rounded-lg border border-mis-border px-3 py-1.5 text-xs font-bold text-mis-primary" to={`${base}/${bank.id}/customers/${item.id}`}><Eye className="h-3.5 w-3.5" />{ct('viewDetails')}</Link></td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <Pagination labels={{ nextPage: '›', pageOf: (current, total) => `${current} / ${total}`, previousPage: '‹', showing: (from, to, total) => `${from}-${to} / ${total}` }} onPageChange={setPage} page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} totalPages={data.totalPages} />
              </>}
      </div>
    </div>
  );
}
