import { useCallback, useEffect, useState } from 'react';
import { Button } from '../../components/common/Button';
import { Modal } from '../../components/common/Modal';
import { StatusBadge } from '../../components/common/StatusBadge';
import { useToast } from '../../components/common/Toast';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { collectionsService } from '../../features/collections/services/collectionsService';
import type { BankPortfolioImport, BankPortfolioImportConfirmResult, BankPortfolioImportDataPreview } from '../../features/collections/types/collections';
import { ExcelDropzone } from '../../features/import';
import { getApiErrorMessage } from '../../services/apiClient';

const maximumBytes = 50 * 1024 * 1024;
const supportedExtensions = ['.xlsx', '.xls', '.csv'];
const extension = (name: string) => name.slice(name.lastIndexOf('.')).toLowerCase();

type Props = {
  bankId: string;
  bankName: string;
  open: boolean;
  onClose: () => void;
  onImported: (result: BankPortfolioImportConfirmResult) => void;
};

export function BankPortfolioUploadModal({ bankId, bankName, open, onClose, onImported }: Props) {
  const { language, ct } = useCollectionsLocalization();
  const toast = useToast();
  const [stage, setStage] = useState<'upload' | 'review' | 'success'>('upload');
  const [candidate, setCandidate] = useState<BankPortfolioImport>();
  const [preview, setPreview] = useState<BankPortfolioImportDataPreview>();
  const [result, setResult] = useState<BankPortfolioImportConfirmResult>();
  const [notes, setNotes] = useState('');
  const [uploading, setUploading] = useState(false);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const number = (value: number) => new Intl.NumberFormat(language === 'ar' ? 'ar-EG' : 'en-GB').format(value);
  const date = (value: string) => new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value));

  useEffect(() => {
    if (!open) {
      setStage('upload'); setCandidate(undefined); setPreview(undefined); setResult(undefined); setNotes('');
    }
  }, [open]);

  const validateFile = useCallback((file: File) => {
    if (!supportedExtensions.includes(extension(file.name))) { toast.error(ct('unsupportedPortfolioFile')); return false; }
    if (file.size <= 0) { toast.error(ct('emptyPortfolioFile')); return false; }
    if (file.size > maximumBytes) { toast.error(ct('portfolioFileTooLarge')); return false; }
    return true;
  }, [ct, toast]);

  const upload = useCallback(async (file?: File) => {
    if (!file || !validateFile(file)) return;
    setUploading(true);
    try {
      const uploaded = await collectionsService.uploadBankPortfolio(bankId, file);
      setCandidate(uploaded);
      setNotes('');
      setLoadingPreview(true);
      try {
        setPreview(await collectionsService.previewBankPortfolioData(bankId, uploaded.id));
      } catch (error) {
        toast.error(getApiErrorMessage(error, ct('portfolioUploadFailed')));
        setPreview(undefined);
      } finally {
        setLoadingPreview(false);
      }
      setStage('review');
    } catch (error) {
      toast.error(getApiErrorMessage(error, ct('portfolioUploadFailed')));
    } finally {
      setUploading(false);
    }
  }, [bankId, ct, toast, validateFile]);

  async function confirm() {
    if (!candidate || confirming) return;
    setConfirming(true);
    try {
      const confirmed = await collectionsService.confirmBankPortfolio(bankId, candidate.id, notes);
      setCandidate(confirmed.import);
      setResult(confirmed);
      setStage('success');
      toast.success(ct('portfolioImportConfirmed'));
      onImported(confirmed);
    } catch (error) {
      toast.error(getApiErrorMessage(error, ct('portfolioUploadFailed')));
    } finally {
      setConfirming(false);
    }
  }

  return (
    <Modal
      open={open}
      onClose={() => { if (!uploading && !confirming) onClose(); }}
      size="xl"
      title={ct('uploadPortfolioFile')}
      footer={stage === 'review' ? <>
        <Button fullWidth={false} disabled={confirming} onClick={onClose} variant="outline">{ct('cancel')}</Button>
        <Button fullWidth={false} disabled={loadingPreview || !candidate} isLoading={confirming} onClick={() => void confirm()}>{ct('confirmImportPortfolio')}</Button>
      </> : stage === 'success' ? <Button fullWidth={false} onClick={onClose}>{ct('cancel')}</Button> : undefined}
    >
      {stage === 'upload' ? (
        <div className="space-y-4">
          <p className="text-sm text-slate-500">{ct('importPortfolioDescription')}</p>
          <p className="text-xs font-semibold uppercase tracking-wide text-mis-primary">{bankName}</p>
          <ExcelDropzone arabic={language === 'ar'} busy={uploading} file={null} label={ct('uploadPortfolioFile')} onFile={(next) => { if (next) void upload(next); }} />
        </div>
      ) : null}

      {stage === 'review' && candidate ? (
        <div className="space-y-5">
          <dl className="grid gap-4 sm:grid-cols-2">
            <Field label={ct('file')} value={candidate.originalFileName} />
            <Field label={ct('bank')} value={bankName} />
            <Field label={ct('records')} value={number(candidate.rowCount)} />
            <Field label={ct('importDate')} value={`${date(candidate.uploadedAt)} · ${ct('automatic')}`} />
          </dl>
          {loadingPreview ? <p className="text-sm text-slate-500">{ct('loading')}</p> : preview ? (
            <div className="space-y-3">
              <div className="flex flex-wrap gap-3 text-sm">
                <StatusBadge tone="success">{ct('Ready')}: {number(preview.readyRows)}</StatusBadge>
                <StatusBadge tone="warning">{ct('Existing')}: {number(preview.existingRows)}</StatusBadge>
                <StatusBadge tone="danger">{ct('Error')}: {number(preview.invalidRows)}</StatusBadge>
              </div>
              <div className="max-h-72 overflow-auto rounded-xl border border-mis-border">
                <table className="min-w-full text-sm">
                  <thead className="sticky top-0 bg-slate-50 text-xs uppercase text-slate-500">
                    <tr>{[ct('row'), ct('customerName'), ct('nationalId'), ct('caseRef'), ct('cardNumber'), ct('currentBkt'), ct('currentBalance'), ct('status'), ct('errors')].map((label) => <th className="px-3 py-2 text-start" key={label}>{label}</th>)}</tr>
                  </thead>
                  <tbody className="divide-y divide-mis-border">
                    {preview.rows.map((row) => (
                      <tr key={row.rowNumber}>
                        <td className="px-3 py-2" data-bidi="ltr">{row.rowNumber}</td>
                        <td className="px-3 py-2">{row.customerName ?? '—'}</td>
                        <td className="px-3 py-2" data-bidi="ltr">{row.nationalId ?? '—'}</td>
                        <td className="px-3 py-2" data-bidi="ltr">{row.accountReference ?? '—'}</td>
                        <td className="px-3 py-2" data-bidi="ltr">{row.cardNumber ?? '—'}</td>
                        <td className="px-3 py-2">{row.bucket ?? '—'}</td>
                        <td className="px-3 py-2" data-bidi="ltr">{row.outstandingBalance == null ? '—' : number(row.outstandingBalance)}</td>
                        <td className="px-3 py-2"><StatusBadge tone={row.status === 'Ready' ? 'success' : row.status === 'Existing' ? 'warning' : 'danger'}>{ct(row.status as never) || row.status}</StatusBadge></td>
                        <td className="max-w-xs px-3 py-2 text-xs text-rose-700">{row.errors.join(' · ') || '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ) : null}
          <label className="block text-sm font-semibold text-slate-700">{ct('notes')}
            <textarea className="field mt-2 min-h-20 resize-y" maxLength={1000} onChange={(event) => setNotes(event.target.value)} placeholder={ct('notesPlaceholder')} value={notes} />
          </label>
        </div>
      ) : null}

      {stage === 'success' && result ? (
        <div className="space-y-4">
          <p className="text-lg font-bold text-mis-navy">{ct('portfolioImported')}</p>
          <div className="grid gap-3 sm:grid-cols-3">
            <Summary label={ct('importedCount')} value={number(result.imported)} />
            <Summary label={ct('skippedCount')} value={number(result.skipped)} />
            <Summary label={ct('failedCount')} value={number(result.invalid)} />
          </div>
        </div>
      ) : null}
    </Modal>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return <div><dt className="text-xs font-semibold text-slate-500">{label}</dt><dd className="mt-1 break-words font-semibold text-mis-navy">{value}</dd></div>;
}

function Summary({ label, value }: { label: string; value: string }) {
  return <div className="rounded-xl border border-mis-border bg-slate-50 p-4"><p className="text-xs font-semibold uppercase text-slate-500">{label}</p><p className="mt-1 text-xl font-bold text-mis-navy">{value}</p></div>;
}
