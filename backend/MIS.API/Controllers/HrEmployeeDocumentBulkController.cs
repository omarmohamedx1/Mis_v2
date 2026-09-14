using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/hr/employee-documents/bulk")]
[Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrEmployeeDocumentBulkController(IEmployeeDocumentBulkService service) : ControllerBase
{
    private const long RequestLimit = 220 * 1024 * 1024;

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    public async Task<ActionResult<EmployeeDocumentBulkUpload>> Upload(
        [FromForm] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        if (files is null || files.Count == 0) throw new HrValidationException("Select at least one document file.");
        var uploads = new List<HrUploadFile>();
        var streams = new List<Stream>();
        try
        {
            foreach (var file in files)
            {
                if (file.Length <= 0) continue;
                var stream = file.OpenReadStream();
                streams.Add(stream);
                uploads.Add(new HrUploadFile(file.FileName, file.ContentType, file.Length, stream));
            }

            return Ok(await service.UploadAsync(uploads, cancellationToken));
        }
        finally
        {
            foreach (var stream in streams)
                await stream.DisposeAsync();
        }
    }

    [HttpPut("{id:guid}/preview")]
    public async Task<ActionResult<EmployeeDocumentBulkUpload>> Preview(
        Guid id,
        [FromBody] EmployeeDocumentBulkConfirmRequest request,
        CancellationToken cancellationToken)
        => Ok(await service.ApplyCorrectionsAsync(id, request.Items, cancellationToken));

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<EmployeeDocumentBulkResult>> Confirm(
        Guid id,
        [FromBody] EmployeeDocumentBulkConfirmRequest request,
        CancellationToken cancellationToken)
        => Ok(await service.ConfirmAsync(id, request.Items, cancellationToken));
}
