using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/hr/social-insurance/imports")]
[Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrSocialInsuranceImportsController(ISocialInsuranceImportService service) : ControllerBase
{
    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        var template = await service.BuildTemplateAsync(cancellationToken);
        return File(template.Content, template.ContentType, template.FileName);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ExcelImportLimits.RequestBytes)]
    public async Task<ActionResult<SocialInsuranceImportUpload>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null) throw new HrValidationException("Select an insurance import file.");
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadAsync(new HrUploadFile(file.FileName, file.ContentType, file.Length, stream), cancellationToken));
    }
    [HttpPost("{id:guid}/preview")]
    public async Task<ActionResult<SocialInsuranceImportPreview>> Preview(Guid id, SocialInsuranceImportMapping mapping, CancellationToken cancellationToken) => Ok(await service.PreviewAsync(id, mapping, cancellationToken));
    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<SocialInsuranceImportResult>> Confirm(Guid id, ConfirmSocialInsuranceImportRequest request, CancellationToken cancellationToken) => Ok(await service.ConfirmAsync(id, request.PreviewId, cancellationToken, request.ExcludedRows));
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SocialInsuranceImportHistory>>> History(CancellationToken cancellationToken) => Ok(await service.HistoryAsync(cancellationToken));
}
