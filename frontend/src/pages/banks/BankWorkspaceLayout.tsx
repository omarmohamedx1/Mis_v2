import { Activity, Archive, ArrowLeft, BarChart3, ClipboardList, FileUp, Gauge, MapPinned, Route, Users, WalletCards } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, Navigate, NavLink, Outlet, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/common/EmptyState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { BankLogo } from '../../features/collections/components/BankLogo';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankDirectoryItem } from '../../features/collections/types/collections';
import {
  classificationPath,
  isUuid,
  organizationRoot,
  organizationWorkspaceBase,
  parseClassification,
  type OrganizationClassification,
  type OrganizationKind,
} from '../../features/collections/organizationClassification';
import { OrganizationClassificationBreadcrumb } from './OrganizationClassificationPages';

export interface BankWorkspaceContext {
  bank: BankDirectoryItem;
  organizationKind: OrganizationKind;
  classification: OrganizationClassification;
}

const sections = [
  ['overview', 'overview', Gauge], ['import', 'importData', FileUp], ['customers', 'customers', Users], ['portfolio', 'portfolio', WalletCards],
  ['distribution', 'distribution', Route], ['ptp', 'ptp', BarChart3], ['dcr', 'dcr', ClipboardList],
  ['visits', 'visits', MapPinned], ['complaints', 'complaints', ClipboardList], ['activity', 'activity', Activity], ['archive', 'archive', Archive],
] as const;

export function BankWorkspaceLayout() {
  const { bankId, companyId, primary, secondary } = useParams();
  const organizationId = companyId ?? bankId ?? '';
  const installment = Boolean(companyId) || window.location.pathname.startsWith('/installment-companies/');
  const kind: OrganizationKind = installment ? 'installment' : 'bank';
  const classification = parseClassification(primary, secondary);
  const root = organizationRoot(kind);
  const { language, ct } = useCollectionsLocalization();
  const [bank, setBank] = useState<BankDirectoryItem>();
  const [notFound, setNotFound] = useState(false);
  const canLoad = Boolean(classification && isUuid(organizationId));
  const directoryPath = classification
    ? classificationPath(kind, classification.primary, classification.secondary)
    : root;
  const workspaceBase = classification && isUuid(organizationId)
    ? organizationWorkspaceBase(kind, classification, organizationId)
    : root;

  useEffect(() => {
    if (!canLoad) return;
    let active = true;
    setBank(undefined); setNotFound(false);
    const request = installment ? collectionsService.installmentCompany(organizationId) : collectionsService.bank(organizationId);
    void request.then((value) => { if (active) setBank(value); }).catch(() => { if (active) setNotFound(true); });
    return () => { active = false; };
  }, [canLoad, installment, organizationId]);

  if (!classification || !isUuid(organizationId)) {
    return <Navigate to={root} replace />;
  }

  if (notFound) {
    return (
      <EmptyState
        className="rounded-3xl border border-mis-border bg-white"
        title={installment ? ct('installmentCompanyNotFound') : ct('bankNotFound')}
        description={installment ? ct('installmentCompanyNotFoundDescription') : ct('bankNotFoundDescription')}
        action={<Link className="rounded-xl bg-mis-primary px-4 py-2.5 text-sm font-bold text-white" to={directoryPath}>{installment ? ct('backToInstallmentCompanies') : ct('backToBanks')}</Link>}
      />
    );
  }
  if (!bank) return <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>;
  const name = language === 'ar' ? bank.nameArabic : bank.nameEnglish;

  return (
    <div className="mx-auto max-w-[1480px]">
      <Link className="mb-5 inline-flex items-center gap-2 text-sm font-semibold text-mis-primary hover:text-mis-deep" to={directoryPath}>
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" />
        {installment ? ct('backToInstallmentCompanies') : ct('backToBanks')}
      </Link>
      <header className="overflow-hidden rounded-[2rem] border border-mis-border bg-white shadow-sm">
        <div className="flex flex-col gap-5 p-6 sm:flex-row sm:items-center sm:p-8">
          <BankLogo className="h-24 w-24 rounded-[1.75rem]" code={bank.code} logoUrl={bank.logoUrl} name={name} />
          <div className="min-w-0 flex-1">
            <OrganizationClassificationBreadcrumb kind={kind} primary={classification.primary} secondary={classification.secondary} organizationName={name} />
            <p className="mt-2 text-xs font-bold uppercase tracking-[0.2em] text-mis-primary">{installment ? ct('installmentCompanyWorkspace') : ct('bankWorkspace')}</p>
            <h1 className="mt-2 text-2xl font-bold text-mis-navy sm:text-3xl">{name}</h1>
            <p className="mt-2 text-sm font-semibold text-slate-400" data-bidi="ltr">{bank.code}</p>
          </div>
        </div>
        <nav className="flex gap-2 overflow-x-auto border-t border-mis-border bg-slate-50/70 px-4 py-3 sm:px-6" aria-label={installment ? ct('installmentCompanyWorkspace') : ct('bankWorkspace')}>
          {sections.map(([path, label, Icon]) => (
            <NavLink
              key={path}
              to={`${workspaceBase}/${path}`}
              className={({ isActive }) => `inline-flex shrink-0 items-center gap-2 rounded-xl px-3.5 py-2.5 text-sm font-semibold transition ${isActive ? 'bg-mis-primary text-white shadow-sm' : 'text-slate-600 hover:bg-white hover:text-mis-primary'}`}
            >
              <Icon className="h-4 w-4" />{ct(label)}
            </NavLink>
          ))}
        </nav>
      </header>
      <main className="mt-6">
        <Outlet context={{ bank, organizationKind: kind, classification } satisfies BankWorkspaceContext} />
      </main>
    </div>
  );
}
