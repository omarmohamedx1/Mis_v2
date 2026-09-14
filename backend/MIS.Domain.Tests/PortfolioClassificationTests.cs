using MIS.Application.Common;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class PortfolioClassificationTests
{
    private static CollectionPortfolio Default() =>
        new(Guid.NewGuid(), "DEFAULT", "المحفظة الافتراضية", "Default Portfolio", "EGP", DateTimeOffset.UtcNow);

    [Theory]
    [InlineData("ACT", "LOAN")]
    [InlineData("ACT", "VISA")]
    [InlineData("ACT", "AUTO")]
    [InlineData("WO", "LOAN")]
    [InlineData("WO", "VISA")]
    [InlineData("WO", "AUTO")]
    [InlineData("CORP", "ACT")]
    [InlineData("CORP", "WO")]
    public void AssignClassification_AcceptsSupportedCombinations(string primary, string sub)
    {
        var portfolio = Default();
        portfolio.AssignClassification(primary, sub);
        Assert.Equal(primary, portfolio.PrimaryClassification);
        Assert.Equal(sub, portfolio.SubClassification);
    }

    [Theory]
    [InlineData("CORP", "LOAN")]
    [InlineData("CORP", "VISA")]
    [InlineData("CORP", "AUTO")]
    [InlineData("ACT", "ACT")]
    [InlineData("WO", "CORP")]
    [InlineData("RETAIL", "LOAN")]
    [InlineData("ACT", "")]
    public void AssignClassification_RejectsUnsupportedCombinations(string primary, string sub)
    {
        var portfolio = Default();
        Assert.Throws<ArgumentException>(() => portfolio.AssignClassification(primary, sub));
        Assert.Null(portfolio.PrimaryClassification);
        Assert.Null(portfolio.SubClassification);
    }

    [Theory]
    [InlineData("W.O")]
    [InlineData("w.o")]
    [InlineData("wo")]
    [InlineData(" Wo ")]
    public void AssignClassification_NormalizesWriteOffToWo(string primary)
    {
        var portfolio = Default();
        portfolio.AssignClassification(primary, "visa");
        Assert.Equal(CollectionsValues.PrimaryClassifications.Wo, portfolio.PrimaryClassification);
        Assert.Equal(CollectionsValues.SubClassifications.Visa, portfolio.SubClassification);
        Assert.Equal("W.O", PortfolioClassification.Display(portfolio.PrimaryClassification));
    }

    [Fact]
    public void AssignClassification_ReplacesDefaultCodeAndReassignsCleanly()
    {
        var portfolio = Default();
        portfolio.AssignClassification("ACT", "LOAN");
        Assert.Equal("ACT-LOAN", portfolio.Code);

        portfolio.AssignClassification("CORP", "WO");
        Assert.Equal("CORP-WO", portfolio.Code);
    }

    [Fact]
    public void AssignClassification_KeepsBusinessOwnedCode()
    {
        var portfolio = new CollectionPortfolio(Guid.NewGuid(), "CIB-2026-Q1", "محفظة", "Portfolio", "EGP", DateTimeOffset.UtcNow);
        portfolio.AssignClassification("WO", "AUTO");
        Assert.Equal("CIB-2026-Q1", portfolio.Code);
        Assert.Equal("WO", portfolio.PrimaryClassification);
        Assert.Equal("AUTO", portfolio.SubClassification);
    }

    [Fact]
    public void ClassifyingConstructor_SetsCodeAndPair()
    {
        var portfolio = new CollectionPortfolio(Guid.NewGuid(), "DEFAULT", "محفظة", "Portfolio", "EGP", DateTimeOffset.UtcNow, "corp", "w.o");
        Assert.Equal("CORP-WO", portfolio.Code);
        Assert.Equal("CORP", portfolio.PrimaryClassification);
        Assert.Equal("WO", portfolio.SubClassification);
    }

    [Fact]
    public void SubClassificationsFor_ListsProductsForActAndWoAndSegmentsForCorp()
    {
        Assert.Equal(CollectionsValues.SubClassifications.Products, PortfolioClassification.SubClassificationsFor("ACT"));
        Assert.Equal(CollectionsValues.SubClassifications.Products, PortfolioClassification.SubClassificationsFor("W.O"));
        Assert.Equal(CollectionsValues.SubClassifications.CorporateSegments, PortfolioClassification.SubClassificationsFor("CORP"));
        Assert.Empty(PortfolioClassification.SubClassificationsFor("RETAIL"));
    }
}

public sealed class CollectionsClassificationContextTests
{
    [Fact]
    public void EmptyRequest_StaysUnscoped()
    {
        var context = new CollectionsClassificationContext();
        context.Set(null, "   ");
        context.RequireValid();
        Assert.False(context.HasValue);
        Assert.Null(context.Primary);
        Assert.Null(context.Sub);
    }

    [Theory]
    [InlineData("ACT", null)]
    [InlineData(null, "LOAN")]
    public void HalfSuppliedPair_IsRejected(string? primary, string? sub)
    {
        var context = new CollectionsClassificationContext();
        context.Set(primary, sub);
        Assert.Throws<HrValidationException>(context.RequireValid);
        Assert.False(context.HasValue);
    }

    [Fact]
    public void UnsupportedCombination_IsRejected()
    {
        var context = new CollectionsClassificationContext();
        context.Set("CORP", "AUTO");
        Assert.Throws<HrValidationException>(context.RequireValid);
    }

    [Fact]
    public void SuppliedPair_IsNormalized()
    {
        var context = new CollectionsClassificationContext();
        context.Set("w.o", "loan");
        context.RequireValid();
        Assert.True(context.HasValue);
        Assert.Equal("WO", context.Primary);
        Assert.Equal("LOAN", context.Sub);
    }
}
