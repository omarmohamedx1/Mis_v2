import { apiClient } from '../../../services/apiClient';
import type {
  LeaveBalance,
  LeaveBalanceQuery,
  LeaveEntitlement,
  LeaveImportReview,
  LeaveImportResult,
  LeaveRequestDetails,
  LeaveRequestQuery,
  PagedLeaveBalances,
  PagedLeaveRequests,
  SaveLeaveRequest,
  UpsertLeaveEntitlementRequest,
} from '../types/leave';
import { loadAllHrPages } from './hrPaging';

export const hrLeaveService = {
  async reviewImport(file: File): Promise<LeaveImportReview> {
    const body = new FormData(); body.append('file', file);
    const { data } = await apiClient.post<LeaveImportReview>('/hr/leaves/imports/review', body);
    return data;
  },

  async confirmImport(importId: string): Promise<LeaveImportResult> {
    const { data } = await apiClient.post<LeaveImportResult>(`/hr/leaves/imports/${importId}/confirm`);
    return data;
  },

  async downloadImportTemplate(): Promise<void> {
    const response = await apiClient.get<Blob>('/hr/leaves/imports/template', { responseType: 'blob' });
    const url = URL.createObjectURL(response.data); const link = document.createElement('a');
    link.href = url; link.download = 'Leave_Import_Template.xlsx'; link.click(); URL.revokeObjectURL(url);
  },
  async getPaged(query: LeaveRequestQuery): Promise<PagedLeaveRequests> {
    return loadAllHrPages(async (page, pageSize) => {
      const { data } = await apiClient.get<PagedLeaveRequests>('/hr/leaves', {
      params: {
        branchId: query.branchId || undefined,
        dateFrom: query.dateFrom || undefined,
        dateTo: query.dateTo || undefined,
        departmentId: query.departmentId || undefined,
        employeeId: query.employeeId || undefined,
        leaveTypeId: query.leaveTypeId || undefined,
        page,
        pageSize,
        search: query.search || undefined,
        sortBy: query.sortBy || undefined,
        sortDescending: query.sortDescending || undefined,
        status: query.status || undefined,
      },
    });
      return data;
    });
  },

  async getDetails(id: string): Promise<LeaveRequestDetails> {
    const { data } = await apiClient.get<LeaveRequestDetails>(`/hr/leaves/${id}`);
    return data;
  },

  async create(request: SaveLeaveRequest): Promise<LeaveRequestDetails> {
    const { data } = await apiClient.post<LeaveRequestDetails>('/hr/leaves', request);
    return data;
  },

  async update(id: string, request: SaveLeaveRequest): Promise<LeaveRequestDetails> {
    const { data } = await apiClient.put<LeaveRequestDetails>(`/hr/leaves/${id}`, request);
    return data;
  },

  async approve(id: string, notes?: string): Promise<LeaveRequestDetails> {
    const { data } = await apiClient.post<LeaveRequestDetails>(`/hr/leaves/${id}/approve`, { notes: notes?.trim() || null });
    return data;
  },

  async reject(id: string, reason: string): Promise<LeaveRequestDetails> {
    const { data } = await apiClient.post<LeaveRequestDetails>(`/hr/leaves/${id}/reject`, { reason: reason.trim() });
    return data;
  },

  async cancel(id: string, reason: string): Promise<LeaveRequestDetails> {
    const { data } = await apiClient.post<LeaveRequestDetails>(`/hr/leaves/${id}/cancel`, { reason: reason.trim() });
    return data;
  },

  async getBalances(query: LeaveBalanceQuery): Promise<PagedLeaveBalances> {
    return loadAllHrPages(async (page, pageSize) => {
      const { data } = await apiClient.get<PagedLeaveBalances>('/hr/leaves/balances', {
      params: {
        branchId: query.branchId || undefined,
        departmentId: query.departmentId || undefined,
        employeeId: query.employeeId || undefined,
        leaveTypeId: query.leaveTypeId || undefined,
        page,
        pageSize,
        search: query.search || undefined,
        year: query.year,
      },
    });
      return data;
    });
  },

  async getEmployeeBalances(employeeId: string, year: number): Promise<LeaveBalance[]> {
    const { data } = await apiClient.get<LeaveBalance[]>(`/hr/leaves/employees/${employeeId}/balances`, { params: { year } });
    return data;
  },

  async upsertEntitlement(employeeId: string, leaveTypeId: string, year: number, request: UpsertLeaveEntitlementRequest): Promise<LeaveEntitlement> {
    const { data } = await apiClient.put<LeaveEntitlement>(`/hr/leaves/employees/${employeeId}/entitlements/${leaveTypeId}/${year}`, request);
    return data;
  },
};

