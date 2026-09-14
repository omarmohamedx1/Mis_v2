export interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number; totalPages: number }
export interface BankDirectoryItem { id: string; code: string; nameArabic: string; nameEnglish: string; logoUrl?: string }
export interface BankPortfolioImport { id: string; bankId: string; bankNameArabic: string; bankNameEnglish: string; portfolioName: string; originalFileName: string; fileType: string; fileSize: number; rowCount: number; status: 'READY' | 'COMPLETED'; uploadedByUserId: string; uploadedBy: string; uploadedAt: string; confirmedAt?: string; notes?: string; updatedAt?: string }
export interface BankPortfolioImportPreviewRow { rowNumber: number; customerName?: string | null; customerCode?: string | null; accountReference?: string | null; outstandingBalance?: number | null; status: string; errors: string[] }
export interface BankPortfolioImportDataPreview { importId: string; totalRows: number; readyRows: number; existingRows: number; invalidRows: number; rows: BankPortfolioImportPreviewRow[] }
export interface BankPortfolioImportConfirmResult { import: BankPortfolioImport; imported: number; skipped: number; invalid: number }
export interface BankPortfolioReplacementPreview { token: string; originalFileName: string; fileType: string; fileSize: number; rowCount: number }
export interface BankPortfolioImportPage { items: BankPortfolioImport[]; totalCount: number; page: number; pageSize: number; totalPages: number }
export interface BankPortfolioAccess { isManager: boolean; canEdit: boolean; canAssign: boolean; canExport: boolean; statuses: string[] }
export interface BankPortfolioCollector { id: string; name: string }
export interface BankPortfolioCase { id: string; caseNumber: string; customerName: string; mobile?: string; outstandingAmount: number; assignedCollectorId?: string; assignedCollectorName?: string; status: string; lastActivityAt?: string; nextFollowUpAt?: string }
export interface BankPortfolioCasePage { items: BankPortfolioCase[]; totalCount: number; page: number; pageSize: number; totalPages: number; access: BankPortfolioAccess }
export interface BankPortfolioCaseDetails extends BankPortfolioCase { customerCode: string; alternativeMobile?: string; nationalId?: string; address?: string; bankName: string; portfolioName: string; accountReference: string; contractReference?: string; productType?: string; originalAmount: number; paidAmount: number; remainingAmount: number; latestNote?: string; sourceImportId?: string; importedFrom?: string; importDate?: string; createdAt: string; updatedAt: string; access: BankPortfolioAccess }
export interface BankPortfolioAssignmentPreview { caseCount: number; collectorId: string; collectorName: string }

export interface BankCustomerListItem {
  id: string;
  customerName: string;
  mobile?: string | null;
  nationalId?: string | null;
  accountReference?: string | null;
  contractReference?: string | null;
  outstandingAmount: number;
  paidAmount: number;
  remainingAmount: number;
  status?: string | null;
  assignedCollectorName?: string | null;
  caseCount: number;
}
export interface BankCustomerPage { items: BankCustomerListItem[]; totalCount: number; page: number; pageSize: number; totalPages: number }
export interface BankCustomerCaseSummary {
  caseId: string; caseNumber: string; accountReference: string; contractReference?: string | null; productType?: string | null;
  portfolioName: string; originalAmount: number; outstandingAmount: number; overdueAmount: number; paidAmount: number;
  remainingAmount: number; daysPastDue: number; status: string; assignedCollectorId?: string | null;
  assignedCollectorName?: string | null; assignmentDate?: string | null; lastPaymentAt?: string | null; nextFollowUpAt?: string | null;
}
export interface BankCustomerLinkSummary { module: string; id: string; title: string; status?: string | null; occurredAt: string; amount?: number | null }
export interface BankCustomerDetails {
  id: string; customerCode: string; customerName: string; fullNameArabic?: string | null; fullNameEnglish?: string | null;
  mobile?: string | null; alternativeMobile?: string | null; nationalId?: string | null; address?: string | null;
  governorate?: string | null; area?: string | null; organizationName: string; organizationType: string;
  totalOriginalAmount: number; totalOutstandingAmount: number; totalPaidAmount: number; totalRemainingAmount: number; totalOverdueAmount: number;
  cases: BankCustomerCaseSummary[]; payments: BankCustomerLinkSummary[]; promises: BankCustomerLinkSummary[];
  visits: BankCustomerLinkSummary[]; complaints: BankCustomerLinkSummary[]; activities: BankCustomerLinkSummary[]; timeline: BankCustomerLinkSummary[];
}
export interface BankCustomerImportSheet { sheetName?: string | null; suggestedHeaderRowNumber: number; detectedColumns: string[] }
export interface BankCustomerImportUpload { id: string; fileName: string; sheets: BankCustomerImportSheet[] }
export interface BankCustomerImportMapping {
  sheetName?: string | null; headerRow: number; firstDataRow: number; portfolioId?: string | null; columns: Record<string, string>;
}
export interface BankCustomerImportRow {
  row: number; customerName: string; mobile?: string | null; nationalId?: string | null; accountReference?: string | null;
  contractReference?: string | null; outstanding?: number | null; paid?: number | null; remaining?: number | null;
  status: string; errors: string[];
}
export interface BankCustomerImportPreview {
  id: string; previewId: string; portfolioId: string; portfolioName: string; rows: BankCustomerImportRow[];
}
export interface BankCustomerImportResult { imported: number; skipped: number; failed: number }

export interface CaseDistributionSummary { totalCases: number; unassignedCases: number; assignedCases: number; collectors: number }
export interface CaseDistributionItem { id: string; caseNumber: string; customerName: string; mobile?: string; outstandingAmount: number; status: string; collectorId?: string; collectorName?: string; assignedAt?: string; importId?: string; importName?: string }
export interface CaseDistributionPage { items: CaseDistributionItem[]; totalCount: number; page: number; pageSize: number; totalPages: number }
export interface DistributionCollector { id: string; name: string; assignedCases: number; totalOutstanding: number }
export interface DistributionImport { id: string; name: string }
export interface DistributionPreview { caseCount: number; totalOutstanding: number; collectorId?: string; collectorName?: string; previousCollectors: string[] }
export interface DistributionResult { caseCount: number; collectorId?: string; collectorName?: string }
export interface AutoDistributionCollector { collectorId: string; collectorName: string; caseCount: number; outstandingAmount: number }
export interface AutoDistributionPreview { method: string; totalCases: number; totalOutstanding: number; collectors: AutoDistributionCollector[] }
export interface BankDistributionImportSheet { sheetName?: string | null; suggestedHeaderRowNumber: number; detectedColumns: string[] }
export interface BankDistributionImportUpload { id: string; fileName: string; sheets: BankDistributionImportSheet[] }
export interface BankDistributionImportMapping {
  sheetName?: string | null; headerRow: number; firstDataRow: number; mode: 'FILE' | 'AUTO';
  columns: Record<string, string>; collectorIds?: string[] | null; reassignExisting?: boolean; reason?: string | null;
}
export interface BankDistributionImportRow {
  row: number; caseId?: string | null; caseNumber?: string | null; accountReference?: string | null; customerName?: string | null;
  currentCollectorName?: string | null; newCollectorId?: string | null; newCollectorName?: string | null;
  organizationName: string; status: string; errors: string[];
}
export interface BankDistributionImportPreview {
  id: string; previewId: string; mode: string; reassignExisting: boolean; readyRows: number; alreadyAssignedRows: number; invalidRows: number;
  rows: BankDistributionImportRow[]; autoPlan: AutoDistributionCollector[];
}
export interface BankDistributionImportResult { assigned: number; reassigned: number; skipped: number; failed: number }
export interface BankActivityAccess { isManager: boolean; canCreate: boolean; activityTypes: string[]; callOutcomes: string[]; filterActivityTypes: string[] }
export interface BankActivitySummary { activitiesToday: number; followUpsToday: number; overdueFollowUps: number; casesContactedToday: number }
export interface BankActivityItem { id: string; caseId: string; caseNumber: string; customerName: string; activityType: string; outcome?: string; notes?: string; activityAt: string; nextFollowUpAt?: string; performedById: string; performedBy: string; access: BankActivityAccess }
export interface BankActivityPage { items: BankActivityItem[]; totalCount: number; page: number; pageSize: number; totalPages: number; access: BankActivityAccess }
export interface BankActivityDetails extends BankActivityItem { mobile?: string; outstandingAmount: number; caseStatus: string; bankName: string; assignedCollectorId?: string; assignedCollectorName?: string; createdAt: string; timeline: BankActivityItem[] }
export interface BankActivityCaseLookup { id: string; caseNumber: string; customerName: string; mobile?: string; outstandingAmount: number; status: string; assignedCollectorId?: string; assignedCollectorName?: string }
export interface BankPtpAccess { isManager: boolean; canCreate: boolean; canChangeStatus: boolean }
export interface BankPtpSummary { dueToday: number; upcoming: number; overdue: number; broken: number }
export interface BankPtpItem { id: string; caseId: string; caseNumber: string; customerName: string; promiseAmount: number; promiseDate: string; status: string; operationalState: string; collectorId: string; collectorName: string; createdAt: string; updatedAt: string }
export interface BankPtpPage { items: BankPtpItem[]; totalCount: number; page: number; pageSize: number; totalPages: number; access: BankPtpAccess }
export interface BankPtpDetails extends BankPtpItem { mobile?: string; outstandingAmount: number; bankName: string; notes?: string }
export interface BankVisitAccess { isManager: boolean; canCreate: boolean; canManageAssignment: boolean }
export interface BankVisitSummary { visitsToday: number; upcomingVisits: number; overdueVisits: number; completedToday: number }
export interface BankVisitItem { id: string; caseId: string; caseNumber: string; customerName: string; scheduledAt: string; collectorId: string; collectorName: string; status: string; result?: string; updatedAt: string }
export interface BankVisitPage { items: BankVisitItem[]; totalCount: number; page: number; pageSize: number; totalPages: number; access: BankVisitAccess }
export interface BankVisitDetails extends BankVisitItem { mobile?: string; bankName: string; address: string; purpose?: string; notes?: string; createdBy: string; createdAt: string; completedAt?: string; access: BankVisitAccess }
export interface BankVisitCaseLookup { id: string; caseNumber: string; customerName: string; mobile?: string; address?: string; governorate?: string; area?: string; assignedCollectorId?: string; assignedCollectorName?: string }
export interface BankDcrAccess { isManager:boolean;canCreate:boolean;timeZoneId:string;businessToday:string }
export interface BankDcrItem { id:string;bankId:string;caseId:string;caseNumber:string;customerName:string;mobile?:string;dcrDate:string;actionCover:string;action:string;feedback:string;comment?:string;createdByUserId:string;createdBy:string;ptpDate?:string;ptpAmount?:number;paidDate?:string;paidAmount?:number;followUpAt?:string;visitDate?:string;linkedPtpId?:string;linkedPtpStatus?:string;linkedVisitId?:string;createdAt:string }
export interface BankDcrPage { items:BankDcrItem[];totalCount:number;page:number;pageSize:number;totalPages:number;access:BankDcrAccess }
export interface BankDcrCollector { id:string;name:string }
export interface CollectionDashboard { totalCases: number; totalOutstanding: number; totalOverdue: number; assignedCases: number; unassignedCases: number; activeCollectors: number; collectedToday: number; collectedMonthToDate: number; achievementPercent: number; activePromises: number; promisesDueToday: number; brokenPromises: number; visitsToday: number; pendingReviews: number; openComplaints: number; highRiskCases: number }
export interface ClientCard { id: string; code: string; name: string; organizationType: string; logoUrl?: string; activePortfolios: number; totalCases: number; totalOutstanding: number; assignedCases: number; unassignedCases: number; activeCollectors: number; collectedToday: number; achievementPercent: number; promiseAmount: number; brokenPromiseAmount: number; health: string; isActive: boolean }
export interface CollectionCase { id: string; caseNumber: string; customerCode: string; customerName: string; accountReference: string; clientName: string; portfolioName: string; outstandingBalance: number; overdueBalance: number; daysPastDue: number; bucket: string; status: string; priority: string; priorityScore: number; priorityExplanation: string; assignedCollectorId?: string; assignedCollectorName?: string; nextFollowUpAt?: string }
export interface Activity { id: string; type: string; result?: string; notes?: string; channel?: string; createdBy: string; createdAt: string; nextFollowUpAt?: string }
export interface PromiseItem { id: string; caseId: string; caseNumber: string; customerName: string; promisedAmount: number; promiseDate: string; actualPaidAmount: number; status: string; collectorName: string; channel: string; createdAt: string }
export interface PaymentItem { id: string; caseId: string; caseNumber: string; customerName: string; amount: number; paymentDate: string; method: string; referenceNumber: string; status: string; submittedBy: string; submittedAt: string; verifiedBy?: string; verifiedAt?: string; rejectionReason?: string; organizationId: string; organizationName: string; organizationType: string; collectorId: string; collectorName: string; currencyCode: string }
export interface PaymentSummary { todayCollectedAmount: number; todayTransactionCount: number; pendingReview: number }
export interface PaymentFilterOption { id: string; name: string }
export interface PaymentFilterOptions { organizations: PaymentFilterOption[]; collectors: PaymentFilterOption[] }
export interface PaymentDetails extends PaymentItem { accountReference: string; portfolioName: string }
export interface CaseDetails { id: string; caseNumber: string; clientName: string; portfolioName: string; customerCode: string; customerName: string; nationalId: string; primaryPhone: string; alternatePhone: string; address: string; governorate?: string; area?: string; accountReference: string; contractReference?: string; productType?: string; originalAmount: number; outstandingBalance: number; overdueBalance: number; penalties: number; fees: number; totalDue: number; daysPastDue: number; bucket: string; status: string; priority: string; priorityScore: number; priorityExplanation: string; assignedCollectorName?: string; sensitiveValuesRevealed: boolean; recommendedActionCode: string; recommendedActionReason: string; timeline: Activity[]; promises: PromiseItem[]; payments: PaymentItem[] }
export interface WorkQueue { callsDue: CollectionCase[]; highPriorityCases: CollectionCase[]; promisesDue: PromiseItem[]; brokenPromises: PromiseItem[]; visitsToday: number; pendingReviews: number; openComplaints: number }
export interface AssignmentPreviewItem { collectorId: string; collectorName: string; currentWorkload: number; proposedAdditionalCases: number; resultingWorkload: number }
export interface AssignmentPreview { caseCount: number; collectors: AssignmentPreviewItem[] }
export interface AutoAssignmentPreview extends AssignmentPreview { ruleCode: string; assignments: { caseId: string; caseNumber: string; collectorId: string; collectorName: string; reason: string }[] }
export interface CollectorLookup { id: string; name: string; activeWorkload: number; teamId?: string; teamName?: string }
export interface FieldVisit { id: string; caseId: string; caseNumber: string; customerName: string; organizationId: string; organizationName: string; organizationType: string; collectorId: string; collectorName: string; scheduledAt: string; status: string; address: string; governorate?: string; area?: string; result?: string; notes?: string }
export interface FieldVisitSummary { visitsToday: number; scheduledVisits: number; completedVisits: number }
export interface FieldVisitFilterOption { id: string; name: string }
export interface FieldVisitFilterOptions { organizations: FieldVisitFilterOption[]; collectors: FieldVisitFilterOption[] }
export interface FieldVisitCaseOption { id: string; caseNumber: string; customerName: string; organizationId: string; organizationName: string; organizationType: string; address?: string; governorate?: string; area?: string; assignedCollectorId?: string; assignedCollectorName?: string }
export interface FieldVisitScheduleOptions { cases: FieldVisitCaseOption[]; collectors: FieldVisitFilterOption[] }
export interface FieldVisitDetails extends FieldVisit { accountReference: string; purpose?: string; createdBy: string; createdAt: string; updatedAt: string; completedAt?: string; relatedDcrId?: string; dcrFeedback?: string; relatedPtpId?: string }
export interface Complaint { id: string; caseId: string; caseNumber: string; customerName: string; clientName: string; reference: string; source: string; category: string; severity: string; description: string; receivedAt: string; slaDueAt?: string; slaStatus: string; status: string; ownerId?: string; ownerName?: string; resolution?: string; closedAt?: string }

export interface BankComplaintAccess { isManager:boolean;canCreate:boolean;canAssign:boolean;canManageWorkflow:boolean;canAddNote:boolean;canUploadAttachment:boolean }
export interface BankComplaintSummary { open:number;inProgress:number;highCritical:number;overdue:number;resolvedToday:number }
export interface BankComplaintItem { id:string;complaintNumber:string;caseId:string;caseNumber:string;customerName:string;type:string;priority:string;status:string;assignedToId?:string;assignedToName?:string;createdAt:string;dueAt?:string;updatedAt:string;isOverdue:boolean }
export interface BankComplaintPage { items:BankComplaintItem[];totalCount:number;page:number;pageSize:number;totalPages:number;access:BankComplaintAccess }
export interface BankComplaintCase { id:string;caseNumber:string;customerName:string;mobile?:string }
export interface BankComplaintEmployee { id:string;name:string }
export interface BankComplaintNote { id:string;text:string;createdById:string;createdByName:string;createdAt:string }
export interface BankComplaintAttachment { id:string;fileName:string;contentType:string;fileSize:number;uploadedById:string;uploadedByName:string;uploadedAt:string }
export interface BankComplaintHistory { id:string;action:string;beforeJson?:string;afterJson?:string;userId?:string;userName:string;occurredAt:string }
export interface BankComplaintDetails extends BankComplaintItem { mobile?:string;bankName:string;description:string;createdById:string;createdByName:string;resolution?:string;resolvedById?:string;resolvedByName?:string;resolvedAt?:string;closedAt?:string;rejectionReason?:string;notes:BankComplaintNote[];attachments:BankComplaintAttachment[];history:BankComplaintHistory[];access:BankComplaintAccess }
export interface ArchiveSummary { archivedCases:number;archivedPortfolios:number;archivedThisMonth:number;restoredThisMonth:number;canManage:boolean }
export interface ArchiveCaseItem { id:string;caseNumber:string;customerName:string;mobile?:string;outstandingAmount:number;previousCollectorId?:string;previousCollector?:string;reason:string;archivedBy:string;archivedAt:string;updatedAt:string }
export interface ArchiveCasePage { items:ArchiveCaseItem[];totalCount:number;page:number;pageSize:number;totalPages:number;canManage:boolean }
export interface ArchiveHistoryItem { action:string;reason?:string;notes?:string;performedBy?:string;occurredAt:string }
export interface ArchiveRelatedItem { id:string;type:string;status:string;result?:string;notes?:string;occurredAt:string }
export interface ArchiveCaseDetails { id:string;caseNumber:string;customerName:string;customerCode:string;mobile?:string;nationalId?:string;address?:string;bankName:string;portfolioName:string;originalAmount:number;outstandingAmount:number;status:string;reason:string;notes?:string;archivedBy:string;archivedAt:string;previousCollector?:string;lifecycle:ArchiveHistoryItem[];activities:ArchiveRelatedItem[];ptps:ArchiveRelatedItem[];visits:ArchiveRelatedItem[];complaints:ArchiveRelatedItem[];attachments:ArchiveRelatedItem[];canRestore:boolean;updatedAt:string }
export interface ArchivePortfolioItem { id:string;portfolioName:string;originalFileName:string;records:number;importDate:string;reason:string;archivedBy:string;archivedAt:string }
export interface ArchivePortfolioPage { items:ArchivePortfolioItem[];totalCount:number;page:number;pageSize:number;totalPages:number;canManage:boolean }
export interface CollectionAudit { id: string; userName: string; action: string; entityType: string; entityId: string; caseId?: string; beforeJson?: string; afterJson?: string; source?: string; occurredAt: string }
export interface PortfolioLookup { id: string; organizationId: string; code: string; name: string; currencyCode: string; isActive: boolean }
export interface ImportBatch { id: string; organizationId: string; organizationName: string; portfolioId: string; portfolioName: string; fileName: string; status: string; totalRows: number; validRows: number; invalidRows: number; insertedRows: number; updatedRows: number; skippedRows: number; uploadedBy: string; uploadedAt: string; previewedAt?: string; confirmedAt?: string; failureReason?: string }
export interface ImportRow { id: string; rowNumber: number; accountReference: string; customerCode: string; customerName?: string; outstandingBalance?: number; overdueBalance?: number; daysPastDue?: number; isValid: boolean; errors: string[] }
export interface ImportPreview { batch: ImportBatch; rows: PagedResult<ImportRow> }
export interface ClientConfiguration { id: string; code: string; nameArabic: string; nameEnglish: string; organizationType: string; logoUrl?: string; contactEmail?: string; contactPhone?: string; settingsJson: string; isActive: boolean }
export interface PortfolioConfiguration { id: string; organizationId: string; code: string; nameArabic: string; nameEnglish: string; currencyCode: string; targetAmount?: number; settingsJson: string; isActive: boolean }
export interface BucketConfiguration { id: string; organizationId: string; portfolioId?: string; code: string; nameArabic: string; nameEnglish: string; minimumDays?: number; maximumDays?: number; sortOrder: number; isActive: boolean }
export interface CollectionsConfiguration { clients: ClientConfiguration[]; portfolios: PortfolioConfiguration[]; buckets: BucketConfiguration[] }
export interface CollectionAttachment { id: string; caseId: string; paymentId?: string; category: string; originalFileName: string; contentType: string; fileSize: number; uploadedBy: string; uploadedAt: string }
export interface CollectionReportSummary { totalCases: number; outstanding: number; overdue: number; approvedCollection: number; promiseAmount: number; fulfilledPromiseAmount: number; recoveryRate: number; promiseFulfillmentRate: number }
export interface CollectionReportBreakdown { code: string; name: string; cases: number; outstanding: number; overdue: number; collected: number; promiseAmount: number; fulfilledPromiseAmount: number }
export interface CollectionReport { filters: { organizationId?: string; portfolioId?: string; collectorId?: string; from: string; to: string }; summary: CollectionReportSummary; byClient: CollectionReportBreakdown[]; byBucket: CollectionReportBreakdown[]; byCollector: CollectionReportBreakdown[] }
export interface CaseFilters { page?: number; pageSize?: number; search?: string; organizationId?: string; portfolioId?: string; collectorId?: string; bucket?: string; status?: string; priority?: string }
