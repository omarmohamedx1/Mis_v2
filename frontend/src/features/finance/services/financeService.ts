import { apiClient } from '../../../services/apiClient';
import type { AccountingPeriod, CollectionFinance, CollectionFinanceListItem, CreateManualJournalInput, CustodyDetails, CustodySummary, FinanceAccount, FinanceDashboard, FinancePagedResult, Journal, JournalListItem, TrialBalance } from '../types/finance';

export const financeService = {
  async dashboard() { return (await apiClient.get<FinanceDashboard>('/finance/dashboard')).data; },
  async accounts() { return (await apiClient.get<FinanceAccount[]>('/finance/accounts')).data; },
  async periods(year?: number) { return (await apiClient.get<AccountingPeriod[]>('/finance/periods', { params: { year } })).data; },
  async initializeYear(year: number) { return (await apiClient.post<AccountingPeriod[]>(`/finance/periods/initialize/${year}`)).data; },
  async periodAction(id: string, action: 'soft-close' | 'close' | 'reopen', reason: string) { return (await apiClient.post<AccountingPeriod>(`/finance/periods/${id}/${action}`, { reason })).data; },
  async journals(page = 1, status?: string) { return (await apiClient.get<FinancePagedResult<JournalListItem>>('/finance/journals', { params: { page, pageSize: 30, status: status || undefined } })).data; },
  async journal(id: string) { return (await apiClient.get<Journal>(`/finance/journals/${id}`)).data; },
  async createJournal(input: CreateManualJournalInput) { return (await apiClient.post<Journal>('/finance/journals', input)).data; },
  async journalAction(id: string, action: 'submit' | 'approve' | 'post') { return (await apiClient.post<Journal>(`/finance/journals/${id}/${action}`)).data; },
  async reverseJournal(id: string, reason: string) { return (await apiClient.post<Journal>(`/finance/journals/${id}/reverse`, { reason })).data; },
  async trialBalance(asOf: string) { return (await apiClient.get<TrialBalance>('/finance/reports/trial-balance', { params: { asOf } })).data; },
  async collections(page = 1, status?: string, channel?: string) { return (await apiClient.get<FinancePagedResult<CollectionFinanceListItem>>('/finance/collections', { params: { page, pageSize: 30, status: status || undefined, channel: channel || undefined } })).data; },
  async collection(paymentId: string) { return (await apiClient.get<CollectionFinance>(`/finance/collections/${paymentId}`)).data; },
  async clearCollection(paymentId: string, clearedOn: string, reference: string) { return (await apiClient.post<CollectionFinance>(`/finance/collections/${paymentId}/clear`, { clearedOn, reference })).data; },
  async reverseCollection(paymentId: string, reason: string) { return (await apiClient.post<CollectionFinance>(`/finance/collections/${paymentId}/reverse`, { reason })).data; },
  async custodies() { return (await apiClient.get<CustodySummary[]>('/finance/custodies')).data; },
  async custody(collectorId: string) { return (await apiClient.get<CustodyDetails>(`/finance/custodies/${collectorId}`)).data; },
  async updateCustodyLimits(collectorId: string, softLimit: number, hardLimit: number, reason: string) { return (await apiClient.put<CustodyDetails>(`/finance/custodies/${collectorId}/limits`, { softLimit, hardLimit, reason })).data; },
};
