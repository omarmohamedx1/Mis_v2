using MIS.Application.DTOs.Collections;

namespace MIS.Application.Interfaces;

public interface ICollectionScoreService
{
    Task<CollectionScoreSourceDto> GetSourceAsync(Guid organizationId, string portfolioType, Guid portfolioId, string reportingPeriod, CancellationToken token);
    Task SaveSourceAsync(Guid organizationId, string portfolioType, Guid portfolioId, SaveCollectionScoreSourceRequest request, CancellationToken token);
    Task<IReadOnlyCollection<CollectionScorePortfolioDto>> GetPortfoliosAsync(Guid organizationId, string portfolioType, CancellationToken token);
    Task<CollectionScoreDashboardDto> GetDashboardAsync(Guid organizationId, string portfolioType, Guid portfolioId, string? reportingPeriod, CancellationToken token);
    Task<CollectionScorePeriodsDto> GetPeriodsAsync(Guid organizationId, string portfolioType, Guid portfolioId, CancellationToken token);
    Task<CollectionScoreSummaryDto> GetSummaryAsync(Guid organizationId, string portfolioType, Guid portfolioId, string? reportingPeriod, CancellationToken token);
    Task<IReadOnlyCollection<CollectionScoreAuditDto>> GetHistoryAsync(Guid organizationId, string portfolioType, Guid portfolioId, string reportingPeriod, string? bucket, int page, int pageSize, CancellationToken token);
    Task<CollectionScoreBucketDto> SaveBucketAsync(Guid organizationId, string portfolioType, Guid portfolioId, string bucket, SaveCollectionScoreBucketRequest request, CancellationToken token);
}
