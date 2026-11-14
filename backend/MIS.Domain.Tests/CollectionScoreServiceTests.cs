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
