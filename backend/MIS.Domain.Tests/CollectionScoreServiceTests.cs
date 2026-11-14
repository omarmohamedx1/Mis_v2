using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class CollectionScoreServiceTests
{
    [Fact]
    public async Task Unsaved_settings_use_all_screenshot_targets_and_weights_without_creating_database_rows()
    {
        await using var fixture = new Fixture();
        await fixture.Db.SaveChangesAsync();
        var dashboard = await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default);
        var expected = new (string Bucket, decimal Stab, decimal? BwdTarget, decimal NormTarget, decimal FwdTarget, decimal? BwdWeight, decimal? NormWeight, decimal FwdWeight)[]
        {
            ("BKT1", 45, null, 10, 45, null, null, 10),
            ("BKT2", 30, 20, 10, 40, 9, 4, 17),
            ("BKT3", 20, 15, 10, 55, 9, 3, 16),
            ("BKT4", 5, 10, 5, 80, 7, 3, 6),
            ("BKT5", -5, 10, 5, 90, 7, 3, 6),
        };
        foreach (var row in expected)
        {
            var bucket = dashboard.Buckets.Single(item => item.Code == row.Bucket);
            var cells = bucket.Categories.ToDictionary(item => item.Category);
            Assert.Equal(row.Stab, cells["STAB"].TargetPercent);
            Assert.Null(cells["STAB"].Weight);
            Assert.Equal(row.NormTarget, cells["NORM"].TargetPercent);
            Assert.Equal(row.NormWeight, cells["NORM"].Weight);
            Assert.Equal(row.FwdTarget, cells["FWD"].TargetPercent);
            Assert.Equal(row.FwdWeight, cells["FWD"].Weight);
            if (row.BwdTarget.HasValue)
            {
                Assert.Equal(row.BwdTarget, cells["BWD"].TargetPercent);
                Assert.Equal(row.BwdWeight, cells["BWD"].Weight);
            }
            Assert.DoesNotContain("MISSING_TARGETS", bucket.Issues!);
            Assert.DoesNotContain("MISSING_WEIGHTS", bucket.Issues!);
            Assert.All(cells.Values, cell => { Assert.Null(cell.CaseCount); Assert.Null(cell.ActualBalance); Assert.Null(cell.Score); });
        }
        Assert.Null(dashboard.Buckets.Single(item => item.Code == "BKT6").Categories.Single().TargetPercent);
        Assert.Empty(await fixture.Db.CollectionScoreValues.ToArrayAsync());
    }

    [Fact]
    public async Task Edited_targets_and_zero_weights_survive_reload_and_only_override_the_selected_scope()
    {
        await using var fixture = new Fixture();
        var otherPortfolio = fixture.Portfolio(fixture.Bank.Id, "WO");
        var otherBank = new ClientOrganization("OTHER", "Other", "Other", "BANK", fixture.Now);
        fixture.Db.Add(otherBank);
        var otherBankPortfolio = fixture.Portfolio(otherBank.Id, "ACT");
        await fixture.Db.SaveChangesAsync();
        var original = (await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default))
            .Buckets.Single(item => item.Code == "BKT2");
        SaveCollectionScoreCellRequest Cell(string category, decimal? target, decimal? weight) =>
            new(category, null, null, target, null, null, null, weight, null, null);
        await fixture.Service.SaveBucketAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "BKT2", new("2026-11", original.Version,
            [Cell("STAB", null, null), Cell("BWD", 25, 0), Cell("NORM", 15, 8), Cell("FWD", 50, 19)]), default);
        fixture.Db.ChangeTracker.Clear();
        var saved = (await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default))
            .Buckets.Single(item => item.Code == "BKT2");
        Assert.Equal(10m, saved.Categories.Single(item => item.Category == "STAB").TargetPercent);
        Assert.Equal(25m, saved.Categories.Single(item => item.Category == "BWD").TargetPercent);
        Assert.Equal(0m, saved.Categories.Single(item => item.Category == "BWD").Weight);
        Assert.Equal(8m, saved.Categories.Single(item => item.Category == "NORM").Weight);
        Assert.Equal(50m, saved.Categories.Single(item => item.Category == "FWD").TargetPercent);
        Assert.Equal(19m, saved.Categories.Single(item => item.Category == "FWD").Weight);
        foreach (var scope in new[] { (fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-12"),
                     (fixture.Bank.Id, "WO", otherPortfolio.Id, "2026-11"), (otherBank.Id, "ACT", otherBankPortfolio.Id, "2026-11") })
        {
            var dashboard = await fixture.Service.GetDashboardAsync(scope.Item1, scope.Item2, scope.Item3, scope.Item4, default);
            var bwd = dashboard.Buckets.Single(item => item.Code == "BKT2").Categories.Single(item => item.Category == "BWD");
            Assert.Equal(20m, bwd.TargetPercent); Assert.Equal(9m, bwd.Weight);
        }
    }

    [Fact]
    public async Task Empty_saved_settings_use_screenshot_defaults_instead_of_hiding_them()
    {
        await using var fixture = new Fixture();
        await fixture.Db.SaveChangesAsync();
        var bucket = (await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default))
            .Buckets.Single(item => item.Code == "BKT5");
        await fixture.Service.SaveBucketAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "BKT5", new("2026-11", bucket.Version,
            bucket.Categories.Select(item => new SaveCollectionScoreCellRequest(item.Category, null, null, null, null, null, null, null, null, null)).ToArray()), default);
        fixture.Db.ChangeTracker.Clear();
        var saved = (await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default))
            .Buckets.Single(item => item.Code == "BKT5");
        var cells = saved.Categories.ToDictionary(item => item.Category);
        Assert.Equal(-5m, cells["STAB"].TargetPercent);
        Assert.Equal(10m, cells["BWD"].TargetPercent); Assert.Equal(7m, cells["BWD"].Weight);
        Assert.Equal(5m, cells["NORM"].TargetPercent); Assert.Equal(3m, cells["NORM"].Weight);
        Assert.Equal(90m, cells["FWD"].TargetPercent); Assert.Equal(6m, cells["FWD"].Weight);
        Assert.DoesNotContain("MISSING_TARGETS", saved.Issues!);
        Assert.DoesNotContain("MISSING_WEIGHTS", saved.Issues!);
    }

    [Fact]
    public async Task Legacy_blank_bkt1_rows_show_targets_and_weight_even_without_period_data()
    {
        await using var fixture = new Fixture();
        foreach (var category in new[] { "STAB", "NORM", "FWD" })
            fixture.Db.CollectionScoreValues.Add(new CollectionScoreValue(fixture.Bank.Id, fixture.Main.Id, "BANK", "ACT",
                new DateOnly(2026, 11, 1), "BKT1", category, fixture.Now, fixture.Actor));
        await fixture.Db.SaveChangesAsync();
        var bucket = (await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default))
            .Buckets.Single(item => item.Code == "BKT1");
        var cells = bucket.Categories.ToDictionary(item => item.Category);
        Assert.Equal(45m, cells["STAB"].TargetPercent);
        Assert.Equal(10m, cells["NORM"].TargetPercent);
        Assert.Equal(45m, cells["FWD"].TargetPercent);
        Assert.Equal(10m, cells["FWD"].Weight);
        Assert.DoesNotContain("MISSING_TARGETS", bucket.Issues!);
        Assert.DoesNotContain("MISSING_WEIGHTS", bucket.Issues!);
        Assert.Contains("MISSING_PERIOD_SOURCE", bucket.Issues!);
        Assert.Null(bucket.Score);
        Assert.All(await fixture.Db.CollectionScoreValues.ToArrayAsync(), row => { Assert.Null(row.TargetPercent); Assert.Null(row.Weight); });
    }

    [Fact]
    public async Task Partially_blank_saved_settings_fill_only_missing_fields_and_keep_explicit_zeroes()
    {
        await using var fixture = new Fixture();
        foreach (var item in new (string Category, decimal? Target, decimal? Weight)[] { ("BWD", null, 12), ("NORM", 0, 0), ("FWD", 55, null) })
        {
            var row = new CollectionScoreValue(fixture.Bank.Id, fixture.Main.Id, "BANK", "ACT", new DateOnly(2026, 11, 1), "BKT2", item.Category, fixture.Now, fixture.Actor);
            row.Update(null, null, item.Target, null, null, null, item.Weight, null, null, fixture.Now, fixture.Actor);
            fixture.Db.Add(row);
        }
        await fixture.Db.SaveChangesAsync();
        var cells = (await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default))
            .Buckets.Single(item => item.Code == "BKT2").Categories.ToDictionary(item => item.Category);
        Assert.Equal(20m, cells["BWD"].TargetPercent); Assert.Equal(12m, cells["BWD"].Weight);
        Assert.Equal(0m, cells["NORM"].TargetPercent); Assert.Equal(0m, cells["NORM"].Weight);
        Assert.Equal(55m, cells["FWD"].TargetPercent); Assert.Equal(17m, cells["FWD"].Weight);
        Assert.Equal(25m, cells["STAB"].TargetPercent);
    }

    [Fact]
    public async Task System_payments_are_month_scoped_approved_non_reversed_and_never_added_to_imported_payments()
    {
        await using var fixture = new Fixture();
        var item = fixture.Case(fixture.Main, fixture.Import.Id, 99999);
        CollectionPayment Payment(decimal amount, DateOnly date, bool approve = true, bool reverse = false)
        {
            var payment = new CollectionPayment(item.Id, amount, date, "CASH", Guid.NewGuid().ToString("N"), fixture.Actor, null, fixture.Now);
            if (approve) payment.Review(fixture.Actor, true, null, false, fixture.Now);
            if (reverse) { payment.MarkFinanciallyPosted(Guid.NewGuid()); payment.MarkFinanciallyReversed(Guid.NewGuid()); }
            fixture.Db.Add(payment); return payment;
        }
        Payment(2, new(2026, 11, 1)); Payment(3, new(2026, 11, 30));
        Payment(1000, new(2026, 10, 31)); Payment(1000, new(2026, 12, 1));
        Payment(1000, new(2026, 11, 10), approve: false); Payment(1000, new(2026, 11, 10), reverse: true);
        await fixture.Db.SaveChangesAsync();
        await fixture.Bind("2026-11", "SYSTEM");
        var dashboard = await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default);
        Assert.Equal(5m, dashboard.Summary.Paid);
        var bucket = dashboard.Buckets.Single(row => row.Code == "BKT2");
        Assert.Equal(1, bucket.CaseCount);
        Assert.Equal(1, bucket.Categories.Single(row => row.Category == "STAB").CaseCount);
        Assert.Equal(0, bucket.Categories.Single(row => row.Category == "NORM").CaseCount);
    }

    [Fact]
    public async Task Counts_are_scoped_to_bank_portfolio_classification_and_non_archived_cases()
    {
        await using var fixture = new Fixture();
        var secondBank = new ClientOrganization("SECOND", "Second", "Second", "BANK", fixture.Now);
        var otherBankPortfolio = fixture.Portfolio(secondBank.Id, "ACT");
        var otherType = fixture.Portfolio(fixture.Bank.Id, "WO");
        var otherPortfolio = fixture.Portfolio(fixture.Bank.Id, "ACT");
        fixture.Db.Add(secondBank);
        fixture.Case(fixture.Main, fixture.Import.Id);
        var archived = fixture.Case(fixture.Main, fixture.Import.Id);
        archived.Archive("OTHER", null, fixture.Actor, fixture.Now);
        fixture.Case(otherBankPortfolio, Guid.NewGuid());
        fixture.Case(otherType, Guid.NewGuid());
        fixture.Case(otherPortfolio, Guid.NewGuid());
        await fixture.Db.SaveChangesAsync();
        var dashboard = await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default);
        Assert.Equal(1, dashboard.Summary.SystemCaseCount);
        Assert.Equal(1, dashboard.Buckets.Single(item => item.Code == "BKT2").SystemCaseCount);
        Assert.Null(dashboard.Buckets.Single(item => item.Code == "BKT2").CaseCount);
        await Assert.ThrowsAsync<HrNotFoundException>(() => fixture.Service.GetDashboardAsync(fixture.Bank.Id, "WO", fixture.Main.Id, "2026-11", default));
        await Assert.ThrowsAsync<HrNotFoundException>(() => fixture.Service.GetDashboardAsync(secondBank.Id, "ACT", fixture.Main.Id, "2026-11", default));
    }

    [Fact]
    public async Task Bound_month_counts_system_cases_ignores_forged_totals_and_detects_changed_opening_data()
    {
        await using var fixture = new Fixture();
        var first = fixture.Case(fixture.Main, fixture.Import.Id, 0);
        fixture.Case(fixture.Main, fixture.Import.Id, 16);
        await fixture.Db.SaveChangesAsync();
        await fixture.Bind("2026-11");
        var dashboard = await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default);
        var bucket = dashboard.Buckets.Single(item => item.Code == "BKT2");
        Assert.Equal(2, bucket.CaseCount);
        Assert.Equal(1, bucket.Categories.Single(item => item.Category == "FWD").CaseCount);
        Assert.Equal(1, bucket.Categories.Single(item => item.Category == "NORM").CaseCount);
        Assert.Equal(200m, bucket.ActualBalance);
        SaveCollectionScoreCellRequest Cell(string category, decimal? target, decimal? weight) =>
            new(category, 9999, 9999, target, 9999, 9999, 99, weight, 9999, 9999);
        var saved = await fixture.Service.SaveBucketAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "BKT2",
            new("2026-11", bucket.Version, [Cell("STAB", 30, null), Cell("BWD", 20, 9), Cell("NORM", 10, 4), Cell("FWD", 40, 17)]), default);
        Assert.Equal(2, saved.CaseCount);
        Assert.Equal(200m, saved.ActualBalance);
        Assert.All(await fixture.Db.CollectionScoreValues.ToArrayAsync(), item => { Assert.Null(item.CaseCount); Assert.Null(item.Score); Assert.Null(item.ActualBalance); });
        var previous = await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-10", default);
        Assert.Null(previous.Buckets.Single(item => item.Code == "BKT2").CaseCount);
        Assert.Equal(1, (await fixture.Service.GetPeriodsAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, default)).Periods.Count(item => item == "2026-11"));
        first.ApplyImportedProfile(null, null, null, null, null, null, null, null, null, null, "{}", fixture.Now);
        await fixture.Db.SaveChangesAsync();
        var preserved = await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default);
        Assert.Equal(2, preserved.Buckets.Single(item => item.Code == "BKT2").CaseCount);
        Assert.Equal(200m, preserved.Buckets.Single(item => item.Code == "BKT2").ActualBalance);
        fixture.Files.Rows[0]["Current Balance"] = "9999";
        var changed = await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default);
        Assert.Null(changed.Buckets.Single(item => item.Code == "BKT2").CaseCount);
        Assert.Contains("MISSING_PERIOD_SOURCE", changed.Buckets.Single(item => item.Code == "BKT2").Issues!);
    }

    [Fact]
    public async Task Updated_cases_are_matched_by_account_even_when_their_source_import_link_is_older()
    {
        await using var fixture = new Fixture();
        var item = fixture.Case(fixture.Main, Guid.NewGuid(), 16);
        item.ApplyImportedBalances(1, 1, 40, item.CurrentBucketId, fixture.Now);
        await fixture.Db.SaveChangesAsync();
        await fixture.Bind("2026-11");
        var dashboard = await fixture.Service.GetDashboardAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default);
        var bucket = dashboard.Buckets.Single(row => row.Code == "BKT2");
        Assert.Equal(1, bucket.CaseCount);
        Assert.Equal(100m, bucket.ActualBalance);
        Assert.Equal(1, bucket.Categories.Single(row => row.Category == "NORM").CaseCount);
    }

    [Fact]
    public async Task Source_cannot_be_bound_to_an_import_from_another_portfolio_or_without_reconciliation()
    {
        await using var fixture = new Fixture();
        fixture.Case(fixture.Main, fixture.Import.Id);
        await fixture.Db.SaveChangesAsync();
        var source = await fixture.Service.GetSourceAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, "2026-11", default);
        var request = new SaveCollectionScoreSourceRequest("2026-11", source.Version, Guid.NewGuid(), true, true, "IMPORT", source.Fields);
        await Assert.ThrowsAsync<HrValidationException>(() => fixture.Service.SaveSourceAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, request, default));
        await Assert.ThrowsAsync<HrValidationException>(() => fixture.Service.SaveSourceAsync(fixture.Bank.Id, "ACT", fixture.Main.Id, request with { OpeningImportId = fixture.Import.Id, PaymentsReconciled = false }, default));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"score-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public Guid Actor { get; } = Guid.NewGuid();
        public ClientOrganization Bank { get; }
        public CollectionPortfolio Main { get; }
        public BankPortfolioImport Import { get; }
        public MemoryFiles Files { get; } = new();
        public CollectionScoreService Service => new(Db, new Principal(Actor), Files);
        public Fixture()
        {
            Bank = new("MAIN", "Main bank", "Main bank", "BANK", Now);
            Db.Add(Bank); Main = Portfolio(Bank.Id, "ACT");
            Import = new(Bank.Id, Main.Id, "Opening", "opening.csv", "text/csv", 100, "hash", "key", 2, Actor, Now);
            Import.Complete(Now, 2, 0, 0, 0); Db.Add(Import);
        }
        public CollectionPortfolio Portfolio(Guid bank, string classification)
        {
            var item = new CollectionPortfolio(bank, Guid.NewGuid().ToString("N"), "Portfolio", "Portfolio", "EGP", Now, classification, "LOAN");
            Db.Add(item); return item;
        }
        public CollectionCase Case(CollectionPortfolio portfolio, Guid import, decimal payment = 0)
        {
            var customer = new CollectionCustomer(portfolio.OrganizationId, Guid.NewGuid().ToString("N"), "Customer", "Customer", Now);
            var bucket = new DelinquencyBucketDefinition(portfolio.OrganizationId, portfolio.Id, "BKT2", "BKT2", "BKT2", 30, 59, 2, Now);
            var item = new CollectionCase(portfolio.Id, customer.Id, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), 100, 20, 20, 40, bucket.Id, Now);
            var fields = new Dictionary<string, string> { ["Account No"] = item.AccountReference, ["Current BKT"] = "BKT2", ["Current Balance"] = "100", ["StartingTotal Dues"] = "16",
                ["Payment"] = payment.ToString(System.Globalization.CultureInfo.InvariantCulture), ["BKT0"] = "1", ["BKT1"] = "5", ["BKT2"] = "5", ["BKT3"] = "5", ["BKT4"] = "0", ["BKT5"] = "0" };
            if (portfolio.Id == Main.Id) Files.Rows.Add(fields);
            var raw = JsonSerializer.Serialize(fields);
            item.ApplyImportedProfile(null, null, null, null, null, null, null, null, null, null, raw, Now);
            item.ApplyImportedDeskFields(null, null, "BKT2"); item.LinkImport(import);
            Db.AddRange(customer, bucket, item); return item;
        }
        public async Task Bind(string period, string paymentSource = "IMPORT")
        {
            var source = await Service.GetSourceAsync(Bank.Id, "ACT", Main.Id, period, default);
            await Service.SaveSourceAsync(Bank.Id, "ACT", Main.Id, new(period, source.Version, Import.Id, true, true, paymentSource, source.Fields), default);
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed class MemoryFiles : IHrFileStorage
    {
        public List<Dictionary<string, string>> Rows { get; } = [];
        public Task<Stream> OpenReadAsync(string key, CancellationToken token)
        {
            var csv = string.Join(',', Rows[0].Keys) + "\r\n" + string.Join("\r\n", Rows.Select(row => string.Join(',', row.Values))) + "\r\n";
            return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(csv)));
        }
        public Task<MIS.Application.DTOs.Hr.StoredFileDto> SaveAsync(string scope, string name, string contentType, Stream content, long maximum, CancellationToken token) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string key, CancellationToken token) => Task.FromResult(true);
    }
    private sealed record Principal(Guid UserId) : ICurrentUserContext
    {
        public string Username => "test";
        public IReadOnlyCollection<string> Roles => [SystemRoleNames.Admin];
        public IReadOnlyCollection<string> Permissions => ["*"];
    }
}
