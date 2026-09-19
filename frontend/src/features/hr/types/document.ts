export type DocumentExpiryStatus = 'All' | 'Expired' | 'ExpiringSoon' | 'Valid' | 'NoExpiry';

export interface EmployeeDocumentListItem {
  id: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  departmentName: string;
  documentTypeId: string | null;
  documentType: string;
  fileName: string;
  mimeType: string;
  fileSize: number;
  issueDate: string | null;
  expiryDate: string | null;
  expiryStatus: Exclude<DocumentExpiryStatus, 'All'>;
  daysUntilExpiry: number | null;
  uploadedBy: string;
  uploadedAt: string;
  updatedAt: string | null;
}

export interface EmployeeDocumentDetails extends Omit<EmployeeDocumentListItem, 'departmentName'> {
  sha256Hash: string | null;
  notes: string | null;
  uploadedByUserId: string;
}

export interface PagedEmployeeDocuments {
  items: EmployeeDocumentListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface DocumentExpirySummary {
  expired: number;
  expiringWithin7Days: number;
  expiringWithin15Days: number;
  expiringWithin30Days: number;
}

export interface EmployeeDocumentQuery {
  page: number;
  pageSize: number;
  search: string;
  employeeId: string;
  departmentId: string;
  documentTypeId: string;
  expiryStatus: DocumentExpiryStatus;
  expiringWithinDays: number;
  sortBy: string;
  sortDirection: 'asc' | 'desc';
}

export interface SaveEmployeeDocumentMetadata {
  documentTypeId: string;
  issueDate: string | null;
  expiryDate: string | null;
  notes: string | null;
}

export type RequiredDocumentCode = 'BIRTH_CERTIFICATE' | 'GRADUATION_CERTIFICATE' | 'NATIONAL_ID_COPY' | 'MILITARY_STATUS' | 'CRIMINAL_RECORD' | 'EMPLOYMENT_APPOINTMENT_PAPER' | 'LABOR_OFFICE_REGISTRATION';
export type PersonnelFileStatus = 'All' | 'Complete' | 'Incomplete';

export interface PersonnelDocumentChecklistItem {
  code: RequiredDocumentCode;
  name: string;
  nameArabic: string;
  isRequired: boolean;
  isUploaded: boolean;
  documentId: string | null;
  fileName: string | null;
  mimeType: string | null;
  fileSize: number | null;
  uploadedBy: string | null;
  uploadedAt: string | null;
  updatedAt: string | null;
}

export interface EmployeePersonnelFile {
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  departmentName: string;
  departmentId: string;
  positionName: string | null;
  positionId: string | null;
  gender: string | null;
  nationalId: string | null;
  isActive: boolean;
  isArchived: boolean;
  completedDocuments: number;
  requiredDocuments: number;
  missingDocuments: number;
  completionPercentage: number;
  status: Exclude<PersonnelFileStatus, 'All'>;
  documents: PersonnelDocumentChecklistItem[];
}

export interface PersonnelFileQuery {
  page: number;
  pageSize: number;
  search: string;
  employeeId: string;
  departmentId: string;
  organizationId: string;
  positionId: string;
  gender: string;
  completionStatus: PersonnelFileStatus;
  missingDocumentCode: RequiredDocumentCode | '';
}

export interface PagedEmployeePersonnelFiles {
  items: EmployeePersonnelFile[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface PersonnelFileSummary {
  totalEmployees: number;
  completeFiles: number;
  incompleteFiles: number;
  missingDocuments: number;
}
