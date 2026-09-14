import { ArrowLeft, Building2, Layers3 } from 'lucide-react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { PageHeader } from '../../components/common/PageHeader';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import {
  PRIMARY_OPTIONS,
  classificationPath,
  displayPrimary,
  normalizePrimary,
  organizationRoot,
  secondaryOptions,
  type OrganizationKind,
  type PrimaryClassification,
  type SubClassification,
} from '../../features/collections/organizationClassification';

function SelectionCard({ to, label, hint }: { to: string; label: string; hint: string }) {
  return (
    <Link
      to={to}
      className="group flex min-h-40 flex-col justify-between rounded-[1.75rem] border border-mis-border bg-white p-7 shadow-sm transition duration-200 hover:-translate-y-0.5 hover:border-mis-sky hover:shadow-panel focus-visible:border-mis-blue"
    >
      <span className="inline-flex h-12 w-12 items-center justify-center rounded-2xl bg-mis-pale text-mis-primary">
        <Layers3 className="h-6 w-6" aria-hidden="true" />
      </span>
      <div>
        <strong className="block text-3xl font-bold tracking-[0.18em] text-mis-navy" data-bidi="ltr">{label}</strong>
        <p className="mt-2 text-sm text-slate-500">{hint}</p>
      </div>
    </Link>
  );
}

export function OrganizationClassificationPrimaryPage({ kind }: { kind: OrganizationKind }) {
  const { ct } = useCollectionsLocalization();
  const root = organizationRoot(kind);
  const title = kind === 'installment' ? ct('installmentCompanies') : ct('banks');

  return (
    <div className="mx-auto max-w-[1100px]">
      <PageHeader eyebrow={ct('collections')} title={title} description={ct('choosePortfolioClassification')} />
      <section className="grid gap-5 md:grid-cols-3" aria-label={ct('choosePortfolioClassification')}>
        {PRIMARY_OPTIONS.map((primary) => (
          <SelectionCard
            key={primary}
            to={classificationPath(kind, primary)}
            label={displayPrimary(primary)}
            hint={ct('continueToSubClassification')}
          />
        ))}
      </section>
      <p className="mt-8 text-center text-sm text-slate-400">
        <Building2 className="mr-2 inline h-4 w-4" aria-hidden="true" />
        {ct('classificationThenOrganizations')}
      </p>
    </div>
  );
}

export function OrganizationClassificationSecondaryPage({ kind }: { kind: OrganizationKind }) {
  const { ct } = useCollectionsLocalization();
  const { primary: raw } = useParams();
  const primary = normalizePrimary(raw);
  const root = organizationRoot(kind);
  const title = kind === 'installment' ? ct('installmentCompanies') : ct('banks');

  if (!primary) return <Navigate to={root} replace />;

  const options = secondaryOptions(primary);
  const label = (value: SubClassification) => displayPrimary(value);

  return (
    <div className="mx-auto max-w-[1100px]">
      <Link className="mb-5 inline-flex items-center gap-2 text-sm font-semibold text-mis-primary hover:text-mis-deep" to={root}>
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" />
        {kind === 'installment' ? ct('backToInstallmentCompanies') : ct('backToBanks')}
      </Link>
      <PageHeader
        eyebrow={`${title} / ${displayPrimary(primary)}`}
        title={primary === 'CORP' ? ct('chooseCorporateClassification') : ct('chooseProductClassification')}
        description={ct('classificationContextHelp')}
      />
      <section className={`grid gap-5 ${options.length === 2 ? 'md:grid-cols-2' : 'md:grid-cols-3'}`} aria-label={ct('chooseProductClassification')}>
        {options.map((secondary) => (
          <SelectionCard
            key={secondary}
            to={classificationPath(kind, primary, secondary)}
            label={label(secondary)}
            hint={ct('continueToOrganizations')}
          />
        ))}
      </section>
    </div>
  );
}

export function OrganizationClassificationBreadcrumb({
  kind,
  primary,
  secondary,
  organizationName,
}: {
  kind: OrganizationKind;
  primary: PrimaryClassification;
  secondary: SubClassification;
  organizationName?: string;
}) {
  const { ct } = useCollectionsLocalization();
  const rootLabel = kind === 'installment' ? ct('installmentCompanies') : ct('banks');
  const parts = [rootLabel, displayPrimary(primary), displayPrimary(secondary)];
  if (organizationName) parts.push(organizationName);
  return (
    <span className="text-xs font-semibold tracking-wide text-slate-400" data-bidi="ltr">
      {parts.join(' / ')}
    </span>
  );
}
