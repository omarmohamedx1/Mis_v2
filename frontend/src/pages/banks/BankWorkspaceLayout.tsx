import { Archive, ArrowLeft, ClipboardList, FileUp, Gauge, HandCoins, MapPinned, MessageSquareWarning, Route, Users, WalletCards, Activity } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, Navigate, NavLink, Outlet, useLocation, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/common/EmptyState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { BankLogo } from '../../features/collections/components/BankLogo';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankDirectoryItem } from '../../features/collections/types/collections';
import {
  deskLabelKey,
  displayPrimary,
  isUuid,
  organizationPath,
  organizationRoot,
  organizationWorkspaceBase,
  parseClassification,
  rewriteLegacyOrganizationPath,
  type OrganizationClassification,
  type OrganizationKind,
} from '../../features/collections/organizationClassification';
import { OrganizationClassificationBreadcrumb } from './OrganizationClassificationPages';

export interface BankWorkspaceContext {
  bank: BankDirectoryItem;
  organizationKind: OrganizationKind;
  classification: OrganizationClassification;
  workspaceBase: string;
}

const stages = [
  {
    label: 'workspaceStageIntake',
    items: [
      ['overview', 'overview', Gauge],
      ['import', 'importData', FileUp],
      ['portfolio', 'portfolio', WalletCards],
      ['customers', 'customers', Users],
    ],
  },
  {
    label: 'workspaceStageFloor',
    items: [
      ['distribution', 'distribution', Route],
      ['activity', 'activity', Activity],
      ['ptp', 'ptp', HandCoins],
      ['visits', 'visits', MapPinned],
      ['dcr', 'dcr', ClipboardList],
    ],
  },
  {
    label: 'workspaceStageClose',
    items: [
      ['complaints', 'complaints', MessageSquareWarning],
      ['archive', 'archive', Archive],
    ],
  },
] as const;

export function BankWorkspaceLayout() {
  const { bankId, companyId, primary, secondary } = useParams();
  const location = useLocation();
  const rewritten = rewriteLegacyOrganizationPath(location.pathname, location.search);
  const organizationId = companyId ?? bankId ?? '';
  const installment = Boolean(companyId) || window.location.pathname.startsWith('/installment-companies/');
  const kind: OrganizationKind = installment ? 'installment' : 'bank';
  const classification = parseClassification(primary, secondary);
  const root = organizationRoot(kind);
  const { language, ct } = useCollectionsLocalization();
  const [bank, setBank] = useState<BankDirectoryItem>();
  const [notFound, setNotFound] = useState(false);
  const canLoad = Boolean(classification && isUuid(organizationId));
  const classificationHome = isUuid(organizationId) ? organizationPath(kind, organizationId) : root;
  const workspaceBase = classification && isUuid(organizationId)
    ? organizationWorkspaceBase(kind, organizationId, classification)
    : root;

  useEffect(() => {
    if (!canLoad) return;
    let active = true;
    setBank(undefined); setNotFound(false);
    const request = installment ? collectionsService.installmentCompany(organizationId) : collectionsService.bank(organizationId);
    void request.then((value) => { if (active) setBank(value); }).catch(() => { if (active) setNotFound(true); });
    return () => { active = false; };
  }, [canLoad, installment, organizationId]);

  if (rewritten) return <Navigate to={rewritten} replace />;

  if (!classification || !isUuid(organizationId)) {
    return <Navigate to={root} replace />;
  }

  if (notFound) {
    return (
      <EmptyState
        className="rounded-3xl border border-mis-border bg-white"
        title={installment ? ct('installmentCompanyNotFound') : ct('bankNotFound')}
        description={installment ? ct('installmentCompanyNotFoundDescription') : ct('bankNotFoundDescription')}
        action={<Link className="rounded-xl bg-mis-primary px-4 py-2.5 text-sm font-bold text-white" to={root}>{installment ? ct('backToInstallmentCompanies') : ct('backToBanks')}</Link>}
      />
    );
  }
  if (!bank) return <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>;
  const name = language === 'ar' ? bank.nameArabic : bank.nameEnglish;

  return (
    <div className="mx-auto max-w-[1480px]">
      <Link className="mb-5 inline-flex items-center gap-2 text-sm font-semibold text-mis-primary hover:text-mis-deep" to={classificationHome}>
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" />
        {installment ? ct('backToInstallmentCompanies') : ct('backToBanks')}
      </Link>
      <header className="overflow-hidden rounded-[2rem] border border-mis-border bg-white shadow-sm">
        <div className="flex flex-col gap-5 p-6 sm:flex-row sm:items-center sm:p-8">
          <BankLogo className="h-24 w-24 rounded-[1.75rem]" code={bank.code} logoUrl={bank.logoUrl} name={name} />
          <div className="min-w-0 flex-1">
            <OrganizationClassificationBreadcrumb kind={kind} primary={classification.primary} secondary={classification.secondary} organizationName={name} />
            <p className="mt-2 text-xs font-bold text-mis-primary">{installment ? ct('installmentCompanyWorkspace') : ct('bankWorkspace')}</p>
            <h1 className="mt-2 text-2xl font-bold text-mis-navy sm:text-3xl">{name}</h1>
            <p className="mt-2 text-sm font-semibold text-mis-navy">
              <span data-bidi="ltr">{displayPrimary(classification.primary)}</span>
              {' · '}
              {ct(deskLabelKey(classification.primary))}
              {' / '}
              <span data-bidi="ltr">{displayPrimary(classification.secondary)}</span>
              {' · '}
              {ct(deskLabelKey(classification.secondary))}
            </p>
            <p className="mt-2 text-sm text-slate-500">{ct('workspaceDeskSubtitle')}</p>
            <p className="mt-1 text-sm font-semibold text-slate-400" data-bidi="ltr">{bank.code}</p>
          </div>
        </div>
        <nav className="workspace-nav-stages border-t border-mis-border bg-slate-50/80 px-3 py-4 sm:px-5" aria-label={installment ? ct('installmentCompanyWorkspace') : ct('bankWorkspace')}>
          {stages.map((stage) => (
            <section className="workspace-nav-stage min-w-0" key={stage.label}>
              <p className="px-1 pb-2 text-[11px] font-bold uppercase tracking-[0.14em] text-slate-400">{ct(stage.label)}</p>
              <div className="workspace-nav-stage-links">
                {stage.items.map(([path, label, Icon]) => (
                  <NavLink
                    key={path}
                    to={`${workspaceBase}/${path}`}
                    className={({ isActive }) => `flex min-w-0 flex-col items-center justify-center gap-1 rounded-xl px-1 py-2 text-center text-[11px] font-semibold leading-5 transition sm:text-xs ${isActive ? 'bg-mis-primary text-white shadow-sm' : 'bg-white text-slate-600 hover:text-mis-primary'}`}
                  >
                    <Icon className="h-4 w-4 shrink-0" /><span className="min-w-0 max-w-full [overflow-wrap:anywhere]">{ct(label)}</span>
                  </NavLink>
                ))}
              </div>
            </section>
          ))}
        </nav>
      </header>
      <main className="mt-6">
        <Outlet context={{ bank, organizationKind: kind, classification, workspaceBase } satisfies BankWorkspaceContext} />
      </main>
    </div>
  );
}
