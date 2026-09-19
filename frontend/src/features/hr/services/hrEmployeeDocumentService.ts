import { apiClient, downloadApiFile, requestApiFile, requestFormData, type ApiFile } from '../../../services/apiClient';
import type { DocumentExpirySummary, EmployeeDocumentDetails, EmployeeDocumentQuery, EmployeePersonnelFile, PagedEmployeeDocuments, PagedEmployeePersonnelFiles, PersonnelFileQuery, PersonnelFileSummary, RequiredDocumentCode, SaveEmployeeDocumentMetadata } from '../types/document';
import { loadAllHrPages } from './hrPaging';

export type EmployeeDocumentBulkStatus =
  | 'Ready'
  | 'EmployeeNotFound'
  | 'AmbiguousEmployee'
  | 'DocumentTypeNotRecognized'
  | 'DocumentAlreadyExists'
  | 'Error';

export interface EmployeeDocumentBulkItem {
  fileId: string;
  fileName: string;
  length: number;
  contentType: string | null;
  employeeId: string | null;
  employeeNumber: string | null;
  employeeName: string | null;
  documentCode: RequiredDocumentCode | null;
  documentName: string | null;
  status: EmployeeDocumentBulkStatus | string;
  duplicateAction: 'Skip' | 'Replace' | string;
  errors: string[];
}

export interface EmployeeDocumentBulkUpload {
  id: string;
  items: EmployeeDocumentBulkItem[];
}

export interface EmployeeDocumentBulkCorrection {
  fileId: string;
  employeeId?: string | null;
  documentCode?: string | null;
  duplicateAction: 'Skip' | 'Replace';
}

export interface EmployeeDocumentBulkResult {
  uploaded: number;
  replaced: number;
  skipped: number;
  failed: number;
}

export const hrEmployeeDocumentService = {
  async getPaged(query: EmployeeDocumentQuery): Promise<PagedEmployeeDocuments> {
    return loadAllHrPages(async (page, pageSize) => { const { data } = await apiClient.get<PagedEmployeeDocuments>('/hr/employee-documents', { params: {
      page, pageSize, search: query.search || undefined, employeeId: query.employeeId || undefined,
      departmentId: query.departmentId || undefined, documentTypeId: query.documentTypeId || undefined,
      expiryStatus: query.expiryStatus, expiringWithinDays: query.expiringWithinDays, sortBy: query.sortBy, sortDirection: query.sortDirection,
    } });
    return data; });
  },
  async getSummary(): Promise<DocumentExpirySummary> { const { data } = await apiClient.get<DocumentExpirySummary>('/hr/employee-documents/expiry-summary'); return data; },
  async getDetails(id: string): Promise<EmployeeDocumentDetails> { const { data } = await apiClient.get<EmployeeDocumentDetails>(`/hr/employee-documents/${id}`); return data; },
  async create(employeeId: string, metadata: SaveEmployeeDocumentMetadata, file: File): Promise<EmployeeDocumentDetails> {
    const form = new FormData(); form.append('employeeId', employeeId); form.append('documentTypeId', metadata.documentTypeId); form.append('file', file);
    if (metadata.issueDate) form.append('issueDate', metadata.issueDate); if (metadata.expiryDate) form.append('expiryDate', metadata.expiryDate); if (metadata.notes) form.append('notes', metadata.notes);
    return requestFormData<EmployeeDocumentDetails>('/hr/employee-documents', form);
  },
  async update(id: string, metadata: SaveEmployeeDocumentMetadata): Promise<EmployeeDocumentDetails> { const { data } = await apiClient.put<EmployeeDocumentDetails>(`/hr/employee-documents/${id}`, metadata); return data; },
  async replace(id: string, file: File): Promise<EmployeeDocumentDetails> { const form = new FormData(); form.append('file', file); return requestFormData<EmployeeDocumentDetails>(`/hr/employee-documents/${id}/file`, form, { method: 'put' }); },
  async download(id: string, fileName: string): Promise<void> { await downloadApiFile(`/hr/employee-documents/${id}/download`, fileName); },
  preview(id: string): Promise<ApiFile> { return requestApiFile(`/hr/employee-documents/${id}/preview`); },
  async delete(id: string, reason: string | null): Promise<void> { await apiClient.delete(`/hr/employee-documents/${id}`, { data: { reason } }); },
  async getPersonnelFiles(query: PersonnelFileQuery): Promise<PagedEmployeePersonnelFiles> {
    return loadAllHrPages(async (page, pageSize) => { const { data } = await apiClient.get<PagedEmployeePersonnelFiles>('/hr/employee-documents/personnel-files', { params: {
      page, pageSize, search: query.search || undefined, employeeId: query.employeeId || undefined,
      departmentId: query.departmentId || undefined, organizationId: query.organizationId || undefined, positionId: query.positionId || undefined, gender: query.gender || undefined,
      completionStatus: query.completionStatus, missingDocumentCode: query.missingDocumentCode || undefined,
    } });
    return data; });
  },
  async getPersonnelFile(employeeId: string): Promise<EmployeePersonnelFile> { const { data } = await apiClient.get<EmployeePersonnelFile>(`/hr/employee-documents/personnel-files/${employeeId}`); return data; },
  async getPersonnelFileSummary(): Promise<PersonnelFileSummary> { const { data } = await apiClient.get<PersonnelFileSummary>('/hr/employee-documents/personnel-files/summary'); return data; },
  async uploadRequired(employeeId: string, code: RequiredDocumentCode, file: File): Promise<EmployeeDocumentDetails> {
    const form = new FormData(); form.append('file', file);
    return requestFormData<EmployeeDocumentDetails>(`/hr/employee-documents/personnel-files/${employeeId}/${code}`, form);
  },
  async bulkUpload(files: File[]): Promise<EmployeeDocumentBulkUpload> {
    const form = new FormData();
    files.forEach((file) => form.append('files', file));
    return requestFormData<EmployeeDocumentBulkUpload>('/hr/employee-documents/bulk/upload', form);
  },
  async bulkPreview(id: string, items: EmployeeDocumentBulkCorrection[]): Promise<EmployeeDocumentBulkUpload> {
    const { data } = await apiClient.put<EmployeeDocumentBulkUpload>(`/hr/employee-documents/bulk/${id}/preview`, { items });
    return data;
  },
  async bulkConfirm(id: string, items: EmployeeDocumentBulkCorrection[]): Promise<EmployeeDocumentBulkResult> {
    const { data } = await apiClient.post<EmployeeDocumentBulkResult>(`/hr/employee-documents/bulk/${id}/confirm`, { items });
    return data;
  },
};
