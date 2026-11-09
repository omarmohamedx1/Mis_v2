using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.DTOs.Finance;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/finance/claims")]
[Authorize(Policy = AuthorizationPolicies.FinanceAccess)]
public sealed class FinanceClaimsController(IFinanceClaimsService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsView)]
    public Task<FinanceClaimPage> List([FromQuery] FinanceClaimFilter filter, CancellationToken token)
        => service.ListAsync(filter, token);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsView)]
    public Task<FinanceClaimDetails> Details(Guid id, CancellationToken token)
        => service.DetailsAsync(id, token);

    [HttpGet("organizations")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsView)]
    public Task<IReadOnlyList<FinanceClaimOrganization>> Organizations(CancellationToken token)
        => service.OrganizationsAsync(token);

    [HttpGet("banks")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsView)]
    public Task<IReadOnlyList<FinanceClaimOrganization>> Banks(CancellationToken token)
        => service.OrganizationsAsync(token);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsCreate)]
    public Task<FinanceClaimListItem> Create(SaveFinanceClaimRequest request, CancellationToken token)
        => service.CreateAsync(request, token);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsEdit)]
    public Task<FinanceClaimListItem> Update(Guid id, SaveFinanceClaimRequest request, CancellationToken token)
        => service.UpdateAsync(id, request, token);

    [HttpPost("{id:guid}/confirm-receipt")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsConfirmReceipt)]
    public Task<FinanceClaimDetails> ConfirmReceipt(Guid id, ConfirmFinanceClaimReceiptRequest request, CancellationToken token)
        => service.ConfirmReceiptAsync(id, request, token);

    [HttpPost("{id:guid}/post-historical-receipt")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsConfirmReceipt)]
    public Task<FinanceClaimDetails> PostHistoricalReceipt(Guid id, CancellationToken token)
        => service.PostHistoricalReceiptAsync(id, token);

    [HttpPost("{id:guid}/reverse-receipt")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsReverseReceipt)]
    public Task<FinanceClaimDetails> ReverseReceipt(Guid id, ReverseFinanceClaimReceiptRequest request, CancellationToken token)
        => service.ReverseReceiptAsync(id, request, token);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsCancel)]
    public Task<FinanceClaimDetails> Cancel(Guid id, [FromBody] CancelFinanceClaimRequest request, CancellationToken token)
        => service.CancelAsync(id, request.Reason, token);

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsCancel)]
    public Task<FinanceClaimDetails> Reactivate(Guid id, CancellationToken token)
        => service.ReactivateAsync(id, token);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token)
    {
        await service.DeleteAsync(id, token);
        return NoContent();
    }

    [HttpPost("import/preview")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsImport)]
    [RequestSizeLimit(52 * 1024 * 1024)]
    public async Task<FinanceClaimImportPreview> PreviewImport([FromForm] IFormFile file, CancellationToken token)
    {
        if (file is null || file.Length <= 0) throw new MIS.Application.Common.HrValidationException("Choose a Claims workbook to upload.");
        await using var stream = file.OpenReadStream();
        return await service.PreviewImportAsync(file.FileName, stream, token);
    }

    [HttpPost("import/confirm")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsImport)]
    public Task<FinanceClaimImportResult> ConfirmImport(FinanceClaimImportConfirmRequest request, CancellationToken token)
        => service.ConfirmImportAsync(request, token);

    [HttpGet("export")]
    [Authorize(Policy = AuthorizationPolicies.FinanceClaimsExport)]
    public async Task<IActionResult> Export([FromQuery] FinanceClaimFilter filter, CancellationToken token)
        => File(await service.ExportAsync(filter, token),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"MIS-Claims-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
}

public sealed record CancelFinanceClaimRequest(string? Reason);
