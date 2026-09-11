using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/collections")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAccess)]
public sealed class CollectionsController : ControllerBase
{
    private readonly ICollectionsService _service;
    public CollectionsController(ICollectionsService service) => _service = service;

    [HttpGet("dashboard")]
    public Task<CollectionDashboardDto> Dashboard([FromQuery] Guid? organizationId, CancellationToken token) => _service.GetDashboardAsync(organizationId, token);

    [HttpGet("clients")]
    public Task<PagedResultDto<ClientOrganizationCardDto>> Clients([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? type = null, [FromQuery] bool? active = null, CancellationToken token = default) => _service.GetClientsAsync(page, pageSize, search, type, active, token);

    [HttpGet("work-queue/my")]
    public Task<WorkQueueDto> MyWork(CancellationToken token) => _service.GetMyWorkAsync(token);
}

[ApiController]
[Route("api/collections/cases")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAccess)]
public sealed class CollectionCasesController : ControllerBase
{
    private readonly ICollectionsService _service;
    public CollectionCasesController(ICollectionsService service) => _service = service;

    [HttpGet]
    public Task<PagedResultDto<CollectionCaseListItemDto>> Cases(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] Guid? organizationId = null, [FromQuery] Guid? portfolioId = null, [FromQuery] Guid? collectorId = null,
        [FromQuery] string? bucket = null, [FromQuery] string? status = null, [FromQuery] string? priority = null,
        CancellationToken token = default) => _service.GetCasesAsync(new CollectionFilters(page, pageSize, search, organizationId, portfolioId, collectorId, bucket, status, priority), token);

    [HttpGet("{id:guid}")]
    public Task<CollectionCaseDetailsDto> Case(Guid id, CancellationToken token) => _service.GetCaseAsync(id, false, token);

    [HttpPost("{id:guid}/reveal-sensitive")]
    [Authorize(Policy = AuthorizationPolicies.CollectionsSensitiveData)]
    public Task<CollectionCaseDetailsDto> RevealSensitive(Guid id, CancellationToken token) => _service.GetCaseAsync(id, true, token);

    [HttpPost("{id:guid}/activities")]
    public async Task<ActionResult<CollectionActivityDto>> Activity(Guid id, CreateActivityRequest request, CancellationToken token)
    {
        var result = await _service.CreateActivityAsync(id, request, token); return Created($"/api/collections/cases/{id}", result);
    }

    [HttpPost("{id:guid}/promises")]
    public async Task<ActionResult<PromiseToPayDto>> Promise(Guid id, CreatePromiseRequest request, CancellationToken token)
    {
        var result = await _service.CreatePromiseAsync(id, request, token); return Created($"/api/collections/promises/{result.Id}", result);
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<CollectionPaymentDto>> Payment(Guid id, SubmitPaymentRequest request, CancellationToken token)
    {
        var result = await _service.SubmitPaymentAsync(id, request, token); return Created($"/api/collections/payments/{result.Id}", result);
    }
}

[ApiController]
[Route("api/collections/promises")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAccess)]
public sealed class CollectionPromisesController : ControllerBase
{
    private readonly ICollectionsService _service;
    public CollectionPromisesController(ICollectionsService service) => _service = service;
    [HttpGet]
    public Task<PagedResultDto<PromiseToPayDto>> Promises([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] Guid? organizationId = null, [FromQuery] Guid? collectorId = null, [FromQuery] string? status = null, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken token = default)
        => _service.GetPromisesAsync(new PromiseFilters(page, pageSize, search, organizationId, collectorId, status, from, to), token);
}

[ApiController]
[Route("api/collections/payments")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAccess)]
public sealed class CollectionPaymentsController : ControllerBase
{
    private readonly ICollectionsService _service;
    public CollectionPaymentsController(ICollectionsService service) => _service = service;
    [HttpGet]
    public Task<PagedResultDto<CollectionPaymentDto>> Payments([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] Guid? organizationId = null, [FromQuery] Guid? collectorId = null, [FromQuery] string? status = null, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken token = default)
        => _service.GetPaymentsAsync(new PaymentFilters(page, pageSize, search, organizationId, collectorId, status, from, to), token);

    [HttpGet("summary")]
    public Task<CollectionPaymentSummaryDto> Summary(CancellationToken token) => _service.GetPaymentSummaryAsync(token);

    [HttpGet("filter-options")]
    public Task<CollectionPaymentFilterOptionsDto> FilterOptions(CancellationToken token) => _service.GetPaymentFilterOptionsAsync(token);

    [HttpGet("{id:guid}")]
    public Task<CollectionPaymentDetailsDto> Payment(Guid id, CancellationToken token) => _service.GetPaymentAsync(id, token);

    [HttpPatch("{id:guid}/review")]
    [Authorize(Policy = AuthorizationPolicies.CollectionsPaymentApprove)]
    public Task<CollectionPaymentDto> Review(Guid id, ReviewPaymentRequest request, CancellationToken token) => _service.ReviewPaymentAsync(id, request, token);
}

[ApiController]
[Route("api/collections/assignments")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAssignmentManage)]
public sealed class CollectionAssignmentsController : ControllerBase
{
    private readonly ICollectionsService _service;
    public CollectionAssignmentsController(ICollectionsService service) => _service = service;
    [HttpGet("collectors")]
    public Task<IReadOnlyCollection<CollectorLookupDto>> Collectors(CancellationToken token) => _service.GetCollectorsAsync(token);
    [HttpPost("preview")]
    public Task<AssignmentPreviewDto> Preview(BulkAssignmentRequest request, CancellationToken token) => _service.PreviewAssignmentAsync(request.CaseIds, request.CollectorId, token);
    [HttpPost]
    public Task<AssignmentPreviewDto> Assign(BulkAssignmentRequest request, CancellationToken token) => _service.AssignCasesAsync(request, token);
    [HttpPost("automatic/preview")]
    public Task<AutoAssignmentPreviewDto> AutomaticPreview(AutoAssignmentRequest request, CancellationToken token) => _service.PreviewAutomaticAssignmentAsync(request, token);
    [HttpPost("automatic")]
    public Task<AutoAssignmentPreviewDto> Automatic(AutoAssignmentRequest request, CancellationToken token) => _service.ApplyAutomaticAssignmentAsync(request, token);
}

[ApiController]
[Route("api/collections/visits")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAccess)]
public sealed class CollectionVisitsController : ControllerBase
{
    private readonly ICollectionsService _service;
    public CollectionVisitsController(ICollectionsService service) => _service = service;
    [HttpGet]
    public Task<PagedResultDto<FieldVisitDto>> Visits([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] Guid? organizationId = null, [FromQuery] Guid? collectorId = null, [FromQuery] string? status = null, [FromQuery] DateOnly? date = null, CancellationToken token = default) => _service.GetVisitsAsync(new VisitFilters(page, pageSize, search, organizationId, collectorId, status, date), token);
    [HttpGet("summary")]
    public Task<FieldVisitSummaryDto> Summary(CancellationToken token) => _service.GetVisitSummaryAsync(token);
    [HttpGet("filter-options")]
    public Task<FieldVisitFilterOptionsDto> FilterOptions(CancellationToken token) => _service.GetVisitFilterOptionsAsync(token);
    [HttpGet("schedule-options")]
    [Authorize(Policy = AuthorizationPolicies.CollectionsAssignmentManage)]
    public Task<FieldVisitScheduleOptionsDto> ScheduleOptions([FromQuery] string? search = null, CancellationToken token = default) => _service.GetVisitScheduleOptionsAsync(search, token);
    [HttpGet("{id:guid}")]
    public Task<FieldVisitDetailsDto> Visit(Guid id, CancellationToken token) => _service.GetVisitAsync(id, token);
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CollectionsAssignmentManage)]
    public async Task<ActionResult<FieldVisitDto>> Create(CreateVisitRequest request, CancellationToken token) { var value = await _service.CreateVisitAsync(request, token); return Created($"/api/collections/visits/{value.Id}", value); }
    [HttpPatch("{id:guid}/complete")]
    public Task<FieldVisitDto> Complete(Guid id, CompleteVisitRequest request, CancellationToken token) => _service.CompleteVisitAsync(id, request, token);
}

[ApiController]
[Route("api/collections/complaints")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAccess)]
public sealed class CollectionComplaintsController : ControllerBase
{
    private readonly ICollectionsService _service;
    public CollectionComplaintsController(ICollectionsService service) => _service = service;
    [HttpGet]
    public Task<PagedResultDto<ComplaintDto>> Complaints([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? status = null, CancellationToken token = default) => _service.GetComplaintsAsync(page, pageSize, search, status, token);
    [HttpPost]
    public async Task<ActionResult<ComplaintDto>> Create(CreateComplaintRequest request, CancellationToken token) { var value = await _service.CreateComplaintAsync(request, token); return Created($"/api/collections/complaints/{value.Id}", value); }
    [HttpPatch("{id:guid}/status")]
    public Task<ComplaintDto> ChangeStatus(Guid id, ChangeComplaintStatusRequest request, CancellationToken token) => _service.ChangeComplaintStatusAsync(id, request, token);
}

[ApiController]
[Route("api/collections/audit")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAuditView)]
public sealed class CollectionAuditController : ControllerBase
{
    private readonly ICollectionsService _service;
    public CollectionAuditController(ICollectionsService service) => _service = service;
    [HttpGet]
    public Task<PagedResultDto<CollectionAuditDto>> Audit([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] Guid? caseId = null, CancellationToken token = default) => _service.GetAuditAsync(page, pageSize, search, caseId, token);
}

[ApiController]
[Route("api/collections/imports")]
[Authorize(Policy = AuthorizationPolicies.CollectionsImportManage)]
public sealed class CollectionImportsController : ControllerBase
{
    private readonly ICollectionsImportService _service;
    public CollectionImportsController(ICollectionsImportService service) => _service = service;
    [HttpGet("portfolios")]
    public Task<IReadOnlyCollection<PortfolioLookupDto>> Portfolios([FromQuery] Guid? organizationId, CancellationToken token) => _service.GetPortfoliosAsync(organizationId, token);
    [HttpPost]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<ActionResult<CollectionImportBatchDto>> Upload([FromForm] Guid organizationId, [FromForm] Guid portfolioId, [FromForm] IFormFile file, CancellationToken token)
    {
        if (file is null || file.Length == 0) return BadRequest(MIS.Application.Common.ApiErrorResponse.Failure("A non-empty CSV or XLSX file is required.")); await using var stream = file.OpenReadStream(); var value = await _service.UploadAsync(organizationId, portfolioId, file.FileName, file.ContentType ?? "application/octet-stream", file.Length, stream, token); return Created($"/api/collections/imports/{value.Id}", value);
    }
    [HttpGet]
    public Task<PagedResultDto<CollectionImportBatchDto>> Batches([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken token = default) => _service.GetBatchesAsync(page, pageSize, token);
    [HttpGet("{id:guid}")]
    public Task<CollectionImportPreviewDto> Preview(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool? valid = null, CancellationToken token = default) => _service.GetPreviewAsync(id, page, pageSize, valid, token);
    [HttpPost("{id:guid}/confirm")]
    public Task<CollectionImportBatchDto> Confirm(Guid id, ConfirmCollectionImportRequest request, CancellationToken token) => _service.ConfirmAsync(id, request, token);
    [HttpGet("{id:guid}/errors.csv")]
    public async Task<IActionResult> Errors(Guid id, CancellationToken token) => File(await _service.ExportErrorsAsync(id, token), "text/csv; charset=utf-8", $"collection-import-{id:N}-errors.csv");
}

[ApiController]
[Route("api/collections/configuration")]
[Authorize(Policy = AuthorizationPolicies.CollectionsConfigurationManage)]
public sealed class CollectionsConfigurationController : ControllerBase
{
    private readonly ICollectionsService _service;
    public CollectionsConfigurationController(ICollectionsService service) => _service = service;
    [HttpGet]
    public Task<CollectionsConfigurationDto> Configuration(CancellationToken token) => _service.GetConfigurationAsync(token);
    [HttpPost("clients")]
    public async Task<ActionResult<ClientConfigurationDto>> CreateClient(SaveClientConfigurationRequest request, CancellationToken token) { var value = await _service.SaveClientAsync(null, request, token); return Created($"/api/collections/configuration/clients/{value.Id}", value); }
    [HttpPut("clients/{id:guid}")]
    public Task<ClientConfigurationDto> UpdateClient(Guid id, SaveClientConfigurationRequest request, CancellationToken token) => _service.SaveClientAsync(id, request, token);
    [HttpPost("portfolios")]
    public async Task<ActionResult<PortfolioConfigurationDto>> CreatePortfolio(SavePortfolioConfigurationRequest request, CancellationToken token) { var value = await _service.SavePortfolioAsync(null, request, token); return Created($"/api/collections/configuration/portfolios/{value.Id}", value); }
    [HttpPut("portfolios/{id:guid}")]
    public Task<PortfolioConfigurationDto> UpdatePortfolio(Guid id, SavePortfolioConfigurationRequest request, CancellationToken token) => _service.SavePortfolioAsync(id, request, token);
    [HttpPost("buckets")]
    public async Task<ActionResult<BucketConfigurationDto>> CreateBucket(SaveBucketConfigurationRequest request, CancellationToken token) { var value = await _service.SaveBucketAsync(null, request, token); return Created($"/api/collections/configuration/buckets/{value.Id}", value); }
    [HttpPut("buckets/{id:guid}")]
    public Task<BucketConfigurationDto> UpdateBucket(Guid id, SaveBucketConfigurationRequest request, CancellationToken token) => _service.SaveBucketAsync(id, request, token);
}

[ApiController]
[Route("api/collections/branding")]
public sealed class CollectionsBrandingController : ControllerBase
{
    private readonly ICollectionsBrandingService _service;
    public CollectionsBrandingController(ICollectionsBrandingService service) => _service = service;

    [HttpPost("clients/{id:guid}/logo")]
    [Authorize(Policy = AuthorizationPolicies.CollectionsConfigurationManage)]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ActionResult<CollectionBrandLogoDto>> Upload(Guid id, [FromForm] IFormFile file, CancellationToken token)
    {
        if (file is null || file.Length == 0) return BadRequest(MIS.Application.Common.ApiErrorResponse.Failure("A non-empty logo is required."));
        await using var stream = file.OpenReadStream();
        return Ok(await _service.UploadLogoAsync(id, file.FileName, file.Length, stream, token));
    }

    [HttpGet("clients/{id:guid}/logo")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Logo(Guid id, CancellationToken token)
    {
        var logo = await _service.DownloadLogoAsync(id, token);
        return File(logo.Content, logo.ContentType);
    }
}

[ApiController]
[Route("api/collections/attachments")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAccess)]
public sealed class CollectionAttachmentsController : ControllerBase
{
    private readonly ICollectionsAttachmentService _service;
    public CollectionAttachmentsController(ICollectionsAttachmentService service) => _service = service;
    [HttpGet("case/{caseId:guid}")]
    public Task<IReadOnlyCollection<CollectionAttachmentDto>> CaseAttachments(Guid caseId, CancellationToken token) => _service.GetCaseAttachmentsAsync(caseId, token);
    [HttpPost]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<CollectionAttachmentDto>> Upload([FromForm] Guid caseId, [FromForm] Guid? paymentId, [FromForm] string category, [FromForm] IFormFile file, CancellationToken token)
    { if (file is null || file.Length == 0) return BadRequest(MIS.Application.Common.ApiErrorResponse.Failure("A non-empty attachment is required.")); await using var stream = file.OpenReadStream(); var value = await _service.UploadAsync(caseId, paymentId, category, file.FileName, file.ContentType ?? "application/octet-stream", file.Length, stream, token); return Created($"/api/collections/attachments/{value.Id}", value); }
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken token) { var value = await _service.DownloadAsync(id, token); return File(value.Content, value.ContentType, value.FileName); }
}

[ApiController]
[Route("api/collections/reports")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAccess)]
public sealed class CollectionsReportsController : ControllerBase
{
    private readonly ICollectionsReportService _service;
    public CollectionsReportsController(ICollectionsReportService service) => _service = service;
    [HttpGet("executive")]
    public Task<CollectionReportDto> Executive([FromQuery] Guid? organizationId, [FromQuery] Guid? portfolioId, [FromQuery] Guid? collectorId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) => _service.GetExecutiveAsync(new CollectionReportFilters(organizationId, portfolioId, collectorId, from, to), token);
    [HttpGet("executive.csv")]
    [Authorize(Policy = AuthorizationPolicies.CollectionsReportExport)]
    public async Task<IActionResult> ExecutiveCsv([FromQuery] Guid? organizationId, [FromQuery] Guid? portfolioId, [FromQuery] Guid? collectorId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) => File(await _service.ExportExecutiveCsvAsync(new CollectionReportFilters(organizationId, portfolioId, collectorId, from, to), token), "text/csv; charset=utf-8", $"collections-executive-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");
}
