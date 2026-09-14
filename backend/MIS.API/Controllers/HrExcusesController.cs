using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
namespace MIS.API.Controllers;

[ApiController, Route("api/hr/excuses"), Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrExcusesController(IHrExcuseMissionService service) : ControllerBase
{
    [HttpGet("types")] public IActionResult Types() => Ok(service.Types());
    [HttpGet("visits/{visitId:guid}")] public async Task<IActionResult> Original(Guid visitId, CancellationToken ct) => Ok(await service.OriginalVisitAsync(visitId, ct));
    [HttpGet] public async Task<IActionResult> List([FromQuery] ExcuseFilter filter, CancellationToken ct) => Ok(await service.ListAsync(filter, ct));
    [HttpGet("notifications")] public async Task<IActionResult> Notifications(CancellationToken ct) => Ok(await service.NotificationsAsync(ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Details(Guid id, CancellationToken ct) => Ok(await service.DetailsAsync(id, ct));
    [HttpPost] public async Task<IActionResult> Create(ManualMissionRequest request, CancellationToken ct) => Ok(await service.SaveManualAsync(null, request, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Edit(Guid id, ManualMissionRequest request, CancellationToken ct) => Ok(await service.SaveManualAsync(id, request, ct));
    [HttpPost("{id:guid}/decision")] public async Task<IActionResult> Decide(Guid id, MissionDecisionRequest request, CancellationToken ct) { await service.DecideAsync(id, request, ct); return NoContent(); }
    [HttpPost("{id:guid}/cancel")] public async Task<IActionResult> Cancel(Guid id, CancelMissionRequest request, CancellationToken ct) { await service.CancelAsync(id, request, ct); return NoContent(); }
    [HttpPost("collectors/{id:guid}/employee")] public async Task<IActionResult> Link(Guid id, LinkCollectorEmployeeRequest request, CancellationToken ct) { await service.LinkEmployeeAsync(id, request.EmployeeId, ct); return NoContent(); }
    [HttpPost("{id:guid}/attachments"), Consumes("multipart/form-data"), RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid id, [FromForm] ExcuseUploadForm form, CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        await service.UploadAsync(id, form.AttachmentId, new HrUploadFile(form.File.FileName, form.File.ContentType, form.File.Length, stream), ct); return NoContent();
    }
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> FileContent(Guid id, Guid attachmentId, CancellationToken ct, bool download = false)
    {
        var file = await service.DownloadAsync(id, attachmentId, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return download ? File(file.Content, file.ContentType, file.FileName) : File(file.Content, file.ContentType);
    }
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Delete(Guid id, Guid attachmentId, CancellationToken ct) { await service.DeleteAttachmentAsync(id, attachmentId, ct); return NoContent(); }
}
public sealed class ExcuseUploadForm
{
    [System.ComponentModel.DataAnnotations.Required] public IFormFile File { get; init; } = null!;
    public Guid? AttachmentId { get; init; }
}
