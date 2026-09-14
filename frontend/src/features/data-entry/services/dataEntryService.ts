import { apiClient, requestFormData } from '../../../services/apiClient';
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
  DataEntryNotification,
  DataEntryOrganization,
  DataEntryPagedResult,
  DataEntryPortfolio,
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

  async clients(search?: string, page = 1, pageSize = 20) {
    return (await apiClient.get<DataEntryPagedResult<DataEntryClientListItem>>('/data-entry/clients', {
      params: { search: search || undefined, page, pageSize },
    })).data;
  },

  async client(customerId: string) {
    return (await apiClient.get<DataEntryClientDetails>(`/data-entry/clients/${customerId}`)).data;
  },

  async createClient(input: CreateDataEntryClientInput) {
    return (await apiClient.post<DataEntryClientDetails>('/data-entry/clients', input)).data;
  },

  async uploadImport(file: File) {
    const form = new FormData();
    form.append('file', file);
    return requestFormData<DataEntryImportUpload>('/data-entry/import/upload', form);
  },

  async previewImport(uploadId: string, mapping: DataEntryImportMappingRequest) {
    return (await apiClient.post<DataEntryImportPreview>(`/data-entry/import/${uploadId}/preview`, mapping)).data;
  },

  async confirmImport(input: ConfirmDataEntryImportInput) {
    return (await apiClient.post<DataEntryBatchListItem>('/data-entry/import/confirm', input)).data;
  },

  async myBatches(status?: string, page = 1, pageSize = 20) {
    return (await apiClient.get<DataEntryPagedResult<DataEntryBatchListItem>>('/data-entry/batches', {
      params: { status: status || undefined, page, pageSize },
    })).data;
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

  async supervisorBatches(status?: string, page = 1, pageSize = 20) {
    return (await apiClient.get<DataEntryPagedResult<DataEntryBatchListItem>>('/data-entry/supervisor/batches', {
      params: { status: status || undefined, page, pageSize },
    })).data;
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
};
