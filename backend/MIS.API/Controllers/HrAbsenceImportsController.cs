using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/hr/absences/imports")]
[Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrAbsenceImportsController(IAbsenceImportService service) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<ActionResult<AbsenceImportUpload>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null) throw new HrValidationException("Select an absence import file.");
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadAsync(new HrUploadFile(file.FileName, file.ContentType, file.Length, stream), cancellationToken));
    }

    [HttpPost("{id:guid}/preview")]
    public async Task<ActionResult<AbsenceImportPreview>> Preview(Guid id, AbsenceImportMapping mapping, CancellationToken cancellationToken) =>
        Ok(await service.PreviewAsync(id, mapping, cancellationToken));

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<AbsenceImportResult>> Confirm(Guid id, ConfirmAbsenceImportRequest request, CancellationToken cancellationToken) =>
        Ok(await service.ConfirmAsync(id, request.PreviewId, cancellationToken));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AbsenceImportHistory>>> History(CancellationToken cancellationToken) =>
        Ok(await service.HistoryAsync(cancellationToken));
}
