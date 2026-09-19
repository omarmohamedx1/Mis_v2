import { apiClient } from '../../../services/apiClient';
import type { HrDashboardSummary } from '../types/dashboard';

export const hrDashboardService = {
  async getSummary(): Promise<HrDashboardSummary> {
    const { data } = await apiClient.get<HrDashboardSummary>('/hr/dashboard');
    return {
      ...data,
      employeesByDepartment: data.employeesByDepartment ?? [],
      employeesByOrganization: data.employeesByOrganization ?? [],
      employeesByBranch: data.employeesByBranch ?? [],
      alerts: data.alerts ?? [],
      recentActivity: data.recentActivity ?? [],
      attendanceTrend: data.attendanceTrend ?? [],
      absenceTrend: data.absenceTrend ?? [],
    };
  },
};
