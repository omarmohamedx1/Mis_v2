using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Accounting;

public sealed record AccountingBookFilter(DateOnly? From = null, DateOnly? To = null, string? Category = null, string? Status = null, string? Search = null, Guid? EmployeeId = null, int Page = 1, int PageSize = 30);
public sealed record AccountingBookEntryDto(Guid Id, string DocumentNumber, string Category, string Direction, DateOnly TransactionDate, decimal Amount,
    Guid AccountId, string AccountCode, string AccountName, Guid CounterAccountId, string CounterAccountName, string Description, string? Reference,
    Guid JournalEntryId, string? JournalNumber, string Status, Guid? EmployeeId, string? EmployeeName, Guid? RelatedAdvanceId, Guid? RecurringExpenseId, string? SourceKey, string CreatedBy, DateTimeOffset CreatedAt);
public sealed record AccountingBookPageDto(IReadOnlyList<AccountingBookEntryDto> Items, int Page, int PageSize, int TotalCount, decimal TotalAmount);
public sealed record CreateAccountingBookEntryRequest(
    [Required, MaxLength(40)] string Category, DateOnly TransactionDate,
    [Range(typeof(decimal), "0.01", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal Amount,
    Guid AccountId, Guid CounterAccountId, [Required, MaxLength(1000)] string Description,
    [MaxLength(160)] string? Reference, Guid? EmployeeId = null, Guid? RelatedAdvanceId = null, Guid? RequestId = null);
public sealed record AccountingBookCategoryTotalDto(string Category, decimal Amount, int Count);
public sealed record AccountingBookSummaryDto(DateOnly From, DateOnly To, decimal Income, decimal Expenses, decimal AdvancesIssued, decimal AdvanceRepayments, decimal CashIn, decimal CashOut, decimal NetCashFlow, decimal PendingAmount, int PendingCount, decimal OutstandingAdvances, decimal CarriedAdvances, int LegacyPaidCount, IReadOnlyList<AccountingBookCategoryTotalDto> Categories);
public sealed record AccountingAdvanceDto(Guid Id, string DocumentNumber, Guid EmployeeId, string EmployeeName, DateOnly Date, decimal Amount, decimal Repaid, decimal Remaining, bool Carried, string Status, Guid JournalEntryId, Guid AccountId);
public sealed record AccountingRecurringExpenseDto(Guid Id, string Name, decimal Amount, int DueDay, Guid AccountId, Guid CashAccountId, DateOnly StartsOn, DateOnly? EndsOn, string? Notes, bool IsActive);
public sealed record SaveRecurringExpenseRequest([Required, MaxLength(200)] string Name,
    [Range(typeof(decimal), "0.01", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal Amount,
    [Range(1, 31)] int DueDay, Guid AccountId, Guid CashAccountId, DateOnly StartsOn, DateOnly? EndsOn, [MaxLength(1000)] string? Notes, bool IsActive = true);
public sealed record GenerateRecurringExpensesRequest([Range(2000, 2100)] int Year, [Range(1, 12)] int Month);
public sealed record GenerateRecurringExpensesResult(int Created, int AlreadyGenerated, int OutsideSchedule);
public sealed record AccountingExportFilter(DateOnly? From = null, DateOnly? To = null, int? Year = null, int? Month = null, string? Category = null, string? Status = null, string? Search = null, Guid? PeriodId = null, DateOnly? AsOf = null);
public sealed record AccountingCommissionPreviewRowDto(string Scope, Guid UserId, string Name, int CaseCount, decimal Collected, decimal Commission, string RuleCode, string Basis, decimal? Percentage, decimal? FixedAmount, bool Locked);
public sealed record AccountingCommissionPreviewDto(int Year, int Month, decimal ApprovedCollections, IReadOnlyList<AccountingCommissionPreviewRowDto> Rows, IReadOnlyList<string> MissingRuleScopes);
public sealed record AccountingPaidSource(string Kind, Guid Id, string Category, decimal Amount, Guid? EmployeeId, string Description, DateOnly PaidOn, decimal Transportation = 0, decimal Commissions = 0, decimal Bonuses = 0);
