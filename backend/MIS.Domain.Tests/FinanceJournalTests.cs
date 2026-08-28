using MIS.Domain.Constants;
using MIS.Domain.Entities;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class FinanceJournalTests
{
    private static readonly Guid LegalEntityId = Guid.NewGuid();
    private static readonly Guid PeriodId = Guid.NewGuid();
    private static readonly Guid MakerId = Guid.NewGuid();
    private static readonly Guid CheckerId = Guid.NewGuid();

    [Fact]
    public void Balanced_journal_can_complete_maker_checker_posting_workflow()
    {
        var journal = NewJournal();
        journal.AddLine(Guid.NewGuid(), 1_000, 0, 1_000, 0, 1, "Cash");
        journal.AddLine(Guid.NewGuid(), 0, 1_000, 0, 1_000, 1, "Client funds", Guid.NewGuid());

        journal.Submit();
        journal.Approve(CheckerId, DateTimeOffset.UtcNow);
        journal.Post("JE-202608-000001", CheckerId, DateTimeOffset.UtcNow);

        Assert.Equal(FinanceValues.JournalStatuses.Posted, journal.Status);
        Assert.Equal(1_000, journal.TotalDebit);
        Assert.Equal(journal.TotalDebit, journal.TotalCredit);
    }

    [Fact]
    public void Unbalanced_journal_cannot_be_submitted()
    {
        var journal = NewJournal();
        journal.AddLine(Guid.NewGuid(), 500, 0, 500, 0, 1, "Debit");
        journal.AddLine(Guid.NewGuid(), 0, 450, 0, 450, 1, "Credit");

        var error = Assert.Throws<InvalidOperationException>(journal.Submit);

        Assert.Contains("balanced", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(FinanceValues.JournalStatuses.Draft, journal.Status);
    }

    [Fact]
    public void Maker_cannot_approve_own_manual_journal()
    {
        var journal = NewJournal();
        journal.AddLine(Guid.NewGuid(), 100, 0, 100, 0, 1, "Debit");
        journal.AddLine(Guid.NewGuid(), 0, 100, 0, 100, 1, "Credit");
        journal.Submit();

        Assert.Throws<InvalidOperationException>(() => journal.Approve(MakerId, DateTimeOffset.UtcNow));
        Assert.Equal(FinanceValues.JournalStatuses.PendingApproval, journal.Status);
    }

    [Fact]
    public void Journal_line_rejects_simultaneous_debit_and_credit()
    {
        var journal = NewJournal();
        Assert.Throws<ArgumentException>(() => journal.AddLine(Guid.NewGuid(), 100, 100, 100, 100, 1, "Invalid"));
    }

    [Fact]
    public void Posted_journal_cannot_accept_new_lines_and_can_only_be_marked_reversed()
    {
        var journal = NewJournal();
        journal.AddLine(Guid.NewGuid(), 100, 0, 100, 0, 1, "Debit");
        journal.AddLine(Guid.NewGuid(), 0, 100, 0, 100, 1, "Credit");
        journal.Submit(); journal.Approve(CheckerId, DateTimeOffset.UtcNow); journal.Post("JE-202608-000002", CheckerId, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => journal.AddLine(Guid.NewGuid(), 1, 0, 1, 0, 1, "Late line"));
        journal.MarkReversed();
        Assert.Equal(FinanceValues.JournalStatuses.Reversed, journal.Status);
    }

    private static JournalEntry NewJournal() => new(LegalEntityId, PeriodId, null, "MANUAL", new DateOnly(2026, 8, 28), new DateOnly(2026, 8, 28), "EGP", "Test journal", MakerId, DateTimeOffset.UtcNow);
}
