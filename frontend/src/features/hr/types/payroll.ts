export interface HrPayrollDeduction {
  code: string;
  units: number;
  amount: number;
  reason: string;
}

export interface HrPayrollEmployee {
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  departmentName: string | null;
  positionName: string | null;
  basicSalary: number;
  allowances: number;
  grossSalary: number;
  dailyRate: number;
  absenceDays: number;
  fridayVisitDays: number;
  offsetAbsenceDays: number;
  chargeableAbsenceDays: number;
  lateMinutes: number;
  lateRemainderMinutes: number;
  lateDeductionDays: number;
  deductions: HrPayrollDeduction[];
  totalDeductions: number;
  netSalary: number;
  hasCompensation: boolean;
  accountingPayrollId: string | null;
  accountingStatus: string | null;
}

export interface HrPayrollSheet {
  year: number;
  month: number;
  lateGraceMinutes: number;
  lateMinutesPerDeductedDay: number;
  monthDivisor: number;
  employeeCount: number;
  totalGross: number;
  totalDeductions: number;
  totalNet: number;
  employees: HrPayrollEmployee[];
}
