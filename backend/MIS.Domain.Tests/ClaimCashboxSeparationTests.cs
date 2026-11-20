using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using MIS.Application.Common;
using MIS.Application.DTOs.Accounting;
using MIS.Application.DTOs.Finance;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Persistence.Configurations;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class ClaimCashboxSeparationTests
{
    [Fact]
    public async Task Partial_receipt_does_not_change_existing_revenue_expense_or_profit_loss_reports()
    {
        await using var f = new Fixture(); await f.Initialize();
        var revenue = new FinanceAccount(Guid.NewGuid(), FinanceSeed.LegalEntityId, "TEST-R", "Test", "Test", "REVENUE", "CREDIT", true);
        var expense = new FinanceAccount(Guid.NewGuid(), FinanceSeed.LegalEntityId, "TEST-E", "Test", "Test", "EXPENSE", "DEBIT", true);
        f.Db.AddRange(revenue, expense); await f.Db.SaveChangesAsync();
        await f.Book.CreateAsync(new("REVENUE", f.Date, 200, revenue.Id, f.Cash.Id, "Existing revenue", null), default);
        await f.Book.CreateAsync(new("OTHER", f.Date, 50, expense.Id, f.Cash.Id, "Existing expense", null), default);
        var before = await f.Book.SummaryAsync(f.Date, f.Date, default);
        var profitBefore = await f.Book.ProfitLossAsync(f.Date, f.Date, default);
        var claim = f.Claim(false); f.Db.Add(claim); await f.Db.SaveChangesAsync();
        await f.Claims.ConfirmReceiptAsync(claim.Id, new(f.Date, 40, Guid.NewGuid()), default);
        var after = await f.Book.SummaryAsync(f.Date, f.Date, default);
        var profitAfter = await f.Book.ProfitLossAsync(f.Date, f.Date, default);
        Assert.Equal(200, before.Revenue); Assert.Equal(50, before.Expenses);
        Assert.Equal(before.Revenue, after.Revenue); Assert.Equal(before.Expenses, after.Expenses);
        Assert.Equal(before.CompanyIncome, after.CompanyIncome);
        Assert.Equal(before.CashIn, after.CashIn); Assert.Equal(before.CashOut, after.CashOut);
        Assert.Equal(before.NetCashFlow, after.NetCashFlow);
        Assert.Equal(profitBefore.TotalRevenue, profitAfter.TotalRevenue);
        Assert.Equal(profitBefore.TotalExpenses, profitAfter.TotalExpenses);
        Assert.Equal(profitBefore.NetProfitLoss, profitAfter.NetProfitLoss);
    }

    [Fact]
    public void Partial_receipt_migration_only_replaces_claim_index_without_data_operations()
    {
        var migration = new MIS.Infrastructure.Persistence.Migrations.AllowPartialClaimReceipts();
        Assert.Equal(2, migration.UpOperations.Count);
        Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.DropIndexOperation>(migration.UpOperations[0]);
        var index = Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation>(migration.UpOperations[1]);
        Assert.Equal("claim_receipt_postings", index.Table);
        Assert.False(index.IsUnique);
        Assert.Equal(new[] { "ClaimId" }, index.Columns);
    }
    [Fact]
    public async Task Partial_payments_retries_and_individual_reversals_preserve_balances()
    {
        await using var f = new Fixture(); await f.Initialize();
        var claim = f.Claim(false); f.Db.Add(claim); await f.Db.SaveChangesAsync();
        var first = new ConfirmFinanceClaimReceiptRequest(f.Date, 40, Guid.NewGuid());
        var partial = await f.Claims.ConfirmReceiptAsync(claim.Id, first, default);
        Assert.Equal("PARTIAL", partial.Claim.CollectionStatus);
        Assert.Equal(40, partial.Claim.ReceivedAmount);
        Assert.Equal(72, partial.Claim.RemainingAmount);
        await f.Claims.ConfirmReceiptAsync(claim.Id, first, default);
        Assert.Single(await f.Db.FinanceClaimReceiptPostings.ToListAsync());
        await Assert.ThrowsAsync<HrConflictException>(() => f.Claims.ConfirmReceiptAsync(claim.Id, first with { Amount = 41 }, default));
        await Assert.ThrowsAsync<HrConflictException>(() => f.Claims.ConfirmReceiptAsync(claim.Id, new(f.Date, 73, Guid.NewGuid()), default));
        var second = new ConfirmFinanceClaimReceiptRequest(f.Date, 72, Guid.NewGuid());
        var complete = await f.Claims.ConfirmReceiptAsync(claim.Id, second, default);
        Assert.Equal("RECEIVED", complete.Claim.CollectionStatus);
        Assert.Equal(0, complete.Claim.RemainingAmount);
        await f.Claims.ConfirmReceiptAsync(claim.Id, second, default);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow,
            OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo").DateTime);
        if (today.Year != f.Date.Year || today.Month != f.Date.Month)
        {
            f.Db.Add(new AccountingPeriod(FinanceSeed.LegalEntityId, today.Year, today.Month, "Reversal",
                new(today.Year, today.Month, 1), new(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))));
            await f.Db.SaveChangesAsync();
        }
        var reverse = new ReverseFinanceClaimReceiptRequest("Test reversal", first.RequestId);
        var reopened = await f.Claims.ReverseReceiptAsync(claim.Id, reverse, default);
        Assert.Equal(72, reopened.Claim.ReceivedAmount);
        Assert.Equal(40, reopened.Claim.RemainingAmount);
        Assert.Equal("PARTIAL", reopened.Claim.CollectionStatus);
        await f.Claims.ReverseReceiptAsync(claim.Id, reverse, default);
        Assert.Equal(2, await f.Db.FinanceClaimReceiptPostings.CountAsync());
        Assert.Single(await f.Db.JournalEntries.Where(x => x.ReversalOfJournalId != null).ToListAsync());
        await Assert.ThrowsAsync<HrConflictException>(() => f.Claims.ConfirmReceiptAsync(claim.Id, first, default));
        var summary = await f.Book.SummaryAsync(f.Date, today, default);
        Assert.Equal(500, summary.CompanyIncome);
        Assert.Equal(500, summary.CashIn);
        Assert.Equal(0, summary.CashOut);
        Assert.Equal(0, summary.Revenue);
        var list = await f.Claims.ListAsync(new(), default);
        Assert.Equal(72, list.Summary.NetReceivedInBank);
        Assert.Equal(40, Assert.Single(list.Items).RemainingAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.001)]
    public async Task Invalid_payment_is_rejected_without_posting(decimal amount)
    {
        await using var f = new Fixture(); await f.Initialize();
        var claim = f.Claim(false); f.Db.Add(claim); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<HrValidationException>(() => f.Claims.ConfirmReceiptAsync(claim.Id, new(f.Date, amount, Guid.NewGuid()), default));
        Assert.Empty(await f.Db.FinanceClaimReceiptPostings.ToListAsync());
    }

    [Fact]
    public async Task Wrong_bank_missing_request_id_and_editing_paid_claim_are_rejected()
    {
        await using var f = new Fixture(); await f.Initialize();
        var claim = f.Claim(false); f.Db.Add(claim); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<HrValidationException>(() => f.Claims.ConfirmReceiptAsync(claim.Id, new(f.Date, 20, Guid.NewGuid(), Guid.NewGuid()), default));
        await Assert.ThrowsAsync<HrValidationException>(() => f.Claims.ConfirmReceiptAsync(claim.Id, new(f.Date, 20), default));
        await f.Claims.ConfirmReceiptAsync(claim.Id, new(f.Date, 20, Guid.NewGuid()), default);
        await Assert.ThrowsAsync<HrConflictException>(() => f.Claims.UpdateAsync(claim.Id,
            new("TEST", null, "Test bank", "Monthly", "2026-01", f.Date, 100, 14, 2), default));
        Assert.Null(Assert.Single(await f.Db.FinanceClaimReceiptPostings.ToListAsync()).ReversedAt);
    }

    [Fact]
    public async Task Historical_unposted_claim_is_displayed_without_creating_entries()
    {
        await using var f = new Fixture(); await f.Initialize();
        var claim = f.Claim(true); f.Db.Add(claim); await f.Db.SaveChangesAsync();
        var result = await f.Claims.DetailsAsync(claim.Id, default);
        Assert.Equal(112, result.Claim.ReceivedAmount);
        Assert.Equal(0, result.Claim.RemainingAmount);
        Assert.Empty(result.ReceiptPostings);
        Assert.Single(await f.Db.AccountingBookEntries.ToListAsync());
    }

    [Fact]
    public async Task Invalid_dates_and_failed_configuration_leave_existing_partial_receipts_unchanged()
    {
        await using var f = new Fixture(); await f.Initialize();
        var claim = f.Claim(false); f.Db.Add(claim); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<HrValidationException>(() => f.Claims.ConfirmReceiptAsync(claim.Id, new(f.Date.AddDays(-1), 20, Guid.NewGuid()), default));
        await f.Claims.ConfirmReceiptAsync(claim.Id, new(f.Date, 20, Guid.NewGuid()), default);
        var principal = new BookTestPrincipal(f.User.Id);
        var unconfigured = new FinanceClaimsService(f.Db, principal, new AccountingBookService(f.Db, principal, f.Finance), f.Finance);
        await Assert.ThrowsAsync<HrValidationException>(() => unconfigured.ConfirmReceiptAsync(claim.Id, new(f.Date, 30, Guid.NewGuid()), default));
        Assert.Equal(20, Assert.Single(await f.Db.FinanceClaimReceiptPostings.ToListAsync()).Amount);
        Assert.Equal(92, (await f.Claims.DetailsAsync(claim.Id, default)).Claim.RemainingAmount);
        Assert.Equal(2, await f.Db.AccountingBookEntries.CountAsync());
    }

    [Fact]
    public void Configuration_errors_distinguish_missing_inactive_and_incorrect_accounts()
    {
        var asset = new FinanceAccount(Guid.NewGuid(), FinanceSeed.LegalEntityId, "TEST-A", "Test", "Test", "ASSET", "DEBIT", true);
        var clearing = new FinanceAccount(Guid.NewGuid(), FinanceSeed.LegalEntityId, "TEST-L", "Test", "Test", "LIABILITY", "CREDIT", true);
        var options = new ClaimReceiptOptions { AssetAccountId = asset.Id, ClearingAccountId = clearing.Id };
        Assert.Contains("does not exist", Assert.Throws<HrValidationException>(() => ClaimReceiptOptions.Validate([asset], options)).Message);
        clearing.Update(clearing.Code, "Test", "Test", "LIABILITY", "CREDIT", true, false, null);
        Assert.Contains("active", Assert.Throws<HrValidationException>(() => ClaimReceiptOptions.Validate([asset, clearing], options)).Message);
        clearing.Update(clearing.Code, "Test", "Test", "REVENUE", "CREDIT", true, true, null);
        Assert.Throws<HrValidationException>(() => ClaimReceiptOptions.Validate([asset, clearing], options));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirming_receipt_preserves_operating_balances_and_records_claim_accounting(bool historical)
    {
        await using var f = new Fixture();
        await f.Initialize();
        var claim = f.Claim(historical);
        f.Db.Add(claim);
        await f.Db.SaveChangesAsync();
        var before = await f.Book.SummaryAsync(f.Date, f.Date, default);
        if (historical)
        {
            await f.Claims.PostHistoricalReceiptAsync(claim.Id, default);
            await f.Claims.PostHistoricalReceiptAsync(claim.Id, default);
        }
        else
        {
            await f.Claims.ConfirmReceiptAsync(claim.Id, new(f.Date), default);
            await f.Claims.ConfirmReceiptAsync(claim.Id, new(f.Date), default);
        }
        var after = await f.Book.SummaryAsync(f.Date, f.Date, default);
        Assert.Equal(before.CompanyIncome, after.CompanyIncome);
        Assert.Equal(before.CashIn, after.CashIn);
        Assert.Equal(before.CashOut, after.CashOut);
        Assert.Equal(before.NetCashFlow, after.NetCashFlow);
        Assert.Equal(before.Revenue, after.Revenue);
        Assert.Equal("RECEIVED", claim.Status);
        var posting = Assert.Single(await f.Db.FinanceClaimReceiptPostings.ToListAsync());
        Assert.Equal(claim.TotalInBank, posting.Amount);
        var entry = await f.Db.AccountingBookEntries.Include(x => x.JournalEntry).ThenInclude(x => x.Lines)
            .SingleAsync(x => x.Id == posting.AccountingBookEntryId);
        Assert.Equal("CLAIM_RECEIPT", entry.Category);
        Assert.Equal("CLAIM_RECEIPT", entry.JournalEntry.EntryType);
        Assert.Equal("POSTED", entry.JournalEntry.Status);
        Assert.Equal(claim.TotalInBank, entry.JournalEntry.TotalDebit);
        Assert.Equal(entry.JournalEntry.TotalDebit, entry.JournalEntry.TotalCredit);
        Assert.All(entry.JournalEntry.Lines, l => Assert.Contains(l.AccountId, new[] { f.Asset.Id, f.Clearing.Id }));
        Assert.DoesNotContain((await f.Book.ListAsync(new(), default)).Items, x => x.Id == entry.Id);
        var received = await f.Claims.ListAsync(new(Status: "RECEIVED"), default);
        Assert.Equal(1, received.TotalCount);
        Assert.Equal(claim.TotalInBank, received.Summary.NetReceivedInBank);
        var operatingBalance = await f.Db.JournalEntryLines.Where(x => x.AccountId == f.Cash.Id)
            .SumAsync(x => x.BaseDebit - x.BaseCredit);
        Assert.Equal(500m, operatingBalance);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow,
            OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo").DateTime);
        if (today.Year != f.Date.Year || today.Month != f.Date.Month)
        {
            f.Db.Add(new AccountingPeriod(FinanceSeed.LegalEntityId, today.Year, today.Month, "Reversal period",
                new(today.Year, today.Month, 1), new(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))));
            await f.Db.SaveChangesAsync();
        }
        await f.Claims.ReverseReceiptAsync(claim.Id, new("Synthetic test reversal"), default);
        var reversed = await f.Book.SummaryAsync(f.Date, today, default);
        Assert.Equal(500m, reversed.CompanyIncome);
        Assert.Equal(500m, reversed.CashIn);
        Assert.Equal(0m, reversed.CashOut);
        Assert.Equal("PENDING", claim.Status);
        Assert.NotNull(posting.ReversedAt);
        Assert.Null(entry.DeletedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Legacy_claim_income_is_excluded_by_source_or_relationship_without_modifying_history(bool sourceKey)
    {
        await using var f = new Fixture();
        await f.Initialize();
        var claim = f.Claim(false); f.Db.Add(claim);
        var journal = new JournalEntry(FinanceSeed.LegalEntityId, f.Period.Id, null, "COMPANY_INCOME", f.Date, f.Date, "EGP", "Legacy claim", f.User.Id, f.Now);
        journal.AddLine(f.Cash.Id, 112, 0, 112, 0, 1, "Legacy claim");
        journal.AddLine(f.Income.Id, 0, 112, 0, 112, 1, "Legacy claim");
        journal.RecordConfirmedAccounting("LEGACY", f.User.Id, f.Now);
        var entry = new AccountingBookEntry(Guid.NewGuid(), "INCOME", f.Date, 112, f.Income.Id, f.Cash.Id, "Legacy claim", null,
            journal.Id, f.User.Id, f.Now, sourceKey: sourceKey ? $"CLAIM:{claim.Id}:{Guid.NewGuid()}" : null);
        f.Db.AddRange(journal, entry);
        if (!sourceKey) f.Db.Add(new FinanceClaimReceiptPosting(Guid.NewGuid(), claim.Id, entry.Id, 112, f.Date, false, f.User.Id, f.Now));
        await f.Db.SaveChangesAsync();
        var summary = await f.Book.SummaryAsync(f.Date, f.Date, default);
        Assert.Equal(500m, summary.CompanyIncome);
        Assert.Equal(500m, summary.CashIn);
        Assert.Equal(500m, summary.NetCashFlow);
        Assert.Equal(1, (await f.Book.ListAsync(new(Category: "INCOME"), default)).TotalCount);
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(await f.Book.ExportAsync("cashbook", new(From: f.Date, To: f.Date), default)));
        Assert.DoesNotContain(workbook.Worksheets.First().CellsUsed(), x => x.GetString() == "Legacy claim");
        Assert.Equal(2, await f.Db.AccountingBookEntries.CountAsync());
        Assert.Equal("POSTED", journal.Status);
        Assert.Null(entry.DeletedAt);
    }

    [Fact]
    public void Claim_category_cannot_be_created_manually_or_counted_as_operating_cash()
    {
        Assert.DoesNotContain("CLAIM_RECEIPT", AccountingBookValues.ManualCategories);
        Assert.Equal(0, AccountingBookValues.CompanyIncomeSign("CLAIM_RECEIPT"));
        Assert.False(AccountingBookValues.IsCashIn("CLAIM_RECEIPT"));
        Assert.False(AccountingBookValues.IsCashOut("CLAIM_RECEIPT"));
    }

    [Fact]
    public async Task Missing_or_operating_accounts_fail_without_recording_a_receipt()
    {
        await using var f = new Fixture(); await f.Initialize();
        var claim = f.Claim(false); f.Db.Add(claim); await f.Db.SaveChangesAsync();
        var unconfigured = new AccountingBookService(f.Db, new BookTestPrincipal(f.User.Id), f.Finance);
        var claims = new FinanceClaimsService(f.Db, new BookTestPrincipal(f.User.Id), unconfigured, f.Finance);
        await Assert.ThrowsAsync<HrValidationException>(() => claims.ConfirmReceiptAsync(claim.Id, new(f.Date), default));
        Assert.Equal("PENDING", claim.Status);
        Assert.Empty(await f.Db.FinanceClaimReceiptPostings.ToListAsync());
        Assert.Throws<HrValidationException>(() => ClaimReceiptOptions.Validate([f.Cash, f.Income], new() { AssetAccountId = f.Cash.Id, ClearingAccountId = f.Income.Id }));
        f.Asset.SetCashAccount();
        Assert.Throws<HrValidationException>(() => ClaimReceiptOptions.Validate([f.Asset, f.Clearing], new() { AssetAccountId = f.Asset.Id, ClearingAccountId = f.Clearing.Id }));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"claim-separation-{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public DateOnly Date { get; } = new(2026, 1, 1);
        public User User { get; }
        public FinanceAccount Asset { get; } = new(Guid.NewGuid(), FinanceSeed.LegalEntityId, "119901", "Claims asset", "Claims asset", "ASSET", "DEBIT", true);
        public FinanceAccount Clearing { get; } = new(Guid.NewGuid(), FinanceSeed.LegalEntityId, "219901", "Claims clearing", "Claims clearing", "LIABILITY", "CREDIT", true);
        public FinanceAccount Cash { get; } = new(FinanceSeed.OperatingCashAccountId, FinanceSeed.LegalEntityId, "110300", "Cash", "Cash", "ASSET", "DEBIT", true);
        public FinanceAccount Income { get; } = new(FinanceSeed.CompanyIncomeClearingAccountId, FinanceSeed.LegalEntityId, "210300", "Income", "Income", "LIABILITY", "CREDIT", true);
        public AccountingPeriod Period { get; } = new(FinanceSeed.LegalEntityId, 2026, 1, "2026-01", new(2026, 1, 1), new(2026, 1, 31));
        public FinanceService Finance { get; }
        public AccountingBookService Book { get; }
        public FinanceClaimsService Claims { get; }
        public Fixture()
        {
            var department = new Department("Test", "TEST", Now);
            User = new("claims-test", "claims@example.invalid", "unused", "Test", department.Id, Now);
            Cash.SetCashAccount();
            Db.AddRange(department, User, Asset, Clearing, Cash, Income, Period);
            var principal = new BookTestPrincipal(User.Id);
            Finance = new(Db, principal);
            Book = new(Db, principal, Finance, Options.Create(new ClaimReceiptOptions { AssetAccountId = Asset.Id, ClearingAccountId = Clearing.Id }));
            Claims = new(Db, principal, Book, Finance);
        }
        public async Task Initialize()
        {
            await Db.SaveChangesAsync();
            await Book.CreateAsync(new("INCOME", Date, 500, Income.Id, Cash.Id, "Explicit cashbox funding", null), default);
        }
        public FinanceClaim Claim(bool historical) => new("TEST", null, "Test bank", "Monthly", "2026-01", Date, 100, 14, 2, historical ? Date : null, historical, null, User.Id, Now);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
