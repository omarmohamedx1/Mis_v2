using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using MIS.Application.Common;
using MIS.Application.DTOs.Accounting;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Services;
using ClosedXML.Excel;
using Npgsql;

using Xunit;

namespace MIS.Domain.Tests;

public sealed class AccountingBookIntegrationTests
{
    [AccountingPostgresFact]
    public async Task Isolated_ledger_advances_recurring_payroll_reversal_exports_and_permissions()
    {
        var cs = Environment.GetEnvironmentVariable("MIS_ACCOUNTING_TEST_CONNECTION")!;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(cs).ReplaceService<IModelCustomizer, BookTestModelCustomizer>().Options;
        await using var db = new ApplicationDbContext(options);
        // Every table and foreign key must belong to our isolated namespace before any SQL runs.
        if (db.Model.GetEntityTypes().Any(x => x.GetSchema() != BookTestModelCustomizer.Schema)) throw new Exception("Unsafe schema mapping");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = tx.GetDbTransaction(); command.CommandText = db.Database.GenerateCreateScript(); command.CommandTimeout = 90; await command.ExecuteNonQueryAsync();
            }
            Console.WriteLine("PASS isolated schema created; existing schemas untouched");
            var now = DateTimeOffset.UtcNow; var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo").DateTime);
            var department = new Department("Test accounting", "CODEXTEST", now); db.Departments.Add(department);
            var maker = new User("testmaker", "maker@example.invalid", "unused", "Maker", department.Id, now);
            var reviewer = new User("testreviewer", "reviewer@example.invalid", "unused", "Reviewer", department.Id, now);
            var employee = new Employee("CODEX-EMP", "Test employee", department.Id, true, now);
            db.Users.AddRange(maker, reviewer); db.Employees.Add(employee); await db.SaveChangesAsync();
            var fm = new FinanceService(db, new BookTestPrincipal(maker.Id)); var fr = new FinanceService(db, new BookTestPrincipal(reviewer.Id));
            var bm = new AccountingBookService(db, new BookTestPrincipal(maker.Id), fm); var br = new AccountingBookService(db, new BookTestPrincipal(reviewer.Id), fr);
            await fm.InitializeYearAsync(today.Year, default);
            var accounts = await fm.GetAccountsAsync(default); Guid Account(string code) => accounts.Single(x => x.Code == code).Id;
            async Task<AccountingBookEntryDto> Create(string cat, decimal amount, DateOnly? date = null, Guid? related = null) => await bm.CreateAsync(new(cat, date ?? today, amount, Account(cat switch { "INCOME" => "410100", "ADVANCE" or "CARRIED_ADVANCE" or "ADVANCE_REPAYMENT" => "113100", _ => "610110" }), Account(cat == "CARRIED_ADVANCE" ? "310100" : "110300"), "Isolated test " + cat, null, cat.Contains("ADVANCE") ? employee.Id : null, related, Guid.NewGuid()), default);
            async Task Post(AccountingBookEntryDto d) { await bm.TransitionAsync(d.Id, "submit", null, default); await br.TransitionAsync(d.Id, "approve", null, default); await br.TransitionAsync(d.Id, "post", null, default); }
            void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
            async Task Conflict(Func<Task> run, string label) { try { await run(); throw new Exception("Expected conflict: " + label); } catch (HrConflictException) { Console.WriteLine("PASS " + label); } }
            var petty = await Create("PETTY", 400.25m); await bm.TransitionAsync(petty.Id, "submit", null, default);
            await Conflict(() => bm.TransitionAsync(petty.Id, "approve", null, default), "maker cannot approve own draft");
            await br.TransitionAsync(petty.Id, "approve", null, default); await br.TransitionAsync(petty.Id, "post", null, default);
            var income = await Create("INCOME", 1000); await Post(income);
            var advance = await Create("ADVANCE", 400, today.AddDays(-2)); await Post(advance);
            var opening = await Create("CARRIED_ADVANCE", 200, today.AddDays(-3)); await Post(opening);
            var repay = await Create("ADVANCE_REPAYMENT", 100, related: advance.Id); await Post(repay);
            await Conflict(() => Create("ADVANCE_REPAYMENT", 301, related: advance.Id), "over-repayment blocked");
            await Conflict(() => fr.ReverseJournalAsync(advance.JournalEntryId, new("test"), default), "direct reversal cannot orphan repayments");
            var balances = await bm.AdvancesAsync(today, null, null, default);
            Check(balances.Sum(x => x.Remaining) == 500, "advance remaining balances");
            Check(balances.Single(x => x.Id == opening.Id).Carried, "opening advance marked carried");
            var summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(summary.Income == 1000 && summary.Expenses == 400.25m && summary.CashIn == 1100 && summary.CashOut == 800.25m, "cash flow and expenses exclude opening balances and repayments");
            await bm.SaveRecurringAsync(null, new("Monthly test", 50, 1, Account("610120"), Account("110300"), new(today.Year, today.Month, 1), null, null), default);
            var first = await bm.GenerateRecurringAsync(new(today.Year, today.Month), default); var second = await bm.GenerateRecurringAsync(new(today.Year, today.Month), default);
            Check(first.Created == 1 && second.Created == 0 && second.AlreadyGenerated == 1, "recurring generation is idempotent");
            var payroll = new AccountingPaidSource("PAYROLL", Guid.NewGuid(), "SALARY", 700, employee.Id, "Test payroll", today, 100, 50, 25);
            await bm.RecordPaidSourceAsync(payroll, default); await bm.RecordPaidSourceAsync(payroll, default);
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(summary.Expenses == 1100.25m && summary.Categories.Single(x => x.Category == "SALARY").Amount == 525 && summary.Categories.Single(x => x.Category == "BONUS").Amount == 25, "payroll components split without duplicate expense");
            Check(await db.AccountingBookEntries.CountAsync(x => x.SourceKey == $"PAYROLL:{payroll.Id}") == 1, "paid source posts one journal only");
            await fr.ReverseJournalAsync(petty.JournalEntryId, new("test correction"), default);
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(summary.Expenses == 700 && summary.CashIn == 1500.25m && summary.CashOut == 1500.25m, "reversal corrects expense and cash flow once");
            var bytes = await bm.ExportAsync("all", new(From: new(today.Year, today.Month, 1), To: today), default);
            using var workbook = new XLWorkbook(new MemoryStream(bytes)); Check(workbook.Worksheets.Count() >= 10, "full multi-sheet workbook loads successfully");
            var preview = await bm.PreviewCommissionsAsync(today.Year, today.Month, default); Check(preview.MissingRuleScopes.Count == 2, "missing commission rules are explicit");
            var custom = await fm.SaveAccountAsync(null, new("610999", "Test expense", "Test expense", "EXPENSE", "DEBIT", true, null, true, false), default);
            Check(accounts.All(x => x.Id != custom.Id), "custom chart account created");
            var trial = await fm.GetTrialBalanceAsync(today, default); Check(trial.TotalDebit == trial.TotalCredit, "trial balance remains balanced");
        }
        finally
        {
            await tx.RollbackAsync(); Console.WriteLine("PASS transaction rolled back, isolated schema removed");
        }
    }
}

public sealed class BookTestPrincipal(Guid id) : ICurrentUserContext
{
    public Guid UserId => id; public string Username => "isolated-test"; public IReadOnlyCollection<string> Roles => ["Admin"]; public IReadOnlyCollection<string> Permissions => ["*"];
}
public sealed class BookTestModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public static string Schema { get; } = "codex_accounting_" + Guid.NewGuid().ToString("N");
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        foreach (var entity in modelBuilder.Model.GetEntityTypes()) entity.SetSchema(Schema);
    }
}

public sealed class AccountingPostgresFactAttribute : FactAttribute
{
    public AccountingPostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MIS_ACCOUNTING_TEST_CONNECTION"))) Skip = "Set MIS_ACCOUNTING_TEST_CONNECTION to run isolated PostgreSQL accounting tests."; }
}
