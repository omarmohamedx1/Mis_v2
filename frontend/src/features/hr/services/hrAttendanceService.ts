import { apiClient } from '../../../services/apiClient';
import type { AttendanceDetails, AttendanceQuery, PagedAttendanceRecords, ProcessAttendanceDayResult, SaveManualAttendanceRequest } from '../types/attendance';
import { loadAllHrPages } from './hrPaging';

function attendanceParams(query: AttendanceQuery) {
  return {
    branchId: query.branchId || undefined,
    dateFrom: query.dateFrom || undefined,
    dateTo: query.dateTo || undefined,
    departmentId: query.departmentId || undefined,
    organizationId: query.organizationId || undefined,
    employeeId: query.employeeId || undefined,
    page: query.page,
    pageSize: query.pageSize,
    search: query.search || undefined,
    sortBy: query.sortBy || undefined,
    sortDescending: query.sortDescending || undefined,
    source: query.source || undefined,
    status: query.status || undefined,
  };
}

export const hrAttendanceService = {
  async getPaged(query: AttendanceQuery): Promise<PagedAttendanceRecords> {
    return loadAllHrPages(async (page, pageSize) => {
      const { data } = await apiClient.get<PagedAttendanceRecords>('/hr/attendance', { params: attendanceParams({ ...query, page, pageSize }) });
      return data;
    });
  },

  async getDetails(id: string): Promise<AttendanceDetails> {
    const { data } = await apiClient.get<AttendanceDetails>(`/hr/attendance/${id}`);
    return data;
  },

  async createManual(request: SaveManualAttendanceRequest): Promise<AttendanceDetails> {
    const { data } = await apiClient.post<AttendanceDetails>('/hr/attendance', request);
    return data;
  },

  async updateManual(id: string, request: SaveManualAttendanceRequest): Promise<AttendanceDetails> {
    const { data } = await apiClient.put<AttendanceDetails>(`/hr/attendance/${id}`, request);
    return data;
  },

  async deleteManual(id: string, reason?: string): Promise<void> {
    await apiClient.delete(`/hr/attendance/${id}`, { data: { reason: reason?.trim() || null } });
  },

  async processDay(attendanceDate: string, notes?: string): Promise<ProcessAttendanceDayResult> {
    const { data } = await apiClient.post<ProcessAttendanceDayResult>('/hr/attendance/process-day', {
      attendanceDate,
      notes: notes?.trim() || null,
    });
    return data;
  },
};
