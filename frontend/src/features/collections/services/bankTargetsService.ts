import { apiClient } from '../../../services/apiClient';
import type { BankTargets, BankTargetScope, BankTargetValues } from '../types/bankTargets';

function path(s: BankTargetScope) {
  return `/collections/bank-targets/${s.organizationId}/${s.portfolioType}/${s.portfolioId}/${encodeURIComponent(s.period)}`;
}
export const bankTargetsService = {
  template: async () => (await apiClient.get<BankTargetValues>('/collections/bank-targets/template')).data,
  get: async (scope: BankTargetScope) => (await apiClient.get<BankTargets>(path(scope))).data,
  save: async (scope: BankTargetScope, values: BankTargetValues, expectedVersion: string | null) =>
    (await apiClient.put<BankTargets>(path(scope), { values, expectedVersion })).data,
  copy: async (scope: BankTargetScope) => (await apiClient.post<BankTargets>(`${path(scope)}/copy-previous`)).data,
  review: async (scope: BankTargetScope, expectedVersion: string, approve: boolean) =>
    (await apiClient.post<BankTargets>(`${path(scope)}/${approve ? 'approve' : 'review'}`, { expectedVersion })).data,
};
