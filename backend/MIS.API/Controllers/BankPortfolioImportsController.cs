using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/banks/{bankId:guid}/portfolio-imports")]
[Route("api/installment-companies/{bankId:guid}/portfolio-imports")]
[Authorize(Policy = AuthorizationPolicies.CollectionsImportManage)]
public sealed class BankPortfolioImportsController : ControllerBase
{
    private readonly IBankPortfolioImportService _imports;
    public BankPortfolioImportsController(IBankPortfolioImportService imports) => _imports = imports;

    [HttpGet]
    public Task<BankPortfolioImportPageDto> History(Guid bankId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, CancellationToken token = default) => _imports.GetHistoryAsync(bankId, page, pageSize, search, token);

    [HttpGet("{importId:guid}")]
    public Task<BankPortfolioImportDto> Get(Guid bankId, Guid importId, CancellationToken token) => _imports.GetAsync(bankId, importId, token);

    [HttpPost]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<ActionResult<BankPortfolioImportDto>> Upload(Guid bankId, [FromForm] IFormFile file, CancellationToken token)
    {
        if (file is null || file.Length == 0) return BadRequest(MIS.Application.Common.ApiErrorResponse.Failure("A non-empty XLSX, XLS, or CSV file is required."));
        await using var stream = file.OpenReadStream();
        var result = await _imports.UploadAsync(bankId, file.FileName, file.ContentType ?? "application/octet-stream", file.Length, stream, token);
        return Created($"/api/banks/{bankId}/portfolio-imports/{result.Id}", result);
    }

    [HttpPost("{importId:guid}/preview-data")]
    public Task<BankPortfolioImportDataPreviewDto> PreviewData(Guid bankId, Guid importId, CancellationToken token) =>
        _imports.PreviewDataAsync(bankId, importId, token);

    [HttpPost("{importId:guid}/confirm")]
    public Task<BankPortfolioImportConfirmResultDto> Confirm(Guid bankId, Guid importId, [FromBody] UpdateBankPortfolioImportRequest? request, CancellationToken token) =>
        _imports.ConfirmAsync(bankId, importId, request?.Notes, token);

    [HttpPatch("{importId:guid}")]
    public Task<BankPortfolioImportDto> Update(Guid bankId, Guid importId, UpdateBankPortfolioImportRequest request, CancellationToken token) => _imports.UpdateNotesAsync(bankId, importId, request.Notes, token);

    [HttpPost("{importId:guid}/replacement")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<BankPortfolioReplacementPreviewDto> PreviewReplacement(Guid bankId, Guid importId, [FromForm] IFormFile file, CancellationToken token)
    {
        if (file is null || file.Length == 0) throw new MIS.Application.Common.HrValidationException("A non-empty XLSX, XLS, or CSV file is required.");
        await using var stream = file.OpenReadStream();
        return await _imports.PreviewReplacementAsync(bankId, importId, file.FileName, file.ContentType ?? "application/octet-stream", file.Length, stream, token);
    }

    [HttpPost("{importId:guid}/replacement/confirm")]
    public Task<BankPortfolioImportDto> ConfirmReplacement(Guid bankId, Guid importId, ConfirmBankPortfolioReplacementRequest request, CancellationToken token) => _imports.ConfirmReplacementAsync(bankId, importId, request.Token, token);

    [HttpDelete("{importId:guid}")]
    public async Task<IActionResult> Delete(Guid bankId, Guid importId, CancellationToken token)
    { await _imports.DeleteAsync(bankId, importId, token); return NoContent(); }
}
