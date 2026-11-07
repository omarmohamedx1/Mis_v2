namespace MIS.Application.DTOs.Collections;

public sealed record CollectionScoreCellDto(
    string Category,
    int? CaseCount,
    decimal? ActualBalance,
    decimal? TargetPercent,
    decimal? TargetBalance,
    decimal? Missing,
    decimal? Percent,
    decimal? Weight,
    decimal? ActualVsTarget,
    decimal? Score);

public sealed record CollectionScoreBucketDto(
    string Code,
    string Version,
    IReadOnlyCollection<CollectionScoreCellDto> Categories);

public sealed record CollectionScoreSummaryDto(decimal? TotalDueAmount, decimal? Paid, decimal? ScoreAll);

public sealed record CollectionScoreOrganizationDto(
    Guid Id,
    string Code,
    string NameArabic,
    string NameEnglish,
    string OrganizationType,
    string? LogoUrl);

public sealed record CollectionScorePortfolioDto(
    Guid Id,
    Guid OrganizationId,
    string Code,
    string NameArabic,
    string NameEnglish,
    string CurrencyCode);

public sealed record CollectionScoreDashboardDto(
    CollectionScoreOrganizationDto Organization,
    string PortfolioType,
    CollectionScorePortfolioDto Portfolio,
    string ReportingPeriod,
    bool CanEdit,
    CollectionScoreSummaryDto Summary,
    IReadOnlyCollection<CollectionScoreBucketDto> Buckets);

public sealed record CollectionScorePeriodsDto(string CurrentPeriod, IReadOnlyCollection<string> Periods);

public sealed record SaveCollectionScoreBucketRequest(
    string ReportingPeriod,
    string ExpectedVersion,
    IReadOnlyCollection<SaveCollectionScoreCellRequest> Categories);

public sealed record SaveCollectionScoreCellRequest(
    string Category,
    int? CaseCount,
    decimal? ActualBalance,
    decimal? TargetPercent,
    decimal? TargetBalance,
    decimal? Missing,
    decimal? Percent,
    decimal? Weight,
    decimal? ActualVsTarget,
    decimal? Score);

public sealed record CollectionScoreAuditDto(
    Guid Id,
    string PortfolioType,
    Guid? PortfolioId,
    string ReportingPeriod,
    string Bucket,
    string Category,
    string? BeforeJson,
    string AfterJson,
    string ModifiedBy,
    DateTimeOffset ModifiedAt);
