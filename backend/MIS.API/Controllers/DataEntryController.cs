using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.DataEntry;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/data-entry")]
[Authorize]
public sealed class DataEntryController(IDataEntryService dataEntry) : ControllerBase
{
    private const long RequestLimit = ExcelImportLimits.RequestBytes;

    [HttpGet("dashboard")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryAccess)]
    public Task<DataEntryDashboardDto> Dashboard(CancellationToken token) => dataEntry.GetDashboardAsync(token);

    [HttpGet("organizations")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryAccess)]
    public Task<IReadOnlyList<DataEntryOrganizationDto>> Organizations(CancellationToken token)
        => dataEntry.ListOrganizationsAsync(token);

    [HttpGet("organizations/{organizationId:guid}/portfolios")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryAccess)]
    public Task<IReadOnlyList<DataEntryPortfolioDto>> Portfolios(Guid organizationId, CancellationToken token)
        => dataEntry.ListPortfoliosAsync(organizationId, token);

    [HttpGet("clients")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryAccess)]
    public Task<DataEntryClientPageDto> Clients([FromQuery] string? search, [FromQuery] bool? hasPhone, [FromQuery] bool? hasAddress, [FromQuery] bool? hasFeedback, [FromQuery] bool? hasData, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken token = default)
        => dataEntry.ListClientsAsync(search, hasPhone, hasAddress, hasFeedback, hasData, page, pageSize, token);

    [HttpGet("clients/{customerId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryAccess)]
    public Task<DataEntryClientDetailsDto> Client(Guid customerId, CancellationToken token)
        => dataEntry.GetClientAsync(customerId, token);

    [HttpPost("clients")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryManage)]
    public Task<DataEntryClientDetailsDto> CreateClient([FromBody] CreateDataEntryClientRequest request, CancellationToken token)
        => dataEntry.CreateManualClientAsync(request, token);

    [HttpDelete("clients/{customerId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryManage)]
    public async Task<IActionResult> DeleteClient(Guid customerId, CancellationToken token)
    {
        await dataEntry.DeleteClientAsync(customerId, token);
        return NoContent();
    }

    [HttpPost("import/upload")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryManage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    public async Task<ActionResult<DataEntryImportUploadDto>> Upload(IFormFile file, CancellationToken token)
    {
        if (file is null || file.Length == 0) throw new HrValidationException("A data entry import file is required.");
        await using var stream = file.OpenReadStream();
        return Ok(await dataEntry.UploadImportAsync(new HrUploadFile(file.FileName, file.ContentType, file.Length, stream), token));
    }

    [HttpGet("import/{uploadId:guid}/sheet")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryManage)]
    public Task<DataEntrySheetPreviewDto> Sheet(Guid uploadId, [FromQuery] string? sheetName, CancellationToken token)
        => dataEntry.ReadSheetAsync(uploadId, sheetName, token);

    [HttpPost("import/{uploadId:guid}/preview")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryManage)]
    public Task<DataEntryImportPreviewDto> Preview(Guid uploadId, [FromBody] DataEntryImportMappingRequest mapping, CancellationToken token)
        => dataEntry.PreviewImportAsync(uploadId, mapping, token);

    [HttpPost("import/confirm")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryManage)]
    public Task<DataEntryBatchListItemDto> Confirm([FromBody] ConfirmDataEntryImportRequest request, CancellationToken token)
        => dataEntry.ConfirmImportAsync(request, token);

    [HttpGet("batches")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryAccess)]
    public Task<DataEntryBatchPageDto> MyBatches([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken token = default)
        => dataEntry.ListMyBatchesAsync(status, page, pageSize, token);

    [HttpGet("batches/{batchId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryAccess)]
    public Task<DataEntryBatchDetailsDto> Batch(Guid batchId, CancellationToken token)
        => dataEntry.GetBatchAsync(batchId, token);

    [HttpGet("notifications")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryBatchReview)]
    public Task<IReadOnlyList<DataEntryNotificationDto>> Notifications(CancellationToken token)
        => dataEntry.ListNotificationsAsync(token);

    [HttpPost("notifications/{notificationId:guid}/read")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryBatchReview)]
    public Task MarkNotificationRead(Guid notificationId, CancellationToken token)
        => dataEntry.MarkNotificationReadAsync(notificationId, token);

    [HttpGet("supervisor/batches")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryBatchReview)]
    public Task<DataEntryBatchPageDto> SupervisorBatches([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken token = default)
        => dataEntry.ListSupervisorBatchesAsync(status, page, pageSize, token);

    [HttpGet("supervisor/batches/{batchId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryBatchReview)]
    public Task<DataEntryBatchDetailsDto> SupervisorBatch(Guid batchId, CancellationToken token)
        => dataEntry.GetBatchAsync(batchId, token);

    [HttpPost("supervisor/batches/{batchId:guid}/accept")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryBatchReview)]
    public Task<DataEntryBatchListItemDto> Accept(Guid batchId, CancellationToken token)
        => dataEntry.AcceptBatchAsync(batchId, token);

    [HttpPost("supervisor/batches/{batchId:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryBatchReview)]
    public Task<DataEntryBatchListItemDto> Reject(Guid batchId, [FromBody] RejectDataEntryBatchRequest request, CancellationToken token)
        => dataEntry.RejectBatchAsync(batchId, request, token);

    [HttpPost("supervisor/batches/{batchId:guid}/send-to-distribution")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryBatchReview)]
    public Task<DataEntryBatchListItemDto> SendToDistribution(Guid batchId, CancellationToken token)
        => dataEntry.SendToDistributionAsync(batchId, token);

    [HttpGet("clients/{customerId:guid}/documents")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryAccess)]
    public Task<IReadOnlyList<DataEntryDocumentDto>> ClientDocuments(Guid customerId, CancellationToken token)
        => dataEntry.ListClientDocumentsAsync(customerId, token);

    [HttpPost("clients/{customerId:guid}/documents")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryManage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    public async Task<ActionResult<DataEntryDocumentDto>> UploadClientDocument(Guid customerId, IFormFile file, [FromForm] string? note, CancellationToken token)
    {
        if (file is null || file.Length == 0) throw new HrValidationException("A supporting file is required.");
        await using var stream = file.OpenReadStream();
        return Ok(await dataEntry.UploadClientDocumentAsync(customerId, new HrUploadFile(file.FileName, file.ContentType, file.Length, stream), note, token));
    }

    [HttpGet("batches/{batchId:guid}/documents")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryAccess)]
    public Task<IReadOnlyList<DataEntryDocumentDto>> BatchDocuments(Guid batchId, CancellationToken token)
        => dataEntry.ListBatchDocumentsAsync(batchId, token);

    [HttpGet("cases/{caseId:guid}/documents")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryBatchReview)]
    public Task<IReadOnlyList<DataEntryDocumentDto>> CaseDocuments(Guid caseId, CancellationToken token)
        => dataEntry.ListCaseDocumentsAsync(caseId, token);

    [HttpGet("documents/{documentId:guid}/download")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryAccess)]
    public async Task<IActionResult> DownloadDocument(Guid documentId, CancellationToken token)
    {
        var file = await dataEntry.DownloadDocumentAsync(documentId, token);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpDelete("documents/{documentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.DataEntryAccess)]
    public async Task<IActionResult> DeleteDocument(Guid documentId, CancellationToken token)
    {
        await dataEntry.DeleteDocumentAsync(documentId, token);
        return NoContent();
    }
}
