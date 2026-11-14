import { apiClient, downloadApiFile, requestFormData } from '../../../services/apiClient';
import type {
  LegalCaseDetails,
  LegalCasePage,
  LegalDashboard,
  LegalClientDetails,
  LegalClientDocument,
  LegalClientImportPreview,
  LegalClientWorkbook,
  LegalClientColumnMapping,
  LegalClientImportResult,
  LegalClientImportSourceRow,
  LegalClientPage,
  LegalImportHistoryItem,
  LegalImportPreview,
  LegalImportResult,
  LegalImportSourceRow,
  LegalOrganization,
  RecordLegalActionInput,
  SaveLegalFileInput,
} from '../types/legal';

export const legalService = {
  async dashboard(): Promise<LegalDashboard> {
    const response = (await apiClient.get<(Omit<LegalDashboard, 'recentlyAdded'> & {
      recentlyAdded?: LegalDashboard['recentlyAdded'] | null;
    }) | null>('/legal/dashboard')).data;

    if (
      !response
      || typeof response.totalClients !== 'number'
      || typeof response.recentlyAddedClients !== 'number'
      || typeof response.transferredFromCollections !== 'number'
      || typeof response.recentlyUpdatedClientFiles !== 'number'
      || typeof response.canManage !== 'boolean'
    ) {
      throw new Error('Invalid Legal dashboard response.');
    }

    return {
      ...response,
      recentlyAdded: Array.isArray(response.recentlyAdded) ? response.recentlyAdded : [],
    };
  },

  async organizations() {
    return (await apiClient.get<LegalOrganization[]>('/legal/organizations')).data;
  },

  async clients(params: { search?: string; page?: number; pageSize?: number }) {
    return (await apiClient.get<LegalClientPage>('/legal/clients', {
      params: {
        search: params.search || undefined,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
      },
    })).data;
  },

  async client(customerId: string) {
    return (await apiClient.get<LegalClientDetails>(`/legal/clients/${customerId}`)).data;
  },

  async updateClient(customerId: string, input: { status: string; notes: string }) {
    return (await apiClient.put<LegalClientDetails>(`/legal/clients/${customerId}`, input)).data;
  },

  async uploadClientDocument(customerId: string, file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return requestFormData<LegalClientDocument>(`/legal/clients/${customerId}/documents`, formData);
  },

  async downloadClientDocument(documentId: string) {
    return downloadApiFile(`/legal/client-documents/${documentId}`, 'legal-client-document');
  },

  async deleteClientDocument(documentId: string) {
    await apiClient.delete(`/legal/client-documents/${documentId}`);
  },

  async inspectClientImport(file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return requestFormData<LegalClientWorkbook>('/legal/client-imports/inspect', formData);
  },

  async previewClientImport(file: File, organizationId: string, mapping?: LegalClientColumnMapping) {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('organizationId', organizationId);
    if (mapping) formData.append('mapping', JSON.stringify(mapping));
    return requestFormData<LegalClientImportPreview>('/legal/client-imports/preview', formData);
  },

  async importClients(fileName: string, organizationId: string, rows: LegalClientImportSourceRow[]) {
    return (await apiClient.post<LegalClientImportResult>('/legal/client-imports', { fileName, organizationId, rows })).data;
  },

  async cases(params: { search?: string; organizationId?: string; stage?: string; page?: number; pageSize?: number }) {
    return (await apiClient.get<LegalCasePage>('/legal/cases', {
      params: {
        search: params.search || undefined,
        organizationId: params.organizationId || undefined,
        stage: params.stage || undefined,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
      },
    })).data;
  },

  async case(collectionCaseId: string) {
    return (await apiClient.get<LegalCaseDetails>(`/legal/cases/${collectionCaseId}`)).data;
  },

  async saveFile(collectionCaseId: string, input: SaveLegalFileInput) {
    return (await apiClient.put<LegalCaseDetails>(`/legal/cases/${collectionCaseId}/file`, input)).data;
  },

  async recordAction(collectionCaseId: string, input: RecordLegalActionInput) {
    return (await apiClient.post<LegalCaseDetails>(`/legal/cases/${collectionCaseId}/actions`, input)).data;
  },

  async downloadImportTemplate() {
    const response = await apiClient.get<Blob>('/legal/imports/template', { responseType: 'blob' });
    const url = URL.createObjectURL(response.data);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'Legal_Cases_Import_Template.xlsx';
    link.click();
    URL.revokeObjectURL(url);
  },

  async previewImport(file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return requestFormData<LegalImportPreview>('/legal/imports/preview', formData);
  },

  async importCases(fileName: string, rows: LegalImportSourceRow[]) {
    return (await apiClient.post<LegalImportResult>('/legal/imports', { fileName, rows })).data;
  },

  async importHistory() {
    return (await apiClient.get<LegalImportHistoryItem[]>('/legal/imports/history')).data;
  },
};
