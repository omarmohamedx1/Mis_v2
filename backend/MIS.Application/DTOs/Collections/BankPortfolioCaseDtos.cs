namespace MIS.Application.DTOs.Collections;

public sealed record BankPortfolioCaseQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Status = null,
    Guid? CollectorId = null, string? SortBy = null, string? SortDirection = null);
public sealed record BankPortfolioAccessDto(bool IsManager, bool CanEdit, bool CanAssign, bool CanExport, IReadOnlyCollection<string> Statuses);
public sealed record BankPortfolioCollectorDto(Guid Id, string Name);
public sealed record BankPortfolioCaseListItemDto(Guid Id, string CaseNumber, string CustomerName, string? Mobile,
    decimal OutstandingAmount, Guid? AssignedCollectorId, string? AssignedCollectorName, string Status,
    DateTimeOffset? LastActivityAt, DateTimeOffset? NextFollowUpAt);
public sealed record BankPortfolioCasePageDto(IReadOnlyCollection<BankPortfolioCaseListItemDto> Items, int TotalCount,
    int Page, int PageSize, int TotalPages, BankPortfolioAccessDto Access);
public sealed record BankPortfolioCaseDetailsDto(Guid Id, string CaseNumber, string CustomerName, string CustomerCode,
    string? Mobile, string? AlternativeMobile, string? NationalId, string? Address, string? SecondaryAddress, string BankName, string PortfolioName,
    string AccountReference, string? ContractReference, string? ProductType, decimal OriginalAmount,
    decimal OutstandingAmount, decimal PaidAmount, decimal RemainingAmount, string Status, Guid? AssignedCollectorId,
    string? AssignedCollectorName, DateTimeOffset? LastActivityAt, DateTimeOffset? NextFollowUpAt, string? LatestNote,
    Guid? SourceImportId, string? ImportedFrom, DateTimeOffset? ImportDate, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    BankPortfolioAccessDto Access);
public sealed record UpdateBankPortfolioCaseRequest(string? Mobile, string? AlternativeMobile, string? Address,
    string Status, DateTimeOffset? NextFollowUpAt);
public sealed record AssignBankPortfolioCasesRequest(IReadOnlyCollection<Guid> CaseIds, Guid CollectorId, string Reason);
public sealed record BankPortfolioAssignmentPreviewDto(int CaseCount, Guid CollectorId, string CollectorName);
