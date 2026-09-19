import { apiClient } from '../../../services/apiClient';
import type { HrPayrollSheet } from '../types/payroll';

export const hrPayrollService = {
  async sheet(year: number, month: number) {
    const { data } = await apiClient.get<HrPayrollSheet>('/hr/payroll', { params: { year, month } });
    return data;
  },
};
