using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.DTOs.Legal;
using MIS.Application.Interfaces;
using MIS.Application.Common;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/legal")]
[Authorize]
public sealed class LegalController(
    ILegalService legal,
    ILegalClientService clients,
    ILegalCaseImportService imports) : ControllerBase
{
    [HttpGet("dashboard")]
    [Authorize(Policy = AuthorizationPolicies.LegalAccess)]
    public Task<LegalClientDashboardDto> Dashboard(CancellationToken token) => clients.GetDashboardAsync(token);

    [HttpGet("clients")]
    [Authorize(Policy = AuthorizationPolicies.LegalAccess)]
    public Task<LegalClientPageDto> Clients(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken token = default) => clients.ListAsync(search, page, pageSize, token);

    [HttpGet("clients/{customerId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.LegalAccess)]
    public Task<LegalClientDetailsDto> Client(Guid customerId, CancellationToken token) => clients.GetAsync(customerId, token);

    [HttpPut("clients/{customerId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    public Task<LegalClientDetailsDto> UpdateClient(
        Guid customerId,
        [FromBody] UpdateLegalClientRequest request,
        CancellationToken token) => clients.UpdateAsync(customerId, request, token);

    [HttpPost("clients/{customerId:guid}/documents")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<LegalClientDocumentDto> UploadClientDocument(
        Guid customerId,
        IFormFile file,
        CancellationToken token)
    {
        if (file is null) throw new HrValidationException("Select a client document.");
        await using var stream = file.OpenReadStream();
        return await clients.UploadDocumentAsync(customerId, file.FileName, file.ContentType, file.Length, stream, token);
    }

    [HttpGet("client-documents/{documentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.LegalAccess)]
    public async Task<IActionResult> DownloadClientDocument(Guid documentId, CancellationToken token)
    {
        var document = await clients.DownloadDocumentAsync(documentId, token);
        return File(document.Content, document.ContentType, document.FileName);
    }

    [HttpDelete("client-documents/{documentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    public async Task<IActionResult> DeleteClientDocument(Guid documentId, CancellationToken token)
    {
        await clients.DeleteDocumentAsync(documentId, token);
        return NoContent();
    }

    [HttpPost("client-imports/inspect")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ExcelImportLimits.RequestBytes)]
    public async Task<LegalClientWorkbookDto> InspectClientImport([FromForm] IFormFile file, CancellationToken token)
    {
        if (file is null) throw new HrValidationException("Select a client import file.");
        await using var stream = file.OpenReadStream();
        return await clients.InspectImportAsync(file.FileName, file.Length, stream, token);
    }

    [HttpPost("client-imports/preview")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ExcelImportLimits.RequestBytes)]
    public async Task<LegalClientImportPreviewDto> PreviewClientImport(
        [FromForm] IFormFile file,
        [FromForm] Guid organizationId,
        CancellationToken token,
        [FromForm] string? mapping = null)
    {
        if (file is null) throw new HrValidationException("Select a client import file.");
        await using var stream = file.OpenReadStream();
        LegalClientColumnMapping? columns = null;
        if (!string.IsNullOrWhiteSpace(mapping))
        {
            try
            {
                columns = System.Text.Json.JsonSerializer.Deserialize<LegalClientColumnMapping>(mapping,
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
                    ?? throw new HrValidationException("Select the workbook columns before previewing.");
            }
            catch (System.Text.Json.JsonException)
            {
                throw new HrValidationException("The workbook column mapping is invalid.");
            }
        }
        return await clients.PreviewImportAsync(file.FileName, file.Length, stream, organizationId, token, columns);
    }

    [HttpPost("client-imports")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    public Task<LegalClientImportResultDto> ImportClients(
        [FromBody] LegalClientImportRequest request,
        CancellationToken token) => clients.ImportAsync(request, token);

    [HttpGet("organizations")]
    [Authorize(Policy = AuthorizationPolicies.LegalAccess)]
    public Task<IReadOnlyList<LegalOrganizationDto>> Organizations(CancellationToken token) => legal.ListOrganizationsAsync(token);

    [HttpGet("cases")]
    [Authorize(Policy = AuthorizationPolicies.LegalAccess)]
    public Task<LegalCasePageDto> Cases([FromQuery] string? search, [FromQuery] Guid? organizationId, [FromQuery] string? stage, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken token = default)
        => legal.ListCasesAsync(search, organizationId, stage, page, pageSize, token);

    [HttpGet("cases/{collectionCaseId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.LegalAccess)]
    public Task<LegalCaseDetailsDto> Case(Guid collectionCaseId, CancellationToken token) => legal.GetCaseAsync(collectionCaseId, token);

    [HttpPut("cases/{collectionCaseId:guid}/file")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    public Task<LegalCaseDetailsDto> SaveFile(Guid collectionCaseId, [FromBody] SaveLegalFileRequest request, CancellationToken token)
        => legal.SaveFileAsync(collectionCaseId, request, token);

    [HttpPost("cases/{collectionCaseId:guid}/actions")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    public Task<LegalCaseDetailsDto> RecordAction(Guid collectionCaseId, [FromBody] RecordLegalActionRequest request, CancellationToken token)
        => legal.RecordActionAsync(collectionCaseId, request, token);

    [HttpGet("imports/template")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    public IActionResult ImportTemplate() =>
        File(imports.BuildTemplate(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Legal_Cases_Import_Template.xlsx");

    [HttpPost("imports/preview")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ExcelImportLimits.RequestBytes)]
    public async Task<LegalImportPreviewDto> PreviewImport(IFormFile file, CancellationToken token)
    {
        if (file is null) throw new HrValidationException("Select a legal cases import file.");
        await using var stream = file.OpenReadStream();
        return await imports.PreviewAsync(file.FileName, file.Length, stream, token);
    }

    [HttpPost("imports")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    public Task<LegalImportResultDto> Import([FromBody] LegalImportRequest request, CancellationToken token)
        => imports.ImportAsync(request, token);

    [HttpGet("imports/history")]
    [Authorize(Policy = AuthorizationPolicies.LegalCaseManage)]
    public Task<IReadOnlyCollection<LegalImportHistoryDto>> ImportHistory(CancellationToken token)
        => imports.GetHistoryAsync(token);
}
