import { apiClient, requestFormData } from '../../../services/apiClient';
import type { AttendanceImportSheet } from '../types/attendance';

export interface EmployeeImportMapping { sheetName: string | null; headerRow: number; firstDataRow: number; dateFormat: string | null; columns: Record<string, string> }
export interface EmployeeImportUpload { id: string; fileName: string; sheets: AttendanceImportSheet[] }
export interface EmployeeImportRow { row: number; employee: { employeeNumber: string; fullName: string; gender: string | null; nationalId: string; mobileNumber: string | null; workStartDate: string | null; workEndDate: string | null; status: string | null; isActive: boolean }; department: string; position: string; status: 'Ready' | 'Warning' | 'Error' | 'Existing'; errors: string[] }
export interface EmployeeImportPreview { id: string; previewId: string; rows: EmployeeImportRow[] }
export interface EmployeeImportResult { imported: number; skipped: number; failed: number }
export interface EmployeeImportHistory { id: string; fileName: string; uploadedBy: string; uploadedAt: string; totalRows: number | null; result: EmployeeImportResult | null }
const base = '/hr/employees/imports';
export const employeeImportService = {
  upload(file: File) { const form = new FormData(); form.append('file', file); return requestFormData<EmployeeImportUpload>(base, form); },
  async preview(id: string, mapping: EmployeeImportMapping) { return (await apiClient.post<EmployeeImportPreview>(`${base}/${id}/preview`, mapping)).data; },
  async confirm(id: string, previewId: string) { return (await apiClient.post<EmployeeImportResult>(`${base}/${id}/confirm`, { previewId })).data; },
  async history() { return (await apiClient.get<EmployeeImportHistory[]>(base)).data; },
};
