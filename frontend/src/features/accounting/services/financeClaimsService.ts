import { apiClient, downloadApiFile, requestFormData } from '../../../services/apiClient';
import type { Claim, ClaimDetails, ClaimFilter, ClaimImportPreview, ClaimImportResult, ClaimInput, ClaimOrganization, ClaimPage } from '../types/claims';

const root = '/finance/claims';

export const financeClaimsService = {
  list: (params: ClaimFilter) => apiClient.get<ClaimPage>(root, { params }).then(response => response.data),
  details: (id: string) => apiClient.get<ClaimDetails>(`${root}/${id}`).then(response => response.data),
  organizations: () => apiClient.get<ClaimOrganization[]>(`${root}/banks`).then(response => response.data),
  create: (input: ClaimInput) => apiClient.post<Claim>(root, input).then(response => response.data),
  update: (id: string, input: ClaimInput) => apiClient.put<Claim>(`${root}/${id}`, input).then(response => response.data),
  confirmReceipt: (id: string, dateInBank?: string) => apiClient.post<ClaimDetails>(`${root}/${id}/confirm-receipt`, { dateInBank }).then(response => response.data),
  postHistoricalReceipt: (id: string) => apiClient.post<ClaimDetails>(`${root}/${id}/post-historical-receipt`).then(response => response.data),
  reverseReceipt: (id: string, reason: string) => apiClient.post<ClaimDetails>(`${root}/${id}/reverse-receipt`, { reason }).then(response => response.data),
  cancel: (id: string, reason?: string) => apiClient.post<ClaimDetails>(`${root}/${id}/cancel`, { reason }).then(response => response.data),
  delete: (id: string) => apiClient.delete<void>(`${root}/${id}`),
  previewImport: (file: File) => {
    const data = new FormData();
    data.append('file', file);
    return requestFormData<ClaimImportPreview>(`${root}/import/preview`, data);
  },
  confirmImport: (previewId: string) => apiClient.post<ClaimImportResult>(`${root}/import/confirm`, { previewId }).then(response => response.data),
  export: (params: ClaimFilter) => downloadApiFile(`${root}/export`, 'MIS-Claims.xlsx', { params }),
};
