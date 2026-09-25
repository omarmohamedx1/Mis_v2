import { FileUp, Paperclip, Plus, Search, Trash2, UsersRound } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { useToast } from '../../components/common/Toast';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { TextInput } from '../../components/forms/TextInput';
import { useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryClientListItem, DataEntryPortfolio } from '../../features/data-entry/types/dataEntry';
import { getApiErrorMessage } from '../../services/apiClient';

export function DataEntryClientsPage() {
  const d = useDataEntryText();
  const navigate = useNavigate();
  const toast = useToast();
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [items, setItems] = useState<DataEntryClientListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [addOpen, setAddOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<DataEntryClientListItem | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [filters, setFilters] = useState({ phone: false, address: false, feedback: false, data: false });

  useEffect(() => {
    const timer = setTimeout(() => setQuery(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);
    dataEntryService.clients(query || undefined, {
      hasPhone: filters.phone,
      hasAddress: filters.address,
      hasFeedback: filters.feedback,
      hasData: filters.data,
    })
      .then((result) => {
        if (cancelled) return;
        setItems(result.items);
      })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [filters, query]);

  return (
    <div className="space-y-5">
      <PageHeader
        title={d.text('العملاء', 'Clients')}
        description={d.text('بيانات العملاء كما رُفعت: الرقم القومي، الاسم، كل أرقام التليفون، العنوان، والأعمدة الإضافية.', 'Client data as uploaded: national ID, name, every phone number, address, and any extra columns.')}
        actions={
          <>
            <Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} size="md" type="button" onClick={() => setAddOpen(true)}>
              {d.text('إضافة عميل', 'Add client')}
            </Button>
            <Button fullWidth={false} leftIcon={<FileUp className="h-4 w-4" />} size="md" type="button" variant="outline" onClick={() => navigate('/data-entry/import')}>
              {d.text('رفع العملاء', 'Upload clients')}
            </Button>
            <Button fullWidth={false} leftIcon={<Paperclip className="h-4 w-4" />} size="md" type="button" variant="outline" onClick={() => navigate('/data-entry/files')}>
              {d.text('ملفات العملاء', 'Client files')}
            </Button>
          </>
        }
      />

      <div className="space-y-3 rounded-2xl border border-mis-border bg-white p-4 shadow-sm">
        <div className="relative">
          <Search className="pointer-events-none absolute top-3.5 h-4 w-4 text-slate-400" style={{ insetInlineStart: '0.9rem' }} />
          <input
            className="h-11 w-full rounded-xl border border-mis-border bg-white pe-4 ps-10 text-sm text-mis-ink placeholder:text-slate-400 focus:border-mis-blue focus:outline-none"
            placeholder={d.text('ابحث بالاسم أو الرقم القومي أو أي تليفون أو العنوان', 'Search by name, national ID, any phone, or address')}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>
        <div className="flex flex-wrap gap-2">
          {([
            ['phone', d.text('فيه تليفون', 'Has a phone')],
            ['address', d.text('فيه عنوان', 'Has an address')],
            ['feedback', d.text('فيه فيدباك', 'Has feedback')],
            ['data', d.text('فيه بيانات', 'Has data')],
          ] as const).map(([key, label]) => (
            <button
              className={`rounded-full px-3 py-1 text-xs font-bold ${filters[key] ? 'bg-mis-primary text-white' : 'bg-slate-100 text-slate-600'}`}
              key={key}
              type="button"
              onClick={() => setFilters((current) => ({ ...current, [key]: !current[key] }))}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {error ? (
        <ErrorState title={d.text('تعذر تحميل العملاء', 'Could not load clients')} onRetry={() => setQuery((value) => value)} />
      ) : loading ? (
        <div className="grid min-h-[280px] place-items-center"><LoadingSpinner /></div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="min-w-[72rem] w-full text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  {[d.text('الرقم القومي', 'National ID'), d.text('الاسم', 'Name'), d.text('التليفونات', 'Phones'), d.text('العنوان', 'Address'), d.text('فيدباك', 'Feedback'), d.text('بيانات', 'Data'), ...extraColumns(items)].map((label) => (
                    <th className="px-4 py-3 text-start" key={label}>{label}</th>
                  ))}
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {items.map((item) => (
                  <tr key={item.id} className="cursor-pointer hover:bg-slate-50" onClick={() => navigate(`/data-entry/clients/${item.id}`)}>
                    <td className="px-4 py-3 font-mono text-xs text-mis-navy" data-bidi="ltr">{item.nationalId || fieldValue(item, 'ID') || '—'}</td>
                    <td className="px-4 py-3 font-semibold text-mis-navy">{item.customerName}</td>
                    <td className="px-4 py-3"><PhoneList numbers={clientPhones(item)} /></td>
                    <td className="max-w-xs px-4 py-3 text-slate-700">{item.address || fieldValue(item, 'All Address', 'Address') || '—'}</td>
                    <td className="max-w-xs px-4 py-3 text-slate-700">{item.feedback || fieldValue(item, 'FEEDBACK', 'Feedback') || '—'}</td>
                    <td className="max-w-xs px-4 py-3 text-slate-700">{item.data || fieldValue(item, 'Data', 'Notes') || '—'}</td>
                    {extraColumns(items).map((column) => (
                      <td className="max-w-xs px-4 py-3 text-slate-700" key={column}>{item.fields?.[column] || '—'}</td>
                    ))}
                    <td className="px-4 py-3 text-end">
                      <div className="flex justify-end gap-1">
                        <Button fullWidth={false} size="sm" type="button" variant="ghost" onClick={(event) => { event.stopPropagation(); navigate(`/data-entry/clients/${item.id}`); }}>
                          {d.text('فتح', 'Open')}
                        </Button>
                        {!item.caseId && !item.caseNumber ? (
                          <Button className="text-red-600 hover:bg-red-50 hover:text-red-700" fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} size="sm" type="button" variant="ghost" onClick={(event) => { event.stopPropagation(); setDeleteTarget(item); }}>
                            {d.text('حذف', 'Delete')}
                          </Button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!items.length ? (
            <EmptyState
              compact
              icon={<UsersRound className="h-5 w-5" />}
              title={query ? d.text('لا توجد نتائج', 'No results') : d.text('لا يوجد عملاء بعد', 'No clients yet')}
              description={d.text('ارفع ملف العملاء، أو أضف عميلًا. كل الأعمدة وأرقام التليفون تظهر هنا.', 'Upload the client file, or add a client. Every column and phone number shows up here.')}
              action={<Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} onClick={() => setAddOpen(true)}>{d.text('إضافة عميل', 'Add client')}</Button>}
            />
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
      <ConfirmDialog
        confirmLabel={d.text('حذف', 'Delete')}
        isConfirming={deleting}
        message={d.text(`حذف ${deleteTarget?.customerName ?? ''} من قائمة العملاء؟`, `Remove ${deleteTarget?.customerName ?? ''} from the client list?`)}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => {
          if (!deleteTarget) return;
          setDeleting(true);
          dataEntryService.deleteClient(deleteTarget.id)
            .then(() => {
              toast.success(d.text('تم حذف العميل.', 'Client deleted.'));
              setItems((current) => current.filter((item) => item.id !== deleteTarget.id));
              setDeleteTarget(null);
            })
            .catch((reason) => toast.error(getApiErrorMessage(reason, d.text('تعذر حذف العميل.', 'Could not delete the client.'))))
            .finally(() => setDeleting(false));
        }}
        open={Boolean(deleteTarget)}
        title={d.text('حذف العميل', 'Delete client')}
      />
    </div>
  );
}

const reservedColumns = new Set(['id', 'name', 'tell', 'tel', 'telephone', 'all address', 'address', 'feedback', 'data', 'notes']);

function columnKey(value: string) {
  return value.trim().toLowerCase();
}

function fieldValue(item: DataEntryClientListItem, ...names: string[]) {
  const fields = item.fields ?? {};
  for (const name of names) {
    const match = Object.entries(fields).find(([key]) => columnKey(key) === columnKey(name));
    if (match?.[1]) return match[1];
  }
  return '';
}

function clientPhones(item: DataEntryClientListItem) {
  const listed = (item.phones ?? []).map((phone) => phone.trim()).filter(Boolean);
  if (listed.length) return listed;
  return item.mobileNumber ? [item.mobileNumber] : [];
}

function extraColumns(items: DataEntryClientListItem[]) {
  const names = new Set<string>();
  items.forEach((item) => {
    Object.keys(item.fields ?? {}).forEach((key) => {
      if (!reservedColumns.has(columnKey(key))) names.add(key);
    });
  });
  return [...names];
}

function PhoneList({ numbers }: { numbers: string[] }) {
  if (!numbers.length) return <span className="text-slate-400">—</span>;
  return (
    <div className="flex max-w-xs flex-wrap gap-1">
      {numbers.map((number) => (
        <span className="rounded-full bg-slate-100 px-2 py-0.5 font-mono text-xs text-mis-navy" data-bidi="ltr" key={number}>{number}</span>
      ))}
    </div>
  );
}

function AddClientModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const d = useDataEntryText();
  const [organizationId, setOrganizationId] = useState('');
  const [portfolioId, setPortfolioId] = useState('');
  const [primaryClassification, setPrimaryClassification] = useState('');
  const [subClassification, setSubClassification] = useState('');
  const [customerName, setCustomerName] = useState('');
  const [nationalId, setNationalId] = useState('');
  const [mobileNumber, setMobileNumber] = useState('');
  const [accountNumber, setAccountNumber] = useState('');
  const [contractNumber, setContractNumber] = useState('');
  const [outstandingBalance, setOutstandingBalance] = useState('');
  const [address, setAddress] = useState('');
  const [feedback, setFeedback] = useState('');
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;
    dataEntryService.organizations().then(async (items) => {
      const org = items[0];
      if (!org || cancelled) return;
      setOrganizationId(org.id);
      const books = await dataEntryService.portfolios(org.id).catch(() => [] as DataEntryPortfolio[]);
      const book = books[0];
      if (!book || cancelled) return;
      setPortfolioId(book.id);
      setPrimaryClassification(book.primaryClassification ?? '');
      setSubClassification(book.subClassification ?? '');
    }).catch(() => undefined);
    return () => { cancelled = true; };
  }, []);

  const deskReady = Boolean(organizationId && portfolioId);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!deskReady || !customerName.trim() || saving) return;
    setSaving(true);
    setError('');
    try {
      const created = await dataEntryService.createClient({
        organizationId,
        portfolioId: portfolioId || null,
        primaryClassification: primaryClassification || null,
        subClassification: subClassification || null,
        customerName: customerName.trim(),
        nationalId: nationalId.trim() || null,
        mobileNumber: mobileNumber.trim() || null,
        address: address.trim() || null,
        feedback: feedback.trim() || null,
        notes: notes.trim() || null,
        accountNumber: accountNumber.trim() || null,
        contractNumber: contractNumber.trim() || null,
        outstandingBalance: outstandingBalance ? Number(outstandingBalance) : null,
      });
      onCreated(created.id);
    } catch (reason) {
      setError(getApiErrorMessage(reason, d.text('تعذر إضافة العميل', 'Could not add client')));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      closeOnBackdrop={!saving}
      closeOnEscape={!saving}
      hideCloseButton={saving}
      onClose={onClose}
      open
      size="lg"
      title={d.text('إضافة عميل', 'Add a client')}
      footer={
        <>
          <Button disabled={saving} fullWidth={false} size="md" type="button" variant="outline" onClick={onClose}>
            {d.text('إلغاء', 'Cancel')}
          </Button>
          <Button disabled={saving || !deskReady} form="add-data-entry-client" fullWidth={false} isLoading={saving} size="md" type="submit">
            {d.text('حفظ العميل', 'Save client')}
          </Button>
        </>
      }
    >
      <p className="mb-4 rounded-xl bg-mis-pale px-3 py-2 text-sm text-mis-navy">
        {d.text('اكتب بيانات العميل كما هي في الشيت. الأعمدة الإضافية تظهر مع العميل بعد الحفظ.', 'Enter the client the same way it appears on the sheet. Extra columns show with the client after save.')}
      </p>
      <form className="grid gap-4 sm:grid-cols-2" id="add-data-entry-client" onSubmit={(event) => void submit(event)}>
        {error ? <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700 sm:col-span-2">{error}</div> : null}
        <TextInput containerClassName="sm:col-span-2" label={d.text('اسم العميل', 'Customer name')} required value={customerName} onChange={(event) => setCustomerName(event.target.value)} />
        <TextInput label={d.text('الرقم القومي', 'National ID')} value={nationalId} onChange={(event) => setNationalId(event.target.value)} />
        <TextInput label={d.text('الموبايل', 'Mobile')} type="tel" value={mobileNumber} onChange={(event) => setMobileNumber(event.target.value)} />
        <TextInput label={d.text('رقم الحساب', 'Account number')} required value={accountNumber} onChange={(event) => setAccountNumber(event.target.value)} />
        <TextInput label={d.text('رقم العقد', 'Contract number')} value={contractNumber} onChange={(event) => setContractNumber(event.target.value)} />
        <TextInput containerClassName="sm:col-span-2" label={d.text('المديونية', 'Outstanding')} type="number" value={outstandingBalance} onChange={(event) => setOutstandingBalance(event.target.value)} />
        <TextAreaInput containerClassName="sm:col-span-2" label={d.text('العنوان', 'Address')} rows={2} value={address} onChange={(event) => setAddress(event.target.value)} />
        <TextAreaInput containerClassName="sm:col-span-2" label={d.text('فيدباك', 'Feedback')} rows={2} value={feedback} onChange={(event) => setFeedback(event.target.value)} />
        <TextAreaInput containerClassName="sm:col-span-2" label={d.text('ملاحظات', 'Notes')} rows={2} value={notes} onChange={(event) => setNotes(event.target.value)} />
      </form>
    </Modal>
  );
}
