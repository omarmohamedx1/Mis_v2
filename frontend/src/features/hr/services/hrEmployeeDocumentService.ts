import { apiClient, downloadApiFile, requestApiFile, requestFormData, type ApiFile } from '../../../services/apiClient';
import type { DocumentExpirySummary, EmployeeDocumentDetails, EmployeeDocumentQuery, EmployeePersonnelFile, PagedEmployeeDocuments, PagedEmployeePersonnelFiles, PersonnelFileQuery, PersonnelFileSummary, RequiredDocumentCode, SaveEmployeeDocumentMetadata } from '../types/document';

export const hrEmployeeDocumentService = {
  async getPaged(query: EmployeeDocumentQuery): Promise<PagedEmployeeDocuments> {
    const { data } = await apiClient.get<PagedEmployeeDocuments>('/hr/employee-documents', { params: {
      page: query.page, pageSize: query.pageSize, search: query.search || undefined, employeeId: query.employeeId || undefined,
      departmentId: query.departmentId || undefined, documentTypeId: query.documentTypeId || undefined,
      expiryStatus: query.expiryStatus, expiringWithinDays: query.expiringWithinDays, sortBy: query.sortBy, sortDirection: query.sortDirection,
    } });
    return data;
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
    const { data } = await apiClient.get<PagedEmployeePersonnelFiles>('/hr/employee-documents/personnel-files', { params: {
      page: query.page, pageSize: query.pageSize, search: query.search || undefined, employeeId: query.employeeId || undefined,
      departmentId: query.departmentId || undefined, positionId: query.positionId || undefined, gender: query.gender || undefined,
      completionStatus: query.completionStatus, missingDocumentCode: query.missingDocumentCode || undefined,
    } });
    return data;
  },
  async getPersonnelFile(employeeId: string): Promise<EmployeePersonnelFile> { const { data } = await apiClient.get<EmployeePersonnelFile>(`/hr/employee-documents/personnel-files/${employeeId}`); return data; },
  async getPersonnelFileSummary(): Promise<PersonnelFileSummary> { const { data } = await apiClient.get<PersonnelFileSummary>('/hr/employee-documents/personnel-files/summary'); return data; },
  async uploadRequired(employeeId: string, code: RequiredDocumentCode, file: File): Promise<EmployeeDocumentDetails> {
    const form = new FormData(); form.append('file', file);
    return requestFormData<EmployeeDocumentDetails>(`/hr/employee-documents/personnel-files/${employeeId}/${code}`, form);
  },
};
