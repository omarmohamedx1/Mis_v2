using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/hr/employees/imports")]
[Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrEmployeeImportsController(IEmployeeImportService service) : ControllerBase
{
    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        var template = await service.BuildTemplateAsync(cancellationToken);
        return File(template.Content, template.ContentType, template.FileName);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<ActionResult<EmployeeImportUpload>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null) throw new HrValidationException("Select an employee import file.");
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadAsync(new HrUploadFile(file.FileName, file.ContentType, file.Length, stream), cancellationToken));
    }
    [HttpPost("{id:guid}/preview")]
    public async Task<ActionResult<EmployeeImportPreview>> Preview(Guid id, EmployeeImportMapping mapping, CancellationToken cancellationToken) => Ok(await service.PreviewAsync(id, mapping, cancellationToken));
    [HttpPost("{id:guid}/revise")]
    public async Task<ActionResult<EmployeeImportPreview>> Revise(Guid id, ReviseEmployeeImportRequest request, CancellationToken cancellationToken) => Ok(await service.ReviseAsync(id, request, cancellationToken));
    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<EmployeeImportResult>> Confirm(Guid id, ConfirmEmployeeImportRequest request, CancellationToken cancellationToken) => Ok(await service.ConfirmAsync(id, request.PreviewId, cancellationToken));
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EmployeeImportHistory>>> History(CancellationToken cancellationToken) => Ok(await service.HistoryAsync(cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteHistory(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteHistoryAsync(id, cancellationToken);
        return NoContent();
    }
}
