using MIS.Domain.Constants;
using MIS.Domain.Entities;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class CollectionCorrectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ManualActivityCorrection_PreservesIdentityAndCreator_AndNormalizesFollowUp()
    {
        var creator = Guid.NewGuid();
        var activity = new CollectionActivity(Guid.NewGuid(), "CALL", "Wrong", "Old", "PHONE", creator, Now, null);
        var id = activity.Id;
        activity.Correct(" note ", " Correct ", " Updated ", " DESK ", Now.ToOffset(TimeSpan.FromHours(3)));
        Assert.Equal(id, activity.Id);
        Assert.Equal(creator, activity.CreatedById);
        Assert.Equal(Now, activity.CreatedAt);
        Assert.Equal("NOTE", activity.ActivityType);
        Assert.Equal("Correct", activity.Result);
        Assert.Equal("Updated", activity.Notes);
        Assert.Equal(TimeSpan.Zero, activity.NextFollowUpAt!.Value.Offset);
    }

    [Theory]
    [InlineData("PAYMENT")]
    [InlineData("PTP_CREATED")]
    [InlineData("PTP_KEPT")]
    public void GeneratedActivity_CannotBeRewrittenAsManualActivity(string type)
    {
        var activity = new CollectionActivity(Guid.NewGuid(), type, null, null, null, Guid.NewGuid(), Now, null);
        Assert.Throws<InvalidOperationException>(() => activity.Correct("NOTE", null, null, null, null));
        Assert.Equal(type, activity.ActivityType);
    }

    [Fact]
    public void ManualActivity_CannotBeConvertedToFinancialEvent()
    {
        var activity = new CollectionActivity(Guid.NewGuid(), "NOTE", null, null, null, Guid.NewGuid(), Now, null);
        Assert.Throws<InvalidOperationException>(() => activity.Correct("PAYMENT", null, null, null, null));
    }

    [Fact]
    public void UnpaidPromiseCorrection_ClearsPreviousEvaluationAndPreservesCreator()
    {
        var promise = new PromiseToPay(Guid.NewGuid(), 100, new DateOnly(2026, 10, 16), Guid.NewGuid(), "CALL", null, Now);
        var creator = promise.CollectorId;
        promise.ApplyEvaluation(CollectionsValues.PromiseStatuses.Fulfilled, 0, Now);
        promise.Correct(400, new DateOnly(2026, 10, 17), " WHATSAPP ", " Corrected ");
        Assert.Equal(400, promise.PromisedAmount);
        Assert.Equal(new DateOnly(2026, 10, 17), promise.PromiseDate);
        Assert.Equal(creator, promise.CollectorId);
        Assert.Equal("WHATSAPP", promise.Channel);
        Assert.Null(promise.FulfilledAt);
        Assert.Null(promise.EvaluatedAt);
        Assert.Equal(CollectionsValues.PromiseStatuses.Active, promise.Status);
    }

    [Fact]
    public void PaidPromise_CannotBeCorrected()
    {
        var promise = new PromiseToPay(Guid.NewGuid(), 100, new DateOnly(2026, 10, 16), Guid.NewGuid(), "CALL", null, Now);
        promise.ApplyEvaluation(CollectionsValues.PromiseStatuses.PartiallyFulfilled, 50, Now);
        Assert.Throws<InvalidOperationException>(() => promise.Correct(200, new DateOnly(2026, 10, 17), "CALL", null));
        Assert.Equal(100, promise.PromisedAmount);
        Assert.Equal(50, promise.ActualPaidAmount);
    }
}
