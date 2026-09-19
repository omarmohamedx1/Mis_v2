import type { EmployeeOrganizationAssignment } from './employee';

export type EmployeeStatus = 'Active' | 'Inactive' | 'OnLeave' | 'Suspended' | 'Terminated';
export type ContractStatus = 'Draft' | 'Active' | 'Expired' | 'Terminated';

export const employeeStatuses: EmployeeStatus[] = ['Active', 'Inactive', 'OnLeave', 'Suspended', 'Terminated'];
export const contractStatuses: ContractStatus[] = ['Draft', 'Active', 'Expired', 'Terminated'];

export interface EmployeePersonalInformation {
  fullNameArabic: string | null;
  fullNameEnglish: string | null;
  nationalId: string | null;
  dateOfBirth: string | null;
  gender: string | null;
  maritalStatus: string | null;
}

export interface EmployeeContactInformation {
  mobileNumber: string | null;
  alternativeMobileNumber: string | null;
  email: string | null;
  address: string | null;
  city: string | null;
}

export interface EmployeeEmploymentInformation {
  departmentId: string;
  departmentName: string;
  departmentCode: string;
  positionId: string | null;
  positionName: string | null;
  branchId: string | null;
  branchName: string | null;
  employmentTypeId: string | null;
  employmentTypeName: string | null;
  directManagerId: string | null;
  directManagerName: string | null;
  hireDate: string | null;
  operationalRole: 'COLLECTOR' | 'ADMIN' | 'SUPERVISOR' | 'OFFICE' | null;
  fingerprintEnrollmentDate: string | null;
  terminationDate: string | null;
  status: EmployeeStatus;
  organizations: EmployeeOrganizationAssignment[];
  workNumber?: string | null;
  packageType?: string | null;
}

export interface EmployeeContractInformation {
  id: string;
  contractTypeId: string | null;
  contractTypeName: string | null;
  startDate: string | null;
  endDate: string | null;
  probationStartDate: string | null;
  probationEndDate: string | null;
  status: ContractStatus;
  notes: string | null;
  updatedAt: string;
}

export interface EmployeeCompensation {
  id: string;
  basicSalary: number;
  allowances: number;
  totalSalary: number;
  effectiveFrom: string;
  bankName: string | null;
  bankAccount: string | null;
  iban: string | null;
  notes: string | null;
  updatedAt: string;
}

export interface EmployeeEmergencyContact {
  id: string;
  contactName: string;
  relationship: string;
  mobileNumber: string;
  alternativeNumber: string | null;
  notes: string | null;
  updatedAt: string;
}

export interface EmployeeProfileCounters {
  documents: number;
  attendanceRecords: number;
  leaveRequests: number;
  absences: number;
  delegations: number;
}

export interface EmployeeProfile {
  id: string;
  employeeNumber: string;
  displayName: string;
  status: EmployeeStatus;
  isActive: boolean;
  isArchived: boolean;
  archivedAt: string | null;
  archiveReason: string | null;
  hasProfilePhoto: boolean;
  canManageCompensation: boolean;
  personal: EmployeePersonalInformation;
  contact: EmployeeContactInformation;
  employment: EmployeeEmploymentInformation;
  contract: EmployeeContractInformation | null;
  compensation: EmployeeCompensation | null;
  emergencyContact: EmployeeEmergencyContact | null;
  counters: EmployeeProfileCounters;
  createdAt: string;
  updatedAt: string | null;
}

export interface ReportingLineEmployee {
  id: string;
  employeeNumber: string;
  fullName: string;
  status: EmployeeStatus;
}

export interface EmployeeReportingLine {
  employeeId: string;
  employeeName: string;
  directManagerId: string | null;
  directManagerName: string | null;
  directReports: ReportingLineEmployee[];
}

export type UpdateEmployeePersonalRequest = EmployeePersonalInformation;
export type UpdateEmployeeContactRequest = EmployeeContactInformation;

export interface UpdateEmployeeEmploymentRequest {
  departmentId: string;
  positionId: string | null;
  branchId: string | null;
  employmentTypeId: string | null;
  directManagerId: string | null;
  hireDate: string | null;
  organizationIds: string[];
  workNumber?: string | null;
  packageType?: string | null;
}

export interface UpdateEmployeeContractRequest {
  contractTypeId: string | null;
  startDate: string | null;
  endDate: string | null;
  probationStartDate: string | null;
  probationEndDate: string | null;
  status: ContractStatus;
  notes: string | null;
}

export interface UpdateEmployeeCompensationRequest {
  basicSalary: number;
  allowances: number;
  effectiveFrom: string;
  bankName: string | null;
  bankAccount: string | null;
  iban: string | null;
  notes: string | null;
}

export interface UpdateEmployeeEmergencyContactRequest {
  contactName: string;
  relationship: string;
  mobileNumber: string;
  alternativeNumber: string | null;
  notes: string | null;
}

export interface ChangeEmployeeStatusRequest {
  status: EmployeeStatus;
  reason: string | null;
  terminationDate: string | null;
}
