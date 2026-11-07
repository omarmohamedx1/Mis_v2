import { ArrowLeft, ArrowRight, BriefcaseBusiness } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { BankLogo } from '../../features/collections/components/BankLogo';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { displayPrimary, isUuid, normalizePrimary } from '../../features/collections/organizationClassification';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankDirectoryItem } from '../../features/collections/types/collections';
import type { CollectionScorePortfolio } from '../../features/collections/types/score';

interface PageData {
  organization?: BankDirectoryItem;
  portfolios: CollectionScorePortfolio[];
  loading: boolean;
  error: boolean;
}

export function CollectionsScorePortfolioPage() {
  const { ct, language, isRtl } = useCollectionsLocalization();
  const { organizationId = '', portfolioType = '' } = useParams();
  const classification = normalizePrimary(portfolioType);
  const [data, setData] = useState<PageData>({ portfolios: [], loading: true, error: false });

  const load = useCallback(async () => {
    if (!classification || !isUuid(organizationId)) return;
    setData(current => ({ ...current, loading: true, error: false }));
    try {
      const [banks, companies, portfolios] = await Promise.all([
        collectionsService.banks(),
        collectionsService.installmentCompanies(),
        collectionsService.scorePortfolios(organizationId, classification),
      ]);
      const organization = [...banks, ...companies].find(item => item.id === organizationId);
      setData({ organization, portfolios, loading: false, error: false });
    } catch {
      setData(current => ({ ...current, loading: false, error: true }));
    }
  }, [classification, organizationId]);

  useEffect(() => {
    void load();
  }, [load]);

  if (!isUuid(organizationId) || !classification) return <Navigate to="/collections/score" replace />;
  if (data.loading) return <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>;
  if (data.error) {
    return <ErrorState description={ct('scoreLoadError')} onRetry={() => void load()} retryLabel={ct('scoreRetry')} title={ct('score')} />;
  }
  if (!data.organization) {
    return (
      <EmptyState
        className="rounded-3xl border border-mis-border bg-white"
        title={ct('scoreOrganizationNotFound')}
        description={ct('scoreOrganizationNotFoundDescription')}
        action={<Link className="rounded-xl bg-mis-primary px-4 py-2.5 text-sm font-bold text-white" to="/collections/score">{ct('scoreBack')}</Link>}
      />
    );
  }

  const organizationName = language === 'ar'
    ? data.organization.nameArabic || data.organization.nameEnglish
    : data.organization.nameEnglish || data.organization.nameArabic;

  return (
    <div className="mx-auto max-w-[1100px]" dir={isRtl ? 'rtl' : 'ltr'}>
      <Link
        className="mb-5 inline-flex items-center gap-2 text-sm font-semibold text-mis-primary hover:text-mis-deep"
        to={`/collections/score/${organizationId}`}
      >
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" />
        {ct('scoreBackToOrganization')}
      </Link>
      <PageHeader
        eyebrow={`${ct('score')} · ${displayPrimary(classification)}`}
        title={(
          <span className="flex min-w-0 items-center gap-4">
            <BankLogo
              className="h-16 w-16 shrink-0 rounded-2xl"
              code={data.organization.code}
              logoUrl={data.organization.logoUrl}
              name={organizationName}
            />
            <span className="truncate">{organizationName}</span>
          </span>
        )}
      />
      <p className="mb-6 text-sm leading-6 text-slate-500">{ct('scoreChooseSpecificPortfolio')}</p>
      {data.portfolios.length === 0 ? (
        <EmptyState
          className="rounded-3xl border border-mis-border bg-white"
          title={ct('scoreNoPortfolios')}
          description={ct('scoreNoPortfoliosDescription')}
        />
      ) : (
        <section aria-label={ct('scoreChooseSpecificPortfolio')} className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {data.portfolios.map(portfolio => {
            const name = language === 'ar'
              ? portfolio.nameArabic || portfolio.nameEnglish
              : portfolio.nameEnglish || portfolio.nameArabic;
            return (
              <Link
                className="group flex min-h-44 min-w-0 flex-col justify-between rounded-[1.75rem] border border-mis-border bg-white p-6 shadow-sm transition duration-200 hover:-translate-y-0.5 hover:border-mis-sky hover:shadow-panel focus-visible:border-mis-blue sm:p-7"
                key={portfolio.id}
                to={`/collections/score/${organizationId}/${classification.toLowerCase()}/portfolios/${portfolio.id}`}
              >
                <span className="inline-flex h-12 w-12 items-center justify-center rounded-2xl bg-mis-pale text-mis-primary">
                  <BriefcaseBusiness className="h-6 w-6" aria-hidden="true" />
                </span>
                <span className="mt-6 min-w-0">
                  <strong className="block truncate text-xl font-bold text-mis-navy">{name}</strong>
                  <span className="mt-2 flex items-center justify-between gap-3 text-sm text-slate-500">
                    <span data-bidi="ltr">{portfolio.code}</span>
                    <span className="inline-flex items-center gap-1 font-semibold text-mis-primary">
                      {ct('scoreOpenPortfolio')}
                      <ArrowRight className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />
                    </span>
                  </span>
                </span>
              </Link>
            );
          })}
        </section>
      )}
    </div>
  );
}
