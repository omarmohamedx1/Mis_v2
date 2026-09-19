import { apiClient, downloadApiFile } from '../../../services/apiClient';
import type { HrReportCatalogItem, HrReportExportFormat, HrReportFilter, HrReportPreview } from '../types/report';

function params(filter: HrReportFilter) {
  return {
    page: filter.page,
    pageSize: filter.pageSize,
    search: filter.search || undefined,
    dateFrom: filter.dateFrom || undefined,
    dateTo: filter.dateTo || undefined,
    employeeId: filter.employeeId || undefined,
    departmentId: filter.departmentId || undefined,
    branchId: filter.branchId || undefined,
    status: filter.status || undefined,
    typeId: filter.typeId || undefined,
    type: filter.type || undefined,
  };
}

export const hrReportService = {
  async getCatalog(): Promise<HrReportCatalogItem[]> {
    const { data } = await apiClient.get<HrReportCatalogItem[]>('/hr/reports');
    return data;
  },

  async getPreview(code: string, filter: HrReportFilter): Promise<HrReportPreview> {
    const loadPage = async (page: number) => (await apiClient.get<HrReportPreview>(`/hr/reports/${encodeURIComponent(code)}/preview`, {
      params: params({ ...filter, page, pageSize: 100 }),
    })).data;
    const first = await loadPage(1);
    const rows = [...first.rows];
    for (let start = 2; start <= first.totalPages; start += 4) {
      const pageNumbers = Array.from({ length: Math.min(4, first.totalPages - start + 1) }, (_, index) => start + index);
      const results = await Promise.all(pageNumbers.map(loadPage));
      results.forEach((result) => rows.push(...result.rows));
    }
    return { ...first, rows, page: 1, pageSize: Math.max(rows.length, 1), totalPages: first.totalCount > 0 ? 1 : 0 };
  },

  async export(code: string, format: HrReportExportFormat, filter: HrReportFilter): Promise<void> {
    await downloadApiFile(`/hr/reports/${encodeURIComponent(code)}/export`, `${code}.${format === 'excel' ? 'xlsx' : 'pdf'}`, {
      params: { ...params(filter), format },
    });
  },
};
