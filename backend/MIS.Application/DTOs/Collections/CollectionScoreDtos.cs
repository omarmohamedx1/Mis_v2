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
    IReadOnlyCollection<CollectionScoreCellDto> Categories,
    int? CaseCount = null,
    decimal? ActualBalance = null,
    decimal? TargetBalance = null,
    decimal? Weight = null,
    decimal? Score = null,
    IReadOnlyCollection<string>? Issues = null,
    int SystemCaseCount = 0);

public sealed record CollectionScoreSummaryDto(decimal? TotalDueAmount, decimal? Paid, decimal? ScoreAll,
    int SystemCaseCount = 0, int PeriodCaseCount = 0, int UnclassifiedCaseCount = 0, decimal? UpperTableScore = null,
    int UnmappedSystemCaseCount = 0);

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

public sealed record CollectionScoreSourceImportDto(Guid Id, string FileName, int CaseCount);
public sealed record CollectionScoreSourceDto(string Version, IReadOnlyCollection<CollectionScoreSourceImportDto> Imports,
    Guid? OpeningImportId, string PaymentSource, IReadOnlyDictionary<string, string> Fields);
public sealed record SaveCollectionScoreSourceRequest(string ReportingPeriod, string ExpectedVersion, Guid OpeningImportId,
    bool CompleteOpeningPopulation, bool PaymentsReconciled, string PaymentSource, IReadOnlyDictionary<string, string> Fields);

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
