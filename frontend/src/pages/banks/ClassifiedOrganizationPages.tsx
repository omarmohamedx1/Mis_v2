import { OrganizationDirectoryPage } from './BanksPage';
import { OrganizationClassificationSecondaryPage } from './OrganizationClassificationPages';

export function BankClassificationSecondaryPage() {
  return <OrganizationClassificationSecondaryPage kind="bank" />;
}

export function InstallmentClassificationSecondaryPage() {
  return <OrganizationClassificationSecondaryPage kind="installment" />;
}

export function BankClassifiedDirectoryPage() {
  return <OrganizationDirectoryPage kind="bank" />;
}

export function InstallmentClassifiedDirectoryPage() {
  return <OrganizationDirectoryPage kind="installment" />;
}
