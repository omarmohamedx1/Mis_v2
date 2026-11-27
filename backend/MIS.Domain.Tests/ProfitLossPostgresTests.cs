using System.Data;
using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using MIS.Application.Common;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Persistence.Configurations;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class ProfitLossPostgresTests
{
    [AccountingPostgresFact]
    public async Task Claims_totals_dates_receipts_expenses_and_bilingual_exports_match_on_postgres()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("MIS_ACCOUNTING_TEST_CONNECTION"))
            .ReplaceService<IModelCustomizer, BookTestModelCustomizer>().Options;
        await using var db = new ApplicationDbContext(options);
        Assert.All(db.Model.GetEntityTypes(), x => Assert.Equal(BookTestModelCustomizer.Schema, x.GetSchema()));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = tx.GetDbTransaction();
                command.CommandText = db.Database.GenerateCreateScript();
                await command.ExecuteNonQueryAsync();
            }
            var now = DateTimeOffset.UtcNow;
            var from = new DateOnly(2026, 11, 10); var to = from.AddDays(2);
            var department = new Department("P&L test", "PL", now);
            var user = new User("pltest", "pl@example.invalid", "unused", "P&L test", department.Id, now);
            db.AddRange(department, user); await db.SaveChangesAsync();
            var principal = new BookTestPrincipal(user.Id);
            var finance = new FinanceService(db, principal);
            await finance.InitializeYearAsync(2026, default);
            var accounts = await finance.GetAccountsAsync(default);
            Guid Account(string code) => accounts.Single(x => x.Code == code).Id;
            var asset = new FinanceAccount(Guid.NewGuid(), FinanceSeed.LegalEntityId, "119901", "Claims", "Claims", "ASSET", "DEBIT", true);
            var clearing = new FinanceAccount(Guid.NewGuid(), FinanceSeed.LegalEntityId, "219901", "Claims", "Claims", "LIABILITY", "CREDIT", true);
            db.AddRange(asset, clearing); await db.SaveChangesAsync();
            var book = new AccountingBookService(db, principal, finance,
                Options.Create(new ClaimReceiptOptions { AssetAccountId = asset.Id, ClearingAccountId = clearing.Id }));
            var claims = new FinanceClaimsService(db, principal, book, finance);
            await book.CreateAsync(new("PETTY", from, 40m, Account("610110"), Account("110300"), "Test expense", null), default);
            var before = await book.ProfitLossAsync(from, to, default);
            Assert.Equal(40m, before.TotalExpenses);
            FinanceClaim Claim(string serial, DateOnly date, decimal amount) => new(serial, null, "Test bank", "Monthly", "2026-11",
                date, amount, 14, 2, null, false, null, user.Id, now);
            var unpaid = Claim("UNPAID", from, 100);
            var partial = Claim("PARTIAL", from.AddDays(1), 200);
            var paid = Claim("PAID", to, 300);
            var cancelled = Claim("CANCELLED", from, 1000); cancelled.Cancel(user.Id, now);
            var deleted = Claim("DELETED", from, 1000); deleted.SoftDelete(user.Id, now);
            db.AddRange(unpaid, partial, paid, cancelled, deleted,
                Claim("BEFORE", from.AddDays(-1), 1000), Claim("AFTER", to.AddDays(1), 1000));
            await db.SaveChangesAsync();
            var request = new MIS.Application.DTOs.Finance.ConfirmFinanceClaimReceiptRequest(to.AddDays(1), 50, Guid.NewGuid());
            await claims.ConfirmReceiptAsync(partial.Id, request, default);
            await claims.ConfirmReceiptAsync(partial.Id, request, default);
            await claims.ConfirmReceiptAsync(partial.Id, new(to.AddDays(1), 25, Guid.NewGuid()), default);
            await claims.ConfirmReceiptAsync(paid.Id, new(to.AddDays(1), 336, Guid.NewGuid()), default);
            Assert.Equal("PENDING", unpaid.Status); Assert.Equal("PENDING", partial.Status); Assert.Equal("RECEIVED", paid.Status);
            var journals = await db.JournalEntries.CountAsync();
            var entries = await db.AccountingBookEntries.CountAsync();
            var summary = await book.SummaryAsync(from, to.AddDays(1), default);
            var report = await book.ProfitLossAsync(from, to, default);
            Assert.Equal(672m, report.TotalClaims); // 112 + 224 + 336, full Total after VAT and withholding.
            Assert.Equal(before.TotalExpenses, report.TotalExpenses);
            Assert.Equal(632m, report.NetProfitLoss);
            Assert.Equal(report.TotalClaims - report.TotalExpenses, report.NetProfitLoss);
            var transactions = Assert.Single(report.ClaimCategories).Transactions;
            Assert.Equal(3, transactions.Count);
            Assert.Equal(new[] { "UNPAID", "PARTIAL", "PAID" }, transactions.Select(x => x.JournalNumber));
            Assert.Equal(new[] { 112m, 224m, 336m }, transactions.Select(x => x.Amount));
            Assert.Equal(112m, (await book.ProfitLossAsync(from, from, default)).TotalClaims);
            Assert.Equal(224m, (await book.ProfitLossAsync(from.AddDays(1), from.AddDays(1), default)).TotalClaims);
            Assert.Equal(336m, (await book.ProfitLossAsync(to, to, default)).TotalClaims);
            Assert.Equal(1120m, (await book.ProfitLossAsync(to.AddDays(1), to.AddDays(1), default)).TotalClaims);
            var empty = await book.ProfitLossAsync(to.AddDays(2), to.AddDays(2), default);
            Assert.Equal(0m, empty.TotalClaims); Assert.Empty(empty.ClaimCategories);
            await Assert.ThrowsAsync<HrValidationException>(() => book.ProfitLossAsync(to, from, default));
            foreach (var language in new[] { "en", "ar" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                using var workbook = new XLWorkbook(new MemoryStream(await book.ExportAsync("profit-loss", new(From: from, To: to), default)));
                var sheet = workbook.Worksheet(1);
                Assert.Equal(language == "ar" ? "إجمالي المطالبات" : "Total Claims", sheet.Cell(7, 1).GetString());
                Assert.Equal(report.TotalClaims, sheet.Cell(7, 2).GetValue<decimal>());
                Assert.Equal(report.TotalExpenses, sheet.Cell(8, 2).GetValue<decimal>());
                Assert.Equal(report.NetProfitLoss, sheet.Cell(9, 2).GetValue<decimal>());
                var rows = workbook.Worksheet(2).RowsUsed().Where(x => x.RowNumber() >= 7).ToArray();
                Assert.Equal(4, rows.Length);
                Assert.Equal(672m, rows.Take(3).Sum(x => x.Cell(6).GetValue<decimal>()));
                Assert.Equal(40m, rows.Last().Cell(6).GetValue<decimal>());
            }
            Assert.Equal(journals, await db.JournalEntries.CountAsync());
            Assert.Equal(entries, await db.AccountingBookEntries.CountAsync());
            Assert.False(db.ChangeTracker.HasChanges());
            var after = await book.SummaryAsync(from, to.AddDays(1), default);
            Assert.Equal(summary.CompanyIncome, after.CompanyIncome);
            Assert.Equal(summary.CashIn, after.CashIn); Assert.Equal(summary.CashOut, after.CashOut);
            Console.WriteLine("PASS PostgreSQL: 3 claims counted once; totals 672 claims, 40 expenses, 632 profit; inclusive dates, partial receipts, excluded claims, Arabic/English Excel, no report writes.");
        }
        finally
        {
            CultureInfo.CurrentUICulture = culture;
            await tx.RollbackAsync();
        }
    }
}
