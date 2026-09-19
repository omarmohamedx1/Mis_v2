export interface LegalDashboard {
  openCases: number;
  intakeCases: number;
  hearingsThisWeek: number;
  returnedThisMonth: number;
  outstandingTotal: number;
  canManage: boolean;
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

export const LEGAL_STAGES = ['INTAKE', 'NOTICE', 'COURT', 'HEARING', 'JUDGMENT', 'SETTLEMENT', 'RETURNED'] as const;
export const LEGAL_ACTIONS = ['NOTE', 'NOTICE', 'HEARING', 'JUDGMENT', 'SETTLEMENT', 'RETURN'] as const;
