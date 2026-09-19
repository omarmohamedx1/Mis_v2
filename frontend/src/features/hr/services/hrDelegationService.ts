import { apiClient, requestApiFile, type ApiFile } from '../../../services/apiClient';
import type { DelegationDetails, DelegationEntityOption, DelegationQuery, PagedDelegations, SaveDelegationRequest } from '../types/delegation';
import { loadAllHrPages } from './hrPaging';

export const hrDelegationService = {
  async getPaged(query: DelegationQuery): Promise<PagedDelegations> { return loadAllHrPages(async (page, pageSize) => { const { data } = await apiClient.get<PagedDelegations>('/hr/delegations', { params: { page, pageSize, search: query.search || undefined, employeeId: query.employeeId || undefined, departmentId: query.departmentId || undefined, delegationTypeId: query.delegationTypeId || undefined, delegatingEntityId: query.delegatingEntityId || undefined, status: query.status || undefined, dateFrom: query.dateFrom || undefined, dateTo: query.dateTo || undefined, sortBy: query.sortBy, sortDirection: query.sortDirection } }); return data; }); },
  async getEntities(): Promise<DelegationEntityOption[]> { const { data } = await apiClient.get<DelegationEntityOption[]>('/hr/delegations/entities'); return data; },
  async getDetails(id: string): Promise<DelegationDetails> { const { data } = await apiClient.get<DelegationDetails>(`/hr/delegations/${id}`); return data; },
  async create(request: SaveDelegationRequest): Promise<DelegationDetails> { const { data } = await apiClient.post<DelegationDetails>('/hr/delegations', request); return data; },
  async update(id: string, request: SaveDelegationRequest): Promise<DelegationDetails> { const { data } = await apiClient.put<DelegationDetails>(`/hr/delegations/${id}`, request); return data; },
  async cancel(id: string, reason: string): Promise<DelegationDetails> { const { data } = await apiClient.post<DelegationDetails>(`/hr/delegations/${id}/cancel`, { reason }); return data; },
  print(id: string): Promise<ApiFile> { return requestApiFile(`/hr/delegations/${id}/print`); },
};
