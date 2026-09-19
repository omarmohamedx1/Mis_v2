namespace MIS.Application.DTOs.Collections;

public sealed record BankCustomerQuery(int Page = 1, int PageSize = 20, string? Search = null);

public sealed record BankCustomerListItemDto(
    Guid Id,
    string CustomerName,
    string? Mobile,
    string? NationalId,
    string? AccountReference,
    string? ContractReference,
    decimal OutstandingAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    string? Status,
    string? AssignedCollectorName,
    int CaseCount);

public sealed record BankCustomerPageDto(
    IReadOnlyCollection<BankCustomerListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record BankCustomerCaseSummaryDto(
    Guid CaseId,
    string CaseNumber,
    string AccountReference,
    string? ContractReference,
    string? ProductType,
    string PortfolioName,
    decimal OriginalAmount,
    decimal OutstandingAmount,
    decimal OverdueAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    int DaysPastDue,
    string Status,
    Guid? AssignedCollectorId,
    string? AssignedCollectorName,
    DateTimeOffset? AssignmentDate,
    DateTimeOffset? LastPaymentAt,
    DateTimeOffset? NextFollowUpAt);

public sealed record BankCustomerLinkSummaryDto(
    string Module,
    Guid Id,
    string Title,
    string? Status,
    DateTimeOffset OccurredAt,
    decimal? Amount);

public sealed record BankCustomerDetailsDto(
    Guid Id,
    string CustomerCode,
    string CustomerName,
    string? FullNameArabic,
    string? FullNameEnglish,
    string? Mobile,
    string? AlternativeMobile,
    string? NationalId,
    string? Address,
    string? SecondaryAddress,
    string? Governorate,
    string? Area,
    string OrganizationName,
    string OrganizationType,
    decimal TotalOriginalAmount,
    decimal TotalOutstandingAmount,
    decimal TotalPaidAmount,
    decimal TotalRemainingAmount,
    decimal TotalOverdueAmount,
    IReadOnlyCollection<BankCustomerCaseSummaryDto> Cases,
    IReadOnlyCollection<BankCustomerLinkSummaryDto> Payments,
    IReadOnlyCollection<BankCustomerLinkSummaryDto> Promises,
    IReadOnlyCollection<BankCustomerLinkSummaryDto> Visits,
    IReadOnlyCollection<BankCustomerLinkSummaryDto> Complaints,
    IReadOnlyCollection<BankCustomerLinkSummaryDto> Activities,
    IReadOnlyCollection<BankCustomerLinkSummaryDto> Timeline);
