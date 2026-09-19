import { ArrowUpRight, ClipboardList, FileUp, HandCoins, MapPinned, MessageSquareWarning, Route, WalletCards } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useOutletContext } from 'react-router-dom';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankActivitySummary, BankComplaintSummary, BankPtpSummary, BankVisitSummary, CaseDistributionSummary } from '../../features/collections/types/collections';
import type { BankWorkspaceContext } from './BankWorkspaceLayout';

type DeskSnapshot = {
  distribution: CaseDistributionSummary;
  promises: BankPtpSummary;
  visits: BankVisitSummary;
  activity: BankActivitySummary;
  complaints: BankComplaintSummary;
};

export function BankWorkspaceSectionPage() {
  const { bank, workspaceBase } = useOutletContext<BankWorkspaceContext>();
  const { language, ct } = useCollectionsLocalization();
  const [data, setData] = useState<DeskSnapshot>();
  const [error, setError] = useState(false);
  const [reload, setReload] = useState(0);
  const locale = language === 'ar' ? 'ar-EG' : 'en-GB';
  const number = (value: number) => new Intl.NumberFormat(locale).format(value);

  useEffect(() => {
    let active = true;
    setError(false);
    setData(undefined);
    Promise.all([
      collectionsService.distributionSummary(bank.id),
      collectionsService.bankPtpSummary(bank.id),
      collectionsService.bankVisitSummary(bank.id),
      collectionsService.bankActivitySummary(bank.id),
      collectionsService.bankComplaintSummary(bank.id),
    ]).then(([distribution, promises, visits, activity, complaints]) => {
      if (active) setData({ distribution, promises, visits, activity, complaints });
    }).catch(() => { if (active) setError(true); });
    return () => { active = false; };
  }, [bank.id, reload]);

  if (error) return <ErrorState title={ct('loadError')} onRetry={() => setReload((value) => value + 1)} />;
  if (!data) return <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>;

  const empty = data.distribution.totalCases === 0;
  const next = empty
    ? { to: `${workspaceBase}/import`, label: ct('overviewReceiveCta'), help: ct('overviewEmptyHelp') }
    : data.distribution.unassignedCases > 0
      ? { to: `${workspaceBase}/distribution`, label: ct('overviewDistributeCta'), help: ct('overviewKpiUnassigned') }
      : data.promises.dueToday > 0 || data.visits.visitsToday > 0
        ? { to: `${workspaceBase}/dcr`, label: ct('overviewDailyCta'), help: ct('dailyCollectionReportDescription') }
        : { to: `${workspaceBase}/portfolio`, label: ct('overviewCasesCta'), help: ct('workspaceDeskSubtitle') };

  const metrics = [
    { label: ct('overviewKpiCases'), value: data.distribution.totalCases, to: `${workspaceBase}/portfolio`, icon: WalletCards },
    { label: ct('overviewKpiUnassigned'), value: data.distribution.unassignedCases, to: `${workspaceBase}/distribution`, icon: Route },
    { label: ct('overviewKpiPromises'), value: data.promises.dueToday, to: `${workspaceBase}/ptp`, icon: HandCoins },
    { label: ct('overviewKpiVisits'), value: data.visits.visitsToday, to: `${workspaceBase}/visits`, icon: MapPinned },
    { label: ct('overviewKpiContacted'), value: data.activity.casesContactedToday, to: `${workspaceBase}/activity`, icon: ClipboardList },
    { label: ct('overviewKpiComplaints'), value: data.complaints.open, to: `${workspaceBase}/complaints`, icon: MessageSquareWarning },
  ];

  return (
    <div className="space-y-6">
      <section className="overflow-hidden rounded-[1.75rem] border border-mis-border bg-white p-6 shadow-sm sm:p-7">
        <p className="text-xs font-bold uppercase tracking-[0.16em] text-mis-primary">{ct('overviewNextAction')}</p>
        <h2 className="mt-2 text-2xl font-bold text-mis-navy">{next.label}</h2>
        <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500">{next.help}</p>
        <div className="mt-5 flex flex-wrap gap-3">
          <Link className="inline-flex items-center gap-2 rounded-xl bg-mis-primary px-4 py-2.5 text-sm font-bold text-white hover:bg-mis-deep" to={next.to}>
            {empty ? <FileUp className="h-4 w-4" /> : <ArrowUpRight className="h-4 w-4" />}
            {next.label}
          </Link>
          {empty ? null : (
            <>
              <Link className="inline-flex items-center gap-2 rounded-xl border border-mis-border px-4 py-2.5 text-sm font-bold text-mis-navy hover:border-mis-sky" to={`${workspaceBase}/import`}>{ct('overviewReceiveCta')}</Link>
              <Link className="inline-flex items-center gap-2 rounded-xl border border-mis-border px-4 py-2.5 text-sm font-bold text-mis-navy hover:border-mis-sky" to={`${workspaceBase}/portfolio`}>{ct('overviewCasesCta')}</Link>
            </>
          )}
        </div>
      </section>
      {empty ? (
        <EmptyState
          className="rounded-[1.75rem] border border-dashed border-mis-border bg-white"
          icon={<FileUp />}
          title={ct('overviewEmptyTitle')}
          description={ct('overviewEmptyHelp')}
          action={<Link className="rounded-xl bg-mis-primary px-4 py-2.5 text-sm font-bold text-white" to={`${workspaceBase}/import`}>{ct('overviewReceiveCta')}</Link>}
        />
      ) : (
        <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {metrics.map((item) => {
            const Icon = item.icon;
            return (
              <Link className="rounded-[1.5rem] border border-mis-border bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-mis-sky hover:shadow-md" key={item.label} to={item.to}>
                <div className="flex items-start justify-between gap-3">
                  <p className="text-sm font-semibold text-slate-500">{item.label}</p>
                  <span className="grid h-10 w-10 place-items-center rounded-xl bg-mis-pale text-mis-primary"><Icon className="h-5 w-5" /></span>
                </div>
                <p className="mt-3 text-3xl font-bold tabular-nums text-mis-navy" data-bidi="ltr">{number(item.value)}</p>
              </Link>
            );
          })}
        </section>
      )}
    </div>
  );
}
