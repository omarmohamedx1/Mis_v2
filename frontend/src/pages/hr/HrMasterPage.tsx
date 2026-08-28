import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { Database, Pencil, Plus, Power, PowerOff, Search } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { Pagination } from '../../components/common/Pagination';
import { StatusBadge } from '../../components/common/StatusBadge';
import { Tabs } from '../../components/common/Tabs';
import { useToast } from '../../components/common/Toast';
import { Checkbox } from '../../components/forms/Checkbox';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { TextInput } from '../../components/forms/TextInput';
import { useLocalization } from '../../context/LocalizationContext';
import { hrMasterDataService } from '../../features/hr/services/hrMasterDataService';
import { masterDataCategories, type MasterDataCategory, type MasterDataItem, type MasterDataLookup, type PagedMasterData, type SaveMasterDataRequest } from '../../features/hr/types/masterData';
import type { TranslationKey } from '../../localization/translations';
import { getApiErrorMessage } from '../../services/apiClient';

const pageSize = 20;
const emptyPage: PagedMasterData = { items: [], page: 1, pageSize, totalCount: 0, totalPages: 0 };

const categoryLabels: Record<MasterDataCategory, TranslationKey> = {
  departments: 'masterDepartments',
  positions: 'masterPositions',
  branches: 'masterBranches',
  'employment-types': 'masterEmploymentTypes',
  'contract-types': 'masterContractTypes',
  'leave-types': 'masterLeaveTypes',
  'document-types': 'masterDocumentTypes',
  'delegation-types': 'masterDelegationTypes',
};

function emptyRequest(): SaveMasterDataRequest {
  return {
    address: null,
    code: '',
    defaultAnnualEntitlement: null,
    departmentId: null,
    description: null,
    isActive: true,
    nameArabic: null,
    nameEnglish: '',
    requiresAttachment: null,
    requiresExpiryDate: null,
  };
}

function requestFromItem(item: MasterDataItem): SaveMasterDataRequest {
  return {
    address: item.address,
    code: item.code,
    defaultAnnualEntitlement: item.defaultAnnualEntitlement,
    departmentId: item.departmentId,
    description: item.description,
    isActive: item.isActive,
    nameArabic: item.nameArabic,
    nameEnglish: item.nameEnglish,
    requiresAttachment: item.requiresAttachment,
    requiresExpiryDate: item.requiresExpiryDate,
  };
}

function nullable(value: string): string | null {
  const trimmed = value.trim();
  return trimmed || null;
}

interface MasterDataFormProps {
  category: MasterDataCategory;
  departments: MasterDataLookup[];
  item: MasterDataItem | null;
  onClose: () => void;
  onSaved: () => void;
}

function MasterDataForm({ category, departments, item, onClose, onSaved }: MasterDataFormProps) {
  const { language, t } = useLocalization();
  const toast = useToast();
  const [form, setForm] = useState<SaveMasterDataRequest>(() => item ? requestFromItem(item) : emptyRequest());
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const formId = 'master-data-form';

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');

    if (!form.code.trim() || form.nameEnglish.trim().length < 2) {
      setError(t('masterRequiredFields'));
      return;
    }
    if (category === 'leave-types' && (form.defaultAnnualEntitlement === null || form.defaultAnnualEntitlement < 0 || form.defaultAnnualEntitlement > 366)) {
      setError(t('masterEntitlementValidation'));
      return;
    }

    const request: SaveMasterDataRequest = {
      ...form,
      address: nullable(form.address ?? ''),
      code: form.code.trim(),
      defaultAnnualEntitlement: category === 'leave-types' ? form.defaultAnnualEntitlement : null,
      departmentId: category === 'positions' ? form.departmentId : null,
      description: nullable(form.description ?? ''),
      nameArabic: nullable(form.nameArabic ?? ''),
      nameEnglish: form.nameEnglish.trim(),
      requiresAttachment: category === 'leave-types' ? Boolean(form.requiresAttachment) : null,
      requiresExpiryDate: category === 'document-types' ? Boolean(form.requiresExpiryDate) : null,
    };

    setSaving(true);
    try {
      if (item) await hrMasterDataService.update(category, item.id, request);
      else await hrMasterDataService.create(category, request);
      toast.success(t(item ? 'masterUpdatedSuccess' : 'masterCreatedSuccess'));
      onSaved();
    } catch (requestError) {
      const message = getApiErrorMessage(requestError, t('saveMasterDataError'));
      setError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      closeLabel={t('close')}
      closeOnBackdrop={!saving}
      closeOnEscape={!saving}
      footer={(
        <>
          <Button disabled={saving} fullWidth={false} onClick={onClose} size="md" type="button" variant="outline">{t('cancel')}</Button>
          <Button form={formId} fullWidth={false} isLoading={saving} size="md" type="submit">{saving ? t('saving') : t('saveChanges')}</Button>
        </>
      )}
      hideCloseButton={saving}
      onClose={onClose}
      open
      size="lg"
      title={t(item ? 'editMasterRecord' : 'addMasterRecord')}
    >
      <form className="grid gap-5 sm:grid-cols-2" id={formId} noValidate onSubmit={submit}>
        {error ? <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 sm:col-span-2" role="alert">{error}</div> : null}
        <TextInput label={t('code')} maxLength={32} name="code" onChange={(event) => setForm((current) => ({ ...current, code: event.target.value }))} required value={form.code} />
        <TextInput label={t('nameEnglish')} maxLength={120} name="nameEnglish" onChange={(event) => setForm((current) => ({ ...current, nameEnglish: event.target.value }))} required value={form.nameEnglish} />
        <TextInput label={t('nameArabic')} maxLength={120} name="nameArabic" onChange={(event) => setForm((current) => ({ ...current, nameArabic: event.target.value }))} value={form.nameArabic ?? ''} />
        {category === 'positions' ? (
          <SelectInput label={t('department')} name="departmentId" onChange={(event) => setForm((current) => ({ ...current, departmentId: event.target.value || null }))} value={form.departmentId ?? ''}>
            <option value="">{t('noDepartment')}</option>
            {departments.map((department) => <option disabled={!department.isActive && department.id !== form.departmentId} key={department.id} value={department.id}>{language === 'ar' && department.nameArabic ? department.nameArabic : department.nameEnglish} ({department.code}){department.isActive ? '' : ` — ${t('inactive')}`}</option>)}
          </SelectInput>
        ) : null}
        {category === 'branches' ? (
          <div className="sm:col-span-2"><TextInput label={t('address')} maxLength={500} name="address" onChange={(event) => setForm((current) => ({ ...current, address: event.target.value }))} value={form.address ?? ''} /></div>
        ) : null}
        {category === 'leave-types' ? (
          <TextInput label={t('defaultAnnualEntitlement')} max={366} min={0} name="defaultAnnualEntitlement" onChange={(event) => setForm((current) => ({ ...current, defaultAnnualEntitlement: event.target.value === '' ? null : Number(event.target.value) }))} required step="0.5" type="number" value={form.defaultAnnualEntitlement ?? ''} />
        ) : null}
        <TextAreaInput className="min-h-24" containerClassName="sm:col-span-2" label={t('description')} maxLength={500} name="description" onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} value={form.description ?? ''} />
        <div className="space-y-3 sm:col-span-2">
          {category === 'leave-types' ? <Checkbox checked={Boolean(form.requiresAttachment)} label={t('requiresAttachment')} name="requiresAttachment" onChange={(event) => setForm((current) => ({ ...current, requiresAttachment: event.target.checked }))} /> : null}
          {category === 'document-types' ? <Checkbox checked={Boolean(form.requiresExpiryDate)} label={t('requiresExpiryDate')} name="requiresExpiryDate" onChange={(event) => setForm((current) => ({ ...current, requiresExpiryDate: event.target.checked }))} /> : null}
          <Checkbox checked={form.isActive} label={t('activeRecord')} name="isActive" onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))} />
        </div>
      </form>
    </Modal>
  );
}

export function HrMasterPage() {
  const { language, t } = useLocalization();
  const toast = useToast();
  const [categories, setCategories] = useState<MasterDataCategory[]>(masterDataCategories.filter((item) => item !== 'branches'));
  const [category, setCategory] = useState<MasterDataCategory>('departments');
  const [data, setData] = useState<PagedMasterData>(emptyPage);
  const [departments, setDepartments] = useState<MasterDataLookup[]>([]);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [activeFilter, setActiveFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [formItem, setFormItem] = useState<MasterDataItem | null | undefined>(undefined);
  const [openingEditId, setOpeningEditId] = useState('');
  const [activationTarget, setActivationTarget] = useState<MasterDataItem | null>(null);
  const [changingActive, setChangingActive] = useState(false);

  useEffect(() => {
    hrMasterDataService.getCategories().then((result) => {
      const visibleCategories = result.filter((item) => item !== 'branches');
      if (visibleCategories.length) setCategories(visibleCategories);
    }).catch(() => undefined);
    hrMasterDataService.getLookup('departments', true).then(setDepartments).catch(() => undefined);
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 350);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const isActive = activeFilter === 'all' ? null : activeFilter === 'active';
      setData(await hrMasterDataService.getPaged({ category, isActive, page, pageSize, search }));
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, t('loadMasterDataError')));
    } finally {
      setLoading(false);
    }
  }, [activeFilter, category, page, search, t]);

  useEffect(() => { void load(); }, [load]);

  const tabs = useMemo(() => categories.map((item) => ({ id: item, label: t(categoryLabels[item]) })), [categories, t]);
  const displayName = (item: MasterDataItem) => language === 'ar' && item.nameArabic ? item.nameArabic : item.nameEnglish;

  function selectCategory(value: string) {
    const next = categories.find((item) => item === value);
    if (!next) return;
    setCategory(next);
    setPage(1);
    setSearchInput('');
    setSearch('');
    setActiveFilter('all');
  }

  async function editItem(item: MasterDataItem) {
    setOpeningEditId(item.id);
    try {
      setFormItem(await hrMasterDataService.getById(category, item.id));
    } catch (requestError) {
      toast.error(getApiErrorMessage(requestError, t('loadMasterRecordError')));
    } finally {
      setOpeningEditId('');
    }
  }

  async function changeActive() {
    if (!activationTarget) return;
    setChangingActive(true);
    try {
      await hrMasterDataService.setActive(category, activationTarget.id, !activationTarget.isActive);
      toast.success(t(activationTarget.isActive ? 'masterDeactivatedSuccess' : 'masterActivatedSuccess'));
      setActivationTarget(null);
      await load();
    } catch (requestError) {
      toast.error(getApiErrorMessage(requestError, t('changeMasterStatusError')));
    } finally {
      setChangingActive(false);
    }
  }

  const hasExtraColumn = category === 'positions' || category === 'branches' || category === 'leave-types' || category === 'document-types';

  return (
    <div className="mx-auto max-w-7xl">
      <PageHeader
        actions={<Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" aria-hidden="true" />} onClick={() => setFormItem(null)} size="md">{t('addMasterRecord')}</Button>}
        description={t('masterSubtitle')}
        eyebrow={t('hrDepartment')}
        title={t('master')}
      />

      <section className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <Tabs ariaLabel={t('masterCategories')} items={tabs} onChange={selectCategory} value={category} />
        <div className="grid gap-3 border-b border-mis-border p-4 md:grid-cols-[minmax(240px,1fr)_200px]">
          <label className="relative">
            <span className="sr-only">{t('searchMasterData')}</span>
            <Search className="absolute start-3 top-3 h-5 w-5 text-slate-400" aria-hidden="true" />
            <input className="h-11 w-full rounded-xl border border-mis-border pe-3 ps-10 text-sm outline-none focus:border-mis-blue focus:shadow-input" onChange={(event) => setSearchInput(event.target.value)} placeholder={t('searchMasterData')} value={searchInput} />
          </label>
          <ProfessionalSelect aria-label={t('status')} className="h-11 rounded-xl border border-mis-border bg-white px-3 text-sm text-slate-700 outline-none focus:border-mis-blue" onChange={(event) => { setActiveFilter(event.target.value as typeof activeFilter); setPage(1); }} value={activeFilter}>
            <option value="all">{t('allStatuses')}</option>
            <option value="active">{t('active')}</option>
            <option value="inactive">{t('inactive')}</option>
          </ProfessionalSelect>
        </div>

        {loading ? <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div> : error ? (
          <ErrorState message={error} onRetry={() => void load()} title={t('masterDataUnavailable')} />
        ) : data.items.length === 0 ? (
          <EmptyState
            action={<Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" aria-hidden="true" />} onClick={() => setFormItem(null)} size="md">{t('addMasterRecord')}</Button>}
            description={search || activeFilter !== 'all' ? t('adjustFilters') : t('addFirstMasterRecord')}
            icon={<Database className="h-6 w-6" aria-hidden="true" />}
            title={t('noMasterDataFound')}
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[880px] text-start">
                <thead className="bg-mis-surface text-xs uppercase tracking-wide text-slate-500">
                  <tr>
                    <th className="px-5 py-4 text-start">{t('code')}</th>
                    <th className="px-5 py-4 text-start">{t('nameEnglish')}</th>
                    <th className="px-5 py-4 text-start">{t('nameArabic')}</th>
                    {hasExtraColumn ? <th className="px-5 py-4 text-start">{category === 'positions' ? t('department') : category === 'branches' ? t('address') : category === 'leave-types' ? t('defaultAnnualEntitlement') : t('requiresExpiryDate')}</th> : null}
                    <th className="px-5 py-4 text-start">{t('status')}</th>
                    <th className="px-5 py-4 text-end">{t('actions')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-mis-border">
                  {data.items.map((item) => (
                    <tr className="hover:bg-slate-50/70" key={item.id}>
                      <td className="px-5 py-4 text-sm font-semibold text-mis-navy">{item.code}</td>
                      <td className="px-5 py-4 text-sm text-slate-700">{item.nameEnglish}</td>
                      <td className="px-5 py-4 text-sm text-slate-600">{item.nameArabic || '—'}</td>
                      {hasExtraColumn ? <td className="px-5 py-4 text-sm text-slate-600">{category === 'positions' ? item.departmentName || t('notAssigned') : category === 'branches' ? item.address || '—' : category === 'leave-types' ? item.defaultAnnualEntitlement ?? 0 : item.requiresExpiryDate ? t('yes') : t('no')}</td> : null}
                      <td className="px-5 py-4"><StatusBadge dot tone={item.isActive ? 'success' : 'neutral'}>{t(item.isActive ? 'active' : 'inactive')}</StatusBadge></td>
                      <td className="px-5 py-4">
                        <div className="flex justify-end gap-1">
                          <Button disabled={openingEditId === item.id} fullWidth={false} leftIcon={<Pencil className="h-4 w-4" aria-hidden="true" />} onClick={() => void editItem(item)} size="sm" variant="ghost">{t('edit')}</Button>
                          <Button className={item.isActive ? 'text-red-600 hover:bg-red-50 hover:text-red-700' : ''} fullWidth={false} leftIcon={item.isActive ? <PowerOff className="h-4 w-4" aria-hidden="true" /> : <Power className="h-4 w-4" aria-hidden="true" />} onClick={() => setActivationTarget(item)} size="sm" variant="ghost">{t(item.isActive ? 'deactivate' : 'activate')}</Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination
              labels={{ nextPage: t('nextPage'), pageOf: (current, total) => t('pageOf', { page: current, total }), previousPage: t('previousPage'), showing: (from, to, total) => t('showing', { from, to, total }) }}
              onPageChange={setPage}
              page={data.page}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              totalPages={data.totalPages}
            />
          </>
        )}
      </section>

      {formItem !== undefined ? <MasterDataForm category={category} departments={departments} item={formItem} onClose={() => setFormItem(undefined)} onSaved={() => { setFormItem(undefined); void load(); }} /> : null}
      <ConfirmDialog
        cancelLabel={t('cancel')}
        confirmLabel={activationTarget?.isActive ? t('deactivate') : t('activate')}
        confirmVariant={activationTarget?.isActive ? 'danger' : 'primary'}
        isConfirming={changingActive}
        message={activationTarget ? t(activationTarget.isActive ? 'deactivateMasterConfirm' : 'activateMasterConfirm', { name: displayName(activationTarget) }) : ''}
        onCancel={() => setActivationTarget(null)}
        onConfirm={() => void changeActive()}
        open={Boolean(activationTarget)}
        title={t(activationTarget?.isActive ? 'deactivateRecord' : 'activateRecord')}
      />
    </div>
  );
}
