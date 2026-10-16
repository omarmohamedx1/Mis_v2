import { apiClient, downloadApiFile } from '../../../services/apiClient';
import type { Advance, BookCreate, BookFilter, BookPage, BookEntry, BookSummary, CommissionPreview, ExportFilter, RecurringExpense, RecurringInput } from '../types/book';
const root = '/accounting/book';
export const accountingBookService = {
  entries: (params: BookFilter) => apiClient.get<BookPage>(`${root}/entries`, { params }).then(r => r.data),
  create: (input: BookCreate) => apiClient.post<BookEntry>(`${root}/entries`, input).then(r => r.data),
  action: (id: string, action: string, reason?: string) => apiClient.post<BookEntry>(`${root}/entries/${id}/${action}`, { reason }).then(r => r.data),
  deleteEntry: (id: string) => apiClient.post(`${root}/entries/${id}/delete`, { confirmed: true }).then(() => undefined),
  deleteSource: (kind: string, id: string) => apiClient.post(`${root}/records/${kind}/${id}/delete`, { confirmed: true }).then(() => undefined),
  summary: (from: string, to: string) => apiClient.get<BookSummary>(`${root}/summary`, { params: { from, to } }).then(r => r.data),
  advances: (asOf: string, search?: string) => apiClient.get<Advance[]>(`${root}/advances`, { params: { asOf, search } }).then(r => r.data),
  recurring: () => apiClient.get<RecurringExpense[]>(`${root}/recurring`).then(r => r.data),
  saveRecurring: (id: string | undefined, input: RecurringInput) => (id ? apiClient.put<RecurringExpense>(`${root}/recurring/${id}`, input) : apiClient.post<RecurringExpense>(`${root}/recurring`, input)).then(r => r.data),
  generate: (year: number, month: number) => apiClient.post<{ created: number; alreadyGenerated: number; outsideSchedule: number; notDue: number }>(`${root}/recurring/generate`, { year, month }).then(r => r.data),
  preview: (year: number, month: number) => apiClient.get<CommissionPreview>(`${root}/commission-preview`, { params: { year, month } }).then(r => r.data),
  export: (report: string, params: ExportFilter) => downloadApiFile(`/accounting/exports/${report}`, `MIS-${report}.xlsx`, { params }),
};
