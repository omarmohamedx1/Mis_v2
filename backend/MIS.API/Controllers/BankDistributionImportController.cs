using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/banks/{organizationId:guid}/distribution/import")]
[Route("api/installment-companies/{organizationId:guid}/distribution/import")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAssignmentManage)]
public sealed class BankDistributionImportController(IBankDistributionImportService imports) : ControllerBase
{
    private const long RequestLimit = ExcelImportLimits.RequestBytes;

    [HttpGet("collectors")]
    public Task<IReadOnlyCollection<DistributionCollectorDto>> Collectors(Guid organizationId, CancellationToken token)
        => imports.GetCollectorsAsync(organizationId, token);

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    public async Task<ActionResult<BankDistributionImportUpload>> Upload(Guid organizationId, IFormFile file, CancellationToken token)
    {
        if (file is null || file.Length == 0) throw new HrValidationException("A distribution import file is required.");
        await using var stream = file.OpenReadStream();
        return Ok(await imports.UploadAsync(organizationId, new HrUploadFile(file.FileName, file.ContentType, file.Length, stream), token));
    }

    [HttpPost("{id:guid}/preview")]
    public Task<BankDistributionImportPreview> Preview(Guid organizationId, Guid id, [FromBody] BankDistributionImportMapping mapping, CancellationToken token)
        => imports.PreviewAsync(organizationId, id, mapping, token);

    [HttpPost("{id:guid}/confirm")]
    public Task<BankDistributionImportResult> Confirm(Guid organizationId, Guid id, [FromBody] ConfirmBankDistributionImportRequest request, CancellationToken token)
        => imports.ConfirmAsync(organizationId, id, request.PreviewId, request.ReassignExisting, token, request.ExcludedRows);
}

public sealed class ConfirmBankDistributionImportRequest
{
    public Guid PreviewId { get; init; }
    public bool ReassignExisting { get; init; }
    public IReadOnlyCollection<int>? ExcludedRows { get; init; }
}
