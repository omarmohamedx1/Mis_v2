export interface DataEntryDashboard {
  draftBatches: number;
  submittedBatches: number;
  acceptedBatches: number;
  distributedBatches: number;
  rejectedBatches: number;
  myClients: number;
  unreadNotifications: number;
}

export interface DataEntryClientListItem {
  id: string;
  customerNumber: string;
  customerName: string;
  mobileNumber?: string | null;
  organizationName: string;
  source?: string | null;
  caseNumber?: string | null;
  caseId?: string | null;
  caseStatus?: string | null;
  batchStatus?: string | null;
}

export interface DataEntryPagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface DataEntryClientDetails {
  id: string;
  customerNumber: string;
  customerNameArabic?: string | null;
  customerNameEnglish?: string | null;
  nationalId?: string | null;
  mobileNumber?: string | null;
  alternateMobile?: string | null;
  address?: string | null;
  feedback?: string | null;
  notes?: string | null;
  source?: string | null;
  createdBy?: string | null;
  createdAt: string;
  organizationId: string;
  organizationName: string;
  organizationType: string;
  portfolioCode?: string | null;
  portfolioName?: string | null;
  primaryClassification?: string | null;
  subClassification?: string | null;
  caseNumber?: string | null;
  caseId?: string | null;
  accountNumber?: string | null;
  contractNumber?: string | null;
  outstandingBalance?: number | null;
  overdueBalance?: number | null;
  daysPastDue?: number | null;
  caseStatus?: string | null;
  batchId?: string | null;
  batchNumber?: string | null;
  batchStatus?: string | null;
}

export interface CreateDataEntryClientInput {
  organizationId: string;
  portfolioId?: string | null;
  primaryClassification?: string | null;
  subClassification?: string | null;
  customerName: string;
  nationalId?: string | null;
  mobileNumber?: string | null;
  address?: string | null;
  feedback?: string | null;
  notes?: string | null;
  accountNumber?: string | null;
  contractNumber?: string | null;
  outstandingBalance?: number | null;
}

export interface DataEntryOrganization {
  id: string;
  code: string;
  nameArabic: string;
  nameEnglish: string;
  organizationType: string;
}

export interface DataEntryPortfolio {
  id: string;
  code: string;
  nameArabic: string;
  nameEnglish: string;
  primaryClassification?: string | null;
  subClassification?: string | null;
}

export interface DataEntryImportSheet {
  sheetName: string;
  suggestedHeaderRowNumber: number;
  detectedColumns: string[];
}

export interface DataEntryImportUpload {
  uploadId: string;
  fileName: string;
  sheets: DataEntryImportSheet[];
}

export interface DataEntrySheetPreview {
  fileName: string;
  sheetName: string;
  columns: string[];
  rows: string[][];
  totalRows: number;
  truncated: boolean;
}

export type DataEntryImportField =
  | 'CustomerCode'
  | 'CustomerName'
  | 'NationalId'
  | 'MobileNumber'
  | 'Address'
  | 'Feedback'
  | 'Notes'
  | 'AccountNumber'
  | 'ContractNumber'
  | 'OutstandingAmount'
  | 'OverdueAmount'
  | 'DaysPastDue';

export interface DataEntryImportMappingRequest {
  organizationId: string;
  portfolioId?: string | null;
  primaryClassification?: string | null;
  subClassification?: string | null;
  sheetName: string;
  headerRow: number;
  firstDataRow: number;
  columns: Record<string, string | null>;
  sheetNames?: string[];
}

export interface DataEntryImportPreviewRow {
  rowNumber: number;
  customerNumber?: string | null;
  customerName: string;
  nationalId?: string | null;
  mobileNumber?: string | null;
  status: string;
  errorMessage?: string | null;
  collectionCustomerId?: string | null;
  collectionCaseId?: string | null;
}

export interface DataEntryImportPreview {
  uploadId: string;
  previewId: string;
  totalRows: number;
  readyRows: number;
  existingCustomerRows: number;
  invalidRows: number;
  rows: DataEntryImportPreviewRow[];
}

export interface ConfirmDataEntryImportInput {
  uploadId: string;
  previewId: string;
  excludedRowNumbers?: number[];
}

export interface DataEntryBatchListItem {
  id: string;
  batchNumber: string;
  fileName: string;
  uploadedBy: string;
  createdAt: string;
  totalRows: number;
  validRows: number;
  invalidRows: number;
  customerCount: number;
  organizationName: string;
  primaryClassification?: string | null;
  subClassification?: string | null;
  status: string;
  source: string;
}

export interface DataEntryBatchDetails {
  summary: DataEntryBatchListItem;
  rejectionReason?: string | null;
  submittedAt?: string | null;
  reviewedBy?: string | null;
  reviewedAt?: string | null;
  distributedAt?: string | null;
  rows: DataEntryImportPreviewRow[];
  documents?: DataEntryDocument[];
}

export interface DataEntryDocument {
  id: string;
  customerId: string;
  batchId?: string | null;
  caseId?: string | null;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  note?: string | null;
  uploadedBy: string;
  uploadedAt: string;
  canDownload: boolean;
}

export interface DataEntryNotification {
  id: string;
  batchId: string;
  batchNumber: string;
  kind: string;
  messageArabic: string;
  messageEnglish: string;
  isRead: boolean;
  createdAt: string;
}

export const DATA_ENTRY_IMPORT_FIELDS: DataEntryImportField[] = [
  'CustomerCode',
  'CustomerName',
  'NationalId',
  'MobileNumber',
  'Address',
  'Feedback',
  'Notes',
  'AccountNumber',
  'ContractNumber',
  'OutstandingAmount',
  'OverdueAmount',
  'DaysPastDue',
];
