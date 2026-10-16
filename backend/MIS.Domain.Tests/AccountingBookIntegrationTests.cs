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
            async Task Post(AccountingBookEntryDto d) { Assert.Equal("POSTED", d.Status); Assert.Equal("POSTED", (await bm.TransitionAsync(d.Id, "record", null, default)).Status); }
            void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
            async Task Conflict(Func<Task> run, string label) { try { await run(); throw new Exception("Expected conflict: " + label); } catch (HrConflictException) { Console.WriteLine("PASS " + label); } }
            var petty = await Create("PETTY", 400.25m); await Post(petty);
            Check(petty.JournalNumber is not null, "confirmed expense records immediately without approval");
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
            Check(summary.Expenses == 1150.25m && summary.Categories.Single(x => x.Category == "SALARY").Amount == 525 && summary.Categories.Single(x => x.Category == "BONUS").Amount == 25, "payroll components split without duplicate expense");
            Check(await db.AccountingBookEntries.CountAsync(x => x.SourceKey == $"PAYROLL:{payroll.Id}") == 1, "paid source posts one journal only");
            await fr.ReverseJournalAsync(petty.JournalEntryId, new("test correction"), default);
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(summary.Expenses == 750 && summary.CashIn == 1500.25m && summary.CashOut == 1550.25m, "reversal corrects expense and cash flow once");
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
            await accounting.RecordPayrollAsync(salary.Id, new(20, 10, 5, 0, 0, "Confirmed components"), default);
            var salaryEntry = await db.AccountingBookEntries.Include(x => x.JournalEntry).SingleAsync(x => x.SourceKey == $"PAYROLL:{salary.Id}");
            Check(salary.Status == "PAID" && salaryEntry.JournalEntry.Status == "POSTED" && salaryEntry.Amount == 535, "actual payroll payment and journal persist together");
            await Conflict(() => accounting.PayPayrollAsync(salary.Id, default), "paid salary cannot be paid twice");
            await Assert.ThrowsAsync<HrValidationException>(() => accounting.CreateTransportationAsync(new(employee.Id, today, 1.001m, "Invalid cents", null, null, null), default));
            await Conflict(() => accounting.CreateTransportationAsync(new(employee.Id, today, 25, "Same month transportation", null, null, null), default), "payroll transportation cannot be paid a second time separately");
            var candidate1 = await Create("ADVANCE_REPAYMENT", 200, related: advance.Id);
            await Post(candidate1);
            await Conflict(() => Create("ADVANCE_REPAYMENT", 200, related: advance.Id), "remaining advance balance is checked during immediate recording");
            await Conflict(() => fm.SaveAccountAsync(Account("610110"), new("610111", "Changed code", "Changed code", "EXPENSE", "DEBIT", true), default), "system account codes remain stable");
            System.Globalization.CultureInfo.CurrentUICulture = new("ar-EG");
            using (var arabicWorkbook = new XLWorkbook(new MemoryStream(await bm.ExportAsync("all", new(From: new(today.Year, today.Month, 1), To: today), default))))
                Check(arabicWorkbook.Worksheets.All(x => x.RightToLeft), "Arabic full workbook uses RTL sheets");
            var longEntry = await bm.CreateAsync(new("PETTY", today, 1, Account("610110"), Account("110300"), new string('x', 1000), null, RequestId: Guid.NewGuid()), default);
            await Post(longEntry); var longReversal = await fr.ReverseJournalAsync(longEntry.JournalEntryId, new(new string('r', 1000)), default);
            Check(longReversal.Description.Length == 1000 && longReversal.Lines.All(x => x.Description.Length == 1000), "maximum-length descriptions and reasons reverse without overflow");
            var beforeDelete = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            var requestId = Guid.NewGuid();
            var request = new CreateAccountingBookEntryRequest("PETTY", today, 120.75m, Account("610110"), Account("110300"), "Mistaken expense", null, RequestId: requestId);
            var mistaken = await bm.CreateAsync(request, default);
            Assert.Equal(mistaken.Id, (await bm.CreateAsync(request, default)).Id);
            await Assert.ThrowsAsync<HrValidationException>(() => bm.DeleteAsync(mistaken.Id, new(false), default));
            Assert.Contains((await bm.ListAsync(new(), default)).Items, x => x.Id == mistaken.Id);
            await bm.DeleteAsync(mistaken.Id, new(true, "Mistaken entry"), default);
            await bm.DeleteAsync(mistaken.Id, new(true), default);
            var afterDelete = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(afterDelete.Expenses == beforeDelete.Expenses && afterDelete.CashIn == beforeDelete.CashIn && afterDelete.CashOut == beforeDelete.CashOut, "deletion removes both expense and cash effects completely");
            Assert.DoesNotContain((await bm.ListAsync(new(), default)).Items, x => x.Id == mistaken.Id);
            var correction = await db.JournalEntries.SingleAsync(x => x.ReversalOfJournalId == mistaken.JournalEntryId);
            foreach (var registerStatus in new string?[] { null, "POSTED", "REVERSED" })
            {
                var register = await fm.GetJournalsAsync(1, 200, registerStatus, null, null, default);
                Assert.DoesNotContain(register.Items, x => x.Id == mistaken.JournalEntryId || x.Id == correction.Id);
                if (registerStatus is null) Assert.Contains(register.Items, x => x.Id == income.JournalEntryId);
            }
            using (var journalExport = new XLWorkbook(new MemoryStream(await bm.ExportAsync("journals", new(From: new(today.Year, today.Month, 1), To: today), default))))
            {
                var exportedText = journalExport.Worksheets.SelectMany(x => x.CellsUsed()).Select(x => x.GetString()).ToArray();
                Assert.DoesNotContain(mistaken.JournalNumber!, exportedText);
                Assert.DoesNotContain(correction.JournalNumber!, exportedText);
                Assert.Contains(income.JournalNumber!, exportedText);
            }
            Check(correction.EntryType == "ACCOUNTING_DELETE" && correction.TotalDebit == correction.TotalCredit, "repeated deletion creates one balanced audit correction");
            await Conflict(() => fr.ReverseJournalAsync(correction.Id, new("Undo"), default), "deletion correction cannot restore hidden money");
            await Conflict(() => bm.CreateAsync(request, default), "replaying a deleted request cannot restore the expense");
            await Conflict(() => bm.DeleteAsync(advance.Id, new(true), default), "advance with repayments cannot be deleted");
            await bm.DeleteAsync(candidate1.Id, new(true), default);
            Check((await bm.AdvancesAsync(today, null, null, default)).Single(x => x.Id == advance.Id).Remaining == 300, "deleting a repayment restores the remaining balance");
            await bm.DeleteAsync(repay.Id, new(true), default);
            await bm.DeleteAsync(advance.Id, new(true), default);
            Assert.DoesNotContain(await bm.AdvancesAsync(today, null, null, default), x => x.Id == advance.Id);
            var recurringEntry = await db.AccountingBookEntries.SingleAsync(x => x.RecurringExpenseId != null && x.DeletedAt == null);
            Assert.Equal("POSTED", (await db.JournalEntries.SingleAsync(x => x.Id == recurringEntry.JournalEntryId)).Status);
            await bm.DeleteAsync(recurringEntry.Id, new(true), default);
            Check((await bm.GenerateRecurringAsync(new(today.Year, today.Month), default)).Created == 1, "deleted recurring expense can be deliberately recorded again");
            var future = today.AddMonths(1);
            var futureResult = await bm.GenerateRecurringAsync(new(future.Year, future.Month), default);
            Check(futureResult.Created == 0 && futureResult.NotDue == 1, "future recurring expenses do not post early");
            var periodId = (await fm.GetPeriodsAsync(today.Year, default)).Single(x => x.PeriodNumber == today.Month).Id;
            var legacyJournal = new JournalEntry(MIS.Infrastructure.Persistence.Configurations.FinanceSeed.LegalEntityId, periodId, null, "MANUAL", today, today, "EGP", "Old draft", maker.Id, now);
            legacyJournal.AddLine(Account("610110"), 7, 0, 7, 0, 1, "Old draft"); legacyJournal.AddLine(Account("110300"), 0, 7, 0, 7, 1, "Old draft");
            var oldEntry = new AccountingBookEntry(Guid.NewGuid(), "PETTY", today, 7, Account("610110"), Account("110300"), "Old draft", null, legacyJournal.Id, maker.Id, now);
            db.JournalEntries.Add(legacyJournal); db.AccountingBookEntries.Add(oldEntry); await db.SaveChangesAsync();
            Assert.Contains((await bm.ListAsync(new(Status: "UNRECORDED"), default)).Items, x => x.Id == oldEntry.Id);
            Check((await bm.TransitionAsync(oldEntry.Id, "record", null, default)).Status == "POSTED", "old drafts record in one confirmation by their maker");
            await bm.DeleteAsync(oldEntry.Id, new(true), default);
            await bm.DeleteSourceAsync("PAYROLL", salary.Id, new(true), default);
            Check(salary.Status == "DELETED" && (await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default)).LegacyPaidCount == 0, "deleted salary stays suppressed and is not counted as an unlinked historical payment");
            db.EmployeeCompensations.Add(new EmployeeCompensation(employee.Id, 500, 0, new(today.Year, today.Month, 1), null, true, null, null, null, null, now));
            await db.SaveChangesAsync();
            var hrPayroll = new HrNetSalaryService(db, new BookTestPrincipal(maker.Id));
            await hrPayroll.GetSheetAsync(today.Year, today.Month, default);
            Check(await db.AccountingEmployeePayrolls.CountAsync(x => x.PeriodId == payrollPeriod.Id && x.EmployeeId == employee.Id) == 1 && salary.Status == "DELETED", "HR synchronization does not silently recreate a deleted salary");
            var readOnly = new AccountingBookService(db, new LimitedBookTestPrincipal(maker.Id, ["finance.access"]), fm);
            await Assert.ThrowsAsync<HrForbiddenException>(() => readOnly.DeleteAsync(income.Id, new(true), default));
            var claim = await accounting.CreateTransportationAsync(new(employee.Id, today, 25.50m, "Confirmed travel", null, null, null), default);
            Check(claim.Status == "PAID", "transportation records and pays in a single confirmation");
            await bm.DeleteSourceAsync("TRANSPORT", claim.Id, new(true), default);
            Assert.False(await db.AccountingTransportationClaims.AnyAsync(x => x.Id == claim.Id));
            var collectorRule = new AccountingCommissionRule("TESTC", "Collector test", "Collector test", "COLLECTOR", "PERCENT_OF_COLLECTED", 10, null, new(today.Year, today.Month, 1), maker.Id, now);
            var supervisorRule = new AccountingCommissionRule("TESTS", "Supervisor test", "Supervisor test", "SUPERVISOR", "PERCENT_OF_COLLECTED", 5, null, new(today.Year, today.Month, 1), maker.Id, now);
            db.AccountingCommissionRules.AddRange(collectorRule, supervisorRule);
            var collector = new AccountingCollectorCommission(today.Year, today.Month, maker.Id, null, 1, 1000, 1000, collectorRule.Id, 1, 10, 100, "[]", now);
            var supervisor = new AccountingSupervisorCommission(today.Year, today.Month, reviewer.Id, null, "One collector", 1, 1000, 1000, supervisorRule.Id, 1, 5, 50, "[]", now);
            db.AccountingCollectorCommissions.Add(collector); db.AccountingSupervisorCommissions.Add(supervisor); await db.SaveChangesAsync();
            var paidCollector = await accounting.RecordCollectorCommissionAsync(collector.Id, new(12.25m, "Confirmed adjustment"), default);
            var paidSupervisor = await accounting.RecordSupervisorCommissionAsync(supervisor.Id, new(-3.50m, "Confirmed adjustment"), default);
            Check(paidCollector.Status == "PAID" && paidCollector.FinalCommission == 112.25m && paidSupervisor.Status == "PAID" && paidSupervisor.FinalCommission == 46.50m, "single payment records exactly the adjustments confirmed in the form");
            await bm.DeleteSourceAsync("COLLECTOR", collector.Id, new(true), default);
            await bm.DeleteSourceAsync("SUPERVISOR", supervisor.Id, new(true), default);
            var finalTrial = await fm.GetTrialBalanceAsync(today, default); Check(finalTrial.TotalDebit == finalTrial.TotalCredit, "trial balance stays balanced after confirmed source deletion");
            var employee2 = new Employee("CODEX-EMP2", "Second test employee", department.Id, true, now); db.Employees.Add(employee2);
            var salary2 = new AccountingEmployeePayroll(payrollPeriod.Id, employee2.Id, employee2.EmployeeNumber, employee2.FullName, department.Name, null, 700, 0, null, now);
            db.AccountingEmployeePayrolls.Add(salary2); await db.SaveChangesAsync(); await accounting.ApprovePayrollAsync(salary2.Id, default);
            var legacyClaim = new AccountingTransportationClaim(employee2.Id, today, 15, "Historical unlinked claim", maker.Id, now);
            legacyClaim.Approve(maker.Id, now); legacyClaim.MarkPaid(now);
            db.AccountingTransportationClaims.Add(legacyClaim); await db.SaveChangesAsync();
            var period = (await fm.GetPeriodsAsync(today.Year, default)).Single(x => x.PeriodNumber == today.Month);
            await fm.ChangePeriodStatusAsync(period.Id, "soft-close", new("Isolated test"), default);
            await Conflict(() => accounting.PayPayrollAsync(salary2.Id, default), "closed accounting period blocks payroll payment");
            await Conflict(() => bm.DeleteAsync(income.Id, new(true), default), "closed period blocks deleting a recorded entry");
            await Conflict(() => bm.DeleteSourceAsync("TRANSPORT", legacyClaim.Id, new(true), default), "closed period also protects historical paid records without linked journals");
            await Conflict(() => Create("PETTY", 5), "closed period blocks direct recording");
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
public sealed class LimitedBookTestPrincipal(Guid id, IReadOnlyCollection<string> permissions) : ICurrentUserContext
{
    public Guid UserId => id; public string Username => "limited-test"; public IReadOnlyCollection<string> Roles => []; public IReadOnlyCollection<string> Permissions => permissions;
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
