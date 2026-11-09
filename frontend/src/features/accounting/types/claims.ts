export interface ClaimFilter {
  search?: string;
  bankOrganizationId?: string;
  claimType?: string;
  month?: string;
  status?: string;
  claimDateFrom?: string;
  claimDateTo?: string;
  bankDateFrom?: string;
  bankDateTo?: string;
  amountFrom?: number;
  amountTo?: number;
  page?: number;
  pageSize?: number;
}

export interface ClaimInput {
  serial?: string;
  bankOrganizationId?: string;
  bankName: string;
  claimType: string;
  month: string;
  claimDate: string;
  amountWithoutVat: number;
  vatPercent: number;
  taxAtSourcePercent: number;
  dateInBank?: string;
}

export interface Claim {
  id: string;
  serial?: string;
  bankOrganizationId?: string;
  bankName: string;
  claimType: string;
  month: string;
  claimDate: string;
  amountWithoutVat: number;
  vatPercent: number;
  vat: number;
  totalWithVat: number;
  taxAtSourcePercent: number;
  taxAtSource: number;
  total: number;
  totalInBank: number;
  dateInBank?: string;
  status: 'PENDING' | 'RECEIVED' | 'CANCELLED';
  isHistoricalImport: boolean;
  hasAccountingPosting: boolean;
  createdBy: string;
  createdAt: string;
  updatedBy?: string;
  updatedAt?: string;
}

export interface ClaimSummary {
  totalClaims: number;
  pendingClaims: number;
  receivedClaims: number;
  totalClaimAmount: number;
  totalReceived: number;
  netReceivedInBank: number;
  totalVat: number;
  totalWithholdingTax: number;
}

export interface ClaimPage {
  items: Claim[];
  page: number;
  pageSize: number;
  totalCount: number;
  summary: ClaimSummary;
}

export interface ClaimOrganization {
  id: string;
  code: string;
  nameArabic: string;
  nameEnglish: string;
  organizationType: 'BANK' | 'CONSUMER_FINANCE' | 'FINANCIAL_INSTITUTION';
}
export interface ClaimReceipt {
  id: string;
  amount: number;
  receiptDate: string;
  isHistoricalPosting: boolean;
  createdBy: string;
  createdAt: string;
  accountingDocumentNumber?: string;
  accountingStatus?: string;
  reversedAt?: string;
  reversedBy?: string;
  reversalJournalId?: string;
}
export interface ClaimAudit {
  action: string;
  actor?: string;
  reason?: string;
  createdAt: string;
  beforeJson: string;
  afterJson: string;
}
export interface ClaimDetails {
  claim: Claim;
  receiptPostings: ClaimReceipt[];
  auditHistory: ClaimAudit[];
  importFileName?: string;
  importedBy?: string;
  importedAt?: string;
  receiptConfirmedBy?: string;
  receiptConfirmedAt?: string;
}

export interface ClaimImportRow {
  rowNumber: number;
  serial?: string;
  bankName?: string;
  claimType?: string;
  month?: string;
  claimDate?: string;
  amountWithoutVat?: number;
  vatPercent?: number;
  excelVat?: number;
  excelTotalWithVat?: number;
  taxAtSourcePercent?: number;
  excelTaxAtSource?: number;
  excelTotal?: number;
  excelTotalInBank?: number;
  dateInBank?: string;
  calculatedVat?: number;
  calculatedTotalWithVat?: number;
  calculatedTaxAtSource?: number;
  calculatedTotal?: number;
  calculatedTotalInBank?: number;
  isValid: boolean;
  possibleDuplicate: boolean;
  errors: string[];
  warnings: string[];
}
export interface ClaimImportPreview {
  previewId: string;
  fileName: string;
  totalRows: number;
  validRows: number;
  warningRows: number;
  invalidRows: number;
  rows: ClaimImportRow[];
}
export interface ClaimImportResult { totalRows: number; imported: number; skipped: number; failed: number }
