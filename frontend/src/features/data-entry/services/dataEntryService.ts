import { apiClient, downloadApiFile, requestApiFile, requestFormData } from '../../../services/apiClient';
import { loadAllPages } from '../../../utils/loadAllPages';
import type {
  ConfirmDataEntryImportInput,
  CreateDataEntryClientInput,
  DataEntryBatchDetails,
  DataEntryBatchListItem,
  DataEntryClientDetails,
  DataEntryClientListItem,
  DataEntryDashboard,
  DataEntryImportMappingRequest,
  DataEntryImportPreview,
  DataEntryImportUpload,
  DataEntrySheetPreview,
  DataEntryNotification,
  DataEntryOrganization,
  DataEntryPagedResult,
  DataEntryPortfolio,
  DataEntryDocument,
} from '../types/dataEntry';

export const dataEntryService = {
  async dashboard() {
    return (await apiClient.get<DataEntryDashboard>('/data-entry/dashboard')).data;
  },

  async organizations() {
    return (await apiClient.get<DataEntryOrganization[]>('/data-entry/organizations')).data;
  },

  async portfolios(organizationId: string) {
    return (await apiClient.get<DataEntryPortfolio[]>(`/data-entry/organizations/${organizationId}/portfolios`)).data;
  },

  async clients(search?: string) {
    return loadAllPages((page, pageSize) => apiClient.get<DataEntryPagedResult<DataEntryClientListItem>>('/data-entry/clients', {
      params: { search: search || undefined, page, pageSize },
    }).then((r) => r.data), 200);
  },

  async client(customerId: string) {
    return (await apiClient.get<DataEntryClientDetails>(`/data-entry/clients/${customerId}`)).data;
  },

  async createClient(input: CreateDataEntryClientInput) {
    return (await apiClient.post<DataEntryClientDetails>('/data-entry/clients', input)).data;
  },

  async deleteClient(customerId: string) {
    await apiClient.delete(`/data-entry/clients/${customerId}`);
  },

  async uploadImport(file: File) {
    const form = new FormData();
    form.append('file', file);
    return requestFormData<DataEntryImportUpload>('/data-entry/import/upload', form);
  },

  async sheetPreview(uploadId: string, sheetName?: string | null) {
    return (await apiClient.get<DataEntrySheetPreview>(`/data-entry/import/${uploadId}/sheet`, {
      params: { sheetName: sheetName || undefined },
    })).data;
  },

  async previewImport(uploadId: string, mapping: DataEntryImportMappingRequest) {
    return (await apiClient.post<DataEntryImportPreview>(`/data-entry/import/${uploadId}/preview`, mapping)).data;
  },

  async confirmImport(input: ConfirmDataEntryImportInput) {
    return (await apiClient.post<DataEntryBatchListItem>('/data-entry/import/confirm', input)).data;
  },

  async myBatches(status?: string) {
    return loadAllPages((page, pageSize) => apiClient.get<DataEntryPagedResult<DataEntryBatchListItem>>('/data-entry/batches', {
      params: { status: status || undefined, page, pageSize },
    }).then((r) => r.data), 200);
  },

  async batch(batchId: string) {
    return (await apiClient.get<DataEntryBatchDetails>(`/data-entry/batches/${batchId}`)).data;
  },

  async notifications() {
    return (await apiClient.get<DataEntryNotification[]>('/data-entry/notifications')).data;
  },

  async markNotificationRead(notificationId: string) {
    await apiClient.post(`/data-entry/notifications/${notificationId}/read`);
  },

  async supervisorBatches(status?: string) {
    return loadAllPages((page, pageSize) => apiClient.get<DataEntryPagedResult<DataEntryBatchListItem>>('/data-entry/supervisor/batches', {
      params: { status: status || undefined, page, pageSize },
    }).then((r) => r.data), 200);
  },

  async supervisorBatch(batchId: string) {
    return (await apiClient.get<DataEntryBatchDetails>(`/data-entry/supervisor/batches/${batchId}`)).data;
  },

  async acceptBatch(batchId: string) {
    return (await apiClient.post<DataEntryBatchListItem>(`/data-entry/supervisor/batches/${batchId}/accept`)).data;
  },

  async rejectBatch(batchId: string, reason: string) {
    return (await apiClient.post<DataEntryBatchListItem>(`/data-entry/supervisor/batches/${batchId}/reject`, { reason })).data;
  },

  async sendToDistribution(batchId: string) {
    return (await apiClient.post<DataEntryBatchListItem>(`/data-entry/supervisor/batches/${batchId}/send-to-distribution`)).data;
  },

  async clientDocuments(customerId: string) {
    return (await apiClient.get<DataEntryDocument[]>(`/data-entry/clients/${customerId}/documents`)).data;
  },

  async uploadClientDocument(customerId: string, file: File, note?: string) {
    const form = new FormData();
    form.append('file', file);
    if (note?.trim()) form.append('note', note.trim());
    return requestFormData<DataEntryDocument>(`/data-entry/clients/${customerId}/documents`, form);
  },

  async caseDocuments(caseId: string) {
    return (await apiClient.get<DataEntryDocument[]>(`/data-entry/cases/${caseId}/documents`)).data;
  },

  async openDocument(documentId: string) {
    return requestApiFile(`/data-entry/documents/${documentId}/download`);
  },

  async downloadDocument(documentId: string, fileName: string) {
    return downloadApiFile(`/data-entry/documents/${documentId}/download`, fileName);
  },

  async deleteDocument(documentId: string) {
    await apiClient.delete(`/data-entry/documents/${documentId}`);
  },
};
