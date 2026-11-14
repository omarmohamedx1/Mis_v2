using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class BankPortfolioImportHistoryTests
{
    [Fact]
    public async Task Empty_desk_returns_an_empty_page_without_creating_a_portfolio()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Service().GetHistoryAsync(fixture.Bank.Id, 1, 10, null, default);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Empty(await fixture.Db.CollectionPortfolios.ToArrayAsync());
        Assert.Equal(0, fixture.Files.SaveCount);
    }

    [Fact]
    public async Task History_is_scoped_to_the_bank_active_classification_and_completed_unarchived_imports()
    {
        await using var fixture = await Fixture.CreateAsync();
        var selected = fixture.Portfolio(fixture.Bank.Id, "WO", "LOAN");
        var act = fixture.Portfolio(fixture.Bank.Id, "ACT", "LOAN");
        var visa = fixture.Portfolio(fixture.Bank.Id, "WO", "VISA");
        var otherBank = new ClientOrganization("OTHER", "جهة أخرى", "Other bank", "BANK", fixture.Now);
        fixture.Db.Add(otherBank);
        var otherPortfolio = fixture.Portfolio(otherBank.Id, "WO", "LOAN");
        var expected = fixture.Import(fixture.Bank.Id, selected.Id, "selected.csv");
        fixture.Import(fixture.Bank.Id, act.Id, "act.csv");
        fixture.Import(fixture.Bank.Id, visa.Id, "visa.csv");
        fixture.Import(otherBank.Id, otherPortfolio.Id, "other-bank.csv");
        fixture.Import(fixture.Bank.Id, selected.Id, "ready.csv", completed: false);
        fixture.Import(fixture.Bank.Id, selected.Id, "archived.csv").Archive("TEST", null, fixture.User.Id, fixture.Now);
        var legacy = fixture.Import(fixture.Bank.Id, null, "legacy.csv");
        var legacyOther = fixture.Import(fixture.Bank.Id, null, "legacy-act.csv");
        fixture.LinkCase(selected.Id, legacy.Id);
        fixture.LinkCase(act.Id, legacyOther.Id);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service().GetHistoryAsync(fixture.Bank.Id, 1, 10, null, default);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(new[] { expected.Id, legacy.Id }.Order(), result.Items.Select(item => item.Id).Order());
        Assert.All(result.Items, item => Assert.Equal(fixture.Bank.Id, item.BankId));
    }

    [Fact]
    public async Task Inactive_desk_returns_an_empty_history_and_is_not_reactivated()
    {
        await using var fixture = await Fixture.CreateAsync();
        var portfolio = fixture.Portfolio(fixture.Bank.Id, "WO", "LOAN");
        fixture.Import(fixture.Bank.Id, portfolio.Id, "old.csv");
        portfolio.Update(portfolio.NameArabic, portfolio.NameEnglish, "EGP", null, null, false);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service().GetHistoryAsync(fixture.Bank.Id, 1, 10, null, default);

        Assert.Empty(result.Items);
        Assert.False(portfolio.IsActive);
        Assert.Equal(1, await fixture.Db.CollectionPortfolios.CountAsync());
    }

    [Fact]
    public async Task First_upload_creates_the_classified_portfolio_without_duplicating_the_bank()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var content = Workbook();

        var uploaded = await fixture.Service().UploadAsync(fixture.Bank.Id, "first.csv", "text/csv", content.Length, content, default);

        var portfolio = Assert.Single(await fixture.Db.CollectionPortfolios.ToArrayAsync());
        Assert.Equal(fixture.Bank.Id, portfolio.OrganizationId);
        Assert.Equal("WO", portfolio.PrimaryClassification);
        Assert.Equal("LOAN", portfolio.SubClassification);
        Assert.Equal(portfolio.Id, uploaded.PortfolioId);
        Assert.Equal("READY", uploaded.Status);
        Assert.Equal(1, await fixture.Db.CollectionClientOrganizations.CountAsync());
        Assert.Empty(await fixture.Db.CollectionCases.ToArrayAsync());
        Assert.Equal(1, fixture.Files.SaveCount);
    }

    [Fact]
    public async Task Upload_reuses_an_existing_active_classified_portfolio()
    {
        await using var fixture = await Fixture.CreateAsync();
        var portfolio = fixture.Portfolio(fixture.Bank.Id, "WO", "LOAN");
        await fixture.Db.SaveChangesAsync();
        using var content = Workbook();

        var uploaded = await fixture.Service().UploadAsync(fixture.Bank.Id, "existing.csv", "text/csv", content.Length, content, default);

        Assert.Equal(portfolio.Id, uploaded.PortfolioId);
        Assert.Equal(1, await fixture.Db.CollectionPortfolios.CountAsync());
    }

    [Fact]
    public async Task Upload_rejects_an_inactive_portfolio_without_writing_a_file_or_reactivating_it()
    {
        await using var fixture = await Fixture.CreateAsync();
        var portfolio = fixture.Portfolio(fixture.Bank.Id, "WO", "LOAN");
        portfolio.Update(portfolio.NameArabic, portfolio.NameEnglish, "EGP", null, null, false);
        await fixture.Db.SaveChangesAsync();
        using var content = Workbook();

        await Assert.ThrowsAsync<HrValidationException>(() => fixture.Service().UploadAsync(fixture.Bank.Id, "inactive.csv", "text/csv", content.Length, content, default));

        Assert.False(portfolio.IsActive);
        Assert.Equal(0, fixture.Files.SaveCount);
        Assert.Empty(await fixture.Db.BankPortfolioImports.ToArrayAsync());
    }

    [Fact]
    public async Task Missing_banks_and_missing_permissions_are_not_hidden_as_empty_history()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<HrNotFoundException>(() => fixture.Service().GetHistoryAsync(Guid.NewGuid(), 1, 10, null, default));
        await Assert.ThrowsAsync<HrForbiddenException>(() => fixture.Service(authorized: false).GetHistoryAsync(fixture.Bank.Id, 1, 10, null, default));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Invalid_pagination_is_rejected_even_on_an_empty_desk(int page, int pageSize)
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<HrValidationException>(() => fixture.Service().GetHistoryAsync(fixture.Bank.Id, page, pageSize, null, default));
    }

    [Fact]
    public async Task Search_and_pagination_apply_after_classification_filtering()
    {
        await using var fixture = await Fixture.CreateAsync();
        var portfolio = fixture.Portfolio(fixture.Bank.Id, "WO", "LOAN");
        fixture.Import(fixture.Bank.Id, portfolio.Id, "november-first.csv");
        fixture.Import(fixture.Bank.Id, portfolio.Id, "november-second.csv");
        fixture.Import(fixture.Bank.Id, portfolio.Id, "october.csv");
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service().GetHistoryAsync(fixture.Bank.Id, 2, 1, "november", default);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.StartsWith("november", Assert.Single(result.Items).OriginalFileName);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("WO", null)]
    [InlineData("CORP", "LOAN")]
    public async Task Missing_or_invalid_classifications_do_not_return_an_unscoped_history(string? primary, string? sub)
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<HrValidationException>(() => fixture.Service(primary: primary, sub: sub).GetHistoryAsync(fixture.Bank.Id, 1, 10, null, default));
        Assert.Empty(await fixture.Db.CollectionPortfolios.ToArrayAsync());
    }

    private static MemoryStream Workbook() => new(Encoding.UTF8.GetBytes("Account No,Customer Name,Current Balance,BKT\r\n0000123,Test customer,100,BKT 1\r\n"));

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"portfolio-history-{Guid.NewGuid():N}").Options);
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public ClientOrganization Bank { get; }
        public User User { get; }
        public MemoryFiles Files { get; } = new();

        private Fixture()
        {
            var department = new Department("Collections", "COLLECTIONS", Now);
            User = new User("test", "test@example.invalid", "test-hash", "Test uploader", department.Id, Now);
            Bank = new ClientOrganization("TEST", "بنك اختبار", "Test bank", "BANK", Now);
            Db.AddRange(department, User, Bank);
        }

        public static async Task<Fixture> CreateAsync() { var fixture = new Fixture(); await fixture.Db.SaveChangesAsync(); return fixture; }
        public BankPortfolioImportService Service(bool authorized = true, string? primary = "WO", string? sub = "LOAN")
        {
            var classification = new CollectionsClassificationContext(); classification.Set(primary, sub);
            return new BankPortfolioImportService(Db, new Principal(User.Id, authorized), Files, NullLogger<BankPortfolioImportService>.Instance, classification);
        }
        public CollectionPortfolio Portfolio(Guid bankId, string primary, string sub)
        {
            var portfolio = new CollectionPortfolio(bankId, "DEFAULT", "محفظة اختبار", "Test portfolio", "EGP", Now, primary, sub);
            Db.Add(portfolio); return portfolio;
        }
        public BankPortfolioImport Import(Guid bankId, Guid? portfolioId, string name, bool completed = true)
        {
            var item = new BankPortfolioImport(bankId, portfolioId, "Test portfolio", name, "text/csv", 100, Guid.NewGuid().ToString("N"), name, 1, User.Id, Now);
            if (completed) item.Complete(Now, 1, 0, 0, 0);
            Db.Add(item); return item;
        }
        public void LinkCase(Guid portfolioId, Guid importId)
        {
            var customer = new CollectionCustomer(Bank.Id, Guid.NewGuid().ToString("N"), "عميل اختبار", "Test customer", Now);
            var item = new CollectionCase(portfolioId, customer.Id, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), 100, 100, 100, 1, Guid.NewGuid(), Now);
            item.LinkImport(importId); Db.AddRange(customer, item);
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class Principal(Guid id, bool authorized) : ICurrentUserContext
    {
        public Guid UserId => id;
        public string Username => "test";
        public IReadOnlyCollection<string> Roles => authorized ? ["Admin"] : [];
        public IReadOnlyCollection<string> Permissions => [];
    }

    private sealed class MemoryFiles : IHrFileStorage
    {
        private readonly Dictionary<string, byte[]> _contents = new();
        public int SaveCount { get; private set; }
        public async Task<StoredFileDto> SaveAsync(string scope, string originalFileName, string contentType, Stream content, long maximumBytes, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray(); var key = Guid.NewGuid().ToString("N"); _contents[key] = bytes; SaveCount++;
            return new StoredFileDto(key, originalFileName, contentType, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        }
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream(_contents[storageKey], writable: false));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) { _contents.Remove(storageKey); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult(_contents.ContainsKey(storageKey));
    }
}
