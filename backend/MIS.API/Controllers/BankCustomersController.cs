using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/banks/{organizationId:guid}/customers")]
[Route("api/installment-companies/{organizationId:guid}/customers")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAccess)]
public sealed class BankCustomersController(IBankCustomerService customers, IBankCustomerImportService imports) : ControllerBase
{
    private const long RequestLimit = ExcelImportLimits.RequestBytes;

    [HttpGet]
    public Task<BankCustomerPageDto> Get(Guid organizationId, [FromQuery] BankCustomerQuery query, CancellationToken token)
        => customers.GetAsync(organizationId, query, token);

    [HttpGet("{customerId:guid}")]
    public Task<BankCustomerDetailsDto> GetDetails(Guid organizationId, Guid customerId, CancellationToken token)
        => customers.GetDetailsAsync(organizationId, customerId, token);

    [HttpGet("import/portfolios")]
    [Authorize(Policy = AuthorizationPolicies.CollectionsImportManage)]
    public Task<IReadOnlyCollection<PortfolioLookupDto>> Portfolios(Guid organizationId, CancellationToken token)
        => imports.GetPortfoliosAsync(organizationId, token);

    [HttpPost("import/upload")]
    [Authorize(Policy = AuthorizationPolicies.CollectionsImportManage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    public async Task<ActionResult<BankCustomerImportUpload>> Upload(Guid organizationId, IFormFile file, CancellationToken token)
    {
        if (file is null || file.Length == 0) throw new HrValidationException("A customer import file is required.");
        await using var stream = file.OpenReadStream();
        return Ok(await imports.UploadAsync(organizationId, new HrUploadFile(file.FileName, file.ContentType, file.Length, stream), token));
    }

    [HttpPost("import/{id:guid}/preview")]
    [Authorize(Policy = AuthorizationPolicies.CollectionsImportManage)]
    public Task<BankCustomerImportPreview> Preview(Guid organizationId, Guid id, [FromBody] BankCustomerImportMapping mapping, CancellationToken token)
        => imports.PreviewAsync(organizationId, id, mapping, token);

    [HttpPost("import/{id:guid}/confirm")]
    [Authorize(Policy = AuthorizationPolicies.CollectionsImportManage)]
    public Task<BankCustomerImportResult> Confirm(Guid organizationId, Guid id, [FromBody] ConfirmBankCustomerImportRequest request, CancellationToken token)
        => imports.ConfirmAsync(organizationId, id, request.PreviewId, token, request.ExcludedRows);
}

public sealed class ConfirmBankCustomerImportRequest
{
    public Guid PreviewId { get; init; }
    public IReadOnlyCollection<int>? ExcludedRows { get; init; }
}
