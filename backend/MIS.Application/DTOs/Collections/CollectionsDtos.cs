using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Collections;

public sealed record CollectionDashboardDto(
    int TotalCases, decimal TotalOutstanding, decimal TotalOverdue, int AssignedCases, int UnassignedCases,
    int ActiveCollectors, decimal CollectedToday, decimal CollectedMonthToDate, decimal AchievementPercent,
    int ActivePromises, int PromisesDueToday, int BrokenPromises, int VisitsToday, int PendingReviews,
    int OpenComplaints, int HighRiskCases, int OverdueFollowUps,
    IReadOnlyCollection<CollectionDashboardClientSliceDto> ByClient,
    IReadOnlyCollection<CollectionDashboardBucketSliceDto> ByBucket,
    IReadOnlyCollection<CollectionDashboardTrendPointDto> CollectionTrend,
    IReadOnlyCollection<CollectionDashboardStatusSliceDto> ByStatus);

public sealed record CollectionDashboardClientSliceDto(Guid Id, string Name, string OrganizationType, int Cases, int Assigned, int Unassigned, int LegalCases, decimal Outstanding, decimal Overdue, string Code, string? LogoUrl);
public sealed record CollectionDashboardBucketSliceDto(string Code, string Name, int Cases, decimal Outstanding, decimal Overdue);
public sealed record CollectionDashboardTrendPointDto(DateOnly Date, decimal Collected);
public sealed record CollectionDashboardStatusSliceDto(string Status, int Cases, decimal Outstanding);

public sealed record CollectionCreditorDeskDto(Guid PortfolioId, string Name, string? PrimaryClassification, string? SubClassification, int Cases, int Assigned, int Unassigned, int LegalCases, decimal Outstanding, decimal Overdue);
public sealed record CollectionCreditorBookDto(Guid Id, string Code, string Name, string? LogoUrl, string OrganizationType, int Cases, int Assigned, int Unassigned, int LegalCases, decimal Outstanding, decimal Overdue, IReadOnlyCollection<CollectionCreditorDeskDto> Desks);
public sealed record CollectionCreditorDashboardDto(IReadOnlyCollection<CollectionCreditorBookDto> Banks, IReadOnlyCollection<CollectionCreditorBookDto> Companies);

public sealed record CollectionCollectorOrgSliceDto(Guid OrganizationId, string Code, string Name, string? LogoUrl, string OrganizationType, int Cases, decimal Outstanding, decimal Overdue, int LegalCases);
public sealed record CollectionCollectorBookDto(Guid Id, string Name, string LoginCode, int Cases, decimal Outstanding, decimal Overdue, int LegalCases, int HighRiskCases, IReadOnlyCollection<CollectionCollectorOrgSliceDto> Organizations);
public sealed record CollectionCollectorDashboardDto(int UnassignedCases, decimal UnassignedOutstanding, decimal UnassignedOverdue, IReadOnlyCollection<CollectionCollectorOrgSliceDto> UnassignedByOrganization, IReadOnlyCollection<CollectionCollectorBookDto> Collectors);

public sealed record ClientOrganizationCardDto(
    Guid Id, string Code, string Name, string OrganizationType, string? LogoUrl, int ActivePortfolios,
    int TotalCases, decimal TotalOutstanding, int AssignedCases, int UnassignedCases, int ActiveCollectors,
    decimal CollectedToday, decimal AchievementPercent, decimal PromiseAmount, decimal BrokenPromiseAmount,
    string Health, bool IsActive);

public sealed record PagedResultDto<T>(IReadOnlyCollection<T> Items, int TotalCount, int Page, int PageSize, int TotalPages);

public sealed record CollectionCaseListItemDto(
    Guid Id, string CaseNumber, string CustomerCode, string CustomerName, string AccountReference, string ClientName,
    string PortfolioName, decimal OutstandingBalance, decimal OverdueBalance, int DaysPastDue, string Bucket,
    string Status, string Priority, int PriorityScore, string PriorityExplanation, Guid? AssignedCollectorId,
    string? AssignedCollectorName, DateTimeOffset? NextFollowUpAt,
    string? NationalId, string? CardNumber, string? ImportStatusText, string? ImportBucketLabel,
    string? PrimaryPhone, string? AlternatePhone, string? TertiaryPhone,
    string? Governorate, string? Area, string? FileCollectorName,
    DateTimeOffset? LastPaymentAt, decimal? LastPaymentAmount, string OrganizationType,
    IReadOnlyCollection<string>? RelatedOrganizationNames = null);

public sealed record CollectionFileSnapshotDto(
    string? Action, string? PtpDate, string? PtpAmount, string? Payment, string? Update, string? Keep, string? Feedback);

public sealed record RelatedCreditorCaseDto(
    Guid CaseId, Guid OrganizationId, string OrganizationName, string OrganizationType,
    string CaseNumber, string AccountReference, decimal OutstandingBalance, string Status, string PortfolioName);

public sealed record NationalIdLookupDto(
    string NationalId, int OrganizationCount, int CaseCount, decimal TotalOutstanding,
    IReadOnlyCollection<RelatedCreditorCaseDto> Cases);

public sealed record CollectionActivityDto(Guid Id, string Type, string? Result, string? Notes, string? Channel, string CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset? NextFollowUpAt);
public sealed record PromiseToPayDto(Guid Id, Guid CaseId, string CaseNumber, string CustomerName, decimal PromisedAmount, DateOnly PromiseDate, decimal ActualPaidAmount, string Status, string CollectorName, string Channel, DateTimeOffset CreatedAt);
public sealed record CollectionPaymentDto(Guid Id, Guid CaseId, string CaseNumber, string CustomerName, decimal Amount, DateOnly PaymentDate, string Method, string ReferenceNumber, string Status, string SubmittedBy, DateTimeOffset SubmittedAt, string? VerifiedBy, DateTimeOffset? VerifiedAt, string? RejectionReason, Guid OrganizationId, string OrganizationName, string OrganizationType, Guid CollectorId, string CollectorName, string CurrencyCode);
public sealed record CollectionPaymentSummaryDto(decimal TodayCollectedAmount, int TodayTransactionCount, int PendingReview);
public sealed record CollectionPaymentFilterOptionDto(Guid Id, string Name);
public sealed record CollectionPaymentFilterOptionsDto(IReadOnlyCollection<CollectionPaymentFilterOptionDto> Organizations, IReadOnlyCollection<CollectionPaymentFilterOptionDto> Collectors);
public sealed record CollectionPaymentDetailsDto(Guid Id, Guid CaseId, string CaseNumber, string AccountReference, string CustomerName, Guid OrganizationId, string OrganizationName, string OrganizationType, string PortfolioName, Guid CollectorId, string CollectorName, decimal Amount, string CurrencyCode, DateOnly PaymentDate, string Method, string ReferenceNumber, string Status, string SubmittedBy, DateTimeOffset SubmittedAt, string? VerifiedBy, DateTimeOffset? VerifiedAt, string? RejectionReason);

public sealed record CollectionCaseDetailsDto(
    Guid Id, string CaseNumber, string ClientName, string PortfolioName, string CustomerCode, string CustomerName,
    string NationalId, string PrimaryPhone, string AlternatePhone, string? TertiaryPhone, string Address, string? SecondaryAddress,
    string? Governorate, string? Area, string? City, string? Employer, string? JobTitle, string? Feedback,
    string AccountReference, string? CardNumber, string? ContractReference, string? ProductType, string? ImportStatusText, string? Stage,
    decimal OriginalAmount, decimal OutstandingBalance, decimal OverdueBalance, decimal Penalties, decimal Fees, decimal TotalDue,
    int DaysPastDue, string Bucket, string? ImportBucketLabel, string Status, string Priority, int PriorityScore, string PriorityExplanation,
    string? AssignedCollectorName, Guid? AssignedCollectorId, string? PreviousCollectorName, string? FileCollectorName,
    Guid? PreviousCollectorUserId, string? PreviousCollectorUserName, Guid? FileCollectorUserId, string? FileCollectorUserName,
    decimal? CreditLimit, decimal? PurchaseAvailableLimit, DateOnly? ActivationDate,
    decimal? LastPaymentAmount, DateTimeOffset? LastPaymentAt, DateOnly? LastTransactionDate, decimal? LastTransactionAmount,
    bool SensitiveValuesRevealed, string RecommendedActionCode, string RecommendedActionReason,
    CollectionFileSnapshotDto? FileSnapshot, IReadOnlyCollection<RelatedCreditorCaseDto> RelatedCreditorCases,
    IReadOnlyCollection<CollectionActivityDto> Timeline, IReadOnlyCollection<PromiseToPayDto> Promises,
    IReadOnlyCollection<CollectionPaymentDto> Payments);

public sealed record WorkQueueDto(
    IReadOnlyCollection<CollectionCaseListItemDto> CallsDue, IReadOnlyCollection<CollectionCaseListItemDto> HighPriorityCases,
    IReadOnlyCollection<PromiseToPayDto> PromisesDue, IReadOnlyCollection<PromiseToPayDto> BrokenPromises,
    int VisitsToday, int PendingReviews, int OpenComplaints);

public sealed record AssignmentPreviewItemDto(Guid CollectorId, string CollectorName, int CurrentWorkload, int ProposedAdditionalCases, int ResultingWorkload);
public sealed record AssignmentPreviewDto(int CaseCount, IReadOnlyCollection<AssignmentPreviewItemDto> Collectors);
public sealed record ImportedCollectorLinkSliceDto(Guid CollectorId, string CollectorName, string FileName, int Cases);
public sealed record ImportedCollectorLinkResultDto(int LinkedCases, int AmbiguousCases, int UnmatchedCases, IReadOnlyCollection<ImportedCollectorLinkSliceDto> ByCollector);
public sealed record AutoAssignmentRequest(IReadOnlyCollection<Guid>? CaseIds, IReadOnlyCollection<Guid>? CollectorIds, Guid? TeamId, [Range(1, 5000)] int MaxActiveCases, bool Confirmed);
public sealed record AutoAssignmentCaseDto(Guid CaseId, string CaseNumber, Guid CollectorId, string CollectorName, string Reason);
public sealed record AutoAssignmentPreviewDto(string RuleCode, int CaseCount, IReadOnlyCollection<AssignmentPreviewItemDto> Collectors, IReadOnlyCollection<AutoAssignmentCaseDto> Assignments);
public sealed record CollectorLookupDto(Guid Id, string Name, int ActiveWorkload, Guid? TeamId, string? TeamName);
public sealed record FieldVisitDto(Guid Id, Guid CaseId, string CaseNumber, string CustomerName, Guid OrganizationId, string OrganizationName, string OrganizationType, Guid CollectorId, string CollectorName, DateTimeOffset ScheduledAt, string Status, string Address, string? Governorate, string? Area, string? Result, string? Notes);
public sealed record FieldVisitSummaryDto(int VisitsToday, int ScheduledVisits, int CompletedVisits);
public sealed record FieldVisitFilterOptionDto(Guid Id, string Name);
public sealed record FieldVisitFilterOptionsDto(IReadOnlyCollection<FieldVisitFilterOptionDto> Organizations, IReadOnlyCollection<FieldVisitFilterOptionDto> Collectors);
public sealed record FieldVisitCaseOptionDto(Guid Id, string CaseNumber, string CustomerName, Guid OrganizationId, string OrganizationName, string OrganizationType, string? Address, string? Governorate, string? Area, Guid? AssignedCollectorId, string? AssignedCollectorName);
public sealed record FieldVisitScheduleOptionsDto(IReadOnlyCollection<FieldVisitCaseOptionDto> Cases, IReadOnlyCollection<FieldVisitFilterOptionDto> Collectors);
public sealed record FieldVisitDetailsDto(Guid Id, Guid CaseId, string CaseNumber, string AccountReference, string CustomerName, Guid OrganizationId, string OrganizationName, string OrganizationType, Guid CollectorId, string CollectorName, DateTimeOffset ScheduledAt, string Status, string Address, string? Governorate, string? Area, string? Purpose, string? Result, string? Notes, string CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? CompletedAt, Guid? RelatedDcrId, string? DcrFeedback, Guid? RelatedPtpId);
public sealed record VisitFilters(int Page = 1, int PageSize = 20, string? Search = null, Guid? OrganizationId = null, Guid? CollectorId = null, string? Status = null, DateOnly? Date = null);
public sealed record CreateVisitRequest(Guid CaseId, Guid CollectorId, DateTimeOffset ScheduledAt, [Required, MaxLength(600)] string Address, [MaxLength(100)] string? Governorate, [MaxLength(100)] string? Area, [MaxLength(3000)] string? Notes = null);
public sealed record CompleteVisitRequest([Required, MaxLength(100)] string Result, [MaxLength(3000)] string? Notes);
public sealed record ComplaintDto(Guid Id, Guid CaseId, string CaseNumber, string CustomerName, string ClientName, string Reference, string Source, string Category, string Severity, string Description, DateTimeOffset ReceivedAt, DateTimeOffset? SlaDueAt, string SlaStatus, string Status, Guid? OwnerId, string? OwnerName, string? Resolution, DateTimeOffset? ClosedAt);
public sealed record CreateComplaintRequest(Guid CaseId, [Required, MaxLength(80)] string Reference, [Required, MaxLength(60)] string Source, [Required, MaxLength(100)] string Category, [Required, MaxLength(30)] string Severity, [Required, MaxLength(4000)] string Description, DateTimeOffset ReceivedAt, DateTimeOffset SlaDueAt, Guid OwnerId);
public sealed record ChangeComplaintStatusRequest([Required, MaxLength(32)] string Status, [MaxLength(4000)] string? Resolution);
public sealed record ArchiveCaseRequest([Required, MaxLength(64)] string Reason, [MaxLength(2000)] string? Notes);
public sealed record RestoreCaseRequest([Required, MaxLength(500)] string Reason);
public sealed record ChangePromiseStatusRequest([Required, MaxLength(32)] string Status);
public sealed record CollectionAuditDto(Guid Id, string UserName, string Action, string EntityType, Guid EntityId, Guid? CaseId, string? BeforeJson, string? AfterJson, string? Source, DateTimeOffset OccurredAt);
public sealed record ClientConfigurationDto(Guid Id, string Code, string NameArabic, string NameEnglish, string OrganizationType, string? LogoUrl, string? ContactEmail, string? ContactPhone, int? PtpGraceDays, decimal? PtpToleranceAmount, bool IsActive);
public sealed record PortfolioConfigurationDto(Guid Id, Guid OrganizationId, string Code, string NameArabic, string NameEnglish, string CurrencyCode, decimal? TargetAmount, string? PrimaryClassification, string? SubClassification, int? PtpGraceDays, decimal? PtpToleranceAmount, bool IsActive, int CaseCount = 0);
public sealed record EnsureCollectionDeskRequest(Guid OrganizationId, [Required, MaxLength(20)] string PrimaryClassification, [Required, MaxLength(20)] string SubClassification);
public sealed record BucketConfigurationDto(Guid Id, Guid OrganizationId, Guid? PortfolioId, string Code, string NameArabic, string NameEnglish, int? MinimumDays, int? MaximumDays, int SortOrder, bool IsActive);
public sealed record CollectionsConfigurationDto(IReadOnlyCollection<ClientConfigurationDto> Clients, IReadOnlyCollection<PortfolioConfigurationDto> Portfolios, IReadOnlyCollection<BucketConfigurationDto> Buckets);
public sealed record SaveClientConfigurationRequest([Required, MaxLength(40)] string Code, [Required, MaxLength(200)] string NameArabic, [Required, MaxLength(200)] string NameEnglish, [Required, MaxLength(40)] string OrganizationType, [EmailAddress, MaxLength(256)] string? ContactEmail, [MaxLength(32)] string? ContactPhone, [Range(0, 30)] int? PtpGraceDays, [Range(typeof(decimal), "0", "999999999")] decimal? PtpToleranceAmount, bool IsActive = true);
public sealed record SavePortfolioConfigurationRequest(Guid OrganizationId, [Required, MaxLength(60)] string Code, [Required, MaxLength(200)] string NameArabic, [Required, MaxLength(200)] string NameEnglish, [Required, StringLength(3, MinimumLength = 3)] string CurrencyCode, [Range(typeof(decimal), "0", "9999999999999999")] decimal? TargetAmount, [MaxLength(20)] string? PrimaryClassification, [MaxLength(20)] string? SubClassification, [Range(0, 30)] int? PtpGraceDays, [Range(typeof(decimal), "0", "999999999")] decimal? PtpToleranceAmount, bool IsActive = true);
public sealed record SaveBucketConfigurationRequest(Guid OrganizationId, Guid? PortfolioId, [Required, MaxLength(40)] string Code, [Required, MaxLength(100)] string NameArabic, [Required, MaxLength(100)] string NameEnglish, int? MinimumDays, int? MaximumDays, int SortOrder, bool IsActive = true);

public sealed record CreateActivityRequest(
    [Required, MaxLength(40)] string ActivityType,
    [MaxLength(100)] string? Result,
    [MaxLength(4000)] string? Notes,
    [MaxLength(40)] string? Channel,
    DateTimeOffset? NextFollowUpAt);

public sealed record CreatePromiseRequest(
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal PromisedAmount,
    DateOnly PromiseDate,
    [Required, MaxLength(40)] string Channel,
    [MaxLength(2000)] string? Notes);

public sealed record SubmitPaymentRequest(
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal Amount,
    DateOnly PaymentDate,
    [Required, MaxLength(40)] string Method,
    [Required, MaxLength(160)] string ReferenceNumber,
    [Required, StringLength(3, MinimumLength = 3)] string CurrencyCode = "EGP");

public sealed record ReviewPaymentRequest(bool Approve, [MaxLength(1000)] string? RejectionReason);
public sealed record BulkAssignmentRequest(IReadOnlyCollection<Guid> CaseIds, Guid CollectorId, Guid? TeamId, [Required, MaxLength(500)] string Reason, bool Confirmed);
public sealed record BulkUnassignRequest(IReadOnlyCollection<Guid> CaseIds, [Required, MaxLength(500)] string Reason, bool Confirmed);

public sealed record CollectionFilters(
    int Page = 1, int PageSize = 20, string? Search = null, Guid? OrganizationId = null, Guid? PortfolioId = null,
    Guid? CollectorId = null, string? Bucket = null, string? Status = null, string? Priority = null, bool? Unassigned = null,
    string? SearchField = null);

public sealed record PromiseFilters(int Page = 1, int PageSize = 20, string? Search = null, Guid? OrganizationId = null, Guid? CollectorId = null, string? Status = null, DateOnly? From = null, DateOnly? To = null);
public sealed record PaymentFilters(int Page = 1, int PageSize = 20, string? Search = null, Guid? OrganizationId = null, Guid? CollectorId = null, string? Status = null, DateOnly? From = null, DateOnly? To = null);
