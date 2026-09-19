import { ChevronLeft, ChevronRight } from 'lucide-react';
import type { ReactNode } from 'react';
import { useLocalization } from '../../context/LocalizationContext';

export interface PaginationLabels {
  nextPage: string;
  pageOf: (page: number, totalPages: number) => ReactNode;
  previousPage: string;
  showing: (from: number, to: number, totalCount: number) => ReactNode;
}

export interface PaginationProps {
  ariaLabel?: string;
  className?: string;
  disabled?: boolean;
  labels?: Partial<PaginationLabels>;
  onPageChange: (page: number) => void;
  page: number;
  pageSize: number;
  showSummary?: boolean;
  totalCount: number;
  totalPages: number;
}

export function Pagination({ ariaLabel, className = '', disabled = false, labels, onPageChange, page, pageSize, showSummary = true, totalCount, totalPages }: PaginationProps) {
  const { t } = useLocalization();
  const defaultLabels: PaginationLabels = {
    nextPage: t('nextPage'),
    pageOf: (currentPage, pageCount) => t('pageOf', { page: currentPage, total: pageCount }),
    previousPage: t('previousPage'),
    showing: (from, to, count) => t('showing', { from, to, total: count }),
  };
  const text = { ...defaultLabels, ...labels };
  const safeTotalPages = Math.max(0, totalPages);
  const safePage = safeTotalPages === 0 ? 0 : Math.min(Math.max(page, 1), safeTotalPages);
  const from = totalCount === 0 || safePage === 0 ? 0 : (safePage - 1) * pageSize + 1;
  const to = totalCount === 0 || safePage === 0 ? 0 : Math.min(safePage * pageSize, totalCount);

  if (safeTotalPages <= 1) return null;

  return (
    <nav aria-label={ariaLabel ?? t('paginationLabel')} className={`flex flex-col items-center justify-between gap-3 border-t border-mis-border px-5 py-4 text-sm text-slate-500 sm:flex-row ${className}`}>
      {showSummary ? <span>{text.showing(from, to, totalCount)}</span> : <span />}
      <div className="flex items-center gap-2">
        <button
          aria-label={text.previousPage}
          className="rounded-lg border border-mis-border p-2 text-slate-600 transition hover:border-mis-blue hover:text-mis-primary disabled:cursor-not-allowed disabled:opacity-40"
          disabled={disabled || safePage <= 1}
          onClick={() => onPageChange(safePage - 1)}
          type="button"
        >
          <ChevronLeft className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />
        </button>
        <span className="px-2 font-semibold text-mis-navy">{text.pageOf(safePage, safeTotalPages)}</span>
        <button
          aria-label={text.nextPage}
          className="rounded-lg border border-mis-border p-2 text-slate-600 transition hover:border-mis-blue hover:text-mis-primary disabled:cursor-not-allowed disabled:opacity-40"
          disabled={disabled || safePage === 0 || safePage >= safeTotalPages}
          onClick={() => onPageChange(safePage + 1)}
          type="button"
        >
          <ChevronRight className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />
        </button>
      </div>
    </nav>
  );
}
