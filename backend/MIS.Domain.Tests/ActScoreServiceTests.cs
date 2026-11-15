using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using MIS.Application.Common;
using MIS.Application.Interfaces;
using MIS.Application.Scoring;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class ActScoreServiceTests
{
    [Theory]
    [InlineData("BANK")]
    [InlineData("CONSUMER_FINANCE")]
    public async Task Reads_compute_the_exact_scope_without_writes_and_discover_source_periods(string organizationType)
    {
        await using var f = await Fixture.Create(organizationType: organizationType);
        var result = await f.Service().GetDashboardAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", default);
        Assert.Equal("CALCULATED", result.CalculationStatus);
        Assert.InRange(result.Summary.ScoreAll!.Value, 63.022483m, 63.022485m);
        Assert.Equal(6, result.Buckets.Count);
        var summary = await f.Service().GetSummaryAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", default);
        Assert.Equal(result.Summary, summary);
        var periods = await f.Service().GetPeriodsAsync(f.Org.Id, "ACT", f.Portfolio.Id, default);
        Assert.Contains("2026-10", periods.Periods);
        var otherPeriod = await f.Service().GetDashboardAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-09", default);
        Assert.Equal("UNAVAILABLE", otherPeriod.CalculationStatus); Assert.Null(otherPeriod.Summary.Paid);
        Assert.Contains("SOURCE_MISSING", otherPeriod.CalculationIssues!);
        Assert.Empty(f.Db.ChangeTracker.Entries());
        Assert.Empty(await f.Db.CollectionScoreValues.ToArrayAsync());
        Assert.Empty(await f.Db.CollectionScoreAudits.ToArrayAsync());
    }

    [Fact]
    public async Task One_historical_manual_row_prevents_any_automatic_replacement_or_mixed_summary()
    {
        await using var f = await Fixture.Create();
        var manual = new CollectionScoreValue(f.Org.Id, f.Portfolio.Id, "BANK", "ACT", new(2026,10,1), "BKT1", "NORM", ActScoreEngineTests.Cutoff, ActScoreEngineTests.Actor);
        manual.Update(7, 123, 10, 12, 111, 20, 3, 2, 6, ActScoreEngineTests.Cutoff, ActScoreEngineTests.Actor);
        f.Db.Add(manual); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var result = await f.Service().GetDashboardAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", default);
        Assert.Equal("MANUAL", result.CalculationStatus);
        Assert.Equal(123m, result.Buckets.First().Categories.Single(c => c.Category == "NORM").ActualBalance);
        Assert.Null(result.Buckets.First().Categories.Single(c => c.Category == "STAB").ActualBalance);
        Assert.Null(result.Summary.ScoreAll); Assert.Null(result.Summary.Paid);
        Assert.Empty(f.Db.ChangeTracker.Entries());
        Assert.Equal(6m, (await f.Db.CollectionScoreValues.SingleAsync()).Score);
        Assert.Empty(await f.Db.CollectionScoreAudits.ToArrayAsync());
    }

    [Fact]
    public async Task Feature_is_disabled_by_default_and_WO_is_never_calculated_as_ACT()
    {
        await using var f = await Fixture.Create();
        var disabled = new CollectionScoreService(f.Db, new Principal());
        var result = await disabled.GetDashboardAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", default);
        Assert.Contains("ACT_NOT_ENABLED", result.CalculationIssues!); Assert.Null(result.Summary.ScoreAll);
        Assert.DoesNotContain("2026-10", (await disabled.GetPeriodsAsync(f.Org.Id, "ACT", f.Portfolio.Id, default)).Periods.Where(p => p != DateTime.UtcNow.ToString("yyyy-MM")));
        var wo = new CollectionPortfolio(f.Org.Id, "WO-TEST", "Test", "Test", "EGP", ActScoreEngineTests.Cutoff, "WO", "LOAN");
        f.Db.Add(wo); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        result = await f.Service().GetDashboardAsync(f.Org.Id, "WO", wo.Id, "2026-10", default);
        Assert.Contains("MODEL_NOT_APPROVED", result.CalculationIssues!);
        Assert.Null(result.Summary.ScoreAll);
    }

    [Fact]
    public async Task Another_portfolio_cannot_reuse_the_snapshot_and_authorization_still_applies()
    {
        await using var f = await Fixture.Create();
        var other = new CollectionPortfolio(f.Org.Id, "OTHER", "Other", "Other", "EGP", ActScoreEngineTests.Cutoff, "ACT", "LOAN");
        f.Db.Add(other); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var result = await f.Service().GetDashboardAsync(f.Org.Id, "ACT", other.Id, "2026-10", default);
        Assert.Contains("SOURCE_MISSING", result.CalculationIssues!);
        Assert.DoesNotContain("2026-10", (await f.Service().GetPeriodsAsync(f.Org.Id, "ACT", other.Id, default)).Periods.Where(p => p != DateTime.UtcNow.ToString("yyyy-MM")));
        await Assert.ThrowsAsync<HrNotFoundException>(() => new CollectionScoreService(f.Db, new Principal(false))
            .GetDashboardAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", default));
    }

    [Fact]
    public async Task Malformed_or_missing_numeric_source_fields_are_not_silently_zero()
    {
        await using var f = await Fixture.Create(addSource: false);
        f.Db.Add(new ActScorePeriod(f.Org.Id, f.Portfolio.Id, new(2026,10,1), "EGP", "{}", "{}", ActScoreEngineTests.Actor, ActScoreEngineTests.Cutoff));
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var result = await f.Service().GetDashboardAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", default);
        Assert.Contains("SOURCE_INVALID", result.CalculationIssues!); Assert.Null(result.Summary.ScoreAll);
    }

    [Fact]
    public async Task Manual_save_endpoint_cannot_overwrite_a_calculated_month()
    {
        await using var f = await Fixture.Create();
        var rows = ActScoreEngine.Movements(1).Select(c => new MIS.Application.DTOs.Collections.SaveCollectionScoreCellRequest(c,
            null,null,null,null,null,null,null,null,null)).ToArray();
        await Assert.ThrowsAsync<HrConflictException>(() => f.Service().SaveBucketAsync(f.Org.Id, "ACT", f.Portfolio.Id, "BKT1",
            new("2026-10", "read-only-calculated", rows), default));
        Assert.Empty(await f.Db.CollectionScoreValues.ToArrayAsync());
        Assert.Empty(await f.Db.CollectionScoreAudits.ToArrayAsync());
    }

    private sealed class Principal(bool admin = true) : ICurrentUserContext
    {
        public Guid UserId => ActScoreEngineTests.Actor;
        public string Username => "unit-test";
        public IReadOnlyCollection<string> Roles => admin ? ["Admin"] : [];
        public IReadOnlyCollection<string> Permissions => admin ? ["*"] : [];
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"act-score-unit-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        public ClientOrganization Org { get; private set; } = null!;
        public CollectionPortfolio Portfolio { get; private set; } = null!;
        public CollectionScoreService Service() => new(Db, new Principal(), Options.Create(new ActScoringOptions { Enabled = true }));
        public static async Task<Fixture> Create(bool addSource = true, string organizationType = "BANK")
        {
            var f = new Fixture();
            f.Org = new("TEST", "Test", "Test", organizationType, ActScoreEngineTests.Cutoff);
            f.Portfolio = new(f.Org.Id, "ACT-TEST", "Test", "Test", "EGP", ActScoreEngineTests.Cutoff, "ACT", "LOAN");
            f.Db.AddRange(f.Org, f.Portfolio);
            if (addSource)
            {
                var (rules, input) = ActScoreEngineTests.Example();
                var scope = rules.Scope with { OrganizationId = f.Org.Id, PortfolioId = f.Portfolio.Id };
                rules = rules with { Scope = scope };
                input = input with { Scope = scope, Cases = input.Cases.Select(c => c with { Scope = scope }).ToArray(), Payments = input.Payments.Select(p => p with { Scope = scope }).ToArray() };
                var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                f.Db.Add(new ActScorePeriod(f.Org.Id, f.Portfolio.Id, scope.Period, scope.Currency,
                    JsonSerializer.Serialize(rules, options), JsonSerializer.Serialize(input, options), ActScoreEngineTests.Actor, ActScoreEngineTests.Cutoff));
            }
            await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear(); return f;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
