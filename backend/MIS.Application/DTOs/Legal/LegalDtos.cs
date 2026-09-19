namespace MIS.Application.DTOs.Legal;

public sealed record LegalDashboardDto(
    int OpenCases,
    int IntakeCases,
    int HearingsThisWeek,
    int ReturnedThisMonth,
    decimal OutstandingTotal,
    bool CanManage);

public sealed record LegalOrganizationDto(Guid Id, string Code, string Name);

public sealed record LegalCaseListItemDto(
    Guid CollectionCaseId,
    Guid? FileId,
    string CaseNumber,
    string CustomerName,
    string OrganizationName,
    string PortfolioName,
    decimal OutstandingBalance,
    int DaysPastDue,
    string CollectionStatus,
    string LegalStage,
    DateOnly? NextHearingOn,
    DateTimeOffset UpdatedAt);

public sealed record LegalCasePageDto(IReadOnlyList<LegalCaseListItemDto> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record LegalCaseActionDto(
    Guid Id,
    string ActionType,
    string? Result,
    string Notes,
    DateOnly? HappenedOn,
    string CreatedByName,
    DateTimeOffset CreatedAt);

public sealed record LegalCaseDetailsDto(
    Guid CollectionCaseId,
    Guid FileId,
    string CaseNumber,
    string AccountReference,
    string CustomerName,
    string? NationalId,
    string? PrimaryPhone,
    string OrganizationName,
    string PortfolioName,
    decimal OriginalAmount,
    decimal OutstandingBalance,
    int DaysPastDue,
    string CollectionStatus,
    string LegalStage,
    string? CourtName,
    string? CourtCaseNumber,
    string? LawyerName,
    DateOnly? NextHearingOn,
    string? Notes,
    DateTimeOffset ReceivedAt,
    string ReceivedByName,
    DateTimeOffset UpdatedAt,
    bool CanManage,
    IReadOnlyList<LegalCaseActionDto> Actions);

public sealed record SaveLegalFileRequest(
    string? CourtName,
    string? CourtCaseNumber,
    string? LawyerName,
    DateOnly? NextHearingOn,
    string? Notes);

public sealed record RecordLegalActionRequest(
    string ActionType,
    string Notes,
    string? Result,
    DateOnly? HappenedOn);
