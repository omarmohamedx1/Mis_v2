using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Finance;

public sealed record FinanceDashboardDto(
    decimal GrossCollectionsToday,
    decimal GrossCollectionsMonthToDate,
    decimal ClientMoneyLiability,
    decimal RevenueMonthToDate,
    decimal ExpensesMonthToDate,
    decimal NetOperatingResult,
    int PendingJournals,
    int FailedEvents,
    int OpenPeriods,
    string BaseCurrencyCode);

public sealed record FinanceAccountDto(Guid Id, string Code, string NameArabic, string NameEnglish, string AccountType, string NormalBalance, bool PostingAllowed, string? ControlAccountType, decimal Balance);
public sealed record AccountingPeriodDto(Guid Id, int Year, int PeriodNumber, string Name, DateOnly StartDate, DateOnly EndDate, string Status, DateTimeOffset? ClosedAt, string? CloseReason);
public sealed record JournalLineDto(Guid Id, int LineNumber, Guid AccountId, string AccountCode, string AccountNameArabic, string AccountNameEnglish, decimal Debit, decimal Credit, decimal BaseDebit, decimal BaseCredit, decimal ExchangeRate, string Description, Guid? ClientId, Guid? CollectorId, Guid? BranchId);
public sealed record JournalDto(Guid Id, string? JournalNumber, string EntryType, DateOnly TransactionDate, DateOnly PostingDate, string CurrencyCode, string Description, decimal TotalDebit, decimal TotalCredit, string Status, string CreatedBy, DateTimeOffset CreatedAt, string? ApprovedBy, DateTimeOffset? ApprovedAt, string? PostedBy, DateTimeOffset? PostedAt, Guid? ReversalOfJournalId, IReadOnlyCollection<JournalLineDto> Lines);
public sealed record FinanceJournalListItemDto(Guid Id, string? JournalNumber, string EntryType, DateOnly PostingDate, string Description, decimal TotalDebit, decimal TotalCredit, string Status, DateTimeOffset CreatedAt);
public sealed record FinancePagedResultDto<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record CreateManualJournalLineRequest(
    Guid AccountId,
    [Range(typeof(decimal), "0", "999999999999999999")] decimal Debit,
    [Range(typeof(decimal), "0", "999999999999999999")] decimal Credit,
    [Required, MaxLength(1000)] string Description,
    Guid? ClientId,
    Guid? CollectorId,
    Guid? BranchId);

public sealed record CreateManualJournalRequest(
    DateOnly TransactionDate,
    DateOnly PostingDate,
    [Required, StringLength(3, MinimumLength = 3)] string CurrencyCode,
    [Required, MaxLength(1000)] string Description,
    [MinLength(2)] IReadOnlyCollection<CreateManualJournalLineRequest> Lines);

public sealed record PeriodActionRequest([Required, MaxLength(1000)] string Reason);
public sealed record ClientLedgerEntryDto(Guid Id, DateOnly TransactionDate, string EntryType, string JournalNumber, string Description, decimal Debit, decimal Credit, decimal RunningBalance, string CurrencyCode, Guid SourceId);
public sealed record ClientLedgerDto(Guid ClientId, string ClientCode, string ClientNameArabic, string ClientNameEnglish, decimal Balance, string CurrencyCode, IReadOnlyCollection<ClientLedgerEntryDto> Entries);
public sealed record TrialBalanceRowDto(Guid AccountId, string AccountCode, string AccountNameArabic, string AccountNameEnglish, string AccountType, decimal DebitMovement, decimal CreditMovement, decimal Balance);
public sealed record TrialBalanceDto(DateOnly AsOf, string CurrencyCode, decimal TotalDebit, decimal TotalCredit, IReadOnlyCollection<TrialBalanceRowDto> Rows);
public sealed record FinancialAuditDto(Guid Id, string Action, string EntityType, Guid EntityId, string Actor, string? Reason, DateTimeOffset CreatedAt);

public sealed record CollectionAllocationDto(
    Guid Id,
    Guid CaseId,
    string CaseNumber,
    int LineNumber,
    decimal Amount,
    decimal OutstandingBefore,
    decimal OverdueBefore);

public sealed record CollectionFinanceListItemDto(
    Guid PaymentId,
    Guid ReceiptId,
    string ReferenceNumber,
    DateOnly PaymentDate,
    string ClientCode,
    string ClientNameArabic,
    string ClientNameEnglish,
    decimal GrossAmount,
    string CurrencyCode,
    string Channel,
    string Status,
    string CollectorName,
    string JournalNumber,
    string? ClearingJournalNumber);

public sealed record CollectionFinanceDto(
    Guid PaymentId,
    Guid ReceiptId,
    string ReferenceNumber,
    DateOnly PaymentDate,
    Guid ClientId,
    string ClientCode,
    string ClientNameArabic,
    string ClientNameEnglish,
    decimal GrossAmount,
    decimal BaseAmount,
    decimal ExchangeRate,
    string CurrencyCode,
    string Channel,
    string DestinationType,
    string? DestinationReference,
    string Status,
    Guid? CollectorId,
    string? CollectorName,
    Guid JournalEntryId,
    string JournalNumber,
    Guid? ClearingJournalEntryId,
    string? ClearingJournalNumber,
    Guid? ReversalJournalEntryId,
    string? ReversalJournalNumber,
    DateTimeOffset PostedAt,
    DateTimeOffset? ClearedAt,
    DateTimeOffset? ReversedAt,
    IReadOnlyCollection<CollectionAllocationDto> Allocations);

public sealed record ClearCollectionRequest(
    DateOnly ClearedOn,
    [Required, MaxLength(200)] string Reference);

public sealed record CustodySummaryDto(
    Guid AccountId,
    Guid CollectorId,
    string CollectorName,
    string CurrencyCode,
    decimal Balance,
    decimal SoftLimit,
    decimal HardLimit,
    bool SoftLimitExceeded,
    bool HardLimitExceeded,
    DateOnly? OldestOutstandingDate,
    string Status);

public sealed record CustodyTransactionDto(
    Guid Id,
    DateOnly TransactionDate,
    string TransactionType,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    Guid SourceId,
    Guid? PaymentId,
    string? PaymentReference,
    Guid JournalEntryId,
    string JournalNumber);

public sealed record CustodyDetailsDto(
    CustodySummaryDto Summary,
    IReadOnlyCollection<CustodyTransactionDto> Transactions);

public sealed record UpdateCustodyLimitsRequest(
    [Range(typeof(decimal), "0", "999999999999999999")] decimal SoftLimit,
    [Range(typeof(decimal), "0.01", "999999999999999999")] decimal HardLimit,
    [Required, MaxLength(1000)] string Reason);
