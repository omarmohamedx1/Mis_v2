import { ArrowLeft, Building2, Layers3, WalletCards } from 'lucide-react';
import { useCallback, useEffect, useState, type ComponentType } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { BankLogo } from '../../features/collections/components/BankLogo';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import {
  PRIMARY_OPTIONS,
  displayPrimary,
  isUuid,
  type PrimaryClassification,
} from '../../features/collections/organizationClassification';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankDirectoryItem } from '../../features/collections/types/collections';

const portfolioIcons: Record<PrimaryClassification, ComponentType<{ className?: string }>> = {
  ACT: WalletCards,
  WO: Layers3,
  CORP: Building2,
};

function useScoreOrganization(organizationId: string) {
  const [result, setResult] = useState<{
    organizationId: string;
    organization?: BankDirectoryItem;
    loading: boolean;
    error: boolean;
    notFound: boolean;
  }>({ organizationId: '', loading: true, error: false, notFound: false });

  const load = useCallback(async () => {
    setResult({ organizationId, loading: true, error: false, notFound: false });
    try {
      const [banks, companies] = await Promise.all([
        collectionsService.banks(),
        collectionsService.installmentCompanies(),
      ]);
      const match = [...banks, ...companies].find((item) => item.id === organizationId);
      setResult({ organizationId, organization: match, loading: false, error: false, notFound: !match });
    } catch {
      setResult({ organizationId, loading: false, error: true, notFound: false });
    }
  }, [organizationId]);

  useEffect(() => {
    void load();
  }, [load]);

  const current = result.organizationId === organizationId
    ? result
    : { organizationId, loading: true, error: false, notFound: false };
  return { ...current, retry: load };
}

function ScoreOrganizationHeader({ organization, name, eyebrow }: {
  organization: BankDirectoryItem;
  name: string;
  eyebrow: string;
}) {
  return (
    <PageHeader
      eyebrow={eyebrow}
      title={(
        <span className="flex min-w-0 items-center gap-4">
          <BankLogo
            className="h-16 w-16 shrink-0 rounded-2xl"
            code={organization.code}
            logoUrl={organization.logoUrl}
            name={name}
          />
          <span className="truncate">{name}</span>
        </span>
      )}
    />
  );
}

function OrganizationLoading() {
  return <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>;
}

export function CollectionsScoreOrganizationPage() {
  const { ct, language } = useCollectionsLocalization();
  const { organizationId = '' } = useParams();
  const { organization, loading, error, notFound, retry } = useScoreOrganization(organizationId);

  if (!isUuid(organizationId)) return <Navigate to="/collections/score" replace />;
  if (loading) return <OrganizationLoading />;
  if (error) {
    return <ErrorState description={ct('scoreLoadError')} onRetry={() => void retry()} retryLabel={ct('scoreRetry')} title={ct('score')} />;
  }
  if (notFound || !organization) {
    return (
      <EmptyState
        className="rounded-3xl border border-mis-border bg-white"
        title={ct('scoreOrganizationNotFound')}
        description={ct('scoreOrganizationNotFoundDescription')}
        action={<Link className="rounded-xl bg-mis-primary px-4 py-2.5 text-sm font-bold text-white" to="/collections/score">{ct('scoreBack')}</Link>}
      />
    );
  }

  const name = language === 'ar'
    ? organization.nameArabic || organization.nameEnglish
    : organization.nameEnglish || organization.nameArabic;

  return (
    <div className="mx-auto max-w-[1100px]">
      <Link className="mb-5 inline-flex items-center gap-2 text-sm font-semibold text-mis-primary hover:text-mis-deep" to="/collections/score">
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" />
        {ct('scoreBack')}
      </Link>
      <ScoreOrganizationHeader eyebrow={ct('score')} name={name} organization={organization} />
      <p className="mb-6 text-sm leading-6 text-slate-500">{ct('scoreChoosePortfolio')}</p>
      <section className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3" aria-label={ct('scoreChoosePortfolio')}>
        {PRIMARY_OPTIONS.map((portfolio) => {
          const Icon = portfolioIcons[portfolio];
          return (
            <Link
              className="group flex min-h-44 min-w-0 flex-col justify-between rounded-[1.75rem] border border-mis-border bg-white p-6 shadow-sm transition duration-200 hover:-translate-y-0.5 hover:border-mis-sky hover:shadow-panel focus-visible:border-mis-blue sm:p-7"
              key={portfolio}
              to={`/collections/score/${organization.id}/${portfolio.toLowerCase()}`}
            >
              <span className="inline-flex h-12 w-12 items-center justify-center rounded-2xl bg-mis-pale text-mis-primary">
                <Icon className="h-6 w-6" aria-hidden="true" />
              </span>
              <span className="mt-6 min-w-0">
                <span className="text-xs font-bold uppercase tracking-[0.16em] text-mis-primary" data-bidi="ltr">{displayPrimary(portfolio)}</span>
                <strong className="mt-1 block text-2xl font-bold text-mis-navy">{ct(`scorePortfolio${portfolio}`)}</strong>
                <span className="mt-2 block text-sm leading-6 text-slate-500 [overflow-wrap:anywhere]">{ct(`scorePortfolio${portfolio}Description`)}</span>
              </span>
            </Link>
          );
        })}
      </section>
    </div>
  );
}
