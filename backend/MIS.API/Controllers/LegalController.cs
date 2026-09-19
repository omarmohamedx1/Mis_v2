using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.DTOs.Legal;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/legal")]
[Authorize]
public sealed class LegalController(ILegalService legal) : ControllerBase
{
    [HttpGet("dashboard")]
    [Authorize(Policy = AuthorizationPolicies.LegalAccess)]
    public Task<LegalDashboardDto> Dashboard(CancellationToken token) => legal.GetDashboardAsync(token);

    [HttpGet("organizations")]
    [Authorize(Policy = AuthorizationPolicies.LegalAccess)]
    public Task<IReadOnlyList<LegalOrganizationDto>> Organizations(CancellationToken token) => legal.ListOrganizationsAsync(token);

    [HttpGet("cases")]
    [Authorize(Policy = AuthorizationPolicies.LegalAccess)]
    public Task<LegalCasePageDto> Cases([FromQuery] string? search, [FromQuery] Guid? organizationId, [FromQuery] string? stage, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken token = default)
        => legal.ListCasesAsync(search, organizationId, stage, page, pageSize, token);

    [HttpGet("cases/{collectionCaseId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.LegalAccess)]
    public Task<LegalCaseDetailsDto> Case(Guid collectionCaseId, CancellationToken token) => legal.GetCaseAsync(collectionCaseId, token);

    [HttpPut("cases/{collectionCaseId:guid}/file")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    public Task<LegalCaseDetailsDto> SaveFile(Guid collectionCaseId, [FromBody] SaveLegalFileRequest request, CancellationToken token)
        => legal.SaveFileAsync(collectionCaseId, request, token);

    [HttpPost("cases/{collectionCaseId:guid}/actions")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    public Task<LegalCaseDetailsDto> RecordAction(Guid collectionCaseId, [FromBody] RecordLegalActionRequest request, CancellationToken token)
        => legal.RecordActionAsync(collectionCaseId, request, token);
}
