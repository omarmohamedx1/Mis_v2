using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/hr/employee-documents")]
[Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrEmployeeDocumentsController : ControllerBase
{
    private const long RequestLimit = 11 * 1024 * 1024;
    private readonly IHrEmployeeDocumentService _service;

    public HrEmployeeDocumentsController(IHrEmployeeDocumentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedEmployeeDocumentsDto>> GetPaged(
        [FromQuery] EmployeeDocumentFilterDto filter,
        CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(filter, cancellationToken));

    [HttpGet("expiry-summary")]
    public async Task<ActionResult<DocumentExpirySummaryDto>> GetExpirySummary(CancellationToken cancellationToken)
        => Ok(await _service.GetExpirySummaryAsync(cancellationToken));

    [HttpGet("personnel-files")]
    public async Task<ActionResult<PagedEmployeePersonnelFilesDto>> GetPersonnelFiles(
        [FromQuery] PersonnelFileFilterDto filter,
        CancellationToken cancellationToken)
        => Ok(await _service.GetPersonnelFilesAsync(filter, cancellationToken));

    [HttpGet("personnel-files/summary")]
    public async Task<ActionResult<PersonnelFileSummaryDto>> GetPersonnelFileSummary(CancellationToken cancellationToken)
        => Ok(await _service.GetPersonnelFileSummaryAsync(cancellationToken));

    [HttpGet("personnel-files/{employeeId:guid}")]
    public async Task<ActionResult<EmployeePersonnelFileDto>> GetPersonnelFile(Guid employeeId, CancellationToken cancellationToken)
        => Ok(await _service.GetPersonnelFileAsync(employeeId, cancellationToken));

    [HttpPost("personnel-files/{employeeId:guid}/{documentCode}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    public async Task<ActionResult<EmployeeDocumentDetailsDto>> UploadRequired(
        Guid employeeId,
        string documentCode,
        [FromForm] ReplaceEmployeeDocumentForm form,
        CancellationToken cancellationToken)
    {
        if (form.File is null || form.File.Length == 0) throw new HrValidationException("A document file is required.");
        await using var stream = form.File.OpenReadStream();
        var result = await _service.UploadRequiredAsync(
            employeeId,
            documentCode,
            new HrUploadFile(form.File.FileName, form.File.ContentType, form.File.Length, stream),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDocumentDetailsDto>> GetDetails(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetDetailsAsync(id, cancellationToken));

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    public async Task<ActionResult<EmployeeDocumentDetailsDto>> Create(
        [FromForm] CreateEmployeeDocumentForm form,
        CancellationToken cancellationToken)
    {
        if (form.File is null || form.File.Length == 0) throw new HrValidationException("A document file is required.");
        await using var stream = form.File.OpenReadStream();
        var created = await _service.CreateAsync(
            new CreateEmployeeDocumentRequest
            {
                EmployeeId = form.EmployeeId,
                DocumentTypeId = form.DocumentTypeId,
                IssueDate = form.IssueDate,
                ExpiryDate = form.ExpiryDate,
                Notes = form.Notes
            },
            new HrUploadFile(form.File.FileName, form.File.ContentType, form.File.Length, stream),
            cancellationToken);
        return CreatedAtAction(nameof(GetDetails), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeDocumentDetailsDto>> Update(
        Guid id,
        UpdateEmployeeDocumentRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/file")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    public async Task<ActionResult<EmployeeDocumentDetailsDto>> Replace(
        Guid id,
        [FromForm] ReplaceEmployeeDocumentForm form,
        CancellationToken cancellationToken)
    {
        if (form.File is null || form.File.Length == 0) throw new HrValidationException("A replacement file is required.");
        await using var stream = form.File.OpenReadStream();
        return Ok(await _service.ReplaceAsync(
            id,
            new HrUploadFile(form.File.FileName, form.File.ContentType, form.File.Length, stream),
            cancellationToken));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var file = await _service.OpenAsync(id, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true);
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken cancellationToken)
    {
        var file = await _service.OpenAsync(id, cancellationToken);
        return File(file.Content, file.ContentType, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromBody] DeleteEmployeeDocumentRequest? request,
        CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, request ?? new DeleteEmployeeDocumentRequest(), cancellationToken);
        return NoContent();
    }
}

public sealed class CreateEmployeeDocumentForm
{
    public Guid EmployeeId { get; init; }
    public Guid DocumentTypeId { get; init; }
    public DateOnly? IssueDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    [StringLength(1000)] public string? Notes { get; init; }
    [Required] public IFormFile? File { get; init; }
}

public sealed class ReplaceEmployeeDocumentForm
{
    [Required] public IFormFile? File { get; init; }
}
