import { apiClient } from '../../../services/apiClient';
import type { DepartmentOption, EmployeeDetails, EmployeeIdentityAvailability, EmployeeOrganizationAssignment, PagedEmployees, SaveEmployeeRequest } from '../types/employee';
import { loadAllHrPages } from './hrPaging';

export type EmployeeSearchField = 'identity' | 'employeeNumber' | 'name' | 'nationalId' | 'mobile' | 'organization' | 'position';
export interface EmployeeQuery { page: number; pageSize: number; search: string; searchField?: EmployeeSearchField; departmentId: string; organizationId?: string; positionId?: string; status: string; role?: string; gender?: string; archived?: boolean; includeInactive?: boolean; all?: boolean; }

export const hrEmployeeService = {
  async getEmployees(query: EmployeeQuery): Promise<PagedEmployees> {
    const loadPage = async (page: number, pageSize: number) => {
      const { data } = await apiClient.get<PagedEmployees>('/hr/employees', { params: { page, pageSize, search: query.search || undefined, searchField: query.searchField || undefined, departmentId: query.departmentId || undefined, organizationId: query.organizationId || undefined, positionId: query.positionId || undefined, status: query.includeInactive ? 'all' : query.status, role: query.role || undefined, gender: query.gender || undefined, archived: query.archived ?? false } });
      return data;
    };
    return query.all ? loadAllHrPages(loadPage) : loadPage(query.page, query.pageSize);
  },
  async getEmployee(id: string): Promise<EmployeeDetails> { const { data } = await apiClient.get<EmployeeDetails>(`/hr/employees/${id}`); return data; },
  async getDepartments(): Promise<DepartmentOption[]> { const { data } = await apiClient.get<DepartmentOption[]>('/hr/departments'); return data; },
  async getOrganizations(): Promise<EmployeeOrganizationAssignment[]> { const { data } = await apiClient.get<EmployeeOrganizationAssignment[]>('/hr/employees/organizations'); return data; },
  async createEmployee(request: SaveEmployeeRequest): Promise<EmployeeDetails> { const { data } = await apiClient.post<EmployeeDetails>('/hr/employees', request); return data; },
  async updateEmployee(id: string, request: SaveEmployeeRequest): Promise<EmployeeDetails> { const { data } = await apiClient.put<EmployeeDetails>(`/hr/employees/${id}`, request); return data; },
  async checkIdentityAvailability(query: { employeeNumber?: string; nationalId?: string; excludingId?: string }): Promise<EmployeeIdentityAvailability> {
    const { data } = await apiClient.get<EmployeeIdentityAvailability>('/hr/employees/identity-availability', { params: { employeeNumber: query.employeeNumber || undefined, nationalId: query.nationalId || undefined, excludingId: query.excludingId || undefined } });
    return data;
  },
  async archiveEmployee(id: string, reason: string): Promise<EmployeeDetails> { const { data } = await apiClient.post<EmployeeDetails>(`/hr/employees/${id}/archive`, { reason }); return data; },
  async restoreEmployee(id: string): Promise<EmployeeDetails> { const { data } = await apiClient.post<EmployeeDetails>(`/hr/employees/${id}/restore`); return data; },
  async deleteEmployee(id: string): Promise<void> { await apiClient.delete(`/hr/employees/${id}`); },
  async removeEmployees(ids: string[], keepData: boolean, reason?: string): Promise<{ kept: number; deleted: number; skipped: number }> {
    const { data } = await apiClient.post<{ kept: number; deleted: number; skipped: number }>('/hr/employees/remove', { ids, keepData, reason });
    return data;
  },
};
