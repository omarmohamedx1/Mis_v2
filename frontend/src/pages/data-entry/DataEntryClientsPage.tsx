import { FileUp, Plus, Search, Trash2, UsersRound } from 'lucide-react';
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
import { DataEntryDeskFields } from '../../features/data-entry/DataEntryDeskFields';
import { DataEntryBatchStatus, useDataEntryText } from '../../features/data-entry/dataEntryUi';
import { dataEntryService } from '../../features/data-entry/services/dataEntryService';
import type { DataEntryClientListItem, DataEntryOrganization, DataEntryPortfolio } from '../../features/data-entry/types/dataEntry';
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

  useEffect(() => {
    const timer = setTimeout(() => setQuery(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);
    dataEntryService.clients(query || undefined)
      .then((result) => {
        if (cancelled) return;
        setItems(result.items);
      })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [query]);

  return (
    <div className="space-y-5">
      <PageHeader
        title={d.text('العملاء', 'Clients')}
        description={d.text('كل عميل يُحفظ هنا يُرسل كدفعة لمراجعة التحصيل، وبعد القبول تظهر حالته في التحصيل.', 'Every client saved here is sent as a batch for collections review. After acceptance, the case appears in collections.')}
        actions={
          <>
            <Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} size="md" type="button" onClick={() => setAddOpen(true)}>
              {d.text('إضافة عميل', 'Add client')}
            </Button>
            <Button fullWidth={false} leftIcon={<FileUp className="h-4 w-4" />} size="md" type="button" variant="outline" onClick={() => navigate('/data-entry/import')}>
              {d.text('رفع ملف', 'Upload file')}
            </Button>
          </>
        }
      />

      <div className="relative rounded-2xl border border-mis-border bg-white p-4 shadow-sm">
        <Search className="pointer-events-none absolute top-7 h-4 w-4 text-slate-400" style={{ insetInlineStart: '1.5rem' }} />
        <input
          className="h-12 w-full rounded-form border border-mis-border bg-white pe-4 ps-11 text-sm text-mis-ink placeholder:text-slate-400 focus:border-mis-blue focus:outline-none"
          placeholder={d.text('ابحث بالاسم أو الرقم أو البطاقة أو الموبايل', 'Search by name, number, National ID, or mobile')}
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
      </div>

      {error ? (
        <ErrorState title={d.text('تعذر تحميل العملاء', 'Could not load clients')} onRetry={() => setQuery((value) => value)} />
      ) : loading ? (
        <div className="grid min-h-[280px] place-items-center"><LoadingSpinner /></div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="min-w-[56rem] w-full text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  <th className="px-4 py-3 text-start">{d.text('العميل', 'Client')}</th>
                  <th className="px-4 py-3 text-start">{d.text('الجهة', 'Organization')}</th>
                  <th className="px-4 py-3 text-start">{d.text('الموبايل', 'Mobile')}</th>
                  <th className="px-4 py-3 text-start">{d.text('مسار التحصيل', 'Collections path')}</th>
                  <th className="px-4 py-3 text-start">{d.text('الحالة', 'Case')}</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {items.map((item) => (
                  <tr key={item.id} className="cursor-pointer hover:bg-slate-50" onClick={() => navigate(`/data-entry/clients/${item.id}`)}>
                    <td className="px-4 py-3">
                      <p className="font-semibold text-mis-navy">{item.customerName}</p>
                      <p className="font-mono text-xs text-slate-500" data-bidi="ltr">{item.customerNumber}</p>
                    </td>
                    <td className="px-4 py-3">{item.organizationName || '—'}</td>
                    <td className="px-4 py-3" data-bidi="ltr">{item.mobileNumber || '—'}</td>
                    <td className="px-4 py-3">{item.batchStatus ? <DataEntryBatchStatus value={item.batchStatus} /> : '—'}</td>
                    <td className="px-4 py-3" data-bidi="ltr">{item.caseNumber || d.text('لم تُنشأ بعد', 'Not created yet')}</td>
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
              description={d.text('أضف عميلًا يدويًا أو ارفع ملفًا ليصل للتحصيل.', 'Add a client manually or upload a file so it can reach collections.')}
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
        message={d.text(`حذف ${deleteTarget?.customerName ?? ''}؟ الحذف متاح فقط قبل إنشاء حالة التحصيل.`, `Delete ${deleteTarget?.customerName ?? ''}? Allowed only before a collections case is created.`)}
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
  const [accountNumber, setAccountNumber] = useState('');
  const [contractNumber, setContractNumber] = useState('');
  const [outstandingBalance, setOutstandingBalance] = useState('');
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
      setPortfolioId('');
    }).catch(() => {
      setPortfolios([]);
      setPortfolioId('');
    });
  }, [organizationId]);

  useEffect(() => {
    const selected = portfolios.find((item) =>
      item.primaryClassification === primaryClassification && item.subClassification === subClassification);
    setPortfolioId(selected?.id ?? '');
  }, [portfolios, primaryClassification, subClassification]);

  const deskReady = Boolean(organizationId && primaryClassification && subClassification);

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
      title={d.text('إضافة عميل للتحصيل', 'Add a client for collections')}
      footer={
        <>
          <Button disabled={saving} fullWidth={false} size="md" type="button" variant="outline" onClick={onClose}>
            {d.text('إلغاء', 'Cancel')}
          </Button>
          <Button disabled={saving || !deskReady} form="add-data-entry-client" fullWidth={false} isLoading={saving} size="md" type="submit">
            {d.text('حفظ وإرسال للتحصيل', 'Save and send to collections')}
          </Button>
        </>
      }
    >
      <p className="mb-4 rounded-xl bg-mis-pale px-3 py-2 text-sm text-mis-navy">
        {d.text('بعد الحفظ تُنشأ دفعة وتُرسل مباشرة لمراجعة مشرف التحصيل. رقم الحساب يساعد على إنشاء الحالة بعد القبول.', 'After save, a batch is created and sent to collections supervisors. An account number helps create the case after acceptance.')}
      </p>
      <form className="grid gap-4 sm:grid-cols-2" id="add-data-entry-client" onSubmit={(event) => void submit(event)}>
        {error ? <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700 sm:col-span-2">{error}</div> : null}
        <div className="sm:col-span-2">
          <DataEntryDeskFields
            organizationId={organizationId}
            organizations={organizations}
            portfolios={portfolios}
            primaryClassification={primaryClassification}
            subClassification={subClassification}
            onOrganizationChange={(id) => {
              setOrganizationId(id);
              setPrimaryClassification('');
              setSubClassification('');
              setPortfolioId('');
            }}
            onPrimaryChange={(value) => {
              setPrimaryClassification(value);
              setSubClassification('');
            }}
            onSubChange={setSubClassification}
          />
        </div>
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
