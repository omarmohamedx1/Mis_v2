import { Building2, Landmark, Search } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState, type KeyboardEvent, type ReactNode } from 'react';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { Link } from 'react-router-dom';
import { BankLogo } from '../../features/collections/components/BankLogo';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankDirectoryItem } from '../../features/collections/types/collections';

type ScoreOrganizationTab = 'banks' | 'companies';

export function CollectionsScorePage() {
  const { ct, isRtl, language } = useCollectionsLocalization();
  const [banks, setBanks] = useState<BankDirectoryItem[]>([]);
  const [companies, setCompanies] = useState<BankDirectoryItem[]>([]);
  const [activeTab, setActiveTab] = useState<ScoreOrganizationTab>('banks');
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [hasError, setHasError] = useState(false);

  const loadOrganizations = useCallback(async () => {
    setLoading(true);
    setHasError(false);
    try {
      const [bankOrganizations, companyOrganizations] = await Promise.all([
        collectionsService.banks(),
        collectionsService.installmentCompanies(),
      ]);
      setBanks(bankOrganizations);
      setCompanies(companyOrganizations);
    } catch {
      setHasError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadOrganizations();
  }, [loadOrganizations]);

  const organizations = activeTab === 'banks' ? banks : companies;
  const filteredOrganizations = useMemo(() => {
    const query = search.trim().toLocaleLowerCase();
    if (!query) return organizations;
    return organizations.filter((organization) =>
      `${organization.nameArabic} ${organization.nameEnglish}`.toLocaleLowerCase().includes(query),
    );
  }, [organizations, search]);
  const numberFormat = new Intl.NumberFormat(language === 'ar' ? 'ar' : 'en');

  return (
    <div dir={isRtl ? 'rtl' : 'ltr'}>
      <PageHeader
        description={ct('scoreDescription')}
        eyebrow={ct('collections')}
        title={ct('score')}
      />

      <section aria-label={ct('score')} className="space-y-5">
        <div aria-label={ct('score')} className="grid grid-cols-2 gap-2 rounded-2xl border border-mis-border bg-slate-50 p-2" role="tablist">
          <OrganizationTab
            active={activeTab === 'banks'}
            count={numberFormat.format(banks.length)}
            icon={<Landmark className="h-4 w-4" />}
            label={ct('scoreBanks')}
            onSelect={setActiveTab}
            panelId="score-banks-panel"
            rtl={isRtl}
            tab="banks"
          />
          <OrganizationTab
            active={activeTab === 'companies'}
            count={numberFormat.format(companies.length)}
            icon={<Building2 className="h-4 w-4" />}
            label={ct('scoreCompanies')}
            onSelect={setActiveTab}
            panelId="score-companies-panel"
            rtl={isRtl}
            tab="companies"
          />
        </div>

        <label className="relative block">
          <Search className="pointer-events-none absolute start-4 top-1/2 h-4 w-4 -translate-y-1/2 text-mis-muted" />
          <input
            aria-label={ct('scoreSearch')}
            className="h-12 w-full rounded-2xl border border-mis-control bg-white pe-4 ps-11 text-sm shadow-sm outline-none transition focus:border-mis-primary focus:ring-2 focus:ring-mis-primary/10"
            onChange={(event) => setSearch(event.target.value)}
            placeholder={ct('scoreSearch')}
            type="search"
            value={search}
          />
        </label>

        <div
          aria-labelledby={activeTab === 'banks' ? 'score-banks-tab' : 'score-companies-tab'}
          className="min-h-56 rounded-2xl border border-mis-border bg-white p-4 shadow-sm sm:p-6"
          id={activeTab === 'banks' ? 'score-banks-panel' : 'score-companies-panel'}
          role="tabpanel"
        >
          {loading ? (
            <div aria-label={ct('scoreLoading')} className="flex min-h-48 items-center justify-center gap-3 text-sm text-mis-muted" role="status">
              <LoadingSpinner className="h-5 w-5 text-mis-primary" />
              <span>{ct('scoreLoading')}</span>
            </div>
          ) : hasError ? (
            <ErrorState
              description={ct('scoreLoadError')}
              onRetry={() => void loadOrganizations()}
              retryLabel={ct('scoreRetry')}
              title={ct('score')}
            />
          ) : filteredOrganizations.length === 0 ? (
            <EmptyState
              icon={activeTab === 'banks' ? <Landmark className="h-6 w-6" /> : <Building2 className="h-6 w-6" />}
              title={search.trim() ? ct('scoreNoMatches') : ct(activeTab === 'banks' ? 'scoreNoBanks' : 'scoreNoCompanies')}
            />
          ) : (
            <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {filteredOrganizations.map((organization) => {
                const name = language === 'ar'
                  ? organization.nameArabic || organization.nameEnglish
                  : organization.nameEnglish || organization.nameArabic;
                return (
                  <li
                    className="flex min-w-0 items-center gap-4 rounded-xl border border-mis-border bg-white p-4 transition hover:border-mis-sky hover:shadow-sm"
                    key={organization.id}
                  >
                    <Link
                      className="flex min-w-0 flex-1 items-center gap-4 rounded-lg outline-none focus-visible:ring-2 focus-visible:ring-mis-primary"
                      to={`/collections/score/${organization.id}`}
                    >
                      <BankLogo
                        className="h-16 w-16 rounded-2xl"
                        code={organization.code}
                        logoUrl={organization.logoUrl}
                        name={name}
                      />
                      <span className="min-w-0">
                        <span className="block truncate font-semibold text-mis-navy">{name}</span>
                        <span className="mt-1 block truncate text-xs text-mis-muted">{organization.code}</span>
                      </span>
                    </Link>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      </section>
    </div>
  );
}

function OrganizationTab({ active, count, icon, label, onSelect, panelId, rtl, tab }: {
  active: boolean;
  count: string;
  icon: ReactNode;
  label: string;
  onSelect: (tab: ScoreOrganizationTab) => void;
  panelId: string;
  rtl: boolean;
  tab: ScoreOrganizationTab;
}) {
  function handleKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    const tabs: ScoreOrganizationTab[] = ['banks', 'companies'];
    const currentIndex = tabs.indexOf(tab);
    let nextIndex: number | null = null;
    if (event.key === 'Home') nextIndex = 0;
    if (event.key === 'End') nextIndex = tabs.length - 1;
    if (event.key === 'ArrowRight') nextIndex = (currentIndex + (rtl ? -1 : 1) + tabs.length) % tabs.length;
    if (event.key === 'ArrowLeft') nextIndex = (currentIndex - (rtl ? -1 : 1) + tabs.length) % tabs.length;
    if (nextIndex === null) return;

    event.preventDefault();
    const nextTab = tabs[nextIndex];
    document.getElementById(`score-${nextTab}-tab`)?.focus();
    if (nextTab !== tab) onSelect(nextTab);
  }

  return (
    <button
      aria-controls={panelId}
      aria-selected={active}
      className={`flex min-h-12 min-w-0 items-center justify-center gap-2 rounded-xl px-3 py-2 text-sm font-semibold transition ${
        active ? 'bg-white text-mis-navy shadow-sm ring-1 ring-mis-border' : 'text-slate-500 hover:bg-white/70 hover:text-mis-navy'
      }`}
      id={`score-${tab}-tab`}
      onClick={() => onSelect(tab)}
      onKeyDown={handleKeyDown}
      role="tab"
      tabIndex={active ? 0 : -1}
      type="button"
    >
      {icon}
      <span className="truncate">{label}</span>
      <span className={`rounded-full px-2 py-0.5 text-xs ${active ? 'bg-mis-pale text-mis-primary' : 'bg-slate-200 text-slate-600'}`}>{count}</span>
    </button>
  );
}
