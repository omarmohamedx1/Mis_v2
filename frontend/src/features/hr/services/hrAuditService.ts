import { apiClient } from '../../../services/apiClient';
import type { AuditQuery, PagedAuditLogs } from '../types/audit';
import { loadAllHrPages } from './hrPaging';

export const hrAuditService = {
  async getPaged(query: AuditQuery): Promise<PagedAuditLogs> {
    return loadAllHrPages(async (page, pageSize) => {
      const { data } = await apiClient.get<PagedAuditLogs>('/hr/audit', { params: {
        action: query.action || undefined,
        employeeId: query.employeeId || undefined,
        entityType: query.entityType || undefined,
        from: query.from || undefined,
        page,
        pageSize,
        search: query.search || undefined,
        to: query.to || undefined,
      } });
      return data;
    });
  },
};
