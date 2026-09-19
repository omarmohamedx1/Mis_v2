using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Accounting;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/accounting")]
[Authorize(Policy = AuthorizationPolicies.AccountingAccess)]
public sealed class AccountingController(IAccountingService accounting) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<AccountingDashboardDto> Dashboard([FromQuery] int year, [FromQuery] int month, CancellationToken token)
        => accounting.GetDashboardAsync(year, month, token);

    [HttpGet("salaries/periods")]
    public Task<IReadOnlyList<AccountingPayrollPeriodDto>> Periods(CancellationToken token) => accounting.ListPeriodsAsync(token);

    [HttpPost("salaries/periods")]
    [Authorize(Policy = AuthorizationPolicies.AccountingPayrollManage)]
    public Task<AccountingPayrollPeriodDto> EnsurePeriod([FromBody] GeneratePayrollRequest request, CancellationToken token)
        => accounting.EnsurePeriodAsync(request, token);

    [HttpPost("salaries/generate")]
    [Authorize(Policy = AuthorizationPolicies.AccountingPayrollManage)]
    public Task<AccountingPayrollPeriodDto> Generate([FromBody] GeneratePayrollRequest request, CancellationToken token)
        => accounting.GeneratePayrollAsync(request, token);

    [HttpGet("salaries")]
    public Task<AccountingPayrollPageDto> Salaries([FromQuery] Guid periodId, [FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken token = default)
        => accounting.ListPayrollsAsync(periodId, search, status, page, pageSize, token);

    [HttpGet("salaries/{id:guid}")]
    public Task<AccountingEmployeePayrollDto> Salary(Guid id, CancellationToken token) => accounting.GetPayrollAsync(id, token);

    [HttpPut("salaries/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AccountingPayrollManage)]
    public Task<AccountingEmployeePayrollDto> UpdateSalary(Guid id, [FromBody] UpdateAccountingPayrollRequest request, CancellationToken token)
        => accounting.UpdatePayrollAsync(id, request, token);

    [HttpPost("salaries/{id:guid}/submit")]
    [Authorize(Policy = AuthorizationPolicies.AccountingPayrollManage)]
    public Task<AccountingEmployeePayrollDto> SubmitSalary(Guid id, CancellationToken token) => accounting.SubmitPayrollAsync(id, token);

    [HttpPost("salaries/{id:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.AccountingPayrollApprove)]
    public Task<AccountingEmployeePayrollDto> ApproveSalary(Guid id, CancellationToken token) => accounting.ApprovePayrollAsync(id, token);

    [HttpPost("salaries/{id:guid}/pay")]
    [Authorize(Policy = AuthorizationPolicies.AccountingPayrollApprove)]
    public Task<AccountingEmployeePayrollDto> PaySalary(Guid id, CancellationToken token) => accounting.PayPayrollAsync(id, token);

    [HttpPost("salaries/{id:guid}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.AccountingPayrollManage)]
    public Task<AccountingEmployeePayrollDto> CancelSalary(Guid id, CancellationToken token) => accounting.CancelPayrollAsync(id, token);

    [HttpGet("transportation")]
    public Task<AccountingTransportationPageDto> Transportation([FromQuery] string? search, [FromQuery] string? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken token = default)
        => accounting.ListTransportationAsync(search, status, from, to, page, pageSize, token);

    [HttpGet("transportation/{id:guid}")]
    public Task<AccountingTransportationDto> TransportationDetails(Guid id, CancellationToken token) => accounting.GetTransportationAsync(id, token);

    [HttpPost("transportation")]
    [Authorize(Policy = AuthorizationPolicies.AccountingTransportationManage)]
    public Task<AccountingTransportationDto> CreateTransportation([FromBody] CreateTransportationRequest request, CancellationToken token)
        => accounting.CreateTransportationAsync(request, token);

    [HttpPut("transportation/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AccountingTransportationManage)]
    public Task<AccountingTransportationDto> UpdateTransportation(Guid id, [FromBody] UpdateTransportationRequest request, CancellationToken token)
        => accounting.UpdateTransportationAsync(id, request, token);

    [HttpPost("transportation/{id:guid}/{action}")]
    [Authorize(Policy = AuthorizationPolicies.AccountingTransportationManage)]
    public Task<AccountingTransportationDto> TransitionTransportation(Guid id, string action, CancellationToken token)
        => accounting.TransitionTransportationAsync(id, action, token);

    [HttpPost("transportation/{id:guid}/attachment")]
    [Authorize(Policy = AuthorizationPolicies.AccountingTransportationManage)]
    [Consumes("multipart/form-data")]
    public async Task<AccountingTransportationDto> UploadAttachment(Guid id, IFormFile file, CancellationToken token)
    {
        if (file is null || file.Length == 0) throw new HrValidationException("Attachment file is required.");
        await using var stream = file.OpenReadStream();
        return await accounting.UploadTransportationAttachmentAsync(id, new HrUploadFile(file.FileName, file.ContentType, file.Length, stream), token);
    }

    [HttpDelete("transportation/{id:guid}/attachment")]
    [Authorize(Policy = AuthorizationPolicies.AccountingTransportationManage)]
    public Task DeleteAttachment(Guid id, CancellationToken token) => accounting.DeleteTransportationAttachmentAsync(id, token);

    [HttpGet("transportation/{id:guid}/attachment")]
    public async Task<IActionResult> DownloadAttachment(Guid id, CancellationToken token)
    {
        var file = await accounting.DownloadTransportationAttachmentAsync(id, token);
        return File(file.Stream, file.ContentType, file.FileName);
    }

    [HttpGet("commission-rules")]
    public Task<IReadOnlyList<AccountingCommissionRuleDto>> Rules([FromQuery] string? scope, CancellationToken token) => accounting.ListRulesAsync(scope, token);

    [HttpPost("commission-rules")]
    [Authorize(Policy = AuthorizationPolicies.AccountingCommissionManage)]
    public Task<AccountingCommissionRuleDto> CreateRule([FromBody] CreateCommissionRuleRequest request, CancellationToken token)
        => accounting.CreateRuleAsync(request, token);

    [HttpPatch("commission-rules/{id:guid}/active")]
    [Authorize(Policy = AuthorizationPolicies.AccountingCommissionManage)]
    public Task<AccountingCommissionRuleDto> SetRuleActive(Guid id, [FromBody] SetActiveRequest request, CancellationToken token)
        => accounting.SetRuleActiveAsync(id, request.IsActive, token);

    [HttpDelete("commission-rules/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AccountingCommissionManage)]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken token)
    {
        await accounting.DeleteRuleAsync(id, token);
        return NoContent();
    }

    [HttpPost("collector-commissions/calculate")]
    [Authorize(Policy = AuthorizationPolicies.AccountingCommissionManage)]
    public Task<int> CalculateCollectors([FromBody] CalculateCommissionsRequest request, CancellationToken token)
        => accounting.CalculateCollectorCommissionsAsync(request, token);

    [HttpGet("collector-commissions")]
    public Task<AccountingCommissionPageDto<AccountingCollectorCommissionDto>> CollectorCommissions([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken token = default)
        => accounting.ListCollectorCommissionsAsync(year, month, search, status, page, pageSize, token);

    [HttpGet("collector-commissions/{id:guid}")]
    public Task<AccountingCollectorCommissionDetailsDto> CollectorDetails(Guid id, CancellationToken token) => accounting.GetCollectorCommissionAsync(id, token);

    [HttpPost("collector-commissions/{id:guid}/adjust")]
    [Authorize(Policy = AuthorizationPolicies.AccountingCommissionManage)]
    public Task<AccountingCollectorCommissionDto> AdjustCollector(Guid id, [FromBody] AdjustCommissionRequest request, CancellationToken token)
        => accounting.AdjustCollectorCommissionAsync(id, request, token);

    [HttpPost("collector-commissions/{id:guid}/{action}")]
    [Authorize(Policy = AuthorizationPolicies.AccountingCommissionManage)]
    public Task<AccountingCollectorCommissionDto> TransitionCollector(Guid id, string action, CancellationToken token)
        => accounting.TransitionCollectorCommissionAsync(id, action, token);

    [HttpPost("supervisor-commissions/calculate")]
    [Authorize(Policy = AuthorizationPolicies.AccountingCommissionManage)]
    public Task<int> CalculateSupervisors([FromBody] CalculateCommissionsRequest request, CancellationToken token)
        => accounting.CalculateSupervisorCommissionsAsync(request, token);

    [HttpGet("supervisor-commissions")]
    public Task<AccountingCommissionPageDto<AccountingSupervisorCommissionDto>> SupervisorCommissions([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken token = default)
        => accounting.ListSupervisorCommissionsAsync(year, month, search, status, page, pageSize, token);

    [HttpGet("supervisor-commissions/{id:guid}")]
    public Task<AccountingSupervisorCommissionDto> SupervisorDetails(Guid id, CancellationToken token) => accounting.GetSupervisorCommissionAsync(id, token);

    [HttpPost("supervisor-commissions/{id:guid}/adjust")]
    [Authorize(Policy = AuthorizationPolicies.AccountingCommissionManage)]
    public Task<AccountingSupervisorCommissionDto> AdjustSupervisor(Guid id, [FromBody] AdjustCommissionRequest request, CancellationToken token)
        => accounting.AdjustSupervisorCommissionAsync(id, request, token);

    [HttpPost("supervisor-commissions/{id:guid}/{action}")]
    [Authorize(Policy = AuthorizationPolicies.AccountingCommissionManage)]
    public Task<AccountingSupervisorCommissionDto> TransitionSupervisor(Guid id, string action, CancellationToken token)
        => accounting.TransitionSupervisorCommissionAsync(id, action, token);

    [HttpGet("employees")]
    public Task<IReadOnlyList<AccountingLookupEmployeeDto>> Employees([FromQuery] string? search, CancellationToken token)
        => accounting.LookupEmployeesAsync(search, token);
}
