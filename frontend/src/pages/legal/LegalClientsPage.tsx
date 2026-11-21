import { Pencil, Search, Trash2, UsersRound } from 'lucide-react';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { useToast } from '../../components/common/Toast';
import { getApiErrorMessage } from '../../services/apiClient';
import { ActionMenu } from '../../components/common/ActionMenu';
import { Button } from '../../components/common/Button';
import { EditLegalClientModal } from '../../features/legal/components/EditLegalClientModal';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { useLegalText } from '../../features/legal/legalUi';
import { legalService } from '../../features/legal/services/legalService';
import type { LegalClientListItem, LegalClientPage } from '../../features/legal/types/legal';

export function LegalClientsPage() {
  const l = useLegalText();
  const toast = useToast();
  const [deleteTarget, setDeleteTarget] = useState<LegalClientListItem>();
  const [deleting, setDeleting] = useState(false);
  const [checkingPermission, setCheckingPermission] = useState(false);

  async function requestDelete(client: LegalClientListItem) {
    if (checkingPermission || deleting) return;
    setCheckingPermission(true);
    try {
      const details = await legalService.client(client.customerId);
      if (!details.canManage) {
        toast.error(l.text('ليس لديك صلاحية إزالة هذا العميل.', 'You do not have permission to remove this client.'));
        return;
      }
      setDeleteTarget(client);
    } catch (reason) {
      toast.error(getApiErrorMessage(reason, l.text('تعذر تحميل ملف العميل.', 'Could not load the client file.')));
    } finally {
      setCheckingPermission(false);
    }
  }

  async function archiveClient() {
    if (!deleteTarget || deleting) return;
    setDeleting(true);
    try {
      await legalService.archiveClient(deleteTarget.customerId);
      setDeleteTarget(undefined);
      toast.success(l.text('تمت أرشفة العميل وإزالته من قائمة Legal.', 'Client archived and removed from the Legal list.'));
      if (result?.items.length === 1 && page > 1) setPage((value) => value - 1);
      setReloadKey((value) => value + 1);
    } catch (reason) {
      toast.error(getApiErrorMessage(reason, l.text('تعذرت أرشفة العميل.', 'Could not archive the client.')));
    } finally {
      setDeleting(false);
    }
  }
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<LegalClientPage>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const [editCustomerId, setEditCustomerId] = useState<string>();

  useEffect(() => {
    const timer = window.setTimeout(() => { setQuery(search.trim()); setPage(1); }, 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(false);
    legalService.clients({ search: query || undefined, page })
      .then((response) => { if (active) setResult(response); })
      .catch(() => { if (active) setError(true); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [query, page, reloadKey]);

  return (
    <div className="space-y-5">
      <PageHeader
        title={l.text('العملاء', 'Clients')}
        description={l.text('العملاء المسجلون للشؤون القانونية، بما في ذلك العملاء المحولون تلقائيًا من التحصيل.', 'Clients assigned to Legal, including clients transferred automatically from Collections.')}
      />
      <div className="relative rounded-2xl border border-mis-border bg-white p-3 shadow-sm">
        <Search className="pointer-events-none absolute top-6 h-4 w-4 text-mis-muted" style={{ insetInlineStart: '1.35rem' }} />
        <input
          aria-label={l.text('ابحث عن عميل', 'Search clients')}
          className="h-11 w-full rounded-xl border border-mis-control bg-white pe-4 ps-10 text-sm focus:border-mis-blue focus:outline-none"
          onChange={(event) => setSearch(event.target.value)}
          placeholder={l.text('ابحث بالاسم أو الرقم القومي أو رقم الهاتف', 'Search by name, National ID, or mobile number')}
          value={search}
        />
      </div>

      {error ? (
        <ErrorState title={l.text('تعذر تحميل العملاء', 'Could not load clients')} onRetry={() => setReloadKey((value) => value + 1)} />
      ) : loading ? (
        <div className="grid min-h-[280px] place-items-center"><LoadingSpinner /></div>
      ) : result?.items.length ? (
        <div className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[68rem] text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  <th className="px-4 py-3 text-start">{l.text('اسم العميل', 'Client Name')}</th>
                  <th className="px-4 py-3 text-start">{l.text('الرقم القومي', 'National ID')}</th>
                  <th className="px-4 py-3 text-start">{l.text('رقم الهاتف', 'Mobile Number')}</th>
                  <th className="px-4 py-3 text-start">{l.text('البنك', 'Bank Name')}</th>
                  <th className="px-4 py-3 text-start">{l.text('العنوان', 'Address')}</th>
                  <th className="px-4 py-3 text-start">{l.text('حالة العميل', 'Client Status')}</th>
                  <th className="w-20 px-4 py-3"><span className="sr-only">{l.text('الإجراءات', 'Actions')}</span></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {result.items.map((client) => (
                  <tr className="hover:bg-slate-50" key={client.customerId}>
                    <td className="px-4 py-3 font-semibold text-mis-navy">{client.name}</td>
                    <td className="px-4 py-3" data-bidi="ltr">{client.nationalId || '—'}</td>
                    <td className="px-4 py-3" data-bidi="ltr">{client.mobileNumber || '—'}</td>
                    <td className="px-4 py-3">{client.bankName}</td>
                    <td className="max-w-56 truncate px-4 py-3">{client.address || '—'}</td>
                    <td className="px-4 py-3">
                      <span className="inline-flex rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-700">
                        {client.transferredFromCollections
                          ? l.text('محول من التحصيل', 'From Collections')
                          : l.clientStatus(client.status)}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-end">
                      <ActionMenu label={l.text(`إجراءات ${client.name}`, `Actions for ${client.name}`)}>
                        <Link to={`/legal/client-files/${client.customerId}`}>{l.text('فتح', 'Open')}</Link>
                        <Button fullWidth={false} leftIcon={<Pencil className="h-4 w-4" />} size="sm" type="button" variant="ghost" onClick={() => setEditCustomerId(client.customerId)}>
                          {l.text('تعديل', 'Edit')}
                        </Button>
                        <Button disabled={checkingPermission || deleting} onClick={() => void requestDelete(client)} className="text-red-600" fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} size="sm" type="button" variant="ghost">
                          {l.text('حذف', 'Delete')}
                        </Button>
                      </ActionMenu>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between gap-3 border-t border-mis-border px-4 py-3 text-sm text-slate-500">
            <span>{l.text('عدد العملاء', 'Clients')}: {l.number(result.totalCount)}</span>
            <div className="flex gap-2">
              <button className="rounded-lg border border-mis-border px-3 py-1.5 disabled:opacity-40" disabled={page <= 1} onClick={() => setPage((value) => value - 1)} type="button">{l.text('السابق', 'Previous')}</button>
              <span className="px-2 py-1.5">{l.number(page)} / {l.number(Math.max(result.totalPages, 1))}</span>
              <button className="rounded-lg border border-mis-border px-3 py-1.5 disabled:opacity-40" disabled={page >= result.totalPages} onClick={() => setPage((value) => value + 1)} type="button">{l.text('التالي', 'Next')}</button>
            </div>
          </div>
        </div>
      ) : (
        <EmptyState
          icon={<UsersRound className="h-5 w-5" />}
          title={l.text('لا يوجد عملاء', 'No clients found')}
          description={l.text('سيظهر العملاء المحولون من التحصيل هنا تلقائيًا.', 'Clients transferred from Collections will appear here automatically.')}
        />
      )}
      {editCustomerId ? <EditLegalClientModal
        key={editCustomerId}
        customerId={editCustomerId}
        onClose={() => setEditCustomerId(undefined)}
        onSaved={() => {
          setEditCustomerId(undefined);
          setReloadKey((value) => value + 1);
        }}
      /> : null}
      <ConfirmDialog open={Boolean(deleteTarget)} isConfirming={deleting}
        title={l.text('إزالة العميل من Legal', 'Remove client from Legal')}
        confirmLabel={l.text('حذف من القائمة', 'Remove from list')}
        message={l.text(
          `إزالة ${deleteTarget?.name ?? ''} من قائمة Legal؟ سيتم أرشفة العميل مع الاحتفاظ بالقضايا وسجلات التحصيل والمستندات والتاريخ المالي.`,
          `Remove ${deleteTarget?.name ?? ''} from the Legal list? The client will be archived; cases, collection records, documents and financial history will be retained.`)}
        onCancel={() => setDeleteTarget(undefined)} onConfirm={() => void archiveClient()} />
    </div>
  );
}
