import { Scale, Search } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { Pagination } from '../../components/common/Pagination';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { LegalStageBadge, useLegalText } from '../../features/legal/legalUi';
import { legalService } from '../../features/legal/services/legalService';
import type { LegalCaseListItem, LegalOrganization } from '../../features/legal/types/legal';

export function LegalCasesPage() {
  const l = useLegalText();
  const navigate = useNavigate();
  const [url] = useSearchParams();
  const [search, setSearch] = useState(url.get('search') ?? '');
  const [query, setQuery] = useState(url.get('search') ?? '');
  const [organizationId, setOrganizationId] = useState(url.get('organizationId') ?? '');
  const [stage, setStage] = useState(url.get('stage') ?? '');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [items, setItems] = useState<LegalCaseListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [organizations, setOrganizations] = useState<LegalOrganization[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => { setQuery(search.trim()); setPage(1); }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    legalService.organizations().then(setOrganizations).catch(() => undefined);
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);
    legalService.cases({ search: query || undefined, organizationId: organizationId || undefined, stage: stage || undefined, page, pageSize })
      .then((result) => {
        if (cancelled) return;
        setItems(result.items);
        setTotalCount(result.totalCount);
        setTotalPages(result.totalPages);
      })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [query, organizationId, stage, page, pageSize]);

  return (
    <div className="space-y-5">
      <PageHeader
        title={l.text('القضايا القانونية', 'Legal cases')}
        description={l.text('طابور الشركة بالكامل: كل حالة تحصيل بحالة LEGAL. لا يُقيَّد الطابور بمحصل معيّن.', 'Firm-wide queue: every collection case with LEGAL status. It is not scoped to a collector.')}
      />

      <div className="grid gap-3 rounded-2xl border border-mis-border bg-white p-4 shadow-sm lg:grid-cols-[minmax(0,1.4fr)_minmax(0,12rem)_minmax(0,12rem)_auto]">
        <div className="relative">
          <Search className="pointer-events-none absolute top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" style={{ insetInlineStart: '1rem' }} />
          <input
            className="h-12 w-full rounded-form border border-mis-border bg-white pe-4 ps-11 text-sm text-mis-ink placeholder:text-slate-400 focus:border-mis-blue focus:outline-none"
            placeholder={l.text('ابحث برقم الحالة أو اسم العميل', 'Search by case number or customer name')}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>
        <ProfessionalSelect value={organizationId} onChange={(event) => { setOrganizationId(event.target.value); setPage(1); }}>
          <option value="">{l.text('كل الجهات', 'All organizations')}</option>
          {organizations.map((org) => <option key={org.id} value={org.id}>{org.name}</option>)}
        </ProfessionalSelect>
        <ProfessionalSelect value={stage} onChange={(event) => { setStage(event.target.value); setPage(1); }}>
          <option value="">{l.text('كل المراحل', 'All stages')}</option>
          {l.stages.filter((item) => item !== 'RETURNED' && item !== 'SETTLEMENT' && item !== 'JUDGMENT').map((item) => (
            <option key={item} value={item}>{l.stage(item)}</option>
          ))}
        </ProfessionalSelect>
        <ProfessionalSelect value={String(pageSize)} onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }}>
          {[20, 50, 100].map((size) => <option key={size} value={size}>{size}</option>)}
        </ProfessionalSelect>
      </div>

      {error ? (
        <ErrorState title={l.text('تعذر تحميل القضايا', 'Could not load legal cases')} onRetry={() => setPage((value) => value)} />
      ) : loading ? (
        <div className="grid min-h-[280px] place-items-center"><LoadingSpinner /></div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="min-w-[56rem] w-full text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  <th className="px-4 py-3 text-start">{l.text('الحالة', 'Case')}</th>
                  <th className="px-4 py-3 text-start">{l.text('العميل', 'Customer')}</th>
                  <th className="px-4 py-3 text-start">{l.text('الجهة / المحفظة', 'Organization / portfolio')}</th>
                  <th className="px-4 py-3 text-end">{l.text('المديونية', 'Outstanding')}</th>
                  <th className="px-4 py-3 text-start">{l.text('التأخر', 'DPD')}</th>
                  <th className="px-4 py-3 text-start">{l.text('المرحلة', 'Stage')}</th>
                  <th className="px-4 py-3 text-start">{l.text('الجلسة', 'Hearing')}</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {items.map((item) => (
                  <tr key={item.collectionCaseId} className="cursor-pointer hover:bg-slate-50" onClick={() => navigate(`/legal/cases/${item.collectionCaseId}`)}>
                    <td className="px-4 py-3 font-semibold text-mis-navy" data-bidi="ltr">{item.caseNumber}</td>
                    <td className="px-4 py-3">{item.customerName}</td>
                    <td className="px-4 py-3">
                      <p>{item.organizationName}</p>
                      <p className="text-xs text-slate-500">{item.portfolioName}</p>
                    </td>
                    <td className="px-4 py-3 text-end font-semibold" data-bidi="ltr">{l.money(item.outstandingBalance)}</td>
                    <td className="px-4 py-3" data-bidi="ltr">{l.number(item.daysPastDue)}</td>
                    <td className="px-4 py-3"><LegalStageBadge value={item.legalStage} /></td>
                    <td className="px-4 py-3">{l.date(item.nextHearingOn)}</td>
                    <td className="px-4 py-3 text-end">
                      <Button fullWidth={false} size="sm" type="button" variant="ghost" onClick={(event) => { event.stopPropagation(); navigate(`/legal/cases/${item.collectionCaseId}`); }}>
                        {l.text('فتح الملف', 'Open file')}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!items.length ? (
            <EmptyState
              compact
              icon={<Scale className="h-5 w-5" />}
              title={query || organizationId || stage ? l.text('لا توجد نتائج', 'No results') : l.text('لا توجد قضايا قانونية', 'No legal cases')}
              description={l.text('حوّل حالة من التحصيل إلى LEGAL لتظهر في هذا الطابور.', 'Refer a collections case to LEGAL to place it in this queue.')}
            />
          ) : (
            <div className="border-t border-mis-border px-4 py-3">
              <Pagination page={page} pageSize={pageSize} totalCount={totalCount} totalPages={totalPages} onPageChange={setPage} />
            </div>
          )}
        </div>
      )}
    </div>
  );
}
