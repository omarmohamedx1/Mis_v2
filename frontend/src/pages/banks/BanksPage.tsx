import { Building2, Search } from 'lucide-react';
import { useEffect, useState } from 'react';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { BankDirectoryCard } from '../../features/collections/components/BankDirectoryCard';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankDirectoryItem } from '../../features/collections/types/collections';
import { organizationRoot, type OrganizationKind } from '../../features/collections/organizationClassification';

export function OrganizationDirectoryPage({ kind }: { kind: OrganizationKind }) {
  const { ct } = useCollectionsLocalization();
  const installment = kind === 'installment';
  const root = organizationRoot(kind);
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [organizations, setOrganizations] = useState<BankDirectoryItem[]>();
  const [error, setError] = useState(false);

  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(search.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let active = true;
    setError(false);
    const request = installment ? collectionsService.installmentCompanies(query) : collectionsService.banks(query);
    void request.then((value) => { if (active) setOrganizations(value); }).catch(() => { if (active) setError(true); });
    return () => { active = false; };
  }, [installment, query]);

  const title = installment ? ct('installmentCompanies') : ct('banks');
  const searchLabel = installment ? ct('searchInstallmentCompanies') : ct('searchBanks');

  return (
    <div className="mx-auto max-w-[1480px]">
      <PageHeader
        eyebrow={ct('collections')}
        title={title}
        description={installment ? ct('installmentCompaniesDescription') : ct('banksDescription')}
      />
      <div className="mb-7 max-w-2xl">
        <label className="relative block">
          <span className="sr-only">{searchLabel}</span>
          <Search className="absolute top-3.5 h-5 w-5 text-slate-400" style={{ insetInlineStart: '0.9rem' }} aria-hidden="true" />
          <input className="field py-3 pe-4 ps-12 shadow-sm" value={search} onChange={(event) => setSearch(event.target.value)} placeholder={searchLabel} type="search" />
        </label>
      </div>
      {error ? <ErrorState title={ct('loadError')} onRetry={() => setQuery((value) => `${value} `)} /> : !organizations ? (
        <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>
      ) : organizations.length === 0 ? (
        <EmptyState className="rounded-3xl border border-dashed border-mis-border bg-white" icon={<Building2 />} title={installment ? ct('noInstallmentCompanies') : ct('noBanks')} description={installment ? ct('noInstallmentCompaniesDescription') : ct('noBanksDescription')} />
      ) : (
        <section className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4" aria-label={title}>
          {organizations.map((item) => (
            <BankDirectoryCard
              bank={item}
              basePath={root}
              key={item.id}
              openLabel={installment ? ct('openInstallmentCompany') : undefined}
            />
          ))}
        </section>
      )}
    </div>
  );
}

export function BanksPage() {
  return <OrganizationDirectoryPage kind="bank" />;
}
