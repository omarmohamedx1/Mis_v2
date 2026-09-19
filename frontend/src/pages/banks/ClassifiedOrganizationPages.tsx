import { OrganizationClassificationPrimaryPage, OrganizationClassificationSecondaryPage } from './OrganizationClassificationPages';

export function BankClassificationPrimaryPage() {
  return <OrganizationClassificationPrimaryPage kind="bank" />;
}

export function InstallmentClassificationPrimaryPage() {
  return <OrganizationClassificationPrimaryPage kind="installment" />;
}

export function BankClassificationSecondaryPage() {
  return <OrganizationClassificationSecondaryPage kind="bank" />;
}

export function InstallmentClassificationSecondaryPage() {
  return <OrganizationClassificationSecondaryPage kind="installment" />;
}
