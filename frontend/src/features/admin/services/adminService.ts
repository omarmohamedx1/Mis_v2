import { apiClient } from '../../../services/apiClient';
import type { AdminAuditPage, AdminDashboard, AdminReferenceData, AdminUser, AdminUserList, CreateAdminUser, SaveUserAccess } from '../types/admin';

export const adminService = {
  async dashboard() { return (await apiClient.get<AdminDashboard>('/admin/dashboard')).data; },
  async referenceData() { return (await apiClient.get<AdminReferenceData>('/admin/reference-data')).data; },
  async users(params: { search?: string; department?: string; status?: string; page?: number; pageSize?: number }) { return (await apiClient.get<AdminUserList>('/admin/users', { params })).data; },
  async user(id: string) { return (await apiClient.get<AdminUser>(`/admin/users/${id}`)).data; },
  async createUser(payload: CreateAdminUser) { return (await apiClient.post<AdminUser>('/admin/users', payload)).data; },
  async saveAccess(id: string, payload: SaveUserAccess) { return (await apiClient.put<AdminUser>(`/admin/users/${id}/access`, payload)).data; },
  async setStatus(id: string, isActive: boolean) { return (await apiClient.patch<AdminUser>(`/admin/users/${id}/status`, { isActive })).data; },
  async resetPassword(id: string, temporaryPassword: string) { await apiClient.post(`/admin/users/${id}/reset-password`, { temporaryPassword }); },
  async audit(params: { search?: string; page?: number; pageSize?: number }) { return (await apiClient.get<AdminAuditPage>('/admin/audit', { params })).data; },
};
