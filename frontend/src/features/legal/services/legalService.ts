import { apiClient } from '../../../services/apiClient';
import type {
  LegalCaseDetails,
  LegalCasePage,
  LegalDashboard,
  LegalOrganization,
  RecordLegalActionInput,
  SaveLegalFileInput,
} from '../types/legal';

export const legalService = {
  async dashboard() {
    return (await apiClient.get<LegalDashboard>('/legal/dashboard')).data;
  },

  async organizations() {
    return (await apiClient.get<LegalOrganization[]>('/legal/organizations')).data;
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
};
