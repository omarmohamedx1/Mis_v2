import { useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { SelectInput } from '../../components/forms/SelectInput';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { TextInput } from '../../components/forms/TextInput';
import { useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryClientListItem, DataEntryOrganization, DataEntryPortfolio } from '../../features/data-entry/types/dataEntry';
import { getApiErrorMessage } from '../../services/apiClient';

export function DataEntryClientsPage() {
  const d = useDataEntryText();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<DataEntryClientListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [addOpen, setAddOpen] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => {
      setQuery(search.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);
    dataEntryService
      .clients(query || undefined, page, 20)
      .then((result) => {
        if (cancelled) return;
        setItems(result.items);
        setTotalCount(result.totalCount);
      })
      .catch(() => {
        if (!cancelled) setError(true);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [query, page]);

  const totalPages = Math.max(1, Math.ceil(totalCount / 20));

  return (
    <div className="space-y-5">
      <PageHeader
        title={d.text('العملاء', 'Clients')}
        actions={
          <>
            <Button fullWidth={false} size="md" type="button" onClick={() => setAddOpen(true)}>
              {d.text('+ إضافة عميل', '+ Add Client')}
            </Button>
            <Link
              className="inline-flex h-10 items-center justify-center rounded-xl border border-slate-300 bg-white px-4 text-sm font-semibold text-slate-800 shadow-sm hover:border-mis-primary hover:bg-mis-pale/40"
              to="/data-entry/import"
            >
              {d.text('رفع ملف العملاء', 'Upload Client File')}
            </Link>
          </>
        }
      />

      <div className="rounded-2xl border border-mis-border bg-white p-4 shadow-sm">
        <input
          className="h-12 w-full rounded-form border border-mis-border bg-white px-4 text-sm text-mis-ink placeholder:text-slate-400 focus:border-mis-blue focus:outline-none"
          placeholder={d.text('ابحث بالاسم أو رقم البطاقة أو رقم الموبايل...', 'Search by name, National ID, or mobile number...')}
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
      </div>

      {error ? (
        <ErrorState title={d.text('تعذر تحميل العملاء', 'Could not load clients')} onRetry={() => setPage((value) => value)} />
      ) : loading ? (
        <div className="grid min-h-[280px] place-items-center">
          <LoadingSpinner />
        </div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  <th className="px-4 py-3 text-start">{d.text('رقم العميل', 'Customer Number')}</th>
                  <th className="px-4 py-3 text-start">{d.text('اسم العميل', 'Customer Name')}</th>
                  <th className="px-4 py-3 text-start">{d.text('الموبايل', 'Mobile')}</th>
                  <th className="px-4 py-3 text-start">{d.text('إجراءات', 'Actions')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {items.length === 0 ? (
                  <tr>
                    <td className="px-4 py-8 text-center text-slate-500" colSpan={4}>
                      {d.text('لا توجد نتائج', 'No results')}
                    </td>
                  </tr>
                ) : (
                  items.map((item) => (
                    <tr
                      key={item.id}
                      className="cursor-pointer hover:bg-slate-50"
                      onClick={() => navigate(`/data-entry/clients/${item.id}`)}
                    >
                      <td className="px-4 py-3 font-semibold text-mis-navy" data-bidi="ltr">
                        {item.customerNumber}
                      </td>
                      <td className="px-4 py-3">{item.customerName}</td>
                      <td className="px-4 py-3" data-bidi="ltr">
                        {item.mobileNumber || '—'}
                      </td>
                      <td className="px-4 py-3">
                        <button
                          className="text-sm font-bold text-mis-primary hover:underline"
                          type="button"
                          onClick={(event) => {
                            event.stopPropagation();
                            navigate(`/data-entry/clients/${item.id}`);
                          }}
                        >
                          {d.text('عرض', 'View')}
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          {totalCount > 20 ? (
            <div className="flex items-center justify-between border-t border-mis-border px-4 py-3 text-sm text-slate-500">
              <span>
                {d.text(`إجمالي ${totalCount}`, `Total ${totalCount}`)}
              </span>
              <div className="flex gap-2">
                <Button disabled={page <= 1} fullWidth={false} size="sm" type="button" variant="outline" onClick={() => setPage((value) => value - 1)}>
                  {d.text('السابق', 'Previous')}
                </Button>
                <span className="grid place-items-center px-2">
                  {page} / {totalPages}
                </span>
                <Button disabled={page >= totalPages} fullWidth={false} size="sm" type="button" variant="outline" onClick={() => setPage((value) => value + 1)}>
                  {d.text('التالي', 'Next')}
                </Button>
              </div>
            </div>
          ) : null}
        </div>
      )}

      {addOpen ? (
        <AddClientModal
          onClose={() => setAddOpen(false)}
          onCreated={(id) => {
            setAddOpen(false);
            navigate(`/data-entry/clients/${id}`);
          }}
        />
      ) : null}
    </div>
  );
}

function AddClientModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const d = useDataEntryText();
  const [organizations, setOrganizations] = useState<DataEntryOrganization[]>([]);
  const [portfolios, setPortfolios] = useState<DataEntryPortfolio[]>([]);
  const [organizationId, setOrganizationId] = useState('');
  const [portfolioId, setPortfolioId] = useState('');
  const [primaryClassification, setPrimaryClassification] = useState('');
  const [subClassification, setSubClassification] = useState('');
  const [customerName, setCustomerName] = useState('');
  const [nationalId, setNationalId] = useState('');
  const [mobileNumber, setMobileNumber] = useState('');
  const [address, setAddress] = useState('');
  const [feedback, setFeedback] = useState('');
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    dataEntryService.organizations().then((items) => {
      setOrganizations(items);
      if (items[0]) setOrganizationId(items[0].id);
    }).catch(() => setOrganizations([]));
  }, []);

  useEffect(() => {
    if (!organizationId) {
      setPortfolios([]);
      setPortfolioId('');
      return;
    }
    dataEntryService.portfolios(organizationId).then((items) => {
      setPortfolios(items);
      setPortfolioId(items[0]?.id ?? '');
      setPrimaryClassification(items[0]?.primaryClassification ?? '');
      setSubClassification(items[0]?.subClassification ?? '');
    }).catch(() => {
      setPortfolios([]);
      setPortfolioId('');
    });
  }, [organizationId]);

  useEffect(() => {
    const selected = portfolios.find((item) => item.id === portfolioId);
    if (selected) {
      setPrimaryClassification(selected.primaryClassification ?? '');
      setSubClassification(selected.subClassification ?? '');
    }
  }, [portfolioId, portfolios]);

  const needsClassification = portfolios.length === 0;

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!organizationId || !customerName.trim() || saving) return;
    setSaving(true);
    setError('');
    try {
      const created = await dataEntryService.createClient({
        organizationId,
        portfolioId: portfolioId || null,
        primaryClassification: needsClassification ? primaryClassification || null : null,
        subClassification: needsClassification ? subClassification || null : null,
        customerName: customerName.trim(),
        nationalId: nationalId.trim() || null,
        mobileNumber: mobileNumber.trim() || null,
        address: address.trim() || null,
        feedback: feedback.trim() || null,
        notes: notes.trim() || null,
      });
      onCreated(created.id);
    } catch (reason) {
      setError(getApiErrorMessage(reason, d.text('تعذر إضافة العميل', 'Could not add client')));
    } finally {
      setSaving(false);
    }
  }

  const orgLabel = (org: DataEntryOrganization) => (d.ar ? org.nameArabic : org.nameEnglish) || org.code;
  const portfolioLabel = (item: DataEntryPortfolio) => (d.ar ? item.nameArabic : item.nameEnglish) || item.code;

  return (
    <Modal
      closeOnBackdrop={!saving}
      closeOnEscape={!saving}
      hideCloseButton={saving}
      onClose={onClose}
      open
      size="lg"
      title={d.text('إضافة عميل', 'Add Client')}
      footer={
        <>
          <Button disabled={saving} fullWidth={false} size="md" type="button" variant="outline" onClick={onClose}>
            {d.text('إلغاء', 'Cancel')}
          </Button>
          <Button form="add-data-entry-client" fullWidth={false} isLoading={saving} size="md" type="submit">
            {d.text('حفظ', 'Save')}
          </Button>
        </>
      }
    >
      <form className="grid gap-4 sm:grid-cols-2" id="add-data-entry-client" onSubmit={(event) => void submit(event)}>
        {error ? <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700 sm:col-span-2">{error}</div> : null}
        <SelectInput
          label={d.text('الجهة', 'Organization')}
          required
          value={organizationId}
          onChange={(event) => setOrganizationId(event.target.value)}
        >
          {organizations.map((org) => (
            <option key={org.id} value={org.id}>
              {orgLabel(org)}
            </option>
          ))}
        </SelectInput>
        {portfolios.length > 0 ? (
          <SelectInput
            label={d.text('المحفظة', 'Portfolio')}
            value={portfolioId}
            onChange={(event) => setPortfolioId(event.target.value)}
          >
            <option value="">—</option>
            {portfolios.map((item) => (
              <option key={item.id} value={item.id}>
                {portfolioLabel(item)}
              </option>
            ))}
          </SelectInput>
        ) : (
          <>
            <TextInput
              label={d.text('التصنيف الرئيسي', 'Primary classification')}
              value={primaryClassification}
              onChange={(event) => setPrimaryClassification(event.target.value)}
            />
            <TextInput
              label={d.text('التصنيف الفرعي', 'Sub classification')}
              value={subClassification}
              onChange={(event) => setSubClassification(event.target.value)}
            />
          </>
        )}
        <TextInput
          containerClassName="sm:col-span-2"
          label={d.text('اسم العميل', 'Customer Name')}
          required
          value={customerName}
          onChange={(event) => setCustomerName(event.target.value)}
        />
        <TextInput
          label={d.text('الرقم القومي', 'National ID')}
          name="nationalId"
          value={nationalId}
          onChange={(event) => setNationalId(event.target.value)}
        />
        <TextInput
          label={d.text('الموبايل', 'Mobile')}
          name="mobileNumber"
          type="tel"
          value={mobileNumber}
          onChange={(event) => setMobileNumber(event.target.value)}
        />
        <TextAreaInput
          containerClassName="sm:col-span-2"
          label={d.text('العنوان', 'Address')}
          rows={2}
          value={address}
          onChange={(event) => setAddress(event.target.value)}
        />
        <TextAreaInput
          containerClassName="sm:col-span-2"
          label={d.text('فيدباك', 'Feedback')}
          rows={2}
          value={feedback}
          onChange={(event) => setFeedback(event.target.value)}
        />
        <TextAreaInput
          containerClassName="sm:col-span-2"
          label={d.text('ملاحظات', 'Notes')}
          rows={2}
          value={notes}
          onChange={(event) => setNotes(event.target.value)}
        />
      </form>
    </Modal>
  );
}
