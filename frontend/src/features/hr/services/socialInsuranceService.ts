import { apiClient } from '../../../services/apiClient';
import { loadAllHrPages } from './hrPaging';

export type InsuranceStatus = 'Insured' | 'NotInsured' | 'Suspended' | 'Ended';
export interface InsuranceRecord {
  id: string; employeeId: string; socialInsuranceNumber: string; insuranceStartDate: string | null;
  insuranceEndDate: string | null; insurableSalary: number; insuranceStatus: InsuranceStatus;
  insuranceOffice: string | null; referenceNumber: string | null; notes: string | null;
}
export interface InsuranceEmployee {
  employeeId: string; employeeNumber: string; employeeName: string; departmentId: string;
  department: string; position: string | null; nationalId: string | null; record: InsuranceRecord | null;
}
export interface InsurancePage {
  items: InsuranceEmployee[]; totalCount: number; page: number; pageSize: number; totalPages: number;
  totalInsured: number; notInsured: number; endedRecords: number; canManage: boolean;
}
export type SaveInsurance = Omit<InsuranceRecord, 'id' | 'insuranceEndDate'>;
export const socialInsuranceService = {
  async list(params: { search?: string; departmentId?: string; status?: string; employeeId?: string; page?: number }) {
    return loadAllHrPages(async (page, pageSize) => (await apiClient.get<InsurancePage>('/hr/social-insurance', { params: { ...params, page, pageSize } })).data);
  },
  async history(employeeId: string) { return (await apiClient.get<InsuranceRecord[]>(`/hr/social-insurance/employees/${employeeId}`)).data; },
  async save(id: string | undefined, request: SaveInsurance) {
    return (id ? await apiClient.put(`/hr/social-insurance/${id}`, request) : await apiClient.post('/hr/social-insurance', request)).data;
  },
  async end(id: string, insuranceEndDate: string) { await apiClient.post(`/hr/social-insurance/${id}/end`, { insuranceEndDate }); },
};
