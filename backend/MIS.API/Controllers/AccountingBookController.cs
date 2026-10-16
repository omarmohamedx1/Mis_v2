using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.DTOs.Accounting;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/accounting/book")]
[Authorize(Policy = AuthorizationPolicies.FinanceAccess)]
public sealed class AccountingBookController(IAccountingBookService service) : ControllerBase
{
    [HttpGet("entries")]
    public Task<AccountingBookPageDto> List([FromQuery] AccountingBookFilter filter, CancellationToken token) => service.ListAsync(filter, token);
    [HttpPost("entries")]
    [Authorize(Policy = AuthorizationPolicies.FinanceJournalCreate)]
    public Task<AccountingBookEntryDto> Create(CreateAccountingBookEntryRequest request, CancellationToken token) => service.CreateAsync(request, token);
    [HttpPost("entries/{id:guid}/{action}")]
    public Task<AccountingBookEntryDto> Transition(Guid id, string action, [FromBody] AccountingBookActionRequest request, CancellationToken token) => service.TransitionAsync(id, action, request.Reason, token);
    [HttpGet("summary")]
    public Task<AccountingBookSummaryDto> Summary([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) => service.SummaryAsync(from, to, token);
    [HttpGet("advances")]
    public Task<IReadOnlyList<AccountingAdvanceDto>> Advances([FromQuery] DateOnly asOf, [FromQuery] string? search, [FromQuery] Guid? employeeId, CancellationToken token) => service.AdvancesAsync(asOf, search, employeeId, token);
    [HttpGet("recurring")]
    public Task<IReadOnlyList<AccountingRecurringExpenseDto>> Recurring(CancellationToken token) => service.RecurringAsync(token);
    [HttpPost("recurring")]
    [Authorize(Policy = AuthorizationPolicies.FinanceJournalCreate)]
    public Task<AccountingRecurringExpenseDto> CreateRecurring(SaveRecurringExpenseRequest request, CancellationToken token) => service.SaveRecurringAsync(null, request, token);
    [HttpPut("recurring/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.FinanceJournalCreate)]
    public Task<AccountingRecurringExpenseDto> UpdateRecurring(Guid id, SaveRecurringExpenseRequest request, CancellationToken token) => service.SaveRecurringAsync(id, request, token);
    [HttpPost("recurring/generate")]
    [Authorize(Policy = AuthorizationPolicies.FinanceJournalCreate)]
    public Task<GenerateRecurringExpensesResult> Generate(GenerateRecurringExpensesRequest request, CancellationToken token) => service.GenerateRecurringAsync(request, token);
    [HttpGet("commission-preview")]
    public Task<AccountingCommissionPreviewDto> Preview([FromQuery] int year, [FromQuery] int month, CancellationToken token) => service.PreviewCommissionsAsync(year, month, token);
}

[ApiController]
[Route("api/accounting/exports")]
[Authorize(Policy = AuthorizationPolicies.FinanceAccess)]
public sealed class AccountingExportsController(IAccountingBookService service) : ControllerBase
{
    [HttpGet("{report}")]
    public async Task<IActionResult> Export(string report, [FromQuery] AccountingExportFilter filter, CancellationToken token)
        => File(await service.ExportAsync(report, filter, token), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"MIS-{report}-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
}
public sealed record AccountingBookActionRequest([MaxLength(1000)] string? Reason);
