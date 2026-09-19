using MIS.Domain.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class CollectionSettingsPolicyTests
{
    [Fact]
    public void Read_EmptyJson_HasNoPolicy()
    {
        var policy = CollectionSettingsPolicy.Read("{}");
        Assert.Null(policy.GraceDays);
        Assert.Null(policy.ToleranceAmount);
        Assert.Null(CollectionSettingsPolicy.Parse("{}"));
    }

    [Fact]
    public void Read_ExtractsAndClampsValues()
    {
        var policy = CollectionSettingsPolicy.Read("""{"ptpGraceDays": 45, "ptpToleranceAmount": -10, "other": "keep"}""");
        Assert.Equal(30, policy.GraceDays);
        Assert.Equal(0m, policy.ToleranceAmount);
    }

    [Fact]
    public void Merge_WritesPolicyAndPreservesUnknownKeys()
    {
        var json = CollectionSettingsPolicy.Merge("""{"channel":"CALL","ptpGraceDays":1}""", 3, 50.5m);
        var policy = CollectionSettingsPolicy.Read(json);
        Assert.Equal(3, policy.GraceDays);
        Assert.Equal(50.5m, policy.ToleranceAmount);
        Assert.Contains("\"channel\":\"CALL\"", json.Replace(" ", string.Empty));
    }

    [Fact]
    public void Merge_NullFields_RemovesPolicyKeys()
    {
        var json = CollectionSettingsPolicy.Merge("""{"ptpGraceDays":3,"ptpToleranceAmount":10,"keep":true}""", null, null);
        Assert.Null(CollectionSettingsPolicy.Parse(json));
        Assert.Contains("\"keep\":true", json.Replace(" ", string.Empty));
        Assert.DoesNotContain("ptpGraceDays", json);
    }

    [Fact]
    public void Parse_MissingKeys_ReturnsNull_OtherwiseDefaultsZeros()
    {
        Assert.Null(CollectionSettingsPolicy.Parse("""{"keep":1}"""));
        var parsed = CollectionSettingsPolicy.Parse("""{"ptpGraceDays":2}""");
        Assert.Equal((2, 0m), parsed);
    }
}
