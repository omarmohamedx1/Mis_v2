import { Navigate } from 'react-router-dom';
import { OrganizationClassificationPrimaryPage } from './OrganizationClassificationPages';

export function InstallmentCompaniesPage() {
  return <OrganizationClassificationPrimaryPage kind="installment" />;
}

export function BanksEntryPage() {
  return <OrganizationClassificationPrimaryPage kind="bank" />;
}

export function LegacyOrganizationRedirect({ kind }: { kind: 'bank' | 'installment' }) {
  return <Navigate to={kind === 'installment' ? '/installment-companies' : '/banks'} replace />;
}
