using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.DTOs.Finance;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/finance")]
[Authorize(Policy = AuthorizationPolicies.FinanceAccess)]
public sealed class FinanceController : ControllerBase
{
    private readonly IFinanceService _service;
    public FinanceController(IFinanceService service) => _service = service;

    [HttpGet("dashboard")]
    public Task<FinanceDashboardDto> Dashboard(CancellationToken token) => _service.GetDashboardAsync(token);

    [HttpGet("accounts")]
    public Task<IReadOnlyCollection<FinanceAccountDto>> Accounts(CancellationToken token) => _service.GetAccountsAsync(token);

    [HttpGet("periods")]
    public Task<IReadOnlyCollection<AccountingPeriodDto>> Periods([FromQuery] int? year, CancellationToken token) => _service.GetPeriodsAsync(year, token);

    [HttpPost("periods/initialize/{year:int}")]
    [Authorize(Policy = AuthorizationPolicies.FinanceConfiguration)]
    public Task<IReadOnlyCollection<AccountingPeriodDto>> InitializeYear(int year, CancellationToken token) => _service.InitializeYearAsync(year, token);

    [HttpPost("periods/{id:guid}/{action}")]
    [Authorize(Policy = AuthorizationPolicies.FinancePeriodClose)]
    public Task<AccountingPeriodDto> ChangePeriod(Guid id, string action, PeriodActionRequest request, CancellationToken token) => _service.ChangePeriodStatusAsync(id, action, request, token);

    [HttpGet("journals")]
    public Task<FinancePagedResultDto<FinanceJournalListItemDto>> Journals([FromQuery] int page = 1, [FromQuery] int pageSize = 30, [FromQuery] string? status = null, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken token = default) => _service.GetJournalsAsync(page, pageSize, status, from, to, token);

    [HttpGet("journals/{id:guid}")]
    public Task<JournalDto> Journal(Guid id, CancellationToken token) => _service.GetJournalAsync(id, token);

    [HttpPost("journals")]
    [Authorize(Policy = AuthorizationPolicies.FinanceJournalCreate)]
    public async Task<ActionResult<JournalDto>> CreateJournal(CreateManualJournalRequest request, CancellationToken token) { var value = await _service.CreateManualJournalAsync(request, token); return Created($"/api/finance/journals/{value.Id}", value); }

    [HttpPost("journals/{id:guid}/submit")]
    [Authorize(Policy = AuthorizationPolicies.FinanceJournalCreate)]
    public Task<JournalDto> Submit(Guid id, CancellationToken token) => _service.SubmitJournalAsync(id, token);

    [HttpPost("journals/{id:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.FinanceJournalApprove)]
    public Task<JournalDto> Approve(Guid id, CancellationToken token) => _service.ApproveJournalAsync(id, token);

    [HttpPost("journals/{id:guid}/post")]
    [Authorize(Policy = AuthorizationPolicies.FinanceJournalPost)]
    public Task<JournalDto> Post(Guid id, CancellationToken token) => _service.PostJournalAsync(id, token);

    [HttpPost("journals/{id:guid}/reverse")]
    [Authorize(Policy = AuthorizationPolicies.FinanceReverse)]
    public Task<JournalDto> Reverse(Guid id, PeriodActionRequest request, CancellationToken token) => _service.ReverseJournalAsync(id, request, token);

    [HttpGet("clients/{clientId:guid}/ledger")]
    public Task<ClientLedgerDto> ClientLedger(Guid clientId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken token) => _service.GetClientLedgerAsync(clientId, from, to, token);

    [HttpGet("reports/trial-balance")]
    public Task<TrialBalanceDto> TrialBalance([FromQuery] DateOnly asOf, CancellationToken token) => _service.GetTrialBalanceAsync(asOf, token);

    [HttpGet("audit")]
    [Authorize(Policy = AuthorizationPolicies.FinanceAudit)]
    public Task<FinancePagedResultDto<FinancialAuditDto>> Audit([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken token = default) => _service.GetAuditAsync(page, pageSize, token);

    [HttpGet("collections")]
    [Authorize(Policy = AuthorizationPolicies.FinanceCollectionReview)]
    public Task<FinancePagedResultDto<CollectionFinanceListItemDto>> Collections(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] string? status = null,
        [FromQuery] string? channel = null,
        CancellationToken token = default) => _service.GetFinancialCollectionsAsync(page, pageSize, status, channel, token);

    [HttpGet("collections/{paymentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.FinanceCollectionReview)]
    public Task<CollectionFinanceDto> Collection(Guid paymentId, CancellationToken token) =>
        _service.GetFinancialCollectionAsync(paymentId, token);

    [HttpPost("collections/{paymentId:guid}/clear")]
    [Authorize(Policy = AuthorizationPolicies.FinanceCustodyReconcile)]
    public Task<CollectionFinanceDto> ClearCollection(Guid paymentId, ClearCollectionRequest request, CancellationToken token) =>
        _service.ClearCollectionAsync(paymentId, request, token);

    [HttpPost("collections/{paymentId:guid}/reverse")]
    [Authorize(Policy = AuthorizationPolicies.FinanceReverse)]
    public Task<CollectionFinanceDto> ReverseCollection(Guid paymentId, PeriodActionRequest request, CancellationToken token) =>
        _service.ReverseCollectionAsync(paymentId, request, token);

    [HttpGet("custodies")]
    [Authorize(Policy = AuthorizationPolicies.FinanceCustodyView)]
    public Task<IReadOnlyCollection<CustodySummaryDto>> Custodies(CancellationToken token) => _service.GetCustodiesAsync(token);

    [HttpGet("custodies/{collectorId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.FinanceCustodyView)]
    public Task<CustodyDetailsDto> Custody(Guid collectorId, CancellationToken token) => _service.GetCustodyAsync(collectorId, token);

    [HttpPut("custodies/{collectorId:guid}/limits")]
    [Authorize(Policy = AuthorizationPolicies.FinanceConfiguration)]
    public Task<CustodyDetailsDto> UpdateCustodyLimits(Guid collectorId, UpdateCustodyLimitsRequest request, CancellationToken token) =>
        _service.UpdateCustodyLimitsAsync(collectorId, request, token);
}
