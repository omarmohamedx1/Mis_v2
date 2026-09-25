import { apiClient, requestFormData } from '../../../services/apiClient';
import type { AttendanceImportSheet } from '../types/attendance';

export interface AbsenceImportMapping {
  sheetName: string | null;
  headerRow: number;
  firstDataRow: number;
  dateFormat: string | null;
  columns: Record<string, string>;
  sheetNames?: string[];
}

export interface AbsenceImportUpload {
  id: string;
  fileName: string;
  sheets: AttendanceImportSheet[];
}

export interface AbsenceImportRow {
  row: number;
  record: {
    employeeId: string;
    absenceDate: string;
    type: string;
    status: string;
    reason: string | null;
    notes: string | null;
  };
  employeeNumber: string;
  employeeName: string;
  mobileNumber: string | null;
  sourceEmployeeName: string;
  status: 'Ready' | 'Warning' | 'Error' | 'Existing';
  errors: string[];
}

export interface AbsenceImportPreview {
  id: string;
  previewId: string;
  rows: AbsenceImportRow[];
}

export interface AbsenceImportResult {
  imported: number;
  skipped: number;
  failed: number;
}

export interface AbsenceImportHistory {
  id: string;
  fileName: string;
  uploadedBy: string;
  uploadedAt: string;
  totalRows: number | null;
  result: AbsenceImportResult | null;
}

const base = '/hr/absences/imports';

export const absenceImportService = {
  async downloadTemplate() {
    const response = await apiClient.get<Blob>(`${base}/template`, { responseType: 'blob' });
    const url = URL.createObjectURL(response.data);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'Absence_Import_Template.xlsx';
    link.click();
    URL.revokeObjectURL(url);
  },
  upload(file: File) {
    const form = new FormData();
    form.append('file', file);
    return requestFormData<AbsenceImportUpload>(base, form);
  },
  async preview(id: string, mapping: AbsenceImportMapping) {
    return (await apiClient.post<AbsenceImportPreview>(`${base}/${id}/preview`, mapping)).data;
  },
  async confirm(id: string, previewId: string, excludedRows?: number[]) {
    return (await apiClient.post<AbsenceImportResult>(`${base}/${id}/confirm`, { previewId, excludedRows })).data;
  },
  async history() {
    return (await apiClient.get<AbsenceImportHistory[]>(base)).data;
  },
};
