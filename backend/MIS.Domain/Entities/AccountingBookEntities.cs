using MIS.Domain.Constants;

namespace MIS.Domain.Entities;

public sealed class AccountingBookEntry
{
    private AccountingBookEntry() { }
    public AccountingBookEntry(Guid id, string category, DateOnly date, decimal amount, Guid accountId, Guid counterAccountId,
        string description, string? reference, Guid journalId, Guid creatorId, DateTimeOffset createdAt,
        Guid? employeeId = null, Guid? relatedAdvanceId = null, Guid? recurringId = null, string? sourceKey = null)
    {
        if (id == Guid.Empty || accountId == Guid.Empty || counterAccountId == Guid.Empty || journalId == Guid.Empty || creatorId == Guid.Empty) throw new ArgumentException("Accounting identities are required.");
        if (!AccountingBookValues.Categories.Contains(category)) throw new ArgumentException("Invalid accounting category.");
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new ArgumentOutOfRangeException(nameof(amount), "Use a positive amount with at most two decimal places.");
        if (accountId == counterAccountId) throw new ArgumentException("Choose two different accounts.");
        if (AccountingBookValues.IsAdvance(category) && employeeId is null) throw new ArgumentException("An employee is required for an advance.");
        if (category == "ADVANCE_REPAYMENT" && relatedAdvanceId is null) throw new ArgumentException("An original advance is required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Id = id; Category = category; TransactionDate = date; Amount = amount; AccountId = accountId; CounterAccountId = counterAccountId;
        Description = description.Trim(); Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(); JournalEntryId = journalId;
        CreatedById = creatorId; CreatedAt = createdAt; EmployeeId = employeeId; RelatedAdvanceId = relatedAdvanceId; RecurringExpenseId = recurringId; SourceKey = sourceKey;
        DocumentNumber = $"ACC-{date:yyyyMM}-{id.ToString("N")[..10].ToUpperInvariant()}";
    }
    public Guid Id { get; private set; }
    public string DocumentNumber { get; private set; } = "";
    public string Category { get; private set; } = "";
    public DateOnly TransactionDate { get; private set; }
    public decimal Amount { get; private set; }
    public Guid AccountId { get; private set; }
    public FinanceAccount Account { get; private set; } = null!;
    public Guid CounterAccountId { get; private set; }
    public FinanceAccount CounterAccount { get; private set; } = null!;
    public string Description { get; private set; } = "";
    public string? Reference { get; private set; }
    public Guid JournalEntryId { get; private set; }
    public JournalEntry JournalEntry { get; private set; } = null!;
    public Guid? EmployeeId { get; private set; }
    public Employee? Employee { get; private set; }
    public Guid? RelatedAdvanceId { get; private set; }
    public AccountingBookEntry? RelatedAdvance { get; private set; }
    public Guid? RecurringExpenseId { get; private set; }
    public AccountingRecurringExpense? RecurringExpense { get; private set; }
    public string? SourceKey { get; private set; }
    public Guid CreatedById { get; private set; }
    public User CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedById { get; private set; }
    public void Delete(Guid actorId, DateTimeOffset at)
    {
        if (DeletedAt.HasValue) return;
        DeletedAt = at; DeletedById = actorId;
        if (SourceKey?.StartsWith("RECURRING:") == true) SourceKey = $"DELETED:{Id}:{SourceKey}";
    }
}

public sealed class AccountingRecurringExpense
{
    private AccountingRecurringExpense() { }
    public AccountingRecurringExpense(string name, decimal amount, int dueDay, Guid accountId, Guid cashAccountId, DateOnly startsOn, DateOnly? endsOn, string? notes, Guid createdById, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid(); CreatedById = createdById; CreatedAt = createdAt;
        Update(name, amount, dueDay, accountId, cashAccountId, startsOn, endsOn, notes, true);
    }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public decimal Amount { get; private set; }
    public int DueDay { get; private set; }
    public Guid AccountId { get; private set; }
    public FinanceAccount Account { get; private set; } = null!;
    public Guid CashAccountId { get; private set; }
    public FinanceAccount CashAccount { get; private set; } = null!;
    public DateOnly StartsOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedById { get; private set; }
    public User CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public void Update(string name, decimal amount, int dueDay, Guid accountId, Guid cashAccountId, DateOnly startsOn, DateOnly? endsOn, string? notes, bool active)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (amount <= 0 || decimal.Round(amount, 2) != amount || dueDay is < 1 or > 31 || endsOn < startsOn) throw new ArgumentException("Invalid recurring expense amount, due day, or dates.");
        if (accountId == Guid.Empty || cashAccountId == Guid.Empty || accountId == cashAccountId) throw new ArgumentException("Two different posting accounts are required.");
        Name = name.Trim(); Amount = amount; DueDay = dueDay; AccountId = accountId; CashAccountId = cashAccountId;
        StartsOn = startsOn; EndsOn = endsOn; Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(); IsActive = active;
    }
    public DateOnly? DueDate(int year, int month)
    {
        var date = new DateOnly(year, month, Math.Min(DueDay, DateTime.DaysInMonth(year, month)));
        return IsActive && date >= StartsOn && (!EndsOn.HasValue || date <= EndsOn) ? date : null;
    }
}
