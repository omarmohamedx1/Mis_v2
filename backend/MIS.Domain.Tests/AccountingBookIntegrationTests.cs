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
        var originalCulture = System.Globalization.CultureInfo.CurrentUICulture;
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
            await bm.SaveRecurringAsync(null, new("Monthly test", 50, 1, Account("610120"), Account("110300"), new(today.Year, today.Month, 1), null, new string('n', 500)), default);
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
            var payrollPeriod = new AccountingPayrollPeriod(today.Year, today.Month, maker.Id, now);
            var salary = new AccountingEmployeePayroll(payrollPeriod.Id, employee.Id, employee.EmployeeNumber, employee.FullName, department.Name, null, 500, 0, null, now);
            salary.ApplyComponents(20, 10, 5, 0, 0, null, now);
            db.AccountingPayrollPeriods.Add(payrollPeriod); db.AccountingEmployeePayrolls.Add(salary); await db.SaveChangesAsync();
            var accounting = new AccountingService(db, new BookTestPrincipal(maker.Id), null!, new HrAuditService(db, new BookTestPrincipal(maker.Id)), null!, bm);
            await accounting.ApprovePayrollAsync(salary.Id, default); await accounting.PayPayrollAsync(salary.Id, default);
            var salaryEntry = await db.AccountingBookEntries.Include(x => x.JournalEntry).SingleAsync(x => x.SourceKey == $"PAYROLL:{salary.Id}");
            Check(salary.Status == "PAID" && salaryEntry.JournalEntry.Status == "POSTED" && salaryEntry.Amount == 535, "actual payroll payment and journal persist together");
            await Conflict(() => accounting.PayPayrollAsync(salary.Id, default), "paid salary cannot be paid twice");
            await Assert.ThrowsAsync<HrValidationException>(() => accounting.CreateTransportationAsync(new(employee.Id, today, 1.001m, "Invalid cents", null, null, null), default));
            var claim = await accounting.CreateTransportationAsync(new(employee.Id, today, 25, "Same month transportation", null, null, null), default);
            await accounting.TransitionTransportationAsync(claim.Id, "approve", default);
            await Conflict(() => accounting.TransitionTransportationAsync(claim.Id, "pay", default), "payroll transportation cannot be paid a second time separately");
            var candidate1 = await Create("ADVANCE_REPAYMENT", 200, related: advance.Id); var candidate2 = await Create("ADVANCE_REPAYMENT", 200, related: advance.Id);
            await Post(candidate1); await bm.TransitionAsync(candidate2.Id, "submit", null, default); await br.TransitionAsync(candidate2.Id, "approve", null, default);
            await Conflict(() => fr.PostJournalAsync(candidate2.JournalEntryId, default), "direct journal posting rechecks remaining advance balance");
            await Conflict(() => fm.SaveAccountAsync(Account("610110"), new("610111", "Changed code", "Changed code", "EXPENSE", "DEBIT", true), default), "system account codes remain stable");
            System.Globalization.CultureInfo.CurrentUICulture = new("ar-EG");
            using (var arabicWorkbook = new XLWorkbook(new MemoryStream(await bm.ExportAsync("all", new(From: new(today.Year, today.Month, 1), To: today), default))))
                Check(arabicWorkbook.Worksheets.All(x => x.RightToLeft), "Arabic full workbook uses RTL sheets");
            var longEntry = await bm.CreateAsync(new("PETTY", today, 1, Account("610110"), Account("110300"), new string('x', 1000), null, RequestId: Guid.NewGuid()), default);
            await Post(longEntry); var longReversal = await fr.ReverseJournalAsync(longEntry.JournalEntryId, new(new string('r', 1000)), default);
            Check(longReversal.Description.Length == 1000 && longReversal.Lines.All(x => x.Description.Length == 1000), "maximum-length descriptions and reasons reverse without overflow");
            var employee2 = new Employee("CODEX-EMP2", "Second test employee", department.Id, true, now); db.Employees.Add(employee2);
            var salary2 = new AccountingEmployeePayroll(payrollPeriod.Id, employee2.Id, employee2.EmployeeNumber, employee2.FullName, department.Name, null, 700, 0, null, now);
            db.AccountingEmployeePayrolls.Add(salary2); await db.SaveChangesAsync(); await accounting.ApprovePayrollAsync(salary2.Id, default);
            var period = (await fm.GetPeriodsAsync(today.Year, default)).Single(x => x.PeriodNumber == today.Month);
            await fm.ChangePeriodStatusAsync(period.Id, "soft-close", new("Isolated test"), default);
            await Conflict(() => accounting.PayPayrollAsync(salary2.Id, default), "closed accounting period blocks payroll payment");
            Check(await db.AccountingEmployeePayrolls.AsNoTracking().Where(x => x.Id == salary2.Id).Select(x => x.Status).SingleAsync() == "APPROVED", "failed posting does not persist paid salary status");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = originalCulture;
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
