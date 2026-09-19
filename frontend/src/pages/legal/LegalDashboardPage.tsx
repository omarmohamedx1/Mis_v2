import { ArrowUpRight, Scale } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { LegalKpi, LegalPipeline, LegalStageBadge, useLegalText } from '../../features/legal/legalUi';
import { legalService } from '../../features/legal/services/legalService';
import type { LegalCaseListItem, LegalDashboard } from '../../features/legal/types/legal';

export function LegalDashboardPage() {
  const l = useLegalText();
  const navigate = useNavigate();
  const [data, setData] = useState<LegalDashboard>();
  const [cases, setCases] = useState<LegalCaseListItem[]>([]);
  const [error, setError] = useState(false);

  function load() {
    setError(false);
    Promise.all([legalService.dashboard(), legalService.cases({ page: 1, pageSize: 20 })])
      .then(([dashboard, page]) => {
        setData(dashboard);
        setCases(page.items.slice(0, 8));
      })
      .catch(() => setError(true));
  }

  useEffect(() => { load(); }, []);

  if (error) {
    return <ErrorState title={l.text('تعذر تحميل لوحة الشؤون القانونية', 'Could not load the legal dashboard')} onRetry={load} />;
  }
  if (!data) {
    return <div className="grid min-h-[440px] place-items-center"><LoadingSpinner /></div>;
  }

  const kpis = [
    { to: '/legal/cases', label: l.text('قضايا مفتوحة', 'Open cases'), value: l.number(data.openCases), tone: 'amber' as const, hint: l.text('حالات التحصيل بحالة LEGAL', 'Collection cases with LEGAL status') },
    { to: '/legal/cases?stage=INTAKE', label: l.text('بانتظار الاستلام', 'Awaiting intake'), value: l.number(data.intakeCases), tone: 'blue' as const, hint: l.text('لم يُفتح ملف المحكمة بعد', 'Court file not opened yet') },
    { to: '/legal/cases?stage=HEARING', label: l.text('جلسات الأسبوع', 'Hearings this week'), value: l.number(data.hearingsThisWeek), tone: 'red' as const, hint: l.text('خلال السبعة أيام القادمة', 'In the next seven days') },
  ];

  return (
    <div className="space-y-8">
      <header className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p className="text-xs font-bold uppercase tracking-[.18em] text-amber-800">{l.text('الشؤون القانونية', 'LEGAL AFFAIRS')}</p>
          <h1 className="mt-2 text-3xl font-bold text-mis-navy">{l.text('طابور القضايا المحوّلة من التحصيل', 'Legal queue from collections')}</h1>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-500">
            {l.text('لا تُنشأ قضايا منفصلة هنا. التحصيل يحوّل الحالة إلى LEGAL فتظهر في هذا الطابور لكل الشؤون القانونية، ويمكن إعادتها أو تسويتها أو إغلاقها من الملف.', 'Cases are not created here. Collections refers a case to LEGAL, it appears in this firm-wide queue, and legal can return, settle, or close it from the file.')}
          </p>
        </div>
        <Button fullWidth={false} leftIcon={<Scale className="h-4 w-4" />} onClick={() => navigate('/legal/cases')}>
          {l.text('فتح القضايا', 'Open cases')}
        </Button>
      </header>

      <LegalPipeline current={data.intakeCases ? 'INTAKE' : data.hearingsThisWeek ? 'HEARING' : data.openCases ? 'COURT' : undefined} />

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {kpis.map((kpi) => (
          <Link key={kpi.label} className="block transition hover:-translate-y-0.5" to={kpi.to}>
            <LegalKpi hint={kpi.hint} label={kpi.label} tone={kpi.tone} value={kpi.value} />
          </Link>
        ))}
        <LegalKpi
          hint={l.text('أُعيدت للتحصيل', 'Sent back to collections')}
          label={l.text('معاد هذا الشهر', 'Returned this month')}
          tone="green"
          value={l.number(data.returnedThisMonth)}
        />
      </section>

      <article className="rounded-2xl border border-amber-200 bg-amber-50/70 px-5 py-4 text-sm text-amber-950">
        <p className="font-bold">{l.text('المديونية القائمة في الطابور', 'Outstanding in the legal queue')}</p>
        <p className="mt-1 text-2xl font-black" data-bidi="ltr">{l.money(data.outstandingTotal)}</p>
      </article>

      <section className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="flex items-center justify-between border-b border-mis-border px-5 py-4">
          <h2 className="text-lg font-bold text-mis-navy">{l.text('أحدث القضايا القانونية', 'Latest legal cases')}</h2>
          <Link className="inline-flex items-center gap-1 text-sm font-bold text-mis-primary" to="/legal/cases">
            {l.text('كل القضايا', 'All cases')}
            <ArrowUpRight className="h-4 w-4" />
          </Link>
        </div>
        {cases.length ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[48rem] text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  <th className="px-4 py-3 text-start">{l.text('الحالة', 'Case')}</th>
                  <th className="px-4 py-3 text-start">{l.text('العميل', 'Customer')}</th>
                  <th className="px-4 py-3 text-start">{l.text('الجهة', 'Organization')}</th>
                  <th className="px-4 py-3 text-end">{l.text('المديونية', 'Outstanding')}</th>
                  <th className="px-4 py-3 text-start">{l.text('المرحلة', 'Stage')}</th>
                  <th className="px-4 py-3 text-start">{l.text('الجلسة القادمة', 'Next hearing')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {cases.map((item) => (
                  <tr key={item.collectionCaseId} className="cursor-pointer hover:bg-slate-50" onClick={() => navigate(`/legal/cases/${item.collectionCaseId}`)}>
                    <td className="px-4 py-3 font-semibold text-mis-navy" data-bidi="ltr">{item.caseNumber}</td>
                    <td className="px-4 py-3">{item.customerName}</td>
                    <td className="px-4 py-3">{item.organizationName}</td>
                    <td className="px-4 py-3 text-end" data-bidi="ltr">{l.money(item.outstandingBalance)}</td>
                    <td className="px-4 py-3"><LegalStageBadge value={item.legalStage} /></td>
                    <td className="px-4 py-3">{l.date(item.nextHearingOn)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <EmptyState
            compact
            icon={<Scale className="h-5 w-5" />}
            title={l.text('لا توجد قضايا قانونية بعد', 'No legal cases yet')}
            description={l.text('عندما يحوّل التحصيل حالة إلى LEGAL تظهر هنا تلقائيًا.', 'When collections sets a case to LEGAL, it appears here automatically.')}
          />
        )}
      </section>
    </div>
  );
}
