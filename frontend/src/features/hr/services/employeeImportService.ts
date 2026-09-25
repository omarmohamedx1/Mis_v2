import { apiClient, requestFormData } from '../../../services/apiClient';
import type { AttendanceImportSheet } from '../types/attendance';

export interface EmployeeImportMapping { sheetName: string | null; headerRow: number; firstDataRow: number; dateFormat: string | null; columns: Record<string, string>; sheetNames?: string[] }
export interface EmployeeImportUpload { id: string; fileName: string; sheets: AttendanceImportSheet[] }
export interface EmployeeImportEmployee {
  employeeNumber: string;
  fullName: string;
  fullNameArabic?: string | null;
  fullNameEnglish?: string | null;
  gender: string | null;
  nationalId: string;
  mobileNumber: string | null;
  dateOfBirth: string | null;
  fingerprintEnrollmentDate: string | null;
  workStartDate: string | null;
  workEndDate: string | null;
  status: string | null;
  isActive: boolean;
  departmentId?: string;
  positionId?: string | null;
  organizationIds?: string[];
  operationalRole?: string | null;
  address?: string | null;
  basicSalary?: number | null;
  allowances?: number | null;
  workNumber?: string | null;
  packageType?: string | null;
}
export interface EmployeeImportRow {
  row: number;
  employee: EmployeeImportEmployee;
  department: string;
  position: string;
  organization?: string | null;
  sourceDepartment?: string | null;
  sourcePosition?: string | null;
  status: 'Ready' | 'Warning' | 'Error' | 'Existing';
  errors: string[];
}
export interface EmployeeImportPreview { id: string; previewId: string; rows: EmployeeImportRow[] }
export interface EmployeeImportResult { imported: number; skipped: number; failed: number }
export interface EmployeeImportHistory { id: string; fileName: string; uploadedBy: string; uploadedAt: string; totalRows: number | null; result: EmployeeImportResult | null }
const base = '/hr/employees/imports';
export const employeeImportService = {
  async downloadTemplate() {
    const response = await apiClient.get<Blob>(`${base}/template`, { responseType: 'blob' });
    const url = URL.createObjectURL(response.data);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'Employee_Import_Template.xlsx';
    link.click();
    URL.revokeObjectURL(url);
  },
  upload(file: File) { const form = new FormData(); form.append('file', file); return requestFormData<EmployeeImportUpload>(base, form); },
  async preview(id: string, mapping: EmployeeImportMapping) { return (await apiClient.post<EmployeeImportPreview>(`${base}/${id}/preview`, mapping)).data; },
  async revise(id: string, previewId: string, rows: Array<{ row: number; employee: EmployeeImportEmployee }>) {
    return (await apiClient.post<EmployeeImportPreview>(`${base}/${id}/revise`, { previewId, rows })).data;
  },
  async confirm(id: string, previewId: string, excludedRows?: number[]) { return (await apiClient.post<EmployeeImportResult>(`${base}/${id}/confirm`, { previewId, excludedRows })).data; },
  async history() { return (await apiClient.get<EmployeeImportHistory[]>(base)).data; },
  async deleteHistory(id: string) { await apiClient.delete(`${base}/${id}`); },
};
