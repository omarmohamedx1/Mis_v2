export type AccountingDashboard = {
  year: number;
  month: number;
  totalPayroll: number;
  totalTransportation: number;
  totalCollectorCommissions: number;
  totalSupervisorCommissions: number;
  pendingApprovals: number;
};

export type AccountingPayrollPeriod = {
  id: string;
  year: number;
  month: number;
  status: string;
  notes?: string | null;
  employeeCount: number;
  totalNet: number;
  createdAt: string;
};

export type AccountingEmployeePayroll = {
  id: string;
  periodId: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  departmentName?: string | null;
  positionName?: string | null;
  basicSalary: number;
  allowances: number;
  transportation: number;
  commissions: number;
  bonuses: number;
  deductions: number;
  otherAdjustments: number;
  netSalary: number;
  status: string;
  notes?: string | null;
  createdAt: string;
};

export type AccountingPaged<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type AccountingTransportation = {
  id: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  departmentName?: string | null;
  positionName?: string | null;
  claimDate: string;
  amount: number;
  purpose: string;
  notes?: string | null;
  fieldVisitId?: string | null;
  caseId?: string | null;
  status: string;
  attachmentFileName?: string | null;
  createdAt: string;
};

export type AccountingCollectorCommission = {
  id: string;
  periodYear: number;
  periodMonth: number;
  collectorUserId: string;
  collectorName: string;
  employeeNumber?: string | null;
  assignedCasesCount: number;
  collectedAmount: number;
  eligibleAmount: number;
  ruleCode: string;
  rateApplied: number;
  commissionAmount: number;
  adjustments: number;
  finalCommission: number;
  status: string;
  notes?: string | null;
  createdAt: string;
};

export type AccountingCommissionContribution = {
  paymentId: string;
  caseId: string;
  caseNumber?: string | null;
  paymentDate: string;
  amount: number;
  referenceNumber: string;
};

export type AccountingCollectorCommissionDetails = {
  summary: AccountingCollectorCommission;
  contributions: AccountingCommissionContribution[];
};

export type AccountingSupervisorCommission = {
  id: string;
  periodYear: number;
  periodMonth: number;
  supervisorUserId: string;
  supervisorName: string;
  employeeNumber?: string | null;
  teamSummary?: string | null;
  collectorCount: number;
  teamCollectedAmount: number;
  eligibleAmount: number;
  ruleCode: string;
  rateApplied: number;
  commissionAmount: number;
  adjustments: number;
  finalCommission: number;
  status: string;
  notes?: string | null;
  createdAt: string;
};

export type AccountingLookupEmployee = {
  id: string;
  employeeNumber: string;
  fullName: string;
  departmentName?: string | null;
  positionName?: string | null;
};

export type AccountingCommissionRule = {
  id: string;
  code: string;
  nameArabic: string;
  nameEnglish: string;
  scope: string;
  basis: string;
  percentage?: number | null;
  fixedAmount?: number | null;
  effectiveFrom: string;
  isActive: boolean;
  version: number;
};
