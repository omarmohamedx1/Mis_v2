export interface DepartmentEmployeeCount {
  departmentId: string;
  departmentName: string;
  departmentCode: string;
  employeeCount: number;
}

export interface OrganizationEmployeeCount {
  organizationId: string;
  organizationName: string;
  organizationCode: string;
  employeeCount: number;
}

export interface BranchEmployeeCount {
  branchId: string | null;
  branchName: string;
  branchCode: string | null;
  employeeCount: number;
}

export interface TodayAttendanceSummary {
  present: number;
  absent: number;
  late: number;
  onLeave: number;
  missingCheckOut: number;
}

export interface HrDashboardAlert {
  category: string;
  entityId: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  title: string;
  dueDate: string;
  daysRemaining: number;
  severity: string;
}

export interface HrDashboardActivity {
  id: string;
  action: string;
  message: string;
  username: string;
  employeeId: string | null;
  employeeName: string | null;
  timestamp: string;
}

export interface AttendanceTrendPoint {
  date: string;
  present: number;
  late: number;
  absent: number;
  onLeave: number;
}

export interface AbsenceTrendPoint {
  date: string;
  absences: number;
}

export interface HrDashboardSummary {
  totalEmployees: number;
  activeEmployees: number;
  inactiveEmployees: number;
  absentToday: number | null;
  attendanceAvailable: boolean;
  documentsRequiringAttention: number | null;
  documentAttentionAvailable: boolean;
  totalDocuments: number;
  employeesByDepartment: DepartmentEmployeeCount[];
  employeesByOrganization: OrganizationEmployeeCount[];
  employeesByBranch: BranchEmployeeCount[];
  todayAttendance: TodayAttendanceSummary;
  alerts: HrDashboardAlert[];
  recentActivity: HrDashboardActivity[];
  attendanceTrend: AttendanceTrendPoint[];
  absenceTrend: AbsenceTrendPoint[];
}
