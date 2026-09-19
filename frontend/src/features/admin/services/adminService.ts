import { apiClient } from '../../../services/apiClient';
import { loadAllPages, withoutPaging } from '../../../utils/loadAllPages';
import type { AdminAuditPage, AdminCredentialIssue, AdminDashboard, AdminLinkableEmployee, AdminReferenceData, AdminUser, AdminUserList, CreateAdminUser, SaveUserAccess } from '../types/admin';

export const adminService = {
  async dashboard() { return (await apiClient.get<AdminDashboard>('/admin/dashboard')).data; },
  async referenceData() { return (await apiClient.get<AdminReferenceData>('/admin/reference-data')).data; },
  async users(params: { search?: string; department?: string; status?: string; page?: number; pageSize?: number }) { return loadAllPages((page, pageSize) => apiClient.get<AdminUserList>('/admin/users', { params: { ...withoutPaging(params), page, pageSize } }).then(r => r.data), 100); },
  async user(id: string) { return (await apiClient.get<AdminUser>(`/admin/users/${id}`)).data; },
  async createUser(payload: CreateAdminUser) { return (await apiClient.post<AdminCredentialIssue>('/admin/users', payload)).data; },
  async linkableEmployees(params?: { search?: string; includeEmployeeId?: string }) { return (await apiClient.get<AdminLinkableEmployee[]>('/admin/employees', { params })).data; },
  async linkEmployee(id: string, employeeId?: string | null) { return (await apiClient.put<AdminUser>(`/admin/users/${id}/employee`, { employeeId: employeeId || null })).data; },
  async saveAccess(id: string, payload: SaveUserAccess) { return (await apiClient.put<AdminUser>(`/admin/users/${id}/access`, payload)).data; },
  async setStatus(id: string, isActive: boolean) { return (await apiClient.patch<AdminUser>(`/admin/users/${id}/status`, { isActive })).data; },
  async deleteUser(id: string) { await apiClient.delete(`/admin/users/${id}`); },
  async resetPassword(id: string, temporaryPassword = '') { return (await apiClient.post<AdminCredentialIssue>(`/admin/users/${id}/reset-password`, { temporaryPassword })).data; },
  async audit(params: { search?: string; page?: number; pageSize?: number }) { return (await apiClient.get<AdminAuditPage>('/admin/audit', { params })).data; },
};
