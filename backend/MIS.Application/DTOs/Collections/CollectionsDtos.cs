using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Collections;

public sealed record CollectionDashboardDto(
    int TotalCases, decimal TotalOutstanding, decimal TotalOverdue, int AssignedCases, int UnassignedCases,
    int ActiveCollectors, decimal CollectedToday, decimal CollectedMonthToDate, decimal AchievementPercent,
    int ActivePromises, int PromisesDueToday, int BrokenPromises, int VisitsToday, int PendingReviews,
    int OpenComplaints, int HighRiskCases);

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
    string? AssignedCollectorName, DateTimeOffset? NextFollowUpAt);

public sealed record CollectionActivityDto(Guid Id, string Type, string? Result, string? Notes, string? Channel, string CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset? NextFollowUpAt);
public sealed record PromiseToPayDto(Guid Id, Guid CaseId, string CaseNumber, string CustomerName, decimal PromisedAmount, DateOnly PromiseDate, decimal ActualPaidAmount, string Status, string CollectorName, string Channel, DateTimeOffset CreatedAt);
public sealed record CollectionPaymentDto(Guid Id, Guid CaseId, string CaseNumber, string CustomerName, decimal Amount, DateOnly PaymentDate, string Method, string ReferenceNumber, string Status, string SubmittedBy, DateTimeOffset SubmittedAt, string? VerifiedBy, DateTimeOffset? VerifiedAt, string? RejectionReason);

public sealed record CollectionCaseDetailsDto(
    Guid Id, string CaseNumber, string ClientName, string PortfolioName, string CustomerCode, string CustomerName,
    string NationalId, string PrimaryPhone, string AlternatePhone, string Address, string? Governorate, string? Area,
    string AccountReference, string? ContractReference, string? ProductType, decimal OriginalAmount, decimal OutstandingBalance,
    decimal OverdueBalance, decimal Penalties, decimal Fees, decimal TotalDue, int DaysPastDue, string Bucket, string Status,
    string Priority, int PriorityScore, string PriorityExplanation, string? AssignedCollectorName, bool SensitiveValuesRevealed,
    string RecommendedActionCode, string RecommendedActionReason,
    IReadOnlyCollection<CollectionActivityDto> Timeline, IReadOnlyCollection<PromiseToPayDto> Promises,
    IReadOnlyCollection<CollectionPaymentDto> Payments);

public sealed record WorkQueueDto(
    IReadOnlyCollection<CollectionCaseListItemDto> CallsDue, IReadOnlyCollection<CollectionCaseListItemDto> HighPriorityCases,
    IReadOnlyCollection<PromiseToPayDto> PromisesDue, IReadOnlyCollection<PromiseToPayDto> BrokenPromises,
    int VisitsToday, int PendingReviews, int OpenComplaints);

public sealed record AssignmentPreviewItemDto(Guid CollectorId, string CollectorName, int CurrentWorkload, int ProposedAdditionalCases, int ResultingWorkload);
public sealed record AssignmentPreviewDto(int CaseCount, IReadOnlyCollection<AssignmentPreviewItemDto> Collectors);
public sealed record AutoAssignmentRequest(IReadOnlyCollection<Guid> CaseIds, IReadOnlyCollection<Guid>? CollectorIds, Guid? TeamId, [Range(1, 5000)] int MaxActiveCases, bool Confirmed);
public sealed record AutoAssignmentCaseDto(Guid CaseId, string CaseNumber, Guid CollectorId, string CollectorName, string Reason);
public sealed record AutoAssignmentPreviewDto(string RuleCode, int CaseCount, IReadOnlyCollection<AssignmentPreviewItemDto> Collectors, IReadOnlyCollection<AutoAssignmentCaseDto> Assignments);
public sealed record CollectorLookupDto(Guid Id, string Name, int ActiveWorkload, Guid? TeamId, string? TeamName);
public sealed record FieldVisitDto(Guid Id, Guid CaseId, string CaseNumber, string CustomerName, Guid CollectorId, string CollectorName, DateTimeOffset ScheduledAt, string Status, string Address, string? Governorate, string? Area, string? Result, string? Notes);
public sealed record CreateVisitRequest(Guid CaseId, Guid CollectorId, DateTimeOffset ScheduledAt, [Required, MaxLength(600)] string Address, [MaxLength(100)] string? Governorate, [MaxLength(100)] string? Area);
public sealed record CompleteVisitRequest([Required, MaxLength(100)] string Result, [MaxLength(3000)] string? Notes);
public sealed record ComplaintDto(Guid Id, Guid CaseId, string CaseNumber, string CustomerName, string ClientName, string Reference, string Source, string Category, string Severity, string Description, DateTimeOffset ReceivedAt, DateTimeOffset? SlaDueAt, string SlaStatus, string Status, Guid? OwnerId, string? OwnerName, string? Resolution, DateTimeOffset? ClosedAt);
public sealed record CreateComplaintRequest(Guid CaseId, [Required, MaxLength(80)] string Reference, [Required, MaxLength(60)] string Source, [Required, MaxLength(100)] string Category, [Required, MaxLength(30)] string Severity, [Required, MaxLength(4000)] string Description, DateTimeOffset ReceivedAt, DateTimeOffset SlaDueAt, Guid OwnerId);
public sealed record ChangeComplaintStatusRequest([Required, MaxLength(32)] string Status, [MaxLength(4000)] string? Resolution);
public sealed record CollectionAuditDto(Guid Id, string UserName, string Action, string EntityType, Guid EntityId, Guid? CaseId, string? BeforeJson, string? AfterJson, string? Source, DateTimeOffset OccurredAt);
public sealed record ClientConfigurationDto(Guid Id, string Code, string NameArabic, string NameEnglish, string OrganizationType, string? LogoUrl, string? ContactEmail, string? ContactPhone, string SettingsJson, bool IsActive);
public sealed record PortfolioConfigurationDto(Guid Id, Guid OrganizationId, string Code, string NameArabic, string NameEnglish, string CurrencyCode, decimal? TargetAmount, string SettingsJson, bool IsActive);
public sealed record BucketConfigurationDto(Guid Id, Guid OrganizationId, Guid? PortfolioId, string Code, string NameArabic, string NameEnglish, int? MinimumDays, int? MaximumDays, int SortOrder, bool IsActive);
public sealed record CollectionsConfigurationDto(IReadOnlyCollection<ClientConfigurationDto> Clients, IReadOnlyCollection<PortfolioConfigurationDto> Portfolios, IReadOnlyCollection<BucketConfigurationDto> Buckets);
public sealed record SaveClientConfigurationRequest([Required, MaxLength(40)] string Code, [Required, MaxLength(200)] string NameArabic, [Required, MaxLength(200)] string NameEnglish, [Required, MaxLength(40)] string OrganizationType, [EmailAddress, MaxLength(256)] string? ContactEmail, [MaxLength(32)] string? ContactPhone, [MaxLength(20000)] string? SettingsJson, bool IsActive = true);
public sealed record SavePortfolioConfigurationRequest(Guid OrganizationId, [Required, MaxLength(60)] string Code, [Required, MaxLength(200)] string NameArabic, [Required, MaxLength(200)] string NameEnglish, [Required, StringLength(3, MinimumLength = 3)] string CurrencyCode, [Range(typeof(decimal), "0", "9999999999999999")] decimal? TargetAmount, [MaxLength(20000)] string? SettingsJson, bool IsActive = true);
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

public sealed record CollectionFilters(
    int Page = 1, int PageSize = 20, string? Search = null, Guid? OrganizationId = null, Guid? PortfolioId = null,
    Guid? CollectorId = null, string? Bucket = null, string? Status = null, string? Priority = null);

public sealed record PromiseFilters(int Page = 1, int PageSize = 20, string? Search = null, Guid? OrganizationId = null, Guid? CollectorId = null, string? Status = null, DateOnly? From = null, DateOnly? To = null);
public sealed record PaymentFilters(int Page = 1, int PageSize = 20, string? Search = null, Guid? OrganizationId = null, string? Status = null, DateOnly? From = null, DateOnly? To = null);
