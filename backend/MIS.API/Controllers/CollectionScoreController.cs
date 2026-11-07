using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/collections/score/organizations/{organizationId:guid}")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAccess)]
public sealed class CollectionScoreController : ControllerBase
{
    private readonly ICollectionScoreService _score;

    public CollectionScoreController(ICollectionScoreService score) => _score = score;

    [HttpGet("{portfolioType}/portfolios")]
    public Task<IReadOnlyCollection<CollectionScorePortfolioDto>> Portfolios(
        Guid organizationId, string portfolioType, CancellationToken token) =>
        _score.GetPortfoliosAsync(organizationId, portfolioType, token);

    [HttpGet("{portfolioType}/portfolios/{portfolioId:guid}")]
    public Task<CollectionScoreDashboardDto> Dashboard(
        Guid organizationId, string portfolioType, Guid portfolioId, [FromQuery] string? reportingPeriod, CancellationToken token) =>
        _score.GetDashboardAsync(organizationId, portfolioType, portfolioId, reportingPeriod, token);

    [HttpGet("{portfolioType}/portfolios/{portfolioId:guid}/periods")]
    public Task<CollectionScorePeriodsDto> Periods(Guid organizationId, string portfolioType, Guid portfolioId, CancellationToken token) =>
        _score.GetPeriodsAsync(organizationId, portfolioType, portfolioId, token);

    [HttpGet("{portfolioType}/portfolios/{portfolioId:guid}/summary")]
    public Task<CollectionScoreSummaryDto> Summary(
        Guid organizationId, string portfolioType, Guid portfolioId, [FromQuery] string? reportingPeriod, CancellationToken token) =>
        _score.GetSummaryAsync(organizationId, portfolioType, portfolioId, reportingPeriod, token);

    [HttpGet("{portfolioType}/portfolios/{portfolioId:guid}/history")]
    public Task<IReadOnlyCollection<CollectionScoreAuditDto>> History(
        Guid organizationId,
        string portfolioType,
        Guid portfolioId,
        [FromQuery] string reportingPeriod,
        [FromQuery] string? bucket,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken token = default) =>
        _score.GetHistoryAsync(organizationId, portfolioType, portfolioId, reportingPeriod, bucket, page, pageSize, token);

    [HttpPut("{portfolioType}/portfolios/{portfolioId:guid}/{bucket}")]
    [Authorize(Policy = AuthorizationPolicies.CollectionsConfigurationManage)]
    public Task<CollectionScoreBucketDto> SaveBucket(
        Guid organizationId,
        string portfolioType,
        Guid portfolioId,
        string bucket,
        SaveCollectionScoreBucketRequest request,
        CancellationToken token) =>
        _score.SaveBucketAsync(organizationId, portfolioType, portfolioId, bucket, request, token);
}
