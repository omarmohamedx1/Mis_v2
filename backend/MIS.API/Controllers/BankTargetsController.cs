using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Application.Scoring;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/collections/bank-targets")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAccess)]
public sealed class BankTargetsController(IBankTargetsService targets) : ControllerBase
{
    [HttpGet("template")]
    public BankTargetValues Template() => BankTargetPolicy.ReferenceTemplate();

    [HttpGet("{organizationId:guid}/{portfolioType}/{portfolioId:guid}/{period}")]
    public Task<BankTargetsDto> Get(Guid organizationId, string portfolioType, Guid portfolioId, string period, CancellationToken token) =>
        targets.GetTargetsAsync(organizationId, portfolioType, portfolioId, period, token);

    [HttpPut("{organizationId:guid}/{portfolioType}/{portfolioId:guid}/{period}")]
    [Authorize(Policy = AuthorizationPolicies.CollectionsConfigurationManage)]
    public Task<BankTargetsDto> Save(Guid organizationId, string portfolioType, Guid portfolioId, string period,
        SaveBankTargetsRequest request, CancellationToken token) =>
        targets.SaveTargetsAsync(organizationId, portfolioType, portfolioId, period, request, token);

    [HttpPost("{organizationId:guid}/{portfolioType}/{portfolioId:guid}/{period}/copy-previous")]
    [Authorize(Policy = AuthorizationPolicies.CollectionsConfigurationManage)]
    public Task<BankTargetsDto> Copy(Guid organizationId, string portfolioType, Guid portfolioId, string period, CancellationToken token) =>
        targets.CopyTargetsAsync(organizationId, portfolioType, portfolioId, period, token);

    [HttpPost("{organizationId:guid}/{portfolioType}/{portfolioId:guid}/{period}/review")]
    public Task<BankTargetsDto> Review(Guid organizationId, string portfolioType, Guid portfolioId, string period,
        BankTargetVersionRequest request, CancellationToken token) =>
        targets.ReviewTargetsAsync(organizationId, portfolioType, portfolioId, period, request, false, token);

    [HttpPost("{organizationId:guid}/{portfolioType}/{portfolioId:guid}/{period}/approve")]
    public Task<BankTargetsDto> Approve(Guid organizationId, string portfolioType, Guid portfolioId, string period,
        BankTargetVersionRequest request, CancellationToken token) =>
        targets.ReviewTargetsAsync(organizationId, portfolioType, portfolioId, period, request, true, token);
}
