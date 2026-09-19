import { Navigate } from 'react-router-dom';
import { OrganizationDirectoryPage } from './BanksPage';

export function InstallmentCompaniesPage() {
  return <OrganizationDirectoryPage kind="installment" />;
}

export function BanksEntryPage() {
  return <OrganizationDirectoryPage kind="bank" />;
}

export function LegacyOrganizationRedirect({ kind }: { kind: 'bank' | 'installment' }) {
  return <Navigate to={kind === 'installment' ? '/installment-companies' : '/banks'} replace />;
}
