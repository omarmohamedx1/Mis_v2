using MIS.Domain.Constants;

namespace MIS.Domain.Entities;

public sealed class CollectionFinancialReceipt
{
    private readonly List<CollectionPaymentAllocation> _allocations = [];
    private CollectionFinancialReceipt() { }

    public CollectionFinancialReceipt(Guid collectionPaymentId, Guid clientId, decimal grossAmount, string currencyCode,
        string channel, string destinationType, string? destinationReference, Guid? collectorId, Guid? branchId,
        DateTimeOffset createdAt)
    {
        if (collectionPaymentId == Guid.Empty || clientId == Guid.Empty || grossAmount <= 0)
            throw new ArgumentException("A valid payment, client, and amount are required.");
        Id = Guid.NewGuid(); CollectionPaymentId = collectionPaymentId; ClientId = clientId; GrossAmount = grossAmount;
        BaseAmount = grossAmount; ExchangeRate = 1; CurrencyCode = Code(currencyCode); Channel = Code(channel);
        DestinationType = Code(destinationType); DestinationReference = Optional(destinationReference);
        CollectorId = collectorId; BranchId = branchId; Status = FinanceValues.CollectionReceiptStatuses.Posted;
        CreatedAt = createdAt; PostedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid CollectionPaymentId { get; private set; }
    public CollectionPayment CollectionPayment { get; private set; } = null!;
    public Guid ClientId { get; private set; }
    public ClientOrganization Client { get; private set; } = null!;
    public decimal GrossAmount { get; private set; }
    public decimal BaseAmount { get; private set; }
    public decimal ExchangeRate { get; private set; }
    public string CurrencyCode { get; private set; } = "EGP";
    public string Channel { get; private set; } = string.Empty;
    public string DestinationType { get; private set; } = string.Empty;
    public string? DestinationReference { get; private set; }
    public Guid? CollectorId { get; private set; }
    public User? Collector { get; private set; }
    public Guid? BranchId { get; private set; }
    public Branch? Branch { get; private set; }
    public string Status { get; private set; } = FinanceValues.CollectionReceiptStatuses.Posted;
    public Guid JournalEntryId { get; private set; }
    public JournalEntry JournalEntry { get; private set; } = null!;
    public Guid? ClearingJournalEntryId { get; private set; }
    public JournalEntry? ClearingJournalEntry { get; private set; }
    public Guid? ReversalJournalEntryId { get; private set; }
    public JournalEntry? ReversalJournalEntry { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset PostedAt { get; private set; }
    public DateTimeOffset? ClearedAt { get; private set; }
    public DateTimeOffset? ReversedAt { get; private set; }
    public IReadOnlyCollection<CollectionPaymentAllocation> Allocations => _allocations.AsReadOnly();

    public CollectionPaymentAllocation AddAllocation(Guid caseId, decimal amount, decimal outstandingBefore, decimal overdueBefore, DateTimeOffset createdAt)
    {
        if (Status != FinanceValues.CollectionReceiptStatuses.Posted || ClearingJournalEntryId.HasValue)
            throw new InvalidOperationException("Allocations cannot change after clearing.");
        if (caseId == Guid.Empty || amount <= 0 || _allocations.Sum(x => x.Amount) + amount > GrossAmount)
            throw new ArgumentException("The allocation is invalid or exceeds the receipt amount.");
        var allocation = new CollectionPaymentAllocation(Id, caseId, _allocations.Count + 1, amount, outstandingBefore, overdueBefore, createdAt);
        _allocations.Add(allocation); return allocation;
    }

    public void LinkPostedJournal(Guid journalEntryId)
    {
        if (journalEntryId == Guid.Empty) throw new ArgumentException("Journal is required.", nameof(journalEntryId));
        if (JournalEntryId != Guid.Empty && JournalEntryId != journalEntryId) throw new InvalidOperationException("Receipt is already linked to another journal.");
        JournalEntryId = journalEntryId;
    }

    public void MarkCleared(Guid clearingJournalEntryId, DateTimeOffset now)
    {
        if (Status != FinanceValues.CollectionReceiptStatuses.Posted || ClearingJournalEntryId.HasValue)
            throw new InvalidOperationException("Only a posted uncleared receipt can be cleared.");
        ClearingJournalEntryId = clearingJournalEntryId; ClearedAt = now; Status = FinanceValues.CollectionReceiptStatuses.Cleared;
    }

    public void MarkReversed(Guid reversalJournalEntryId, DateTimeOffset now)
    {
        if (Status == FinanceValues.CollectionReceiptStatuses.Reversed) throw new InvalidOperationException("Receipt is already reversed.");
        ReversalJournalEntryId = reversalJournalEntryId; ReversedAt = now; Status = FinanceValues.CollectionReceiptStatuses.Reversed;
    }

    private static string Code(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim().ToUpperInvariant(); }
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CollectionPaymentAllocation
{
    private CollectionPaymentAllocation() { }
    internal CollectionPaymentAllocation(Guid receiptId, Guid caseId, int lineNumber, decimal amount, decimal outstandingBefore, decimal overdueBefore, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid(); ReceiptId = receiptId; CaseId = caseId; LineNumber = lineNumber; Amount = amount;
        OutstandingBefore = outstandingBefore; OverdueBefore = overdueBefore; CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid ReceiptId { get; private set; }
    public CollectionFinancialReceipt Receipt { get; private set; } = null!;
    public Guid CaseId { get; private set; }
    public CollectionCase Case { get; private set; } = null!;
    public int LineNumber { get; private set; }
    public decimal Amount { get; private set; }
    public decimal OutstandingBefore { get; private set; }
    public decimal OverdueBefore { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class CollectorCustodyAccount
{
    private CollectorCustodyAccount() { }
    public CollectorCustodyAccount(Guid collectorId, string currencyCode, Guid? branchId, decimal softLimit, decimal hardLimit, DateTimeOffset createdAt)
    {
        if (collectorId == Guid.Empty || softLimit < 0 || hardLimit <= 0 || hardLimit < softLimit) throw new ArgumentException("Custody account limits are invalid.");
        Id = Guid.NewGuid(); CollectorId = collectorId; CurrencyCode = currencyCode.Trim().ToUpperInvariant(); BranchId = branchId;
        SoftLimit = softLimit; HardLimit = hardLimit; Status = "ACTIVE"; CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid CollectorId { get; private set; }
    public User Collector { get; private set; } = null!;
    public Guid? BranchId { get; private set; }
    public Branch? Branch { get; private set; }
    public string CurrencyCode { get; private set; } = "EGP";
    public decimal SoftLimit { get; private set; }
    public decimal HardLimit { get; private set; }
    public string Status { get; private set; } = "ACTIVE";
    public DateTimeOffset CreatedAt { get; private set; }

    public void UpdateLimits(decimal softLimit, decimal hardLimit)
    {
        if (softLimit < 0 || hardLimit <= 0 || hardLimit < softLimit)
            throw new ArgumentException("The custody hard limit must be positive and greater than or equal to the soft limit.");
        SoftLimit = softLimit;
        HardLimit = hardLimit;
    }
}

public sealed class CollectorCustodyTransaction
{
    private CollectorCustodyTransaction() { }
    public CollectorCustodyTransaction(Guid custodyAccountId, Guid journalEntryLineId, Guid? receiptId, string transactionType,
        decimal debit, decimal credit, Guid sourceId, DateOnly transactionDate, DateTimeOffset createdAt)
    {
        if (custodyAccountId == Guid.Empty || journalEntryLineId == Guid.Empty || sourceId == Guid.Empty || (debit > 0) == (credit > 0))
            throw new ArgumentException("Custody transaction is invalid.");
        Id = Guid.NewGuid(); CustodyAccountId = custodyAccountId; JournalEntryLineId = journalEntryLineId; ReceiptId = receiptId;
        TransactionType = transactionType.Trim().ToUpperInvariant(); Debit = debit; Credit = credit; SourceId = sourceId;
        TransactionDate = transactionDate; CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid CustodyAccountId { get; private set; }
    public CollectorCustodyAccount CustodyAccount { get; private set; } = null!;
    public Guid JournalEntryLineId { get; private set; }
    public JournalEntryLine JournalEntryLine { get; private set; } = null!;
    public Guid? ReceiptId { get; private set; }
    public CollectionFinancialReceipt? Receipt { get; private set; }
    public string TransactionType { get; private set; } = string.Empty;
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public Guid SourceId { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class CollectionClearingEvent
{
    private CollectionClearingEvent() { }
    public CollectionClearingEvent(Guid receiptId, Guid fromAccountId, Guid toAccountId, Guid journalEntryId, decimal amount,
        string reference, DateOnly occurredOn, Guid createdById, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (receiptId == Guid.Empty || fromAccountId == Guid.Empty || toAccountId == Guid.Empty || journalEntryId == Guid.Empty || amount <= 0 || createdById == Guid.Empty)
            throw new ArgumentException("Clearing event is invalid.");
        Id = Guid.NewGuid(); ReceiptId = receiptId; FromAccountId = fromAccountId; ToAccountId = toAccountId;
        JournalEntryId = journalEntryId; Amount = amount; Reference = reference.Trim().ToUpperInvariant(); OccurredOn = occurredOn;
        CreatedById = createdById; CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid ReceiptId { get; private set; }
    public CollectionFinancialReceipt Receipt { get; private set; } = null!;
    public Guid FromAccountId { get; private set; }
    public FinanceAccount FromAccount { get; private set; } = null!;
    public Guid ToAccountId { get; private set; }
    public FinanceAccount ToAccount { get; private set; } = null!;
    public Guid JournalEntryId { get; private set; }
    public JournalEntry JournalEntry { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public DateOnly OccurredOn { get; private set; }
    public Guid CreatedById { get; private set; }
    public User CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
}
