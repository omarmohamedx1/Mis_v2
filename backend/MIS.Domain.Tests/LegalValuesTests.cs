using MIS.Domain.Constants;
using MIS.Domain.Entities;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class LegalValuesTests
{
    [Theory]
    [InlineData(LegalValues.Actions.Settlement, CollectionsValues.CaseStatuses.Settled)]
    [InlineData(LegalValues.Actions.Judgment, CollectionsValues.CaseStatuses.Closed)]
    [InlineData(LegalValues.Actions.Return, CollectionsValues.CaseStatuses.Active)]
    public void Outcome_actions_map_back_to_collection_status(string action, string expected)
    {
        Assert.Equal(expected, LegalValues.CollectionStatusForAction(action));
    }

    [Theory]
    [InlineData(LegalValues.Actions.Note)]
    [InlineData(LegalValues.Actions.Notice)]
    [InlineData(LegalValues.Actions.Hearing)]
    public void Working_actions_keep_the_case_in_legal(string action)
    {
        Assert.Null(LegalValues.CollectionStatusForAction(action));
    }

    [Fact]
    public void Legal_file_starts_in_intake_and_rejects_unknown_stages()
    {
        var file = new LegalCaseFile(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal(LegalValues.Stages.Intake, file.Stage);
        Assert.Throws<ArgumentException>(() => file.SetStage("PLEADINGS", DateTimeOffset.UtcNow));
        file.SetStage(LegalValues.Stages.Court, DateTimeOffset.UtcNow);
        Assert.Equal(LegalValues.Stages.Court, file.Stage);
    }
}
