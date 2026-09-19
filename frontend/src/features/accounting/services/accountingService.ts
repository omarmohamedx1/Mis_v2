import { apiClient, downloadApiFile } from '../../../services/apiClient';
import { loadAllPages, withoutPaging } from '../../../utils/loadAllPages';
import type {
  AccountingCollectorCommission,
  AccountingCollectorCommissionDetails,
  AccountingCommissionRule,
  AccountingDashboard,
  AccountingEmployeePayroll,
  AccountingLookupEmployee,
  AccountingPaged,
  AccountingPayrollPeriod,
  AccountingSupervisorCommission,
  AccountingTransportation,
} from '../types/accounting';

export const accountingService = {
  dashboard(year: number, month: number) {
    return apiClient.get<AccountingDashboard>('/accounting/dashboard', { params: { year, month } }).then((r) => r.data);
  },
  periods() {
    return apiClient.get<AccountingPayrollPeriod[]>('/accounting/salaries/periods').then((r) => r.data);
  },
  ensurePeriod(year: number, month: number, notes?: string) {
    return apiClient.post<AccountingPayrollPeriod>('/accounting/salaries/periods', { year, month, notes }).then((r) => r.data);
  },
  generatePayroll(year: number, month: number, notes?: string) {
    return apiClient.post<AccountingPayrollPeriod>('/accounting/salaries/generate', { year, month, notes }).then((r) => r.data);
  },
  salaries(periodId: string, search?: string, status?: string) {
    return loadAllPages((page, pageSize) => apiClient.get<AccountingPaged<AccountingEmployeePayroll>>('/accounting/salaries', { params: { periodId, search: search || undefined, status: status || undefined, page, pageSize } }).then((r) => r.data), 200);
  },
  salary(id: string) {
    return apiClient.get<AccountingEmployeePayroll>(`/accounting/salaries/${id}`).then((r) => r.data);
  },
  updateSalary(id: string, body: { transportation: number; commissions: number; bonuses: number; deductions: number; otherAdjustments: number; notes?: string }) {
    return apiClient.put<AccountingEmployeePayroll>(`/accounting/salaries/${id}`, body).then((r) => r.data);
  },
  salaryAction(id: string, action: 'submit' | 'approve' | 'pay' | 'cancel') {
    return apiClient.post<AccountingEmployeePayroll>(`/accounting/salaries/${id}/${action}`).then((r) => r.data);
  },
  transportation(params: { search?: string; status?: string; from?: string; to?: string; page?: number }) {
    return loadAllPages((page, pageSize) => apiClient.get<AccountingPaged<AccountingTransportation>>('/accounting/transportation', { params: { ...withoutPaging(params), page, pageSize } }).then((r) => r.data), 200);
  },
  createTransportation(body: { employeeId: string; claimDate: string; amount: number; purpose: string; notes?: string; fieldVisitId?: string; caseId?: string }) {
    return apiClient.post<AccountingTransportation>('/accounting/transportation', body).then((r) => r.data);
  },
  updateTransportation(id: string, body: { claimDate: string; amount: number; purpose: string; notes?: string; fieldVisitId?: string; caseId?: string }) {
    return apiClient.put<AccountingTransportation>(`/accounting/transportation/${id}`, body).then((r) => r.data);
  },
  transportationAction(id: string, action: string) {
    return apiClient.post<AccountingTransportation>(`/accounting/transportation/${id}/${action}`).then((r) => r.data);
  },
  uploadTransportationAttachment(id: string, file: File) {
    const form = new FormData();
    form.append('file', file);
    return apiClient.post<AccountingTransportation>(`/accounting/transportation/${id}/attachment`, form).then((r) => r.data);
  },
  deleteTransportationAttachment(id: string) {
    return apiClient.delete(`/accounting/transportation/${id}/attachment`);
  },
  downloadTransportationAttachment(id: string, fileName: string) {
    return downloadApiFile(`/accounting/transportation/${id}/attachment`, fileName);
  },
  commissionRules(scope?: string) {
    return apiClient.get<AccountingCommissionRule[]>('/accounting/commission-rules', { params: { scope: scope || undefined } }).then((r) => r.data);
  },
  createCommissionRule(body: { code: string; nameArabic: string; nameEnglish: string; scope: string; basis: string; percentage?: number | null; fixedAmount?: number | null; effectiveFrom: string }) {
    return apiClient.post<AccountingCommissionRule>('/accounting/commission-rules', body).then((r) => r.data);
  },
  setCommissionRuleActive(id: string, isActive: boolean) {
    return apiClient.patch<AccountingCommissionRule>(`/accounting/commission-rules/${id}/active`, { isActive }).then((r) => r.data);
  },
  deleteCommissionRule(id: string) {
    return apiClient.delete(`/accounting/commission-rules/${id}`);
  },
  calculateCollectorCommissions(year: number, month: number) {
    return apiClient.post<number>('/accounting/collector-commissions/calculate', { year, month }).then((r) => r.data);
  },
  collectorCommissions(params: { year?: number; month?: number; search?: string; status?: string; page?: number }) {
    return loadAllPages((page, pageSize) => apiClient.get<AccountingPaged<AccountingCollectorCommission>>('/accounting/collector-commissions', { params: { ...withoutPaging(params), page, pageSize } }).then((r) => r.data), 200);
  },
  collectorCommissionDetails(id: string) {
    return apiClient.get<AccountingCollectorCommissionDetails>(`/accounting/collector-commissions/${id}`).then((r) => r.data);
  },
  adjustCollectorCommission(id: string, adjustments: number, notes?: string) {
    return apiClient.post<AccountingCollectorCommission>(`/accounting/collector-commissions/${id}/adjust`, { adjustments, notes }).then((r) => r.data);
  },
  collectorCommissionAction(id: string, action: string) {
    return apiClient.post<AccountingCollectorCommission>(`/accounting/collector-commissions/${id}/${action}`).then((r) => r.data);
  },
  calculateSupervisorCommissions(year: number, month: number) {
    return apiClient.post<number>('/accounting/supervisor-commissions/calculate', { year, month }).then((r) => r.data);
  },
  supervisorCommissions(params: { year?: number; month?: number; search?: string; status?: string; page?: number }) {
    return loadAllPages((page, pageSize) => apiClient.get<AccountingPaged<AccountingSupervisorCommission>>('/accounting/supervisor-commissions', { params: { ...withoutPaging(params), page, pageSize } }).then((r) => r.data), 200);
  },
  adjustSupervisorCommission(id: string, adjustments: number, notes?: string) {
    return apiClient.post<AccountingSupervisorCommission>(`/accounting/supervisor-commissions/${id}/adjust`, { adjustments, notes }).then((r) => r.data);
  },
  supervisorCommissionAction(id: string, action: string) {
    return apiClient.post<AccountingSupervisorCommission>(`/accounting/supervisor-commissions/${id}/${action}`).then((r) => r.data);
  },
  employees(search?: string) {
    return apiClient.get<AccountingLookupEmployee[]>('/accounting/employees', { params: { search: search || undefined } }).then((r) => r.data);
  },
};
