import { apiClient, requestFormData } from '../../../services/apiClient';
import type {
  AttendanceImportBatch,
  AttendanceImportConfirmResult,
  AttendanceImportHistoryQuery,
  AttendanceImportMappingRequest,
  AttendanceImportPreviewQuery,
  AttendanceImportUpload,
  PagedAttendanceImportHistory,
  PagedAttendanceImportPreview,
} from '../types/attendance';
import { loadAllHrPages } from './hrPaging';

export const hrAttendanceImportService = {
  async downloadTemplate() {
    const response = await apiClient.get<Blob>('/hr/attendance/imports/template', { responseType: 'blob' });
    const url = URL.createObjectURL(response.data);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'Attendance_Import_Template.xlsx';
    link.click();
    URL.revokeObjectURL(url);
  },

  async upload(file: File): Promise<AttendanceImportUpload> {
    const formData = new FormData();
    formData.append('file', file);
    return requestFormData<AttendanceImportUpload>('/hr/attendance/imports', formData);
  },

  async buildPreview(batchId: string, request: AttendanceImportMappingRequest): Promise<AttendanceImportBatch> {
    const { data } = await apiClient.post<AttendanceImportBatch>(`/hr/attendance/imports/${batchId}/preview`, request);
    return data;
  },

  async getBatch(batchId: string): Promise<AttendanceImportBatch> {
    const { data } = await apiClient.get<AttendanceImportBatch>(`/hr/attendance/imports/${batchId}`);
    return data;
  },

  async getPreview(batchId: string, query: AttendanceImportPreviewQuery): Promise<PagedAttendanceImportPreview> {
    return loadAllHrPages(async (page, pageSize) => {
      const { data } = await apiClient.get<PagedAttendanceImportPreview>(`/hr/attendance/imports/${batchId}/preview`, {
      params: {
        category: query.category || undefined,
        page,
        pageSize,
        search: query.search || undefined,
      },
    });
      return data;
    });
  },

  async confirm(batchId: string, includeRowsWithWarnings: boolean, notes?: string): Promise<AttendanceImportConfirmResult> {
    const { data } = await apiClient.post<AttendanceImportConfirmResult>(`/hr/attendance/imports/${batchId}/confirm`, {
      includeRowsWithWarnings,
      notes: notes?.trim() || null,
    });
    return data;
  },

  async cancel(batchId: string, notes?: string): Promise<AttendanceImportBatch> {
    const { data } = await apiClient.post<AttendanceImportBatch>(`/hr/attendance/imports/${batchId}/cancel`, {
      notes: notes?.trim() || null,
    });
    return data;
  },

  async getHistory(query: AttendanceImportHistoryQuery): Promise<PagedAttendanceImportHistory> {
    return loadAllHrPages(async (page, pageSize) => {
      const { data } = await apiClient.get<PagedAttendanceImportHistory>('/hr/attendance/imports', {
      params: {
        page,
        pageSize,
        search: query.search || undefined,
        status: query.status || undefined,
        uploadedFrom: query.uploadedFrom || undefined,
        uploadedTo: query.uploadedTo || undefined,
      },
    });
      return data;
    });
  },
};
