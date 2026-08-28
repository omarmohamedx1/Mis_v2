using MIS.Domain.Constants;

namespace MIS.Domain.Entities;

public sealed class FinanceLegalEntity
{
    private FinanceLegalEntity() { }
    public FinanceLegalEntity(Guid id, string code, string nameArabic, string nameEnglish, string baseCurrencyCode, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Legal entity id is required.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(code); ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic); ArgumentException.ThrowIfNullOrWhiteSpace(nameEnglish);
        Id = id; Code = code.Trim().ToUpperInvariant(); NameArabic = nameArabic.Trim(); NameEnglish = nameEnglish.Trim(); BaseCurrencyCode = NormalizeCode(baseCurrencyCode); IsActive = true; CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameArabic { get; private set; } = string.Empty;
    public string NameEnglish { get; private set; } = string.Empty;
    public string BaseCurrencyCode { get; private set; } = "EGP";
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    private static string NormalizeCode(string value) => string.IsNullOrWhiteSpace(value) ? "EGP" : value.Trim().ToUpperInvariant();
}

public sealed class FinanceCurrency
{
    private FinanceCurrency() { }
    public FinanceCurrency(string code, string nameArabic, string nameEnglish, int minorUnits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code); if (minorUnits is < 0 or > 6) throw new ArgumentOutOfRangeException(nameof(minorUnits));
        Code = code.Trim().ToUpperInvariant(); NameArabic = nameArabic.Trim(); NameEnglish = nameEnglish.Trim(); MinorUnits = minorUnits; IsActive = true;
    }
    public string Code { get; private set; } = string.Empty;
    public string NameArabic { get; private set; } = string.Empty;
    public string NameEnglish { get; private set; } = string.Empty;
    public int MinorUnits { get; private set; }
    public bool IsActive { get; private set; }
}

public sealed class AccountingPeriod
{
    private AccountingPeriod() { }
    public AccountingPeriod(Guid legalEntityId, int year, int periodNumber, string name, DateOnly startDate, DateOnly endDate)
    {
        if (legalEntityId == Guid.Empty) throw new ArgumentException("Legal entity is required.", nameof(legalEntityId));
        if (periodNumber is < 1 or > 13 || endDate < startDate) throw new ArgumentException("Accounting period range is invalid.");
        Id = Guid.NewGuid(); LegalEntityId = legalEntityId; Year = year; PeriodNumber = periodNumber; Name = name.Trim(); StartDate = startDate; EndDate = endDate; Status = FinanceValues.PeriodStatuses.Open;
    }
    public Guid Id { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public FinanceLegalEntity LegalEntity { get; private set; } = null!;
    public int Year { get; private set; }
    public int PeriodNumber { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string Status { get; private set; } = FinanceValues.PeriodStatuses.Open;
    public Guid? ClosedById { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public string? CloseReason { get; private set; }
    public void SoftClose(Guid userId, DateTimeOffset now) { EnsureOpen(); Status = FinanceValues.PeriodStatuses.SoftClosed; ClosedById = userId; ClosedAt = now; }
    public void Close(Guid userId, string reason, DateTimeOffset now) { if (Status == FinanceValues.PeriodStatuses.Closed) throw new InvalidOperationException("The period is already closed."); ArgumentException.ThrowIfNullOrWhiteSpace(reason); Status = FinanceValues.PeriodStatuses.Closed; ClosedById = userId; ClosedAt = now; CloseReason = reason.Trim(); }
    public void Reopen(string reason) { if (Status != FinanceValues.PeriodStatuses.Closed) throw new InvalidOperationException("Only a closed period can be reopened."); ArgumentException.ThrowIfNullOrWhiteSpace(reason); Status = FinanceValues.PeriodStatuses.Open; CloseReason = reason.Trim(); ClosedById = null; ClosedAt = null; }
    private void EnsureOpen() { if (Status != FinanceValues.PeriodStatuses.Open) throw new InvalidOperationException("Only an open period can be soft-closed."); }
}

public sealed class FinanceAccount
{
    private FinanceAccount() { }
    public FinanceAccount(Guid id, Guid legalEntityId, string code, string nameArabic, string nameEnglish, string accountType, string normalBalance, bool postingAllowed, string? controlAccountType = null)
    {
        if (id == Guid.Empty || legalEntityId == Guid.Empty) throw new ArgumentException("Account and legal entity ids are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(code); ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic); ArgumentException.ThrowIfNullOrWhiteSpace(nameEnglish);
        Id = id; LegalEntityId = legalEntityId; Code = code.Trim(); NameArabic = nameArabic.Trim(); NameEnglish = nameEnglish.Trim(); AccountType = accountType.Trim().ToUpperInvariant(); NormalBalance = normalBalance.Trim().ToUpperInvariant(); PostingAllowed = postingAllowed; ControlAccountType = Normalize(controlAccountType); IsActive = true;
    }
    public Guid Id { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public FinanceLegalEntity LegalEntity { get; private set; } = null!;
    public Guid? ParentId { get; private set; }
    public FinanceAccount? Parent { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameArabic { get; private set; } = string.Empty;
    public string NameEnglish { get; private set; } = string.Empty;
    public string AccountType { get; private set; } = string.Empty;
    public string NormalBalance { get; private set; } = string.Empty;
    public bool PostingAllowed { get; private set; }
    public string? ControlAccountType { get; private set; }
    public bool RequiresClient { get; private set; }
    public bool RequiresCollector { get; private set; }
    public bool RequiresBranch { get; private set; }
    public bool IsActive { get; private set; }
    public void SetDimensionRules(bool requiresClient, bool requiresCollector, bool requiresBranch) { RequiresClient = requiresClient; RequiresCollector = requiresCollector; RequiresBranch = requiresBranch; }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}

public sealed class AccountingEvent
{
    private AccountingEvent() { }
    public AccountingEvent(string eventType, string sourceType, Guid sourceId, int sourceVersion, string idempotencyKey, string payloadSnapshot, DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType); ArgumentException.ThrowIfNullOrWhiteSpace(sourceType); ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (sourceId == Guid.Empty || sourceVersion < 1) throw new ArgumentException("Source identity is invalid.");
        Id = Guid.NewGuid(); EventType = eventType.Trim(); SourceType = sourceType.Trim(); SourceId = sourceId; SourceVersion = sourceVersion; IdempotencyKey = idempotencyKey.Trim(); PayloadSnapshot = string.IsNullOrWhiteSpace(payloadSnapshot) ? "{}" : payloadSnapshot; Status = FinanceValues.EventStatuses.Received; OccurredAt = occurredAt; CreatedAt = DateTimeOffset.UtcNow;
    }
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string SourceType { get; private set; } = string.Empty;
    public Guid SourceId { get; private set; }
    public int SourceVersion { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string PayloadSnapshot { get; private set; } = "{}";
    public string Status { get; private set; } = FinanceValues.EventStatuses.Received;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? Error { get; private set; }
    public void MarkPosted() { Status = FinanceValues.EventStatuses.Posted; Error = null; }
    public void MarkFailed(string error) { Status = FinanceValues.EventStatuses.Failed; Error = error.Trim(); }
}

public sealed class JournalEntry
{
    private readonly List<JournalEntryLine> _lines = [];
    private JournalEntry() { }
    public JournalEntry(Guid legalEntityId, Guid periodId, Guid? accountingEventId, string entryType, DateOnly transactionDate, DateOnly postingDate, string currencyCode, string description, Guid createdById, DateTimeOffset createdAt)
    {
        if (legalEntityId == Guid.Empty || periodId == Guid.Empty || createdById == Guid.Empty) throw new ArgumentException("Journal ownership is required.");
        Id = Guid.NewGuid(); LegalEntityId = legalEntityId; PeriodId = periodId; AccountingEventId = accountingEventId; EntryType = entryType.Trim().ToUpperInvariant(); TransactionDate = transactionDate; PostingDate = postingDate; CurrencyCode = currencyCode.Trim().ToUpperInvariant(); Description = description.Trim(); Status = FinanceValues.JournalStatuses.Draft; CreatedById = createdById; CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public FinanceLegalEntity LegalEntity { get; private set; } = null!;
    public Guid PeriodId { get; private set; }
    public AccountingPeriod Period { get; private set; } = null!;
    public Guid? AccountingEventId { get; private set; }
    public AccountingEvent? AccountingEvent { get; private set; }
    public string? JournalNumber { get; private set; }
    public string EntryType { get; private set; } = string.Empty;
    public DateOnly TransactionDate { get; private set; }
    public DateOnly PostingDate { get; private set; }
    public string CurrencyCode { get; private set; } = "EGP";
    public string Description { get; private set; } = string.Empty;
    public decimal TotalDebit { get; private set; }
    public decimal TotalCredit { get; private set; }
    public string Status { get; private set; } = FinanceValues.JournalStatuses.Draft;
    public Guid CreatedById { get; private set; }
    public User CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ApprovedById { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? PostedById { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public Guid? ReversalOfJournalId { get; private set; }
    public JournalEntry? ReversalOfJournal { get; private set; }
    public IReadOnlyCollection<JournalEntryLine> Lines => _lines.AsReadOnly();
    public JournalEntryLine AddLine(Guid accountId, decimal debit, decimal credit, decimal baseDebit, decimal baseCredit, decimal exchangeRate, string description, Guid? clientId = null, Guid? collectorId = null, Guid? branchId = null, Guid? costCenterId = null)
    {
        if (Status != FinanceValues.JournalStatuses.Draft) throw new InvalidOperationException("Lines can only be added to a draft journal.");
        var line = new JournalEntryLine(Id, _lines.Count + 1, accountId, debit, credit, baseDebit, baseCredit, exchangeRate, description, clientId, collectorId, branchId, costCenterId); _lines.Add(line); Recalculate(); return line;
    }
    public void Submit() { EnsureDraftAndBalanced(); Status = FinanceValues.JournalStatuses.PendingApproval; }
    public void Approve(Guid approverId, DateTimeOffset now) { if (Status != FinanceValues.JournalStatuses.PendingApproval) throw new InvalidOperationException("Only a pending journal can be approved."); if (approverId == CreatedById) throw new InvalidOperationException("The maker cannot approve their own journal."); Status = FinanceValues.JournalStatuses.Approved; ApprovedById = approverId; ApprovedAt = now; }
    public void Post(string journalNumber, Guid posterId, DateTimeOffset now, bool allowDraftSystemPosting = false)
    {
        if (Status != FinanceValues.JournalStatuses.Approved && !(allowDraftSystemPosting && Status == FinanceValues.JournalStatuses.Draft)) throw new InvalidOperationException("The journal is not approved for posting.");
        EnsureBalanced(); ArgumentException.ThrowIfNullOrWhiteSpace(journalNumber); JournalNumber = journalNumber.Trim(); Status = FinanceValues.JournalStatuses.Posted; PostedById = posterId; PostedAt = now;
    }
    public void MarkReversed() { if (Status != FinanceValues.JournalStatuses.Posted) throw new InvalidOperationException("Only a posted journal can be reversed."); Status = FinanceValues.JournalStatuses.Reversed; }
    public void LinkReversal(Guid originalJournalId) { if (originalJournalId == Guid.Empty) throw new ArgumentException("Original journal is required."); ReversalOfJournalId = originalJournalId; }
    private void Recalculate() { TotalDebit = _lines.Sum(x => x.BaseDebit); TotalCredit = _lines.Sum(x => x.BaseCredit); }
    private void EnsureDraftAndBalanced() { if (Status != FinanceValues.JournalStatuses.Draft) throw new InvalidOperationException("Only a draft journal can be submitted."); EnsureBalanced(); }
    private void EnsureBalanced() { Recalculate(); if (_lines.Count < 2 || TotalDebit <= 0 || TotalDebit != TotalCredit) throw new InvalidOperationException("A journal must contain at least two balanced non-zero lines."); }
}

public sealed class JournalEntryLine
{
    private JournalEntryLine() { }
    internal JournalEntryLine(Guid journalEntryId, int lineNumber, Guid accountId, decimal debit, decimal credit, decimal baseDebit, decimal baseCredit, decimal exchangeRate, string description, Guid? clientId, Guid? collectorId, Guid? branchId, Guid? costCenterId)
    {
        if ((debit > 0) == (credit > 0) || (baseDebit > 0) == (baseCredit > 0) || debit < 0 || credit < 0 || exchangeRate <= 0) throw new ArgumentException("A line must contain either debit or credit with a positive exchange rate.");
        Id = Guid.NewGuid(); JournalEntryId = journalEntryId; LineNumber = lineNumber; AccountId = accountId; Debit = debit; Credit = credit; BaseDebit = baseDebit; BaseCredit = baseCredit; ExchangeRate = exchangeRate; Description = description.Trim(); ClientId = clientId; CollectorId = collectorId; BranchId = branchId; CostCenterId = costCenterId;
    }
    public Guid Id { get; private set; }
    public Guid JournalEntryId { get; private set; }
    public JournalEntry JournalEntry { get; private set; } = null!;
    public int LineNumber { get; private set; }
    public Guid AccountId { get; private set; }
    public FinanceAccount Account { get; private set; } = null!;
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public decimal BaseDebit { get; private set; }
    public decimal BaseCredit { get; private set; }
    public decimal ExchangeRate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid? ClientId { get; private set; }
    public ClientOrganization? Client { get; private set; }
    public Guid? CollectorId { get; private set; }
    public User? Collector { get; private set; }
    public Guid? BranchId { get; private set; }
    public Branch? Branch { get; private set; }
    public Guid? CostCenterId { get; private set; }
}

public sealed class ClientLedgerEntry
{
    private ClientLedgerEntry() { }
    public ClientLedgerEntry(Guid clientId, Guid journalEntryLineId, string entryType, decimal debit, decimal credit, string currencyCode, Guid sourceId, DateOnly transactionDate, DateTimeOffset createdAt)
    { if (clientId == Guid.Empty || journalEntryLineId == Guid.Empty || sourceId == Guid.Empty || (debit > 0) == (credit > 0)) throw new ArgumentException("Client ledger entry is invalid."); Id = Guid.NewGuid(); ClientId = clientId; JournalEntryLineId = journalEntryLineId; EntryType = entryType.Trim().ToUpperInvariant(); Debit = debit; Credit = credit; CurrencyCode = currencyCode.Trim().ToUpperInvariant(); SourceId = sourceId; TransactionDate = transactionDate; CreatedAt = createdAt; }
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public ClientOrganization Client { get; private set; } = null!;
    public Guid JournalEntryLineId { get; private set; }
    public JournalEntryLine JournalEntryLine { get; private set; } = null!;
    public string EntryType { get; private set; } = string.Empty;
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public string CurrencyCode { get; private set; } = "EGP";
    public Guid SourceId { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class FinancialAuditLog
{
    private FinancialAuditLog() { }
    public FinancialAuditLog(Guid? actorId, string action, string entityType, Guid entityId, string beforeJson, string afterJson, string? reason, DateTimeOffset createdAt)
    { Id = Guid.NewGuid(); ActorId = actorId; Action = action.Trim(); EntityType = entityType.Trim(); EntityId = entityId; BeforeJson = beforeJson; AfterJson = afterJson; Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(); CreatedAt = createdAt; }
    public Guid Id { get; private set; }
    public Guid? ActorId { get; private set; }
    public User? Actor { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string BeforeJson { get; private set; } = "{}";
    public string AfterJson { get; private set; } = "{}";
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
