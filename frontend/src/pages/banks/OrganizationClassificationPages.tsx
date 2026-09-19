import { ArrowLeft, Building2, Car, CreditCard, Landmark, Layers3, WalletCards } from 'lucide-react';
import { useEffect, useState, type ComponentType } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/common/EmptyState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import {
  PRIMARY_OPTIONS,
  classificationPath,
  deskHintKey,
  deskLabelKey,
  displayPrimary,
  isUuid,
  normalizePrimary,
  organizationPath,
  organizationRoot,
  secondaryOptions,
  type OrganizationKind,
  type PrimaryClassification,
  type SubClassification,
} from '../../features/collections/organizationClassification';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankDirectoryItem } from '../../features/collections/types/collections';

const primaryIcons: Record<PrimaryClassification, ComponentType<{ className?: string }>> = {
  ACT: WalletCards,
  WO: Layers3,
  CORP: Building2,
};

const secondaryIcons: Record<SubClassification, ComponentType<{ className?: string }>> = {
  LOAN: Landmark,
  VISA: CreditCard,
  AUTO: Car,
  ACT: WalletCards,
  WO: Layers3,
};

function SelectionCard({ to, code, label, hint, icon: Icon }: { to: string; code: string; label: string; hint: string; icon: ComponentType<{ className?: string }> }) {
  return (
    <Link
      to={to}
      className="group flex min-h-44 min-w-0 flex-col justify-between rounded-[1.75rem] border border-mis-border bg-white p-6 shadow-sm transition duration-200 hover:-translate-y-0.5 hover:border-mis-sky hover:shadow-panel focus-visible:border-mis-blue sm:p-7"
    >
      <span className="inline-flex h-12 w-12 items-center justify-center rounded-2xl bg-mis-pale text-mis-primary">
        <Icon className="h-6 w-6" aria-hidden="true" />
      </span>
      <div className="min-w-0">
        <span className="text-xs font-bold uppercase tracking-[0.16em] text-mis-primary" data-bidi="ltr">{code}</span>
        <strong className="mt-1 block text-2xl font-bold text-mis-navy">{label}</strong>
        <p className="mt-2 text-sm leading-6 text-slate-500 [overflow-wrap:anywhere]">{hint}</p>
      </div>
    </Link>
  );
}

function useOrganizationRoute(kind: OrganizationKind) {
  const { bankId, companyId } = useParams();
  const organizationId = (kind === 'installment' ? companyId : bankId) ?? bankId ?? companyId ?? '';
  return { organizationId, root: organizationRoot(kind) };
}

function useOrganization(kind: OrganizationKind, organizationId: string) {
  const [organization, setOrganization] = useState<BankDirectoryItem>();
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    if (!isUuid(organizationId)) return;
    let active = true;
    setOrganization(undefined);
    setNotFound(false);
    const request = kind === 'installment'
      ? collectionsService.installmentCompany(organizationId)
      : collectionsService.bank(organizationId);
    void request.then((value) => { if (active) setOrganization(value); }).catch(() => { if (active) setNotFound(true); });
    return () => { active = false; };
  }, [kind, organizationId]);

  return { organization, notFound };
}

function OrganizationMissing({ kind }: { kind: OrganizationKind }) {
  const { ct } = useCollectionsLocalization();
  const root = organizationRoot(kind);
  const installment = kind === 'installment';
  return (
    <EmptyState
      className="rounded-3xl border border-mis-border bg-white"
      title={installment ? ct('installmentCompanyNotFound') : ct('bankNotFound')}
      description={installment ? ct('installmentCompanyNotFoundDescription') : ct('bankNotFoundDescription')}
      action={<Link className="rounded-xl bg-mis-primary px-4 py-2.5 text-sm font-bold text-white" to={root}>{installment ? ct('backToInstallmentCompanies') : ct('backToBanks')}</Link>}
    />
  );
}

export function OrganizationClassificationPrimaryPage({ kind }: { kind: OrganizationKind }) {
  const { language, ct } = useCollectionsLocalization();
  const { organizationId, root } = useOrganizationRoute(kind);
  const { organization, notFound } = useOrganization(kind, organizationId);
  const title = kind === 'installment' ? ct('installmentCompanies') : ct('banks');

  if (!isUuid(organizationId)) return <Navigate to={root} replace />;
  if (notFound) return <OrganizationMissing kind={kind} />;
  if (!organization) return <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>;

  const name = language === 'ar' ? organization.nameArabic : organization.nameEnglish;

  return (
    <div className="mx-auto max-w-[1100px]">
      <Link className="mb-5 inline-flex items-center gap-2 text-sm font-semibold text-mis-primary hover:text-mis-deep" to={root}>
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" />
        {kind === 'installment' ? ct('backToInstallmentCompanies') : ct('backToBanks')}
      </Link>
      <PageHeader eyebrow={title} title={name} description={ct('chooseDeskHelp')} />
      <section className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3" aria-label={ct('chooseDesk')}>
        {PRIMARY_OPTIONS.map((primary) => (
          <SelectionCard
            key={primary}
            to={classificationPath(kind, organizationId, primary)}
            code={displayPrimary(primary)}
            label={ct(deskLabelKey(primary))}
            hint={ct(deskHintKey(primary))}
            icon={primaryIcons[primary]}
          />
        ))}
      </section>
    </div>
  );
}

export function OrganizationClassificationSecondaryPage({ kind }: { kind: OrganizationKind }) {
  const { language, ct } = useCollectionsLocalization();
  const { primary: raw } = useParams();
  const { organizationId, root } = useOrganizationRoute(kind);
  const { organization, notFound } = useOrganization(kind, organizationId);
  const primary = normalizePrimary(raw);
  const title = kind === 'installment' ? ct('installmentCompanies') : ct('banks');

  if (!isUuid(organizationId)) return <Navigate to={root} replace />;
  if (!primary) return <Navigate to={organizationPath(kind, organizationId)} replace />;
  if (notFound) return <OrganizationMissing kind={kind} />;
  if (!organization) return <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>;

  const options = secondaryOptions(primary);
  const name = language === 'ar' ? organization.nameArabic : organization.nameEnglish;

  return (
    <div className="mx-auto max-w-[1100px]">
      <Link className="mb-5 inline-flex items-center gap-2 text-sm font-semibold text-mis-primary hover:text-mis-deep" to={organizationPath(kind, organizationId)}>
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" />
        {kind === 'installment' ? ct('backToInstallmentCompanies') : ct('backToBanks')}
      </Link>
      <PageHeader
        eyebrow={`${title} / ${name} / ${displayPrimary(primary)} · ${ct(deskLabelKey(primary))}`}
        title={primary === 'CORP' ? ct('chooseCorporateClassification') : ct('chooseProductClassification')}
        description={primary === 'CORP' ? ct('chooseCorporateHelp') : ct('chooseProductHelp')}
      />
      <section className={`grid gap-5 ${options.length === 2 ? 'md:grid-cols-2' : 'md:grid-cols-3'}`} aria-label={ct('chooseProductClassification')}>
        {options.map((secondary) => (
          <SelectionCard
            key={secondary}
            to={classificationPath(kind, organizationId, primary, secondary)}
            code={displayPrimary(secondary)}
            label={ct(deskLabelKey(secondary))}
            hint={ct(deskHintKey(primary, secondary))}
            icon={secondaryIcons[secondary]}
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
  const parts = [rootLabel];
  if (organizationName) parts.push(organizationName);
  parts.push(`${displayPrimary(primary)} · ${ct(deskLabelKey(primary))}`);
  parts.push(`${displayPrimary(secondary)} · ${ct(deskLabelKey(secondary))}`);
  return (
    <span className="text-xs font-semibold text-slate-400 [overflow-wrap:anywhere]">
      {parts.join(' / ')}
    </span>
  );
}
