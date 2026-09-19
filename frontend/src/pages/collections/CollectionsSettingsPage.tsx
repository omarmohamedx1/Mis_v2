import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { Car, CreditCard, ImageUp, Landmark, Layers3, Pencil, Plus, Search, ShieldCheck, Timer, Trash2, WalletCards } from 'lucide-react';
import { useEffect, useMemo, useState, type ComponentType } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge } from '../../components/common/StatusBadge';
import { Tabs } from '../../components/common/Tabs';
import { useToast } from '../../components/common/Toast';
import { Checkbox } from '../../components/forms/Checkbox';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextInput } from '../../components/forms/TextInput';
import { BankLogo } from '../../features/collections/components/BankLogo';
import { OrgTypeBadge } from '../../features/collections/components/CollectionsUi';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { DESK_SLOTS, deskHintKey, deskLabelKey, displayPrimary, organizationWorkspaceBase, type OrganizationClassification, type OrganizationKind, type SubClassification } from '../../features/collections/organizationClassification';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BucketConfiguration, ClientConfiguration, CollectionsConfiguration, PortfolioConfiguration, SaveBucketConfiguration, SaveClientConfiguration, SavePortfolioConfiguration } from '../../features/collections/types/collections';
import { getApiErrorMessage } from '../../services/apiClient';

type SettingsTab = 'identity' | 'portfolios' | 'buckets';
type Editor = { type: 'client'; item?: ClientConfiguration } | { type: 'portfolio'; item?: PortfolioConfiguration } | { type: 'bucket'; item?: BucketConfiguration };
type Removal = { type: 'client' | 'portfolio' | 'bucket'; id: string; name: string };
type StatusFilter = 'all' | 'active' | 'inactive';

const deskIcons: Record<SubClassification, ComponentType<{ className?: string }>> = {
  LOAN: Landmark,
  VISA: CreditCard,
  AUTO: Car,
  ACT: WalletCards,
  WO: Layers3,
};

function organizationKind(type: string): OrganizationKind {
  return type === 'CONSUMER_FINANCE' ? 'installment' : 'bank';
}

function findDesk(portfolios: PortfolioConfiguration[], organizationId: string, slot: OrganizationClassification) {
  return portfolios.find((item) => item.organizationId === organizationId && item.primaryClassification === slot.primary && item.subClassification === slot.secondary);
}

const organizationTypes = ['BANK', 'CONSUMER_FINANCE', 'FINANCIAL_INSTITUTION', 'OTHER'] as const;
const emptyConfig: CollectionsConfiguration = { clients: [], portfolios: [], buckets: [] };

function optionalNumber(value: string) {
  const trimmed = value.trim();
  if (!trimmed) return undefined;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function matchesQuery(search: string, ...values: Array<string | undefined>) {
  if (!search) return true;
  return values.some((value) => (value ?? '').toLowerCase().includes(search));
}

function withLogoRevision(logoUrl: string | undefined, revision: number) {
  if (!logoUrl) return undefined;
  const separator = logoUrl.includes('?') ? '&' : '?';
  return `${logoUrl}${separator}v=${revision}`;
}

export function CollectionsSettingsPage() {
  const { language, ct } = useCollectionsLocalization();
  const toast = useToast();
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedTab = searchParams.get('tab');
  const tab: SettingsTab = requestedTab === 'portfolios' || requestedTab === 'buckets' ? requestedTab : 'identity';
  const [data, setData] = useState<CollectionsConfiguration>(emptyConfig);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [editor, setEditor] = useState<Editor>();
  const [removal, setRemoval] = useState<Removal>();
  const [deleting, setDeleting] = useState(false);
  const [logoBusy, setLogoBusy] = useState<string>();
  const [logoRevision, setLogoRevision] = useState(0);
  const [enablingDesk, setEnablingDesk] = useState<string>();

  const load = async () => {
    setLoading(true);
    setError('');
    try { setData(await collectionsService.configuration()); }
    catch (requestError) { setError(getApiErrorMessage(requestError, ct('loadError'))); }
    finally { setLoading(false); }
  };

  useEffect(() => { void load(); }, []);
  useEffect(() => {
    const timer = window.setTimeout(() => setSearch(searchInput.trim().toLowerCase()), 250);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const setTab = (id: string) => {
    const next = id === 'portfolios' || id === 'buckets' ? id : 'identity';
    setSearchParams(next === 'identity' ? {} : { tab: next }, { replace: true });
  };

  const clients = useMemo(() => data.clients.filter((item) => {
    if (statusFilter === 'active' && !item.isActive) return false;
    if (statusFilter === 'inactive' && item.isActive) return false;
    const name = language === 'ar' ? item.nameArabic : item.nameEnglish;
    return matchesQuery(search, name, item.nameArabic, item.nameEnglish, item.code, item.contactEmail, item.contactPhone);
  }), [data.clients, language, search, statusFilter]);

  const buckets = useMemo(() => data.buckets.filter((item) => {
    if (statusFilter === 'active' && !item.isActive) return false;
    if (statusFilter === 'inactive' && item.isActive) return false;
    const org = data.clients.find((client) => client.id === item.organizationId);
    const portfolio = data.portfolios.find((row) => row.id === item.portfolioId);
    return matchesQuery(search, item.nameArabic, item.nameEnglish, item.code, org?.nameArabic, org?.nameEnglish, portfolio?.nameArabic, portfolio?.nameEnglish);
  }), [data.buckets, data.clients, data.portfolios, search, statusFilter]);

  const organizationName = (id: string) => {
    const org = data.clients.find((item) => item.id === id);
    if (!org) return '—';
    return language === 'ar' ? org.nameArabic : org.nameEnglish;
  };

  const deskName = (primary?: string, sub?: string) => {
    if (!primary || !sub) return ct('unclassified');
    return `${ct(deskLabelKey(primary))} · ${ct(deskLabelKey(sub))}`;
  };

  const ptpLabel = (graceDays?: number, tolerance?: number) => {
    if (graceDays == null && tolerance == null) return ct('ptpNotSet');
    const days = `${graceDays ?? 0} ${ct('daysUnit')}`;
    const amount = new Intl.NumberFormat(language === 'ar' ? 'ar-EG' : 'en-EG').format(tolerance ?? 0);
    return `${days} · ${amount} ${ct('currency')}`;
  };

  const uploadLogo = async (client: ClientConfiguration, file: File) => {
    setLogoBusy(client.id);
    try {
      await collectionsService.uploadClientLogo(client.id, file);
      setData(await collectionsService.configuration());
      setLogoRevision((current) => current + 1);
      toast.success(ct('logoUpdated'));
    } catch (requestError) {
      toast.error(getApiErrorMessage(requestError, ct('saveError')));
    } finally {
      setLogoBusy(undefined);
    }
  };

  const confirmDelete = async () => {
    if (!removal) return;
    setDeleting(true);
    try {
      if (removal.type === 'client') await collectionsService.deleteClient(removal.id);
      else if (removal.type === 'portfolio') await collectionsService.deletePortfolio(removal.id);
      else await collectionsService.deleteBucket(removal.id);
      toast.success(ct('deleteSuccess'));
      setRemoval(undefined);
      setData(await collectionsService.configuration());
    } catch (requestError) {
      toast.error(getApiErrorMessage(requestError, ct('deleteFailed')));
      setRemoval(undefined);
    } finally {
      setDeleting(false);
    }
  };

  const addLabel = tab === 'identity' ? ct('addClient') : ct('addBucket');
  const onAdd = () => {
    if (tab === 'portfolios') return;
    if (tab !== 'identity' && data.clients.length === 0) {
      toast.warning(ct('addFirstClient'));
      setSearchParams({}, { replace: true });
      setEditor({ type: 'client' });
      return;
    }
    setEditor({ type: tab === 'identity' ? 'client' : 'bucket' });
  };

  const enableDesk = async (client: ClientConfiguration, slot: OrganizationClassification) => {
    const key = `${client.id}:${slot.primary}:${slot.secondary}`;
    setEnablingDesk(key);
    try {
      await collectionsService.ensureDesk(client.id, slot.primary, slot.secondary);
      setData(await collectionsService.configuration());
      toast.success(ct('deskEnabled'));
    } catch (requestError) {
      toast.error(getApiErrorMessage(requestError, ct('saveError')));
    } finally {
      setEnablingDesk(undefined);
    }
  };

  const classifiedPortfolios = data.portfolios.filter((item) => item.primaryClassification && item.subClassification);
  const legacyPortfolios = data.portfolios.filter((item) => !item.primaryClassification || !item.subClassification);
  const filteredEmpty = search || statusFilter !== 'all';

  return (
    <div>
      <PageHeader
        actions={tab === 'portfolios' ? undefined : <Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" aria-hidden="true" />} onClick={onAdd} size="md">{addLabel}</Button>}
        description={ct('settingsSubtitle')}
        eyebrow={ct('collections')}
        title={ct('settings')}
      />

      <section className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <Tabs
          ariaLabel={ct('settings')}
          items={[
            { id: 'identity', label: ct('settingsIdentityTab'), badge: data.clients.length || undefined, icon: <Landmark className="h-4 w-4" aria-hidden="true" /> },
            { id: 'portfolios', label: ct('settingsPortfoliosTab'), badge: classifiedPortfolios.length || undefined, icon: <Layers3 className="h-4 w-4" aria-hidden="true" /> },
            { id: 'buckets', label: ct('settingsBucketsTab'), badge: data.buckets.length || undefined, icon: <Timer className="h-4 w-4" aria-hidden="true" /> },
          ]}
          onChange={setTab}
          value={tab}
        />
        <div className="module-filter-grid border-b border-mis-border p-4">
          <label className="relative">
            <span className="sr-only">{ct('searchSettings')}</span>
            <Search className="absolute start-3 top-3 h-5 w-5 text-slate-400" aria-hidden="true" />
            <input className="h-11 w-full rounded-xl border border-mis-border pe-3 ps-10 text-sm outline-none focus:border-mis-blue focus:shadow-input" onChange={(event) => setSearchInput(event.target.value)} placeholder={ct('searchSettings')} value={searchInput} />
          </label>
          <ProfessionalSelect aria-label={ct('status')} className="h-11 rounded-xl border border-mis-border bg-white px-3 text-sm text-slate-700 outline-none focus:border-mis-blue" onChange={(event) => setStatusFilter(event.target.value as StatusFilter)} value={statusFilter}>
            <option value="all">{ct('allStatuses')}</option>
            <option value="active">{ct('active')}</option>
            <option value="inactive">{ct('inactive')}</option>
          </ProfessionalSelect>
        </div>

        {loading ? <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div> : error ? (
          <ErrorState message={error} onRetry={() => void load()} title={ct('loadError')} />
        ) : tab === 'identity' ? (
          clients.length === 0 ? (
            <EmptyState action={<Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" aria-hidden="true" />} onClick={onAdd} size="md">{ct('addClient')}</Button>} description={filteredEmpty ? undefined : ct('addFirstClient')} icon={<Landmark className="h-6 w-6" aria-hidden="true" />} title={ct('noIdentityFound')} />
          ) : (
            <div className="grid gap-4 p-4 sm:grid-cols-2 xl:grid-cols-3">
              {clients.map((client) => {
                const name = language === 'ar' ? client.nameArabic : client.nameEnglish;
                const secondary = language === 'ar' ? client.nameEnglish : client.nameArabic;
                return (
                  <article className="flex flex-col rounded-2xl border border-mis-border bg-white p-5 shadow-sm" key={client.id}>
                    <div className="flex items-start gap-4">
                      <BankLogo className="h-16 w-16" code={client.code} logoUrl={withLogoRevision(client.logoUrl, logoRevision)} name={name} />
                      <div className="min-w-0 flex-1">
                        <div className="flex items-start justify-between gap-2">
                          <h2 className="truncate font-bold text-mis-navy">{name}</h2>
                          <StatusBadge dot tone={client.isActive ? 'success' : 'neutral'}>{ct(client.isActive ? 'active' : 'inactive')}</StatusBadge>
                        </div>
                        <p className="mt-0.5 truncate text-sm text-slate-500">{secondary}</p>
                        <div className="mt-2 flex flex-wrap items-center gap-2">
                          <p className="text-xs font-semibold text-slate-400" data-bidi="ltr">{client.code}</p>
                          <OrgTypeBadge type={client.organizationType} />
                        </div>
                      </div>
                    </div>
                    <dl className="mt-4 space-y-1.5 text-sm text-slate-600">
                      <div className="flex justify-between gap-3"><dt>{ct('identityContact')}</dt><dd className="truncate text-end">{client.contactPhone || client.contactEmail || '—'}</dd></div>
                      <div className="flex justify-between gap-3"><dt>{ct('ptpPolicy')}</dt><dd className="truncate text-end">{ptpLabel(client.ptpGraceDays, client.ptpToleranceAmount)}</dd></div>
                    </dl>
                    {client.logoUrl ? <p className="mt-3 inline-flex items-center gap-1 text-xs font-bold text-emerald-700"><ShieldCheck className="h-3.5 w-3.5" aria-hidden="true" />{ct('officialLogoUploaded')}</p> : <p className="mt-3 text-xs text-slate-400">{ct('identityLogoHint')}</p>}
                    <div className="mt-4 flex flex-wrap gap-2">
                      <label className={`inline-flex cursor-pointer items-center gap-2 rounded-xl border border-dashed border-mis-sky bg-mis-pale/40 px-3 py-2 text-sm font-semibold text-mis-primary ${logoBusy === client.id ? 'pointer-events-none opacity-50' : ''}`}>
                        <ImageUp className="h-4 w-4" aria-hidden="true" />
                        {logoBusy === client.id ? ct('loading') : ct('chooseLogoFile')}
                        <input accept=".png,.jpg,.jpeg,.webp" className="sr-only" onChange={(event) => { const file = event.target.files?.[0]; if (file) void uploadLogo(client, file); event.target.value = ''; }} type="file" />
                      </label>
                      <Button fullWidth={false} leftIcon={<Pencil className="h-4 w-4" aria-hidden="true" />} onClick={() => setEditor({ type: 'client', item: client })} size="sm" variant="ghost">{ct('edit')}</Button>
                      <Button className="text-red-600 hover:bg-red-50 hover:text-red-700" fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" aria-hidden="true" />} onClick={() => setRemoval({ type: 'client', id: client.id, name })} size="sm" variant="ghost">{ct('delete')}</Button>
                    </div>
                  </article>
                );
              })}
            </div>
          )
        ) : tab === 'portfolios' ? (
          clients.length === 0 ? (
            <EmptyState action={<Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" aria-hidden="true" />} onClick={() => { setSearchParams({}, { replace: true }); setEditor({ type: 'client' }); }} size="md">{ct('addClient')}</Button>} description={filteredEmpty ? undefined : ct('addFirstPortfolio')} icon={<Layers3 className="h-6 w-6" aria-hidden="true" />} title={ct('noPortfoliosFound')} />
          ) : (
            <div className="space-y-8 p-4 sm:p-5">
              <p className="text-sm leading-6 text-slate-500">{ct('desksHelp')}</p>
              {clients.map((client) => {
                const name = language === 'ar' ? client.nameArabic : client.nameEnglish;
                return (
                  <section className="rounded-2xl border border-mis-border bg-white" key={client.id}>
                    <header className="flex items-center gap-3 border-b border-mis-border px-4 py-4 sm:px-5">
                      <BankLogo className="h-12 w-12" code={client.code} logoUrl={withLogoRevision(client.logoUrl, logoRevision)} name={name} />
                      <div className="min-w-0 flex-1">
                        <h2 className="truncate font-bold text-mis-navy">{name}</h2>
                        <p className="text-xs font-semibold text-slate-400" data-bidi="ltr">{client.code}</p>
                      </div>
                      <OrgTypeBadge type={client.organizationType} />
                    </header>
                    <div className="grid gap-3 p-4 sm:grid-cols-2 xl:grid-cols-4">
                      {DESK_SLOTS.map((slot) => {
                        const desk = findDesk(data.portfolios, client.id, slot);
                        const Icon = deskIcons[slot.secondary];
                        const key = `${client.id}:${slot.primary}:${slot.secondary}`;
                        const code = `${displayPrimary(slot.primary)} · ${displayPrimary(slot.secondary)}`;
                        return (
                          <article className={`flex flex-col rounded-2xl border p-4 ${desk ? 'border-mis-border bg-white' : 'border-dashed border-slate-200 bg-slate-50/70'}`} key={key}>
                            <div className="flex items-start gap-3">
                              <span className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-mis-pale text-mis-primary"><Icon className="h-5 w-5" aria-hidden="true" /></span>
                              <div className="min-w-0 flex-1">
                                <p className="text-[11px] font-bold uppercase tracking-wide text-mis-primary" data-bidi="ltr">{code}</p>
                                <h3 className="font-bold text-mis-navy">{ct(deskLabelKey(slot.primary))} · {ct(deskLabelKey(slot.secondary))}</h3>
                                <p className="mt-1 text-xs leading-5 text-slate-500">{ct(deskHintKey(slot.primary, slot.secondary))}</p>
                              </div>
                            </div>
                            {desk ? (
                              <dl className="mt-3 space-y-1 text-xs text-slate-600">
                                <div className="flex justify-between gap-2"><dt>{ct('casesOnDesk')}</dt><dd className="tabular-nums">{desk.caseCount ?? 0}</dd></div>
                                <div className="flex justify-between gap-2"><dt>{ct('target')}</dt><dd className="tabular-nums">{desk.targetAmount != null ? `${new Intl.NumberFormat(language === 'ar' ? 'ar-EG' : 'en-EG').format(desk.targetAmount)} ${desk.currencyCode}` : desk.currencyCode}</dd></div>
                                <div className="flex justify-between gap-2"><dt>{ct('ptpPolicy')}</dt><dd>{ptpLabel(desk.ptpGraceDays, desk.ptpToleranceAmount)}</dd></div>
                              </dl>
                            ) : null}
                            <div className="mt-4 flex flex-wrap items-center gap-2">
                              <StatusBadge dot tone={desk?.isActive ? 'success' : 'neutral'}>{desk ? ct(desk.isActive ? 'deskReady' : 'inactive') : ct('deskNotReady')}</StatusBadge>
                              {desk ? (
                                <>
                                  <Button fullWidth={false} leftIcon={<Pencil className="h-4 w-4" aria-hidden="true" />} onClick={() => setEditor({ type: 'portfolio', item: desk })} size="sm" variant="ghost">{ct('edit')}</Button>
                                  <Link className="inline-flex min-h-9 items-center rounded-xl px-3 text-sm font-semibold text-mis-primary hover:bg-mis-pale" to={organizationWorkspaceBase(organizationKind(client.organizationType), client.id, slot)}>{ct('openThisDesk')}</Link>
                                </>
                              ) : (
                                <Button fullWidth={false} isLoading={enablingDesk === key} onClick={() => void enableDesk(client, slot)} size="sm">{ct('enableDesk')}</Button>
                              )}
                            </div>
                          </article>
                        );
                      })}
                    </div>
                    {legacyPortfolios.some((item) => item.organizationId === client.id) ? (
                      <div className="border-t border-amber-200 bg-amber-50/60 px-4 py-4 sm:px-5">
                        <p className="text-sm font-bold text-amber-900">{ct('legacyUnclassified')}</p>
                        <p className="mt-1 text-xs text-amber-800">{ct('legacyUnclassifiedHelp')}</p>
                        <ul className="mt-3 space-y-2">
                          {legacyPortfolios.filter((item) => item.organizationId === client.id).map((item) => (
                            <li className="flex flex-wrap items-center justify-between gap-2 rounded-xl bg-white px-3 py-2 text-sm" key={item.id}>
                              <span className="font-semibold text-mis-navy">{language === 'ar' ? item.nameArabic : item.nameEnglish} <span className="text-xs text-slate-400" data-bidi="ltr">{item.code}</span></span>
                              <div className="flex gap-1">
                                <Button fullWidth={false} leftIcon={<Pencil className="h-4 w-4" aria-hidden="true" />} onClick={() => setEditor({ type: 'portfolio', item })} size="sm" variant="ghost">{ct('edit')}</Button>
                                <Button className="text-red-600 hover:bg-red-50 hover:text-red-700" fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" aria-hidden="true" />} onClick={() => setRemoval({ type: 'portfolio', id: item.id, name: language === 'ar' ? item.nameArabic : item.nameEnglish })} size="sm" variant="ghost">{ct('delete')}</Button>
                              </div>
                            </li>
                          ))}
                        </ul>
                      </div>
                    ) : null}
                  </section>
                );
              })}
            </div>
          )
        ) : buckets.length === 0 ? (
          <EmptyState action={<Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" aria-hidden="true" />} onClick={onAdd} size="md">{ct('addBucket')}</Button>} description={filteredEmpty ? undefined : ct('addFirstBucket')} icon={<Timer className="h-6 w-6" aria-hidden="true" />} title={ct('noBucketsFound')} />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[800px] text-start">
              <thead className="bg-mis-surface text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-5 py-4 text-start">{ct('code')}</th>
                  <th className="px-5 py-4 text-start">{ct('selectClient')}</th>
                  <th className="px-5 py-4 text-start">{ct('selectPortfolio')}</th>
                  <th className="px-5 py-4 text-start">{language === 'ar' ? ct('nameArabic') : ct('nameEnglish')}</th>
                  <th className="px-5 py-4 text-start">{ct('dpdBucket')}</th>
                  <th className="px-5 py-4 text-start">{ct('status')}</th>
                  <th className="px-5 py-4 text-end">{ct('actions')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {buckets.map((item) => {
                  const portfolio = data.portfolios.find((row) => row.id === item.portfolioId);
                  return (
                    <tr className="hover:bg-slate-50/70" key={item.id}>
                      <td className="px-5 py-4 text-sm font-semibold text-mis-navy" data-bidi="ltr">{item.code}</td>
                      <td className="px-5 py-4 text-sm text-slate-700">{organizationName(item.organizationId)}</td>
                      <td className="px-5 py-4 text-sm text-slate-600">{portfolio ? deskName(portfolio.primaryClassification, portfolio.subClassification) : ct('allPortfolios')}</td>
                      <td className="px-5 py-4 text-sm text-slate-700">{language === 'ar' ? item.nameArabic : item.nameEnglish}</td>
                      <td className="px-5 py-4 text-sm tabular-nums text-slate-600">{item.minimumDays ?? 0}–{item.maximumDays ?? '∞'}</td>
                      <td className="px-5 py-4"><StatusBadge dot tone={item.isActive ? 'success' : 'neutral'}>{ct(item.isActive ? 'active' : 'inactive')}</StatusBadge></td>
                      <td className="px-5 py-4">
                        <div className="flex justify-end gap-1">
                          <Button fullWidth={false} leftIcon={<Pencil className="h-4 w-4" aria-hidden="true" />} onClick={() => setEditor({ type: 'bucket', item })} size="sm" variant="ghost">{ct('edit')}</Button>
                          <Button className="text-red-600 hover:bg-red-50 hover:text-red-700" fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" aria-hidden="true" />} onClick={() => setRemoval({ type: 'bucket', id: item.id, name: language === 'ar' ? item.nameArabic : item.nameEnglish })} size="sm" variant="ghost">{ct('delete')}</Button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {editor ? (
        <SettingsEditor
          data={data}
          editor={editor.type === 'client' && editor.item ? { type: 'client', item: data.clients.find((client) => client.id === editor.item?.id) ?? editor.item } : editor}
          language={language}
          logoRevision={logoRevision}
          onClose={() => setEditor(undefined)}
          onSaved={async (notice) => {
            setEditor(undefined);
            toast.success(notice);
            setData(await collectionsService.configuration());
          }}
          onUploadLogo={editor.type === 'client' && editor.item ? (file) => void uploadLogo(editor.item as ClientConfiguration, file) : undefined}
          uploadingLogo={editor.type === 'client' ? logoBusy === editor.item?.id : false}
        />
      ) : null}

      <ConfirmDialog
        cancelLabel={ct('cancel')}
        confirmLabel={ct('delete')}
        description={removal?.name}
        isConfirming={deleting}
        message={removal?.type === 'client' ? ct('deleteClientConfirm') : removal?.type === 'portfolio' ? ct('deletePortfolioConfirm') : ct('deleteBucketConfirm')}
        onCancel={() => setRemoval(undefined)}
        onConfirm={() => void confirmDelete()}
        open={Boolean(removal)}
        title={removal?.type === 'client' ? ct('deleteClient') : removal?.type === 'portfolio' ? ct('deletePortfolio') : ct('deleteBucket')}
      />
    </div>
  );
}

function SettingsEditor({ data, editor, language, logoRevision, onClose, onSaved, onUploadLogo, uploadingLogo }: {
  data: CollectionsConfiguration;
  editor: Editor;
  language: string;
  logoRevision: number;
  onClose: () => void;
  onSaved: (notice: string) => Promise<void>;
  onUploadLogo?: (file: File) => void;
  uploadingLogo: boolean;
}) {
  const { ct } = useCollectionsLocalization();
  const toast = useToast();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [code, setCode] = useState(editor.item?.code ?? '');
  const [nameArabic, setNameArabic] = useState(editor.item?.nameArabic ?? '');
  const [nameEnglish, setNameEnglish] = useState(editor.item?.nameEnglish ?? '');
  const [isActive, setIsActive] = useState(editor.item?.isActive ?? true);
  const [organizationType, setOrganizationType] = useState(editor.type === 'client' ? editor.item?.organizationType ?? 'BANK' : 'BANK');
  const [contactEmail, setContactEmail] = useState(editor.type === 'client' ? editor.item?.contactEmail ?? '' : '');
  const [contactPhone, setContactPhone] = useState(editor.type === 'client' ? editor.item?.contactPhone ?? '' : '');
  const [organizationId, setOrganizationId] = useState(editor.type !== 'client' ? editor.item?.organizationId ?? '' : '');
  const [portfolioId, setPortfolioId] = useState(editor.type === 'bucket' ? editor.item?.portfolioId ?? '' : '');
  const [currencyCode, setCurrencyCode] = useState(editor.type === 'portfolio' ? editor.item?.currencyCode ?? 'EGP' : 'EGP');
  const [targetAmount, setTargetAmount] = useState(editor.type === 'portfolio' && editor.item?.targetAmount != null ? String(editor.item.targetAmount) : '');
  const [primaryClassification] = useState(editor.type === 'portfolio' ? editor.item?.primaryClassification ?? '' : '');
  const [subClassification] = useState(editor.type === 'portfolio' ? editor.item?.subClassification ?? '' : '');
  const [ptpGraceDays, setPtpGraceDays] = useState(editor.type !== 'bucket' && editor.item && 'ptpGraceDays' in editor.item && editor.item.ptpGraceDays != null ? String(editor.item.ptpGraceDays) : '');
  const [ptpToleranceAmount, setPtpToleranceAmount] = useState(editor.type !== 'bucket' && editor.item && 'ptpToleranceAmount' in editor.item && editor.item.ptpToleranceAmount != null ? String(editor.item.ptpToleranceAmount) : '');
  const [minimumDays, setMinimumDays] = useState(editor.type === 'bucket' ? String(editor.item?.minimumDays ?? '') : '');
  const [maximumDays, setMaximumDays] = useState(editor.type === 'bucket' ? String(editor.item?.maximumDays ?? '') : '');
  const [sortOrder, setSortOrder] = useState(editor.type === 'bucket' ? String(editor.item?.sortOrder ?? 0) : '0');
  const formId = 'collections-settings-form';
  const locked = Boolean(editor.item);

  const title = editor.type === 'client' ? (editor.item ? ct('editClient') : ct('addClient')) : editor.type === 'portfolio' ? (editor.item ? ct('editPortfolio') : ct('addPortfolio')) : (editor.item ? ct('editBucket') : ct('addBucket'));

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    setSaving(true);
    try {
      if (editor.type === 'client') {
        const request: SaveClientConfiguration = {
          code, nameArabic, nameEnglish, organizationType,
          contactEmail: contactEmail.trim() || undefined,
          contactPhone: contactPhone.trim() || undefined,
          ptpGraceDays: optionalNumber(ptpGraceDays),
          ptpToleranceAmount: optionalNumber(ptpToleranceAmount),
          isActive,
        };
        await collectionsService.saveClient(editor.item?.id, request);
        await onSaved(ct('clientCreated'));
      } else if (editor.type === 'portfolio') {
        if (!editor.item?.id && (!primaryClassification || !subClassification)) {
          setError(ct('classificationRequired'));
          setSaving(false);
          return;
        }
        const request: SavePortfolioConfiguration = {
          organizationId, code, nameArabic, nameEnglish, currencyCode,
          targetAmount: optionalNumber(targetAmount),
          primaryClassification, subClassification,
          ptpGraceDays: optionalNumber(ptpGraceDays),
          ptpToleranceAmount: optionalNumber(ptpToleranceAmount),
          isActive,
        };
        await collectionsService.savePortfolio(editor.item?.id, request);
        await onSaved(ct('portfolioCreated'));
      } else {
        const request: SaveBucketConfiguration = {
          organizationId,
          portfolioId: portfolioId || undefined,
          code, nameArabic, nameEnglish,
          minimumDays: optionalNumber(minimumDays),
          maximumDays: optionalNumber(maximumDays),
          sortOrder: optionalNumber(sortOrder) ?? 0,
          isActive,
        };
        await collectionsService.saveBucket(editor.item?.id, request);
        await onSaved(ct('bucketCreated'));
      }
    } catch (requestError) {
      const message = getApiErrorMessage(requestError, ct('saveError'));
      setError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  }

  const scopedPortfolios = data.portfolios.filter((item) => !organizationId || item.organizationId === organizationId);
  const clientItem = editor.type === 'client' ? editor.item : undefined;

  return (
    <Modal
      closeLabel={ct('cancel')}
      closeOnBackdrop={!saving}
      closeOnEscape={!saving}
      description={editor.type === 'client' ? ct('identityHelp') : editor.type === 'portfolio' ? ct('deskLockedHint') : ct('addFirstBucket')}
      footer={(
        <>
          <Button disabled={saving} fullWidth={false} onClick={onClose} size="md" type="button" variant="outline">{ct('cancel')}</Button>
          <Button form={formId} fullWidth={false} isLoading={saving} size="md" type="submit">{ct('submit')}</Button>
        </>
      )}
      hideCloseButton={saving}
      onClose={onClose}
      open
      size="lg"
      title={title}
    >
      <form className="grid gap-5 sm:grid-cols-2" id={formId} noValidate onSubmit={(event) => void submit(event)}>
        {error ? <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 sm:col-span-2" role="alert">{error}</div> : null}

        {clientItem ? (
          <div className="flex items-center gap-4 rounded-2xl border border-mis-border bg-mis-pale/30 p-4 sm:col-span-2">
            <BankLogo className="h-16 w-16" code={clientItem.code} logoUrl={withLogoRevision(clientItem.logoUrl, logoRevision)} name={language === 'ar' ? clientItem.nameArabic : clientItem.nameEnglish} />
            <div className="min-w-0 flex-1">
              <p className="text-sm font-semibold text-mis-navy">{ct('officialLogo')}</p>
              <p className="mt-1 text-xs text-slate-500">{ct('identityLogoHint')}</p>
            </div>
            {onUploadLogo ? (
              <label className={`inline-flex cursor-pointer items-center gap-2 rounded-xl bg-white px-3 py-2 text-sm font-semibold text-mis-primary shadow-sm ${uploadingLogo ? 'pointer-events-none opacity-50' : ''}`}>
                <ImageUp className="h-4 w-4" aria-hidden="true" />
                {uploadingLogo ? ct('loading') : ct('chooseLogoFile')}
                <input accept=".png,.jpg,.jpeg,.webp" className="sr-only" onChange={(event) => { const file = event.target.files?.[0]; if (file) onUploadLogo(file); event.target.value = ''; }} type="file" />
              </label>
            ) : null}
          </div>
        ) : null}

        {editor.type !== 'client' ? (
          <SelectInput disabled={locked} label={ct('selectClient')} name="organizationId" onChange={(event) => { setOrganizationId(event.target.value); setPortfolioId(''); }} required value={organizationId}>
            <option value="">—</option>
            {data.clients.map((item) => <option key={item.id} value={item.id}>{language === 'ar' ? item.nameArabic : item.nameEnglish} ({item.code})</option>)}
          </SelectInput>
        ) : null}

        {editor.type === 'bucket' ? (
          <SelectInput disabled={locked} label={ct('selectPortfolio')} name="portfolioId" onChange={(event) => setPortfolioId(event.target.value)} value={portfolioId}>
            <option value="">{ct('allPortfolios')}</option>
            {scopedPortfolios.map((item) => <option key={item.id} value={item.id}>{item.primaryClassification && item.subClassification ? `${ct(deskLabelKey(item.primaryClassification))} · ${ct(deskLabelKey(item.subClassification))}` : (language === 'ar' ? item.nameArabic : item.nameEnglish)}</option>)}
          </SelectInput>
        ) : null}

        {editor.type !== 'portfolio' ? <TextInput disabled={locked} label={ct('code')} maxLength={40} name="code" onChange={(event) => setCode(event.target.value)} required value={code} /> : <TextInput disabled label={ct('code')} name="code" value={code} />}
        <TextInput dir="rtl" label={ct('nameArabic')} maxLength={200} name="nameArabic" onChange={(event) => setNameArabic(event.target.value)} required value={nameArabic} />
        <TextInput dir="ltr" label={ct('nameEnglish')} maxLength={200} name="nameEnglish" onChange={(event) => setNameEnglish(event.target.value)} required value={nameEnglish} />

        {editor.type === 'client' ? (
          <>
            <SelectInput label={ct('organizationType')} name="organizationType" onChange={(event) => setOrganizationType(event.target.value)} required value={organizationType}>
              {organizationTypes.map((type) => <option key={type} value={type}>{ct(type)}</option>)}
            </SelectInput>
            <TextInput label={ct('email')} name="contactEmail" onChange={(event) => setContactEmail(event.target.value)} type="email" value={contactEmail} />
            <TextInput label={ct('phone')} name="contactPhone" onChange={(event) => setContactPhone(event.target.value)} type="tel" value={contactPhone} />
          </>
        ) : null}

        {editor.type === 'portfolio' ? (
          <>
            <div className="rounded-2xl border border-mis-border bg-mis-pale/40 px-4 py-3 sm:col-span-2">
              <p className="text-xs font-bold uppercase tracking-wide text-mis-primary" data-bidi="ltr">{displayPrimary(primaryClassification || '—')} · {displayPrimary(subClassification || '—')}</p>
              <p className="mt-1 font-bold text-mis-navy">{primaryClassification && subClassification ? `${ct(deskLabelKey(primaryClassification))} · ${ct(deskLabelKey(subClassification))}` : ct('unclassified')}</p>
            </div>
            <TextInput label={ct('currencyCode')} maxLength={3} minLength={3} name="currencyCode" onChange={(event) => setCurrencyCode(event.target.value.toUpperCase())} required value={currencyCode} />
            <TextInput label={ct('target')} min={0} name="targetAmount" onChange={(event) => setTargetAmount(event.target.value)} step="0.01" type="number" value={targetAmount} />
          </>
        ) : null}

        {editor.type === 'bucket' ? (
          <>
            <TextInput label={ct('minimumDays')} min={0} name="minimumDays" onChange={(event) => setMinimumDays(event.target.value)} type="number" value={minimumDays} />
            <TextInput label={ct('maximumDays')} min={0} name="maximumDays" onChange={(event) => setMaximumDays(event.target.value)} type="number" value={maximumDays} />
            <TextInput label={ct('sortOrder')} name="sortOrder" onChange={(event) => setSortOrder(event.target.value)} required type="number" value={sortOrder} />
          </>
        ) : null}

        {editor.type !== 'bucket' ? (
          <div className="grid gap-5 rounded-2xl border border-mis-border bg-slate-50/70 p-4 sm:col-span-2 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <p className="text-sm font-bold text-mis-navy">{ct('ptpSection')}</p>
              <p className="mt-1 text-xs leading-5 text-slate-500">{ct('ptpPolicyHelp')}</p>
            </div>
            <TextInput hint={editor.type === 'portfolio' ? ct('inheritFromClient') : undefined} label={ct('ptpGraceDays')} max={30} min={0} name="ptpGraceDays" onChange={(event) => setPtpGraceDays(event.target.value)} type="number" value={ptpGraceDays} />
            <TextInput label={ct('ptpToleranceAmount')} min={0} name="ptpToleranceAmount" onChange={(event) => setPtpToleranceAmount(event.target.value)} step="0.01" type="number" value={ptpToleranceAmount} />
          </div>
        ) : null}

        <div className="sm:col-span-2">
          <Checkbox checked={isActive} label={ct('active')} name="isActive" onChange={(event) => setIsActive(event.target.checked)} />
        </div>
      </form>
    </Modal>
  );
}
