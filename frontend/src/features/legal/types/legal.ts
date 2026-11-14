export interface LegalDashboard {
  totalClients: number;
  recentlyAddedClients: number;
  transferredFromCollections: number;
  recentlyUpdatedClientFiles: number;
  recentlyAdded: LegalClientListItem[];
  canManage: boolean;
}

export interface LegalClientListItem {
  customerId: string;
  name: string;
  nationalId?: string | null;
  mobileNumber?: string | null;
  bankName: string;
  address?: string | null;
  status: string;
  transferredFromCollections: boolean;
  updatedAt: string;
}

export interface LegalClientPage {
  items: LegalClientListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface LegalClientDocument {
  id: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  uploadedBy: string;
  uploadedAt: string;
}

export interface LegalClientDetails {
  client: LegalClientListItem;
  customerCode: string;
  alternatePhone?: string | null;
  tertiaryPhone?: string | null;
  governorate?: string | null;
  area?: string | null;
  city?: string | null;
  employer?: string | null;
  jobTitle?: string | null;
  secondaryAddress?: string | null;
  notes?: string | null;
  customerNotes?: string | null;
  customerFeedback?: string | null;
  dataEntrySource?: string | null;
  createdBy?: string | null;
  createdAt: string;
  collectionCaseNumbers: string[];
  documents: LegalClientDocument[];
  canManage: boolean;
  importedRecords?: Array<Record<string, string>> | null;
}

export interface LegalClientWorkbook {
  fileName: string;
  sheets: Array<{ name: string | null; rows: string[][] }>;
}

export interface LegalClientColumnMapping {
  sheetName: string | null;
  headerRow: number;
  columns: Record<string, string>;
  extraColumns?: string[] | null;
}

export interface LegalClientImportSourceRow {
  rowNumber: number;
  name?: string | null;
  nameArabic?: string | null;
  nameEnglish?: string | null;
  nationalId?: string | null;
  mobileNumber?: string | null;
  bankName?: string | null;
  address?: string | null;
  addressArabic?: string | null;
  addressEnglish?: string | null;
  notes?: string | null;
  externalIdentifier?: string | null;
  originalFields?: Record<string, string> | null;
}

export interface LegalClientImportPreviewRow {
  source: LegalClientImportSourceRow;
  status: 'VALID' | 'EXISTING' | 'INVALID' | 'DUPLICATE';
  matchedCustomerName?: string | null;
  errors: string[];
}

export interface LegalClientImportPreview {
  fileName: string;
  totalRows: number;
  validRows: number;
  invalidRows: number;
  existingCustomersMatched: number;
  duplicateRows: number;
  rows: LegalClientImportPreviewRow[];
}

export interface LegalClientImportResult {
  totalRows: number;
  successfulRows: number;
  failedRows: number;
  rows: Array<{ rowNumber: number; status: string; errors: string[] }>;
}

export interface LegalOrganization {
  id: string;
  code: string;
  name: string;
}

export interface LegalCaseListItem {
  collectionCaseId: string;
  fileId?: string | null;
  caseNumber: string;
  customerName: string;
  organizationName: string;
  portfolioName: string;
  outstandingBalance: number;
  daysPastDue: number;
  collectionStatus: string;
  legalStage: string;
  nextHearingOn?: string | null;
  updatedAt: string;
}

export interface LegalCasePage {
  items: LegalCaseListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface LegalCaseAction {
  id: string;
  actionType: string;
  result?: string | null;
  notes: string;
  happenedOn?: string | null;
  createdByName: string;
  createdAt: string;
}

export interface LegalCaseDetails {
  collectionCaseId: string;
  fileId: string;
  caseNumber: string;
  accountReference: string;
  customerName: string;
  nationalId?: string | null;
  primaryPhone?: string | null;
  organizationName: string;
  portfolioName: string;
  originalAmount: number;
  outstandingBalance: number;
  daysPastDue: number;
  collectionStatus: string;
  legalStage: string;
  courtName?: string | null;
  courtCaseNumber?: string | null;
  lawyerName?: string | null;
  nextHearingOn?: string | null;
  notes?: string | null;
  receivedAt: string;
  receivedByName: string;
  updatedAt: string;
  canManage: boolean;
  actions: LegalCaseAction[];
}

export interface SaveLegalFileInput {
  courtName?: string;
  courtCaseNumber?: string;
  lawyerName?: string;
  nextHearingOn?: string | null;
  notes?: string;
}

export interface RecordLegalActionInput {
  actionType: string;
  notes: string;
  result?: string;
  happenedOn?: string | null;
}

export interface LegalImportSourceRow {
  rowNumber: number;
  customerCode?: string | null;
  customerName?: string | null;
  nationalId?: string | null;
  mobileNumber?: string | null;
  collectionCaseNumber?: string | null;
  accountNumber?: string | null;
  organization?: string | null;
  portfolio?: string | null;
  legalCaseNumber?: string | null;
  legalStage?: string | null;
  courtName?: string | null;
  nextSessionDate?: string | null;
  lawyerName?: string | null;
  notes?: string | null;
}

export interface LegalImportPreviewRow {
  source: LegalImportSourceRow;
  status: 'VALID' | 'INVALID' | 'DUPLICATE' | 'CUSTOMER_NOT_FOUND' | 'CASE_ALREADY_EXISTS';
  matchedCustomerName?: string | null;
  matchedCaseNumber?: string | null;
  errors: string[];
}

export interface LegalImportPreview {
  fileName: string;
  totalRows: number;
  validRows: number;
  invalidRows: number;
  existingCustomersMatched: number;
  existingCasesMatched: number;
  duplicateRows: number;
  newLegalCases: number;
  rows: LegalImportPreviewRow[];
}

export interface LegalImportResult {
  totalRows: number;
  successfulRows: number;
  failedRows: number;
  rows: Array<{ rowNumber: number; status: string; errors: string[] }>;
}

export interface LegalImportHistoryItem {
  id: string;
  fileName: string;
  totalRows: number;
  successfulRows: number;
  failedRows: number;
  uploadedBy: string;
  uploadedAt: string;
  errors: Array<{ rowNumber: number; status: string; errors: string[] }>;
}

export const LEGAL_STAGES = ['INTAKE', 'NOTICE', 'COURT', 'HEARING', 'JUDGMENT', 'SETTLEMENT', 'RETURNED'] as const;
export const LEGAL_ACTIONS = ['NOTE', 'NOTICE', 'HEARING', 'JUDGMENT', 'SETTLEMENT', 'RETURN'] as const;
