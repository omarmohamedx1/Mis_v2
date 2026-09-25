using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/hr/attendance/imports")]
[Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrAttendanceImportsController : ControllerBase
{
    private const long MaximumImportBytes = ExcelImportLimits.MaximumBytes;
    private readonly IHrAttendanceImportService _service;

    public HrAttendanceImportsController(IHrAttendanceImportService service)
    {
        _service = service;
    }

    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        var template = await _service.BuildTemplateAsync(cancellationToken);
        return File(template.Content, template.ContentType, template.FileName);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumImportBytes + 1024 * 1024)]
    public async Task<ActionResult<AttendanceImportUploadDto>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0) throw new HrValidationException("Select a non-empty CSV or Excel file.");
        if (file.Length > MaximumImportBytes) throw new HrValidationException("Attendance import files cannot exceed 50 MB.");

        await using var stream = file.OpenReadStream();
        var uploaded = await _service.UploadAsync(
            new AttendanceImportFile(file.FileName, file.ContentType, file.Length, stream),
            cancellationToken);
        return CreatedAtAction(nameof(GetBatch), new { id = uploaded.BatchId }, uploaded);
    }

    [HttpPost("{id:guid}/preview")]
    public async Task<ActionResult<AttendanceImportBatchDto>> BuildPreview(
        Guid id,
        AttendanceImportColumnMappingRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.BuildPreviewAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AttendanceImportBatchDto>> GetBatch(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetBatchAsync(id, cancellationToken));

    [HttpGet("{id:guid}/preview")]
    public async Task<ActionResult<PagedAttendanceImportPreviewDto>> GetPreview(
        Guid id,
        [FromQuery] AttendanceImportPreviewFilterDto filter,
        CancellationToken cancellationToken)
        => Ok(await _service.GetPreviewAsync(id, filter, cancellationToken));

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<AttendanceImportConfirmResultDto>> Confirm(
        Guid id,
        ConfirmAttendanceImportRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.ConfirmAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<AttendanceImportBatchDto>> Cancel(
        Guid id,
        CancelAttendanceImportRequest? request,
        CancellationToken cancellationToken)
        => Ok(await _service.CancelAsync(id, request ?? new CancelAttendanceImportRequest(), cancellationToken));

    [HttpGet]
    public async Task<ActionResult<PagedAttendanceImportHistoryDto>> GetHistory(
        [FromQuery] AttendanceImportHistoryFilterDto filter,
        CancellationToken cancellationToken)
        => Ok(await _service.GetHistoryAsync(filter, cancellationToken));
}
