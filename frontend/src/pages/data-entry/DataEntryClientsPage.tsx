import { FileUp, Paperclip, Pencil, Plus, Search, Trash2, UsersRound, X } from 'lucide-react';
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
import { EditClientSheetModal } from '../../features/data-entry/EditClientSheetModal';
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
  const [column, setColumn] = useState('all');
  const [presence, setPresence] = useState<'any' | 'filled' | 'empty'>('any');
  const [columnSearch, setColumnSearch] = useState('');
  const [columnQuery, setColumnQuery] = useState('');
  const [reloadKey, setReloadKey] = useState(0);
  const [editTarget, setEditTarget] = useState<DataEntryClientListItem | null>(null);
  const [newColumn, setNewColumn] = useState('');
  const [columnBusy, setColumnBusy] = useState(false);
  const [deleteColumn, setDeleteColumn] = useState<string | null>(null);
  const [deleteAllOpen, setDeleteAllOpen] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => {
      setQuery(search.trim());
      setColumnQuery(columnSearch.trim());
    }, 300);
    return () => clearTimeout(timer);
  }, [search, columnSearch]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);
    dataEntryService.clients(query || undefined, {
      column: column === 'all' ? undefined : column,
      value: columnQuery || undefined,
      presence: presence === 'any' ? undefined : presence,
    })
      .then((result) => {
        if (cancelled) return;
        setItems(result.items);
      })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [column, columnQuery, presence, query, reloadKey]);

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
            <Button className="text-red-700" disabled={!items.length || deleting} fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} size="md" type="button" variant="outline" onClick={() => setDeleteAllOpen(true)}>
              {d.text('حذف الكل', 'Delete all')}
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
        <div className="grid gap-3 md:grid-cols-[minmax(0,16rem)_minmax(0,12rem)_minmax(0,1fr)_auto]">
          <label className="block text-sm">
            <span className="mb-1 block font-semibold text-slate-600">{d.text('العمود', 'Column')}</span>
            <select className="h-11 w-full rounded-xl border border-mis-border bg-white px-3 text-sm" value={column} onChange={(event) => setColumn(event.target.value)}>
              {sheetColumns(items, d.text).map((item) => <option key={item.key} value={item.key}>{item.label}</option>)}
            </select>
          </label>
          <label className="block text-sm">
            <span className="mb-1 block font-semibold text-slate-600">{d.text('القيمة', 'Value')}</span>
            <select className="h-11 w-full rounded-xl border border-mis-border bg-white px-3 text-sm" value={presence} onChange={(event) => setPresence(event.target.value as 'any' | 'filled' | 'empty')}>
              <option value="any">{d.text('الكل', 'Any')}</option>
              <option value="filled">{d.text('فيه بيانات', 'Filled')}</option>
              <option value="empty">{d.text('فاضي', 'Empty')}</option>
            </select>
          </label>
          <label className="block text-sm">
            <span className="mb-1 block font-semibold text-slate-600">{d.text('يحتوي على', 'Contains')}</span>
            <input className="h-11 w-full rounded-xl border border-mis-border bg-white px-3 text-sm" value={columnSearch} onChange={(event) => setColumnSearch(event.target.value)} />
          </label>
          <form className="flex items-end gap-2" onSubmit={(event) => {
            event.preventDefault();
            const name = newColumn.trim();
            if (!name || columnBusy) return;
            setColumnBusy(true);
            dataEntryService.addColumn(name)
              .then(() => { setNewColumn(''); setReloadKey((value) => value + 1); toast.success(d.text('تمت إضافة العمود.', 'Column added.')); })
              .catch((reason) => toast.error(getApiErrorMessage(reason, d.text('تعذر إضافة العمود.', 'Could not add the column.'))))
              .finally(() => setColumnBusy(false));
          }}>
            <TextInput label={d.text('عمود جديد', 'New column')} value={newColumn} onChange={(event) => setNewColumn(event.target.value)} />
            <Button disabled={columnBusy || !newColumn.trim()} fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} size="md" type="submit">{d.text('إضافة', 'Add')}</Button>
          </form>
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
                  {tableColumns(items, d.text).map((item) => (
                    <th className="px-4 py-3 text-start" key={item.key}>
                      <span className="inline-flex items-center gap-1">
                        {item.label}
                        {item.key !== 'name' ? (
                          <button className="rounded p-0.5 text-slate-400 hover:bg-red-50 hover:text-red-600" type="button" onClick={() => setDeleteColumn(item.deleteKey)}>
                            <X className="h-3.5 w-3.5" />
                          </button>
                        ) : null}
                      </span>
                    </th>
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
                    {extraColumns(items).map((columnName) => (
                      <td className="max-w-xs px-4 py-3 text-slate-700" key={columnName}>{item.fields?.[columnName] || '—'}</td>
                    ))}
                    <td className="px-4 py-3 text-end">
                      <div className="flex justify-end gap-1">
                        <Button fullWidth={false} leftIcon={<Pencil className="h-4 w-4" />} size="sm" type="button" variant="ghost" onClick={(event) => { event.stopPropagation(); setEditTarget(item); }}>
                          {d.text('تعديل', 'Edit')}
                        </Button>
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
      {editTarget ? (
        <EditClientSheetModal
          client={{
            id: editTarget.id,
            customerName: editTarget.customerName,
            nationalId: editTarget.nationalId || fieldValue(editTarget, 'ID'),
            phones: phoneCell(editTarget),
            address: editTarget.address || fieldValue(editTarget, 'All Address', 'Address'),
            feedback: editTarget.feedback || fieldValue(editTarget, 'FEEDBACK', 'Feedback'),
            data: editTarget.data || fieldValue(editTarget, 'Data', 'Notes'),
            fields: editTarget.fields,
          }}
          onClose={() => setEditTarget(null)}
          onSaved={() => {
            setEditTarget(null);
            setReloadKey((value) => value + 1);
            toast.success(d.text('تم حفظ التعديل.', 'Changes saved.'));
          }}
        />
      ) : null}
      <ConfirmDialog
        confirmLabel={d.text('حذف العمود', 'Delete column')}
        isConfirming={columnBusy}
        message={d.text(`حذف عمود ${deleteColumn ?? ''} من كل العملاء؟`, `Remove the ${deleteColumn ?? ''} column from every client?`)}
        onCancel={() => setDeleteColumn(null)}
        onConfirm={() => {
          if (!deleteColumn) return;
          setColumnBusy(true);
          dataEntryService.deleteColumn(deleteColumn)
            .then(() => {
              toast.success(d.text('تم حذف العمود.', 'Column removed.'));
              setDeleteColumn(null);
              setReloadKey((value) => value + 1);
            })
            .catch((reason) => toast.error(getApiErrorMessage(reason, d.text('تعذر حذف العمود.', 'Could not remove the column.'))))
            .finally(() => setColumnBusy(false));
        }}
        open={Boolean(deleteColumn)}
        title={d.text('حذف العمود', 'Delete column')}
      />
      <ConfirmDialog
        confirmLabel={d.text('حذف الكل', 'Delete all')}
        isConfirming={deleting}
        message={d.text('حذف كل العملاء من القائمة؟ العملاء المرتبطون بحالة لن يُحذفوا.', 'Delete every client in the list? Clients tied to a case stay.')}
        onCancel={() => setDeleteAllOpen(false)}
        onConfirm={() => {
          setDeleting(true);
          dataEntryService.deleteAllClients()
            .then((result) => {
              toast.success(d.text(`اتحذف ${result.deleted} عميل${result.skipped ? `، وتساب ${result.skipped}` : ''}.`, `Deleted ${result.deleted} client${result.deleted === 1 ? '' : 's'}${result.skipped ? `, left ${result.skipped}` : ''}.`));
              setDeleteAllOpen(false);
              if (result.skipped) setReloadKey((value) => value + 1);
              else setItems([]);
            })
            .catch((reason) => toast.error(getApiErrorMessage(reason, d.text('تعذر حذف العملاء.', 'Could not delete the clients.'))))
            .finally(() => setDeleting(false));
        }}
        open={deleteAllOpen}
        title={d.text('حذف كل العملاء', 'Delete all clients')}
      />
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

function phoneCell(item: DataEntryClientListItem) {
  return fieldValue(item, 'Tell', 'Tel', 'Telephone', 'Mobile', 'Phone') || (item.phones ?? []).filter(Boolean).join(' / ') || item.mobileNumber || '';
}

function clientPhones(item: DataEntryClientListItem) {
  const raw = phoneCell(item);
  if (!raw) return [];
  const parts = raw.split(/[/|،,;\n\r]+/).map((part) => part.trim()).filter(Boolean);
  return parts.length ? parts : [raw];
}

function sheetColumns(items: DataEntryClientListItem[], text: (arabic: string, english: string) => string) {
  return [
    { key: 'all', label: text('كل الأعمدة', 'All columns') },
    { key: 'id', label: text('الرقم القومي', 'National ID') },
    { key: 'name', label: text('الاسم', 'Name') },
    { key: 'phone', label: text('التليفون', 'Phone') },
    { key: 'address', label: text('العنوان', 'Address') },
    { key: 'feedback', label: text('فيدباك', 'Feedback') },
    { key: 'data', label: text('بيانات', 'Data') },
    ...extraColumns(items).map((name) => ({ key: name, label: name })),
  ];
}

function tableColumns(items: DataEntryClientListItem[], text: (arabic: string, english: string) => string) {
  return [
    { key: 'id', label: text('الرقم القومي', 'National ID'), deleteKey: 'ID' },
    { key: 'name', label: text('الاسم', 'Name'), deleteKey: 'Name' },
    { key: 'phone', label: text('التليفونات', 'Phones'), deleteKey: 'Tell' },
    { key: 'address', label: text('العنوان', 'Address'), deleteKey: 'All Address' },
    { key: 'feedback', label: text('فيدباك', 'Feedback'), deleteKey: 'FEEDBACK' },
    { key: 'data', label: text('بيانات', 'Data'), deleteKey: 'Data' },
    ...extraColumns(items).map((name) => ({ key: name, label: name, deleteKey: name })),
  ];
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
    <div className="flex max-w-xs flex-col gap-1">
      {numbers.map((number, index) => (
        <span className="font-mono text-xs text-mis-navy" data-bidi="ltr" key={`${number}-${index}`}>{number}</span>
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
