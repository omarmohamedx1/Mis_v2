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
    [MaxLength(160)] string? Reference, Guid? EmployeeId = null, Guid? RelatedAdvanceId = null, Guid? RequestId = null,
    [MaxLength(80)] string? PaymentMethod = null, [MaxLength(1000)] string? Notes = null);
public sealed record AccountingBookCategoryTotalDto(string Category, decimal Amount, int Count);
public sealed record AccountingBookSummaryDto(DateOnly From, DateOnly To, decimal CompanyIncome, decimal Revenue, decimal Expenses, decimal AdvancesIssued, decimal AdvanceRepayments, decimal CashIn, decimal CashOut, decimal NetCashFlow, decimal PendingAmount, int PendingCount, decimal OutstandingAdvances, decimal CarriedAdvances, int LegacyPaidCount, IReadOnlyList<AccountingBookCategoryTotalDto> Categories);
/// <summary>Management profit and loss from active registered claims and existing expense calculations.</summary>
public sealed record AccountingProfitLossDto(
    DateOnly From, DateOnly To, decimal TotalClaims, decimal TotalExpenses, decimal NetProfitLoss,
    IReadOnlyList<AccountingProfitLossCategoryDto> ClaimCategories,
    IReadOnlyList<AccountingProfitLossCategoryDto> ExpenseCategories);
/// <summary>A claim or expense category and its selected-period transactions.</summary>
public sealed record AccountingProfitLossCategoryDto(
    string Category, decimal Amount, IReadOnlyList<AccountingProfitLossTransactionDto> Transactions);
/// <summary>A posted journal movement contributing to a profit and loss category.</summary>
public sealed record AccountingProfitLossTransactionDto(
    DateOnly Date, string? JournalNumber, string Description, decimal Amount);
public sealed record AccountingAdvancesFilter(
    DateOnly? From = null,
    DateOnly? To = null,
    string? EmployeeName = null,
    string? EmployeeNumber = null,
    Guid? DepartmentId = null,
    string? Status = null,
    Guid? EmployeeId = null,
    DateOnly? AsOf = null,
    string? Search = null,
    bool CarriedOnly = false);
public sealed record AccountingAdvancesResultDto(IReadOnlyList<AccountingAdvanceDto> Items, decimal TotalAmount, decimal TotalPaid, decimal TotalRemaining, int OpenCount);
public sealed record AccountingAdvanceDto(Guid Id, string DocumentNumber, Guid EmployeeId, string EmployeeNumber, string EmployeeName, string? DepartmentName, Guid? DepartmentId,
    DateOnly Date, decimal Amount, decimal Repaid, decimal Remaining, string Reason, string? PaymentMethod, string? Reference, string? Notes,
    string Status, Guid JournalEntryId, Guid AccountId, string CreatedBy, DateTimeOffset CreatedAt, bool Carried);
public sealed record AccountingAdvancePaymentDto(Guid Id, DateOnly Date, decimal Amount, string Status, string? PaymentMethod, string? Reference, string? Notes,
    string CreatedBy, DateTimeOffset CreatedAt, decimal RemainingAfter);
public sealed record AccountingAdvanceDetailsDto(AccountingAdvanceDto Advance, decimal EmployeeTotalOutstanding, IReadOnlyList<AccountingAdvancePaymentDto> Payments);
public sealed record AccountingRecurringExpenseDto(Guid Id, string Name, decimal Amount, int DueDay, Guid AccountId, Guid CashAccountId, DateOnly StartsOn, DateOnly? EndsOn, string? Notes, bool IsActive);
public sealed record SaveRecurringExpenseRequest([Required, MaxLength(200)] string Name,
    [Range(typeof(decimal), "0.01", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal Amount,
    [Range(1, 31)] int DueDay, Guid AccountId, Guid CashAccountId, DateOnly StartsOn, DateOnly? EndsOn, [MaxLength(1000)] string? Notes, bool IsActive = true);
public sealed record GenerateRecurringExpensesRequest([Range(2000, 2100)] int Year, [Range(1, 12)] int Month);
public sealed record GenerateRecurringExpensesResult(int Created, int AlreadyGenerated, int OutsideSchedule, int NotDue = 0);
public sealed record ConfirmAccountingDeletionRequest(bool Confirmed, [MaxLength(1000)] string? Reason = null);
public sealed record AccountingExportFilter(DateOnly? From = null, DateOnly? To = null, int? Year = null, int? Month = null, string? Category = null, string? Status = null, string? Search = null, Guid? PeriodId = null, DateOnly? AsOf = null, Guid? DepartmentId = null, string? EmployeeName = null, string? EmployeeNumber = null, bool CarriedOnly = false);
public sealed record AccountingCommissionPreviewRowDto(string Scope, Guid UserId, string Name, int CaseCount, decimal Collected, decimal Commission, string RuleCode, string Basis, decimal? Percentage, decimal? FixedAmount, bool Locked);
public sealed record AccountingCommissionPreviewDto(int Year, int Month, decimal ApprovedCollections, IReadOnlyList<AccountingCommissionPreviewRowDto> Rows, IReadOnlyList<string> MissingRuleScopes);
public sealed record AccountingPaidSource(string Kind, Guid Id, string Category, decimal Amount, Guid? EmployeeId, string Description, DateOnly PaidOn, decimal Transportation = 0, decimal Commissions = 0, decimal Bonuses = 0);
