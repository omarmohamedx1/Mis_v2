import { apiClient, requestFormData } from '../../../services/apiClient';
import type { AttendanceImportSheet } from '../types/attendance';

export interface SocialInsuranceImportMapping { sheetName: string | null; headerRow: number; firstDataRow: number; dateFormat: string | null; columns: Record<string, string> }
export interface SocialInsuranceImportUpload { id: string; fileName: string; sheets: AttendanceImportSheet[] }
export interface SocialInsuranceImportRow { row: number; record: { socialInsuranceNumber: string; insuranceStartDate: string | null; insuranceEndDate: string | null; insurableSalary: number; insuranceStatus: string }; employeeNumber: string; employeeName: string; sourceEmployeeName: string; status: 'Ready' | 'Warning' | 'Error' | 'Existing'; errors: string[] }
export interface SocialInsuranceImportPreview { id: string; previewId: string; rows: SocialInsuranceImportRow[] }
export interface SocialInsuranceImportResult { imported: number; skipped: number; failed: number }
export interface SocialInsuranceImportHistory { id: string; fileName: string; uploadedBy: string; uploadedAt: string; totalRows: number | null; result: SocialInsuranceImportResult | null }
const base = '/hr/social-insurance/imports';
export const socialInsuranceImportService = {
  async downloadTemplate() {
    const response = await apiClient.get<Blob>(`${base}/template`, { responseType: 'blob' });
    const url = URL.createObjectURL(response.data);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'Social_Insurance_Import_Template.xlsx';
    link.click();
    URL.revokeObjectURL(url);
  },
  upload(file: File) { const form = new FormData(); form.append('file', file); return requestFormData<SocialInsuranceImportUpload>(base, form); },
  async preview(id: string, mapping: SocialInsuranceImportMapping) { return (await apiClient.post<SocialInsuranceImportPreview>(`${base}/${id}/preview`, mapping)).data; },
  async confirm(id: string, previewId: string) { return (await apiClient.post<SocialInsuranceImportResult>(`${base}/${id}/confirm`, { previewId })).data; },
  async history() { return (await apiClient.get<SocialInsuranceImportHistory[]>(base)).data; },
};
