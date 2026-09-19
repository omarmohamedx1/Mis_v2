namespace MIS.Application.DTOs.Collections;

public sealed record CaseDistributionQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Status = null,
    Guid? CollectorId = null, Guid? ImportId = null, string? SortBy = null, string? SortDirection = null);
public sealed record CaseDistributionSummaryDto(int TotalCases, int UnassignedCases, int AssignedCases, int Collectors);
public sealed record CaseDistributionItemDto(Guid Id, string CaseNumber, string CustomerName, string? Mobile,
    decimal OutstandingAmount, string Status, Guid? CollectorId, string? CollectorName, DateTimeOffset? AssignedAt,
    Guid? ImportId, string? ImportName);
public sealed record CaseDistributionPageDto(IReadOnlyCollection<CaseDistributionItemDto> Items, int TotalCount,
    int Page, int PageSize, int TotalPages);
public sealed record DistributionCollectorDto(Guid Id, string Name, int AssignedCases, decimal TotalOutstanding);
public sealed record DistributionImportDto(Guid Id, string Name);
public sealed record DistributionMutationRequest(IReadOnlyCollection<Guid> CaseIds, Guid? CollectorId, string Reason);
public sealed record DistributionPreviewDto(int CaseCount, decimal TotalOutstanding, Guid? CollectorId,
    string? CollectorName, IReadOnlyCollection<string> PreviousCollectors);
public sealed record DistributionResultDto(int CaseCount, Guid? CollectorId, string? CollectorName);
public sealed record AutoDistributionRequest(IReadOnlyCollection<Guid>? CaseIds, IReadOnlyCollection<Guid> CollectorIds, string Method, string Reason);
public sealed record AutoDistributionCollectorDto(Guid CollectorId, string CollectorName, int CaseCount, decimal OutstandingAmount);
public sealed record AutoDistributionPreviewDto(string Method, int TotalCases, decimal TotalOutstanding,
    IReadOnlyCollection<AutoDistributionCollectorDto> Collectors);

