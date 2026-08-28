using MIS.Domain.Constants;
using MIS.Domain.Entities;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class CollectionFinanceTests
{
    [Fact]
    public void Receipt_allocations_must_not_exceed_gross_amount()
    {
        var receipt = NewReceipt(1_000m);
        receipt.AddAllocation(Guid.NewGuid(), 700m, 2_000m, 1_500m, DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() =>
            receipt.AddAllocation(Guid.NewGuid(), 301m, 1_000m, 800m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Cleared_receipt_is_immutable_and_can_be_reversed()
    {
        var receipt = NewReceipt(500m);
        receipt.AddAllocation(Guid.NewGuid(), 500m, 800m, 600m, DateTimeOffset.UtcNow);
        receipt.LinkPostedJournal(Guid.NewGuid());
        receipt.MarkCleared(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            receipt.AddAllocation(Guid.NewGuid(), 1m, 1m, 1m, DateTimeOffset.UtcNow));

        receipt.MarkReversed(Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal(FinanceValues.CollectionReceiptStatuses.Reversed, receipt.Status);
    }

    [Fact]
    public void Custody_transaction_requires_exactly_one_side()
    {
        Assert.Throws<ArgumentException>(() => new CollectorCustodyTransaction(
            Guid.NewGuid(), Guid.NewGuid(), null, FinanceValues.CustodyTransactionTypes.Collection,
            100m, 100m, Guid.NewGuid(), new DateOnly(2026, 8, 28), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Custody_limits_are_validated_and_can_be_updated()
    {
        var account = new CollectorCustodyAccount(Guid.NewGuid(), "EGP", null, 25_000m, 50_000m, DateTimeOffset.UtcNow);

        account.UpdateLimits(30_000m, 60_000m);

        Assert.Equal(30_000m, account.SoftLimit);
        Assert.Equal(60_000m, account.HardLimit);
        Assert.Throws<ArgumentException>(() => account.UpdateLimits(70_000m, 60_000m));
    }

    private static CollectionFinancialReceipt NewReceipt(decimal amount) => new(
        Guid.NewGuid(), Guid.NewGuid(), amount, "EGP", FinanceValues.CollectionChannels.CashCollector,
        "COLLECTOR_CUSTODY", "TEST-001", Guid.NewGuid(), null, DateTimeOffset.UtcNow);
}
