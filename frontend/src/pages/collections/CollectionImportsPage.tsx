import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { Download, FileSpreadsheet, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { ExcelDropzone, ExcelPreviewToolbar, ExtraFieldsPanel, importCopy, rememberExtraColumns } from '../../features/import';
import { CollectionStatus, KpiCard, useCollectionFormat } from '../../features/collections/components/CollectionsUi';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { ClientCard, ImportBatch, ImportPreview, PagedResult, PortfolioLookup } from '../../features/collections/types/collections';

export function CollectionImportsPage() {
  const { language, ct } = useCollectionsLocalization();
  const f = useCollectionFormat();
  const ar = language === 'ar';
  const [clients, setClients] = useState<ClientCard[]>([]);
  const [portfolios, setPortfolios] = useState<PortfolioLookup[]>([]);
  const [history, setHistory] = useState<PagedResult<ImportBatch>>();
  const [preview, setPreview] = useState<ImportPreview>();
  const [organizationId, setOrganizationId] = useState('');
  const [portfolioId, setPortfolioId] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const [removal, setRemoval] = useState<ImportBatch>();
  const [deleting, setDeleting] = useState(false);
  const [excludedRows, setExcludedRows] = useState<number[]>([]);
  const [selectedRows, setSelectedRows] = useState<number[]>([]);
  const [confirmOpen, setConfirmOpen] = useState(false);

  const confirmDelete = async () => {
    if (!removal) return;
    setDeleting(true);
    setError('');
    try {
      await collectionsService.deleteImport(removal.id);
      if (preview?.batch.id === removal.id) setPreview(undefined);
      setRemoval(undefined);
      loadHistory();
    } catch {
      setError(ct('deleteFailed'));
      setRemoval(undefined);
    } finally {
      setDeleting(false);
    }
  };

  const loadHistory = () => collectionsService.imports({ page: 1, pageSize: 20 }).then(setHistory);
  useEffect(() => {
    Promise.all([collectionsService.clients({ pageSize: 100 }), collectionsService.portfolios(), collectionsService.imports({ pageSize: 20 })])
      .then(([c, p, h]) => {
        setClients(c.items);
        setPortfolios(p);
        setHistory(h);
        if (c.items[0]) setOrganizationId((current) => current || c.items[0].id);
        const firstBook = p.find((item) => !c.items[0] || item.organizationId === c.items[0].id) ?? p[0];
        if (firstBook) setPortfolioId((current) => current || firstBook.id);
      })
      .catch(() => setError(ct('loadError')));
  }, [ct]);

  const upload = async (selected?: File | null, orgId = organizationId, bookId = portfolioId) => {
    const target = selected ?? file;
    if (!target) return;
    setFile(target);
    if (!orgId || !bookId) {
      setError(ct('selectClient'));
      return;
    }
    setSaving(true);
    setError('');
    try {
      const batch = await collectionsService.uploadImport(orgId, bookId, target);
      setPreview(await collectionsService.importPreview(batch.id, { pageSize: 100 }));
      setExcludedRows([]);
      setSelectedRows([]);
      loadHistory();
    } catch {
      setError(ct('saveError'));
    } finally {
      setSaving(false);
    }
  };

  const confirm = async () => {
    if (!preview) return;
    setSaving(true);
    try {
      const extras = preview.rows.items.flatMap((row) => Object.keys(row.extraFields ?? {}));
      rememberExtraColumns('collections', extras);
      const batch = await collectionsService.confirmImport(preview.batch.id, undefined, excludedRows);
      setPreview(await collectionsService.importPreview(batch.id, { pageSize: 100 }));
      setConfirmOpen(false);
      loadHistory();
    } catch {
      setError(ct('saveError'));
    } finally {
      setSaving(false);
    }
  };

  const readyCount = preview?.rows.items.filter((row) => row.isValid && !excludedRows.includes(row.rowNumber)).length ?? 0;

  return (
    <div>
      <PageHeader eyebrow={ct('collections')} title={ct('imports')} description={ct('importSubtitle')} />
      <section className="rounded-2xl border border-mis-border bg-white p-4 sm:p-6">
        <div className="grid min-w-0 items-end gap-4 sm:grid-cols-2">
          <Field label={ct('selectClient')}>
            <ProfessionalSelect required className="field" value={organizationId} onChange={(e) => setOrganizationId(e.target.value)}>
              <option value="">—</option>
              {clients.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </ProfessionalSelect>
          </Field>
          <Field label={ct('selectPortfolio')}>
            <ProfessionalSelect required className="field" value={portfolioId} onChange={(e) => setPortfolioId(e.target.value)}>
              <option value="">—</option>
              {portfolios.filter((x) => !organizationId || x.organizationId === organizationId).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </ProfessionalSelect>
          </Field>
        </div>
        <div className="mt-5">
          <ExcelDropzone arabic={ar} busy={saving} file={file} label={ct('file')} onFile={(next) => { if (next) void upload(next); else { setFile(null); setPreview(undefined); } }} />
        </div>
        {error ? <p className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{error}</p> : null}
      </section>
      {preview ? (
        <section className="mt-6">
          <div className="grid gap-4 sm:grid-cols-3">
            <KpiCard label={ct('totalRows')} value={f.number(preview.batch.totalRows)} />
            <KpiCard label={ct('validRows')} value={f.number(preview.batch.validRows)} accent="green" />
            <KpiCard label={ct('invalidRows')} value={f.number(preview.batch.invalidRows)} accent={preview.batch.invalidRows ? 'red' : 'blue'} />
          </div>
          <div className="mt-4">
            <ExcelPreviewToolbar
              arabic={ar}
              excludedCount={excludedRows.length}
              onExclude={() => setExcludedRows((current) => [...new Set([...current, ...selectedRows])])}
              onRestore={() => { setExcludedRows((current) => current.filter((row) => !selectedRows.includes(row))); setSelectedRows([]); }}
              readyCount={readyCount}
              selectedCount={selectedRows.length}
            />
          </div>
          <div className="mt-5 overflow-hidden rounded-2xl border border-mis-border bg-white">
            <header className="flex flex-wrap items-center gap-3 border-b border-mis-border px-5 py-4">
              <h2 className="font-bold text-mis-navy">{ct('preview')} — {preview.batch.fileName}</h2>
              <CollectionStatus value={preview.batch.status} />
              <div className="ms-auto flex gap-2">
                {preview.batch.invalidRows > 0 ? <button onClick={() => collectionsService.downloadImportErrors(preview.batch.id)} className="inline-flex items-center gap-2 rounded-lg border border-mis-border px-3 py-2 text-sm font-bold text-mis-primary" type="button"><Download className="h-4 w-4" />{ct('downloadErrors')}</button> : null}
                {preview.batch.status === 'PREVIEW_READY' ? <button disabled={saving || readyCount === 0} onClick={() => setConfirmOpen(true)} className="rounded-lg bg-mis-primary px-4 py-2 text-sm font-bold text-white" type="button">{importCopy.confirmAction(ar, readyCount)}</button> : null}
              </div>
            </header>
            <div className="max-h-[500px] overflow-auto">
              <table className="w-full min-w-[48rem] text-sm">
                <thead className="sticky top-0 bg-slate-50">
                  <tr>{['', ct('row'), ct('accountReference'), ct('customer'), ct('balance'), ct('overdue'), 'DPD', ct('status'), ct('errors')].map((x) => <th key={x} className="px-4 py-3 text-start text-xs text-slate-500">{x}</th>)}</tr>
                </thead>
                <tbody className="divide-y divide-mis-border">
                  {preview.rows.items.map((x) => (
                    <tr className={excludedRows.includes(x.rowNumber) ? 'bg-slate-100 opacity-60' : undefined} key={x.id}>
                      <td className="px-4 py-3"><input checked={selectedRows.includes(x.rowNumber)} onChange={() => setSelectedRows((current) => current.includes(x.rowNumber) ? current.filter((item) => item !== x.rowNumber) : [...current, x.rowNumber])} type="checkbox" /></td>
                      <td className="px-4 py-3" data-bidi="ltr">{x.rowNumber}{x.sheetName ? <p className="text-xs text-slate-400">{x.sheetName}</p> : null}</td>
                      <td className="px-4 py-3" data-bidi="ltr">{x.accountReference}</td>
                      <td className="px-4 py-3"><p>{x.customerName}</p><p className="text-xs text-slate-500">{x.customerCode}</p></td>
                      <td className="px-4 py-3" data-bidi="ltr">{x.outstandingBalance == null ? '—' : f.money(x.outstandingBalance)}</td>
                      <td className="px-4 py-3" data-bidi="ltr">{x.overdueBalance == null ? '—' : f.money(x.overdueBalance)}</td>
                      <td className="px-4 py-3" data-bidi="ltr">{x.daysPastDue ?? '—'}</td>
                      <td className="px-4 py-3"><CollectionStatus value={x.isValid ? 'APPROVED' : 'REJECTED'} /></td>
                      <td className="max-w-xs px-4 py-3 text-xs text-rose-700">{x.errors.join(' · ')}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
          <div className="mt-5">
            <ExtraFieldsPanel arabic={ar} fields={[...new Set(preview.rows.items.flatMap((row) => Object.entries(row.extraFields ?? {})))] } />
          </div>
        </section>
      ) : null}
      <section className="mt-8">
        <h2 className="mb-4 text-xl font-bold text-mis-navy">{ct('importHistory')}</h2>
        {!history ? error ? <ErrorState title={error} /> : <div className="flex min-h-40 items-center justify-center"><LoadingSpinner /></div> : history.items.length ? (
          <div className="grid gap-4">
            {history.items.map((x) => (
              <div key={x.id} className="flex flex-wrap items-center gap-4 rounded-2xl border border-mis-border bg-white p-5 hover:border-mis-sky">
                <button type="button" onClick={async () => setPreview(await collectionsService.importPreview(x.id, { pageSize: 100 }))} className="flex min-w-0 flex-1 items-center gap-4 text-start">
                  <FileSpreadsheet className="h-7 w-7 shrink-0 text-mis-primary" />
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-bold text-mis-navy">{x.fileName}</p>
                    <p className="mt-1 text-xs text-slate-500">{x.organizationName} · {x.portfolioName} · {f.dateTime(x.uploadedAt)}</p>
                  </div>
                </button>
                <div className="flex gap-4 text-xs text-slate-500"><span>{ct('validRows')}: <b>{x.validRows}</b></span><span>{ct('invalidRows')}: <b>{x.invalidRows}</b></span></div>
                <CollectionStatus value={x.status} />
                <button type="button" onClick={() => setRemoval(x)} aria-label={ct('deleteImportBatch')} title={ct('deleteImportBatch')} className="rounded-lg p-2 text-rose-600 hover:bg-rose-50"><Trash2 className="h-4 w-4" /></button>
              </div>
            ))}
          </div>
        ) : <p className="rounded-2xl border border-dashed border-mis-border p-10 text-center text-slate-500">{ct('noImports')}</p>}
      </section>
      {removal ? <ConfirmDialog open title={ct('deleteImportBatch')} message={ct('deleteImportBatchConfirm')} description={removal.fileName} confirmLabel={ct('delete')} cancelLabel={ct('cancel')} isConfirming={deleting} onCancel={() => setRemoval(undefined)} onConfirm={() => void confirmDelete()} /> : null}
      <ConfirmDialog
        confirmLabel={importCopy.confirmAction(ar, readyCount)}
        confirmVariant="primary"
        isConfirming={saving}
        message={importCopy.confirmMessage(ar, readyCount)}
        onCancel={() => { if (!saving) setConfirmOpen(false); }}
        onConfirm={() => void confirm()}
        open={confirmOpen}
        title={importCopy.confirmTitle(ar)}
      />
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return <label><span className="mb-1.5 block text-sm font-semibold text-slate-600">{label}</span>{children}</label>;
}
