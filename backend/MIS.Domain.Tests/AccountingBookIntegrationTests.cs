using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using MIS.Application.Common;
using MIS.Application.DTOs.Accounting;
using MIS.Application.DTOs.Finance;
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
            var emptyProfitLoss = await bm.ProfitLossAsync(today.AddMonths(1), today.AddMonths(1), default);
            Check(emptyProfitLoss.TotalRevenue == 0 && emptyProfitLoss.TotalExpenses == 0 && emptyProfitLoss.NetProfitLoss == 0 &&
                  emptyProfitLoss.RevenueCategories.Count == 0 && emptyProfitLoss.ExpenseCategories.Count == 0,
                "an empty accounting period returns a successful zero profit and loss report");
            await Assert.ThrowsAsync<HrValidationException>(() => bm.ProfitLossAsync(today, today.AddDays(-1), default));
            async Task<AccountingBookEntryDto> Create(string cat, decimal amount, DateOnly? date = null, Guid? related = null, string? reference = null) => await bm.CreateAsync(new(cat, date ?? today, amount, Account(MIS.Domain.Constants.AccountingBookValues.AccountCode(cat)), Account(cat == "CARRIED_ADVANCE" ? "310100" : "110300"), "Isolated test " + cat, reference, cat.Contains("ADVANCE") ? employee.Id : null, related, Guid.NewGuid()), default);
            async Task Post(AccountingBookEntryDto d) { Assert.Equal("POSTED", d.Status); Assert.Equal("POSTED", (await bm.TransitionAsync(d.Id, "record", null, default)).Status); }
            void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
            async Task Conflict(Func<Task> run, string label) { try { await run(); throw new Exception("Expected conflict: " + label); } catch (HrConflictException) { Console.WriteLine("PASS " + label); } }
            var petty = await Create("PETTY", 400.25m); await Post(petty);
            Check(petty.JournalNumber is not null, "confirmed expense records immediately without approval");
            var income = await Create("INCOME", 1000); await Post(income);
            Assert.Equal("COMPANY_INCOME", await db.JournalEntries.Where(x => x.Id == income.JournalEntryId).Select(x => x.EntryType).SingleAsync());
            var advance = await Create("ADVANCE", 400, today.AddDays(-2)); await Post(advance);
            var opening = await Create("CARRIED_ADVANCE", 200, today.AddDays(-3), reference: "CARRY-001"); await Post(opening);
            var repay = await Create("ADVANCE_REPAYMENT", 100, related: advance.Id); await Post(repay);
            var priorCompanyIncome = await Create("INCOME", 100000, today.AddMonths(-1)); await Post(priorCompanyIncome);
            await Conflict(() => Create("CARRIED_ADVANCE", 200, today.AddDays(-3), reference: "carry-001"), "duplicate carried advance reference is rejected");
            var beforeRepayment = (await bm.AdvancesAsync(new(AsOf: today.AddDays(-1), Search: advance.DocumentNumber), default)).Items.Single();
            var balanceAfterRepayment = (await bm.AdvancesAsync(new(AsOf: today, Search: advance.DocumentNumber), default)).Items.Single();
            Check(beforeRepayment.Repaid == 0 && beforeRepayment.Remaining == 400 && balanceAfterRepayment.Repaid == 100 && balanceAfterRepayment.Remaining == 300,
                "advance balances respect the selected cutoff date");
            var carriedRows = await bm.AdvancesAsync(new(AsOf: today.AddDays(-2), Search: "CARRY-001", CarriedOnly: true), default);
            Check(carriedRows.Items.Single().Id == opening.Id && carriedRows.TotalAmount == 200 && carriedRows.TotalRemaining == 200,
                "carried advances can be filtered by reference and as-of date");
            using (var carriedExport = new XLWorkbook(new MemoryStream(await bm.ExportAsync("advances", new(AsOf: today.AddDays(-2), Search: "CARRY-001", CarriedOnly: true), default))))
            {
                var sheet = carriedExport.Worksheets.Single();
                Check(sheet.Cell(7, 3).GetString() == "CARRY-001"
                    && sheet.Cell(8, 1).GetString() is "TOTAL" or "الإجمالي"
                    && sheet.Cell(8, 5).GetValue<double>() == 200
                    && sheet.Cell(8, 6).GetValue<double>() == 0
                    && sheet.Cell(8, 7).GetValue<double>() == 200,
                    "carried advance Excel export includes the cutoff-filtered report and totals");
            }
            await Conflict(() => Create("ADVANCE_REPAYMENT", 301, related: advance.Id), "over-repayment blocked");
            await Conflict(() => fr.ReverseJournalAsync(advance.JournalEntryId, new("test"), default), "direct reversal cannot orphan repayments");
            var balances = await bm.AdvancesAsync(today, null, null, default);
            Check(balances.Sum(x => x.Remaining) == 500, "advance remaining balances");
            Check(balances.Single(x => x.Id == opening.Id).Carried, "opening advance marked carried");
            var summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(summary.CompanyIncome == 100599.75m && summary.Revenue == 0 && summary.Expenses == 400.25m && summary.CashIn == 1100 && summary.CashOut == 800.25m, "company income carries its prior balance, nets normal expenses and excludes advances");
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
            var companyIncomeBeforeMovements = summary.CompanyIncome;
            var normalRevenue = await Create("REVENUE", 1000); await Post(normalRevenue);
            var fixedExpense = await Create("FIXED", 100); await Post(fixedExpense);
            var bonusExpense = await Create("BONUS", 50); await Post(bonusExpense);
            var legalExpense = await Create("LEGAL", 75); await Post(legalExpense);
            var otherExpense = await Create("OTHER", 25); await Post(otherExpense);
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(summary.CompanyIncome == companyIncomeBeforeMovements + 750 && summary.Revenue == 1000 &&
                  summary.Expenses == 1000 && summary.Categories.Single(x => x.Category == "BONUS").Amount == 50 &&
                  summary.Categories.Single(x => x.Category == "LEGAL").Amount == 75,
                "revenue increases company income while fixed, bonus, legal and other expenses decrease it");
            var manualRevenueJournal = new JournalEntry(MIS.Infrastructure.Persistence.Configurations.FinanceSeed.LegalEntityId,
                (await fm.GetPeriodsAsync(today.Year, default)).Single(x => x.PeriodNumber == today.Month).Id, null, "MANUAL",
                today, today, "EGP", "Manual revenue receipt", maker.Id, now);
            manualRevenueJournal.AddLine(Account("110300"), 30, 0, 30, 0, 1, "Manual revenue receipt");
            manualRevenueJournal.AddLine(Account("410100"), 0, 30, 0, 30, 1, "Manual revenue receipt");
            manualRevenueJournal.Post("ACC-MANUAL-REVENUE", maker.Id, now, true);
            db.JournalEntries.Add(manualRevenueJournal); await db.SaveChangesAsync();
            var manualExpenseJournal = new JournalEntry(MIS.Infrastructure.Persistence.Configurations.FinanceSeed.LegalEntityId,
                manualRevenueJournal.PeriodId, null, "MANUAL", today, today, "EGP", "Manual expense payment", maker.Id, now);
            manualExpenseJournal.AddLine(Account("610100"), 20, 0, 20, 0, 1, "Manual expense payment");
            manualExpenseJournal.AddLine(Account("110300"), 0, 20, 0, 20, 1, "Manual expense payment");
            manualExpenseJournal.Post("ACC-MANUAL-EXPENSE", maker.Id, now, true);
            db.JournalEntries.Add(manualExpenseJournal); await db.SaveChangesAsync();
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(summary.CompanyIncome == companyIncomeBeforeMovements + 760 && summary.Revenue == 1030 &&
                  summary.Expenses == 1020,
                "manual journal inflows and outflows affect company income once");
            var manualReceipt = new JournalEntry(MIS.Infrastructure.Persistence.Configurations.FinanceSeed.LegalEntityId,
                manualRevenueJournal.PeriodId, null, "MANUAL", today, today, "EGP", "Other manual receipt", maker.Id, now);
            manualReceipt.AddLine(Account("110300"), 40, 0, 40, 0, 1, "Other manual receipt");
            manualReceipt.AddLine(Account("210300"), 0, 40, 0, 40, 1, "Other manual receipt");
            manualReceipt.Post("ACC-MANUAL-RECEIPT", maker.Id, now, true);
            db.JournalEntries.Add(manualReceipt); await db.SaveChangesAsync();
            var manualOutflow = new JournalEntry(MIS.Infrastructure.Persistence.Configurations.FinanceSeed.LegalEntityId,
                manualRevenueJournal.PeriodId, null, "MANUAL", today, today, "EGP", "Other manual outflow", maker.Id, now);
            manualOutflow.AddLine(Account("210300"), 15, 0, 15, 0, 1, "Other manual outflow");
            manualOutflow.AddLine(Account("110300"), 0, 15, 0, 15, 1, "Other manual outflow");
            manualOutflow.Post("ACC-MANUAL-OUTFLOW", maker.Id, now, true);
            db.JournalEntries.Add(manualOutflow); await db.SaveChangesAsync();
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(summary.CompanyIncome == companyIncomeBeforeMovements + 785 && summary.Revenue == 1030 &&
                  summary.Expenses == 1020,
                "other manual cash receipts and outflows change company income by their net direction");
            await fr.ReverseJournalAsync(fixedExpense.JournalEntryId, new("Expense correction"), default);
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(summary.CompanyIncome == companyIncomeBeforeMovements + 885 &&
                  summary.Expenses == 920,
                "reversing a normal expense restores its company income effect");
            var bytes = await bm.ExportAsync("all", new(From: new(today.Year, today.Month, 1), To: today), default);
            using var workbook = new XLWorkbook(new MemoryStream(bytes)); Check(workbook.Worksheets.Count() >= 10, "full multi-sheet workbook loads successfully");
            var preview = await bm.PreviewCommissionsAsync(today.Year, today.Month, default); Check(preview.MissingRuleScopes.Count == 2, "missing commission rules are explicit");

            var incomeBeforeAdvance = summary.CompanyIncome;
            var revenueBeforeAdvance = summary.Revenue;
            var advancesBeforeIncomeAndRevenue = await bm.AdvancesAsync(new(EmployeeId: employee.Id), default);
            var additionalCompanyIncome = await Create("INCOME", 10000);
            await Post(additionalCompanyIncome);
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(summary.CompanyIncome == incomeBeforeAdvance + 10000 && summary.Revenue == revenueBeforeAdvance,
                "company income receipts change the net company income without affecting advances");
            var dashboard = await fm.GetDashboardAsync(default);
            var revenueBeforeManualRevenue = dashboard.RevenueMonthToDate;
            var revenueTransaction = await Create("REVENUE", 5000);
            await Post(revenueTransaction);
            Assert.Equal("REVENUE", await db.JournalEntries.Where(x => x.Id == revenueTransaction.JournalEntryId).Select(x => x.EntryType).SingleAsync());
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            dashboard = await fm.GetDashboardAsync(default);
            var advancesAfterIncomeAndRevenue = await bm.AdvancesAsync(new(EmployeeId: employee.Id), default);
            Check(summary.CompanyIncome == incomeBeforeAdvance + 15000 && summary.Revenue == revenueBeforeAdvance + 5000 &&
                  dashboard.RevenueMonthToDate == revenueBeforeManualRevenue + 5000 &&
                  advancesAfterIncomeAndRevenue.TotalPaid == advancesBeforeIncomeAndRevenue.TotalPaid &&
                  advancesAfterIncomeAndRevenue.TotalRemaining == advancesBeforeIncomeAndRevenue.TotalRemaining,
                "revenue changes only revenue and both income modules leave advances unchanged");
            await fr.ReverseJournalAsync(additionalCompanyIncome.JournalEntryId, new("test company income reversal"), default);
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            dashboard = await fm.GetDashboardAsync(default);
            Check(summary.CompanyIncome == incomeBeforeAdvance + 5000 && summary.Revenue == revenueBeforeAdvance + 5000 &&
                  dashboard.RevenueMonthToDate == revenueBeforeManualRevenue + 5000,
                "reversing company income changes only company income");
            await fr.ReverseJournalAsync(revenueTransaction.JournalEntryId, new("test revenue reversal"), default);
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            dashboard = await fm.GetDashboardAsync(default);
            Check(summary.CompanyIncome == incomeBeforeAdvance && summary.Revenue == revenueBeforeAdvance &&
                  dashboard.RevenueMonthToDate == revenueBeforeManualRevenue,
                "reversing revenue changes only revenue");
            async Task<AccountingAdvancesResultDto> GetEmployeeAdvanceTotals() =>
                await bm.AdvancesAsync(new(EmployeeId: employee.Id), default);
            var totalsBeforeAdvance = await GetEmployeeAdvanceTotals();
            var independentAdvance = await Create("ADVANCE", 10000);
            await Post(independentAdvance);
            async Task<AccountingAdvanceDto> GetIndependentAdvance() =>
                (await GetEmployeeAdvanceTotals()).Items.Single(x => x.Id == independentAdvance.Id);
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(summary.CompanyIncome == incomeBeforeAdvance && summary.Revenue == revenueBeforeAdvance, "creating an advance does not affect company income or revenue");
            var totalsAfterAdvance = await GetEmployeeAdvanceTotals();
            var advanceState = await GetIndependentAdvance();
            Check(advanceState.Repaid == 0 && advanceState.Remaining == 10000 && advanceState.Status == "OPEN" &&
                  totalsAfterAdvance.TotalPaid == totalsBeforeAdvance.TotalPaid &&
                  totalsAfterAdvance.TotalRemaining == totalsBeforeAdvance.TotalRemaining + 10000,
                "new advance balance and totals start independently");

            await Assert.ThrowsAsync<MIS.Application.Common.HrValidationException>(() =>
                Create("INCOME", 50, related: independentAdvance.Id));
            await db.AccountingBookEntries.Where(x => x.Id == income.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RelatedAdvanceId, independentAdvance.Id), default);
            var asOfAdvance = (await bm.AdvancesAsync(today, null, employee.Id, default)).Single(x => x.Id == independentAdvance.Id);
            Check((await GetIndependentAdvance()).Remaining == 10000 && asOfAdvance.Remaining == 10000,
                "legacy cross-linked income cannot change current or as-of advance balances");

            var independentPartialPayment = await Create("ADVANCE_REPAYMENT", 2000, related: independentAdvance.Id);
            await Post(independentPartialPayment);
            advanceState = await GetIndependentAdvance();
            var partialTotals = await GetEmployeeAdvanceTotals();
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(advanceState.Repaid == 2000 && advanceState.Remaining == 8000 && advanceState.Status == "PARTIAL" &&
                  partialTotals.TotalPaid == totalsAfterAdvance.TotalPaid + 2000 &&
                  partialTotals.TotalRemaining == totalsAfterAdvance.TotalRemaining - 2000 &&
                  summary.CompanyIncome == incomeBeforeAdvance && summary.Revenue == revenueBeforeAdvance,
                "partial repayment updates only the advance balance, not company income or revenue");

            var independentFinalPayment = await Create("ADVANCE_REPAYMENT", 8000, related: independentAdvance.Id);
            await Post(independentFinalPayment);
            advanceState = await GetIndependentAdvance();
            var settledTotals = await GetEmployeeAdvanceTotals();
            var advanceDetails = await bm.AdvanceDetailsAsync(independentAdvance.Id, default);
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(advanceState.Repaid == 10000 && advanceState.Remaining == 0 && advanceState.Status == "SETTLED" &&
                  settledTotals.TotalPaid == totalsAfterAdvance.TotalPaid + 10000 &&
                  settledTotals.TotalRemaining == totalsAfterAdvance.TotalRemaining &&
                  advanceDetails.Payments.Count == 2 && summary.CompanyIncome == incomeBeforeAdvance && summary.Revenue == revenueBeforeAdvance,
                "full repayment settles the advance while preserving payment history and both income totals");

            await fr.ReverseJournalAsync(independentFinalPayment.JournalEntryId, new("test repayment reversal"), default);
            advanceState = await GetIndependentAdvance();
            var reversedTotals = await GetEmployeeAdvanceTotals();
            advanceDetails = await bm.AdvanceDetailsAsync(independentAdvance.Id, default);
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(advanceState.Repaid == 2000 && advanceState.Remaining == 8000 &&
                  reversedTotals.TotalPaid == totalsBeforeAdvance.TotalPaid + 2000 &&
                  reversedTotals.TotalRemaining == totalsBeforeAdvance.TotalRemaining + 8000 &&
                  summary.CompanyIncome == incomeBeforeAdvance && summary.Revenue == revenueBeforeAdvance,
                "reversing an advance payment restores its balance without changing company income or revenue");
            Check(advanceDetails.Payments.Count == 2 && advanceDetails.Payments.Single(x => x.Id == independentFinalPayment.Id).Status == "REVERSED",
                "reversed payments remain visible in advance history");

            var addedIncome = await Create("INCOME", 300);
            await Post(addedIncome);
            advanceState = await GetIndependentAdvance();
            var totalsAfterIncome = await GetEmployeeAdvanceTotals();
            summary = await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default);
            Check(summary.CompanyIncome == incomeBeforeAdvance + 300 && summary.Revenue == revenueBeforeAdvance &&
                  advanceState.Repaid == 2000 && advanceState.Remaining == 8000,
                "company income changes do not affect employee advances or settlements");
            Check(totalsAfterIncome.TotalPaid == reversedTotals.TotalPaid && totalsAfterIncome.TotalRemaining == reversedTotals.TotalRemaining,
                "company income leaves advance totals unchanged");

            var custom = await fm.SaveAccountAsync(null, new("610999", "Test expense", "Test expense", "EXPENSE", "DEBIT", true, null, true, false), default);
            Check(accounts.All(x => x.Id != custom.Id), "custom chart account created");
            var trial = await fm.GetTrialBalanceAsync(today, default); Check(trial.TotalDebit == trial.TotalCredit, "trial balance remains balanced");
            var payrollPeriod = new AccountingPayrollPeriod(today.Year, today.Month, maker.Id, now);
            var salary = new AccountingEmployeePayroll(payrollPeriod.Id, employee.Id, employee.EmployeeNumber, employee.FullName, department.Name, null, 500, 0, null, now);
            salary.ApplyComponents(20, 10, 5, 0, 0, null, now);
            db.AccountingPayrollPeriods.Add(payrollPeriod); db.AccountingEmployeePayrolls.Add(salary); await db.SaveChangesAsync();
            var accounting = new AccountingService(db, new BookTestPrincipal(maker.Id), null!, new HrAuditService(db, new BookTestPrincipal(maker.Id)), null!, bm);
            var companyIncomeBeforePayrollEdit = (await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default)).CompanyIncome;
            var editedPayroll = await accounting.UpdatePayrollAsync(salary.Id, new(30, 10, 5, 0, 0, "Edited before payment"), default);
            Check(editedPayroll.NetSalary == 545 && (await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default)).CompanyIncome == companyIncomeBeforePayrollEdit,
                "editing an unposted normal transaction has no duplicate or premature company income effect");
            await accounting.RecordPayrollAsync(salary.Id, new(30, 10, 5, 0, 0, "Confirmed edited components"), default);
            var salaryEntry = await db.AccountingBookEntries.Include(x => x.JournalEntry).SingleAsync(x => x.SourceKey == $"PAYROLL:{salary.Id}");
            Check(salary.Status == "PAID" && salaryEntry.JournalEntry.Status == "POSTED" && salaryEntry.Amount == 545, "actual payroll payment and journal persist together");
            Check((await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default)).CompanyIncome == companyIncomeBeforePayrollEdit - 545 &&
                  await db.AccountingBookEntries.CountAsync(x => x.SourceKey == $"PAYROLL:{salary.Id}") == 1,
                "edited payroll decreases company income once when paid");
            var companyIncomeBeforeCancel = (await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default)).CompanyIncome;
            var cancelledJournal = new JournalEntry(MIS.Infrastructure.Persistence.Configurations.FinanceSeed.LegalEntityId,
                payrollPeriod.Id, null, "ACCOUNTING_OPERATION", today, today, "EGP", "Cancelled before posting", maker.Id, now);
            cancelledJournal.AddLine(Account("610100"), 10, 0, 10, 0, 1, "Cancelled before posting");
            cancelledJournal.AddLine(Account("110300"), 0, 10, 0, 10, 1, "Cancelled before posting");
            var cancelledEntry = new AccountingBookEntry(Guid.NewGuid(), "OTHER", today, 10, Account("610100"), Account("110300"),
                "Cancelled before posting", null, cancelledJournal.Id, maker.Id, now);
            db.JournalEntries.Add(cancelledJournal); db.AccountingBookEntries.Add(cancelledEntry); await db.SaveChangesAsync();
            await bm.TransitionAsync(cancelledEntry.Id, "cancel", "Not needed", default);
            Check((await bm.SummaryAsync(new(today.Year, today.Month, 1), today, default)).CompanyIncome == companyIncomeBeforeCancel &&
                  (await db.JournalEntries.SingleAsync(x => x.Id == cancelledJournal.Id)).Status == "CANCELLED",
                "cancelling an unposted normal transaction has no company income effect");
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
            var employee3 = new Employee("CODEX-EMP3", "Third test employee", department.Id, true, now);
            var employee4 = new Employee("CODEX-EMP4", "Fourth test employee", department.Id, true, now);
            db.Employees.AddRange(employee3, employee4);
            var salary3 = new AccountingEmployeePayroll(payrollPeriod.Id, employee3.Id, employee3.EmployeeNumber, employee3.FullName, department.Name, null, 400, 0, null, now);
            var salary4 = new AccountingEmployeePayroll(payrollPeriod.Id, employee4.Id, employee4.EmployeeNumber, employee4.FullName, department.Name, null, 450, 0, null, now);
            var nextMonth = today.AddMonths(1);
            var nextPayrollPeriod = new AccountingPayrollPeriod(nextMonth.Year, nextMonth.Month, maker.Id, now);
            var nextMonthSalary = new AccountingEmployeePayroll(nextPayrollPeriod.Id, employee3.Id, employee3.EmployeeNumber, employee3.FullName, department.Name, null, 400, 0, null, now);
            db.AccountingPayrollPeriods.Add(nextPayrollPeriod);
            db.AccountingEmployeePayrolls.AddRange(salary3, salary4, nextMonthSalary); await db.SaveChangesAsync();
            var suspendedRows = await accounting.BulkChangeEmployeePayrollStatusAsync(new([salary3.Id, salary4.Id], "SUSPENDED", "Payroll review"), default);
            Check(suspendedRows.All(x => x.EmployeePayrollStatus == "SUSPENDED" && x.PaymentStatus == "NOT_PAYABLE"), "bulk employee status change is period-scoped and audited");
            Check(nextMonthSalary.EmployeePayrollStatus == "ACTIVE", "employee status does not carry over to another payroll month");
            await Conflict(() => accounting.PayPayrollAsync(salary3.Id, default), "suspended salary payment is rejected by the backend");
            await Conflict(() => accounting.BulkPayPayrollsAsync(new([salary3.Id, salary4.Id]), default), "bulk salary payment validates all employees before paying any");
            Check(salary3.Status == "DRAFT" && salary4.Status == "DRAFT", "failed bulk salary payment leaves every salary unpaid");
            var restored = await accounting.ChangeEmployeePayrollStatusAsync(salary3.Id, new("ACTIVE", null), default);
            Check(restored.PaymentStatus == "READY" && salary4.EmployeePayrollStatus == "SUSPENDED", "individual status restoration does not change unselected employees");
            Check((await accounting.PayPayrollAsync(salary3.Id, default)).PaymentStatus == "PAID", "restored active employee can be paid");
            var legacyClaim = new AccountingTransportationClaim(employee2.Id, today, 15, "Historical unlinked claim", maker.Id, now);
            legacyClaim.Approve(maker.Id, now); legacyClaim.MarkPaid(now);
            db.AccountingTransportationClaims.Add(legacyClaim); await db.SaveChangesAsync();
            var detailedAdvance = await bm.CreateAsync(new("ADVANCE", today, 300, Account("113100"), Account("110300"), "Test advance reason", "ADV-DETAIL", employee.Id, null, Guid.NewGuid(), "BANK_TRANSFER", "Advance notes"), default);
            var partialPayment = await bm.CreateAsync(new("ADVANCE_REPAYMENT", today, 100, Account("113100"), Account("110300"), "First repayment", "PAY-001", employee.Id, detailedAdvance.Id, Guid.NewGuid(), "CASH", "Payment notes"), default);
            var secondAdvance = await bm.CreateAsync(new("ADVANCE", today, 70, Account("113100"), Account("110300"), "Independent advance", null, employee.Id, null, Guid.NewGuid(), "CASH", null), default);
            var partialDetails = await bm.AdvanceDetailsAsync(detailedAdvance.Id, default);
            Check(partialDetails.Advance.Status == "PARTIAL" && partialDetails.Advance.Remaining == 200 && partialDetails.Advance.Reason == "Test advance reason", "advance details calculate partial status and preserve reason");
            Check(partialDetails.Advance.PaymentMethod == "BANK_TRANSFER" && partialDetails.Advance.Notes == "Advance notes" && partialDetails.Payments.Single().PaymentMethod == "CASH" && partialDetails.Payments.Single().Notes == "Payment notes", "advance and payment metadata persist separately");
            Check(partialDetails.Payments.Single().RemainingAfter == 200 && partialDetails.EmployeeTotalOutstanding == 470, "payment history running balance and employee total outstanding are correct");
            Check((await bm.AdvanceDetailsAsync(secondAdvance.Id, default)).Advance.Remaining == 70, "multiple advances remain independent");
            var finalPayment = await bm.CreateAsync(new("ADVANCE_REPAYMENT", today, 200, Account("113100"), Account("110300"), "Final repayment", null, employee.Id, detailedAdvance.Id, Guid.NewGuid(), "BANK_TRANSFER", null), default);
            var settledDetails = await bm.AdvanceDetailsAsync(detailedAdvance.Id, default);
            Check(settledDetails.Advance.Status == "SETTLED" && settledDetails.Advance.Remaining == 0 && settledDetails.Payments.Last().RemainingAfter == 0, "final repayment settles the advance");
            Check(settledDetails.EmployeeTotalOutstanding == 270 && (await bm.AdvanceDetailsAsync(secondAdvance.Id, default)).Advance.Remaining == 70, "settling one advance does not change another advance balance");
            await Conflict(() => bm.CreateAsync(new("ADVANCE_REPAYMENT", today, 0.01m, Account("113100"), Account("110300"), "Overpayment", null, employee.Id, detailedAdvance.Id, Guid.NewGuid(), "CASH"), default), "overpayment after settlement is rejected");
            var filteredAdvances = await bm.AdvancesAsync(new(today, today, "Test employee", "CODEX-EMP", department.Id, "SETTLED"), default);
            Check(filteredAdvances.Items.Count == 1 && filteredAdvances.TotalAmount == 300 && filteredAdvances.TotalPaid == 300 && filteredAdvances.TotalRemaining == 0 && filteredAdvances.OpenCount == 0, "advance filters and backend dashboard totals match");
            using (var advanceExport = new XLWorkbook(new MemoryStream(await bm.ExportAsync("advances", new(From: today, To: today, Status: "SETTLED", DepartmentId: department.Id, EmployeeName: "Test employee", EmployeeNumber: "CODEX-EMP"), default))))
            {
                var sheet = advanceExport.Worksheets.Single();
                Check(sheet.Cell(7, 4).GetString() == detailedAdvance.DocumentNumber
                    && sheet.Cell(8, 1).GetString() is "TOTAL" or "الإجمالي"
                    && sheet.Cell(8, 6).GetValue<double>() == 300
                    && sheet.Cell(8, 7).GetValue<double>() == 300
                    && sheet.Cell(8, 8).GetValue<double>() == 0,
                    "filtered advances export includes the matching transaction and correctly calculated totals");
            }
            await bm.DeleteAsync(finalPayment.Id, new(true, "Test cleanup"), default);
            await bm.DeleteAsync(partialPayment.Id, new(true, "Test cleanup"), default);
            await bm.DeleteAsync(detailedAdvance.Id, new(true, "Test cleanup"), default);
            await bm.DeleteAsync(secondAdvance.Id, new(true, "Test cleanup"), default);
            var claimService = new FinanceClaimsService(db, new BookTestPrincipal(maker.Id), bm, fm);
            var claimBank = new ClientOrganization("CLAIM-FILTER", "بنك اختبار المطالبات", "Claims Filter Bank", "BANK", DateTimeOffset.UtcNow);
            db.CollectionClientOrganizations.Add(claimBank);
            await db.SaveChangesAsync();
            var organizationClaim = await claimService.CreateAsync(
                new SaveFinanceClaimRequest("CLAIM-ORG", claimBank.Id, claimBank.NameEnglish, "Monthly", today.ToString("yyyy-MM"), today, 100m, 14m, 2m), default);
            var organizationFilteredClaims = await claimService.ListAsync(new FinanceClaimFilter(
                Search: "CLAIM-ORG", BankOrganizationId: claimBank.Id, Status: "ACTIVE", PageSize: 1), default);
            Check(organizationFilteredClaims.TotalCount == 1 && organizationFilteredClaims.Items.Single().Id == organizationClaim.Id,
                "organization, search, and active status filters compose before pagination");
            var claimRangeFrom = new DateOnly(today.Year, today.Month, 1);
            var incomeBeforeClaim = await bm.SummaryAsync(claimRangeFrom, today, default);
            var profitLossBeforeClaim = await bm.ProfitLossAsync(claimRangeFrom, today, default);
            var advancesBeforeClaim = (await bm.AdvancesAsync(today, null, null, default)).Sum(x => x.Remaining);
            var claimInput = new SaveFinanceClaimRequest("CLAIM-1", null, "Example Bank", "Monthly", today.ToString("yyyy-MM"),
                today, 100000m, 14m, 3m);
            var pendingCancellation = await claimService.CreateAsync(claimInput with { Serial = "CLAIM-CANCEL" }, default);
            await claimService.CancelAsync(pendingCancellation.Id, "Pending claim cancelled in integration test", default);
            Check((await bm.SummaryAsync(claimRangeFrom, today, default)).CompanyIncome == incomeBeforeClaim.CompanyIncome &&
                  (await claimService.DetailsAsync(pendingCancellation.Id, default)).Claim.Status == "CANCELLED",
                "cancelling a pending claim does not change Company Income");
            var cancelledSearch = await claimService.ListAsync(new FinanceClaimFilter(Search: "CLAIM-CANCEL", Status: "CANCELLED", PageSize: 1), default);
            var activeSearch = await claimService.ListAsync(new FinanceClaimFilter(Search: "CLAIM-CANCEL", Status: "ACTIVE", PageSize: 1), default);
            var allSearch = await claimService.ListAsync(new FinanceClaimFilter(Search: "CLAIM-CANCEL", Status: "ALL", PageSize: 1), default);
            Check(cancelledSearch.TotalCount == 1 && cancelledSearch.Items.Single().Status == "CANCELLED" && activeSearch.TotalCount == 0 && allSearch.TotalCount == 1,
                "active, cancelled, and all claim filters apply search before pagination");
            await claimService.ReactivateAsync(pendingCancellation.Id, default);
            Check((await claimService.ListAsync(new FinanceClaimFilter(Search: "CLAIM-CANCEL", Status: "ACTIVE"), default)).TotalCount == 1 &&
                  (await claimService.ListAsync(new FinanceClaimFilter(Search: "CLAIM-CANCEL", Status: "CANCELLED"), default)).TotalCount == 0,
                "reactivating a claim returns it to Active Claims");
            await Conflict(() => claimService.ReactivateAsync(pendingCancellation.Id, default), "duplicate claim reactivation is rejected");
            await Conflict(() => claimService.CancelAsync(pendingCancellation.Id, "repeat", default), "duplicate claim cancellation is rejected");
            var financialClaim = await claimService.CreateAsync(claimInput, default);
            var pendingClaims = await claimService.ListAsync(new FinanceClaimFilter(Search: "CLAIM-1", Status: "ACTIVE", Page: 1, PageSize: 1), default);
            var allClaimsPage1 = await claimService.ListAsync(new FinanceClaimFilter(Search: "Example Bank", Status: "ALL", Page: 1, PageSize: 1), default);
            var allClaimsPage2 = await claimService.ListAsync(new FinanceClaimFilter(Search: "Example Bank", Status: "ALL", Page: 2, PageSize: 1), default);
            Check(pendingClaims.TotalCount == 1 && pendingClaims.Items.Single().Id == financialClaim.Id && pendingClaims.Items.Single().Status == "PENDING" &&
                  allClaimsPage1.TotalCount > 1 && allClaimsPage1.Items.Count == 1 && allClaimsPage2.Items.Count == 1 && allClaimsPage1.Items[0].Id != allClaimsPage2.Items[0].Id,
                "legacy pending claims remain visible and ALL status search paginates consistently");
            Check(financialClaim.Vat == 14000m && financialClaim.TotalWithVat == 114000m && financialClaim.TaxAtSource == 3000m && financialClaim.TotalInBank == 111000m,
                "claim amounts are calculated from the pre-VAT base");
            Check((await bm.SummaryAsync(claimRangeFrom, today, default)).CompanyIncome == incomeBeforeClaim.CompanyIncome,
                "creating a pending claim does not change Company Income");
            await claimService.UpdateAsync(financialClaim.Id, claimInput with { Month = "Edited month" }, default);
            Check((await bm.SummaryAsync(claimRangeFrom, today, default)).CompanyIncome == incomeBeforeClaim.CompanyIncome,
                "editing a pending claim does not change Company Income");
            await claimService.ConfirmReceiptAsync(financialClaim.Id, new(today), default);
            await claimService.ConfirmReceiptAsync(financialClaim.Id, new(today), default);
            Check(await db.AccountingBookEntries.CountAsync(x => x.SourceKey != null && x.SourceKey.StartsWith($"CLAIM:{financialClaim.Id}:")) == 1,
                "repeated claim receipt confirmation creates one accounting entry");
            Check((await bm.SummaryAsync(claimRangeFrom, today, default)).CompanyIncome == incomeBeforeClaim.CompanyIncome + 111000m,
                "confirmed claim posts only net Total In Bank to Company Income");
            var postedEntryBeforeCancel = await db.AccountingBookEntries.SingleAsync(x => x.SourceKey != null && x.SourceKey.StartsWith($"CLAIM:{financialClaim.Id}:") && x.DeletedAt == null);
            await Conflict(() => claimService.CancelAsync(financialClaim.Id, "Unsafe cancellation test", default), "claim with active posted receipt cannot be cancelled");
            Check((await db.AccountingBookEntries.SingleAsync(x => x.Id == postedEntryBeforeCancel.Id)).DeletedAt == null &&
                  (await bm.SummaryAsync(claimRangeFrom, today, default)).CompanyIncome == incomeBeforeClaim.CompanyIncome + 111000m,
                "rejected claim cancellation leaves posted accounting entry and Company Income unchanged");
            var profitLossAfterClaimReceipt = await bm.ProfitLossAsync(claimRangeFrom, today, default);
            Check(profitLossAfterClaimReceipt.TotalRevenue == profitLossBeforeClaim.TotalRevenue &&
                  profitLossAfterClaimReceipt.TotalExpenses == profitLossBeforeClaim.TotalExpenses,
                "claim receipt is not duplicated as profit and loss revenue");
            Check((await bm.AdvancesAsync(today, null, null, default)).Sum(x => x.Remaining) == advancesBeforeClaim,
                "claim receipt does not change employee advances");
            await claimService.UpdateAsync(financialClaim.Id, claimInput with { Month = "Edited month", AmountWithoutVat = 90000m }, default);
            Check((await bm.SummaryAsync(claimRangeFrom, today, default)).CompanyIncome == incomeBeforeClaim.CompanyIncome,
                "editing a received claim reverses its prior posting before updating");
            await claimService.ConfirmReceiptAsync(financialClaim.Id, new(today), default);
            Check((await bm.SummaryAsync(claimRangeFrom, today, default)).CompanyIncome == incomeBeforeClaim.CompanyIncome + 99900m &&
                  await db.AccountingBookEntries.CountAsync(x => x.SourceKey != null && x.SourceKey.StartsWith($"CLAIM:{financialClaim.Id}:")) == 2,
                "reconfirming an edited claim posts the revised net amount without deleting the original posting");
            await claimService.ReverseReceiptAsync(financialClaim.Id, new("Integration test reversal"), default);
            Check((await bm.SummaryAsync(claimRangeFrom, today, default)).CompanyIncome == incomeBeforeClaim.CompanyIncome,
                "claim receipt reversal removes its Company Income effect exactly once");
            await Conflict(() => claimService.ReverseReceiptAsync(financialClaim.Id, new("Duplicate reversal"), default),
                "a claim receipt cannot be reversed twice");
            var profitLossAfterClaimReversal = await bm.ProfitLossAsync(claimRangeFrom, today, default);
            Check(profitLossAfterClaimReversal.TotalRevenue == profitLossBeforeClaim.TotalRevenue &&
                  profitLossAfterClaimReversal.TotalExpenses == profitLossBeforeClaim.TotalExpenses,
                "claim receipt reversals leave profit and loss unchanged");

            using var claimsWorkbook = new XLWorkbook();
            var claimSheet = claimsWorkbook.Worksheets.Add("Claims");
            var claimHeaders = new[] { "S", "Bank Name", "The type of Claim", "Month", "Date of claim", "total claim without vat", "Vat%", "Vat", "total claim with vat", "Tax at source %", "Tax at source", "Total", "Total in bank", "Date In Bank" };
            for (var index = 0; index < claimHeaders.Length; index++) claimSheet.Cell(1, index + 1).Value = claimHeaders[index];
            object?[] claimValues = ["HIST-1", "Example Bank", "Historical", today.ToString("yyyy-MM"), today.ToDateTime(TimeOnly.MinValue), 1000d, 14d, 140d, 1140d, 3d, 30d, 1110d, 1111d, today.ToDateTime(TimeOnly.MinValue)];
            for (var index = 0; index < claimValues.Length; index++) claimSheet.Cell(2, index + 1).Value = XLCellValue.FromObject(claimValues[index]);
            using var claimsWorkbookStream = new MemoryStream(); claimsWorkbook.SaveAs(claimsWorkbookStream); claimsWorkbookStream.Position = 0;
            var claimPreview = await claimService.PreviewImportAsync("claims.xlsx", claimsWorkbookStream, default);
            Check(claimPreview.ValidRows == 1 && claimPreview.Rows.Single().CalculatedTotalInBank == 1110m &&
                  claimPreview.Rows.Single().Warnings.Count > 0,
                "Claims Excel preview validates and recalculates net bank amount");
            var claimImport = await claimService.ConfirmImportAsync(new(claimPreview.PreviewId), default);
            Check(claimImport.Imported == 1, "Claims Excel import stores the valid historical row");
            var historicalClaim = await db.FinanceClaims.SingleAsync(x => x.Serial == "HIST-1");
            Check(historicalClaim.Status == "RECEIVED" && !await db.AccountingBookEntries.AnyAsync(x => x.SourceKey != null && x.SourceKey.StartsWith($"CLAIM:{historicalClaim.Id}:")),
                "historical imported bank receipt is not silently posted");
            Check((await claimService.ListAsync(new FinanceClaimFilter(Search: "HIST-1", Status: "ACTIVE"), default)).Items.Single().Status == "RECEIVED" &&
                  (await claimService.ListAsync(new FinanceClaimFilter(Search: "HIST-1", Status: "ALL"), default)).Items.Single().Status == "RECEIVED",
                "legacy received claims remain visible in ACTIVE and ALL filters");
            Check((await bm.SummaryAsync(claimRangeFrom, today, default)).CompanyIncome == incomeBeforeClaim.CompanyIncome,
                "historical imported receipt leaves Company Income unchanged");
            var profitLossBeforeHistoricalClaim = await bm.ProfitLossAsync(claimRangeFrom, today, default);
            await claimService.PostHistoricalReceiptAsync(historicalClaim.Id, default);
            await claimService.PostHistoricalReceiptAsync(historicalClaim.Id, default);
            Check((await bm.SummaryAsync(claimRangeFrom, today, default)).CompanyIncome == incomeBeforeClaim.CompanyIncome + 1110m,
                "explicit historical posting is idempotent and uses the recalculated net amount");
            var profitLossAfterHistoricalClaim = await bm.ProfitLossAsync(claimRangeFrom, today, default);
            Check(profitLossAfterHistoricalClaim.TotalRevenue == profitLossBeforeHistoricalClaim.TotalRevenue &&
                  profitLossAfterHistoricalClaim.TotalExpenses == profitLossBeforeHistoricalClaim.TotalExpenses,
                "historical claim posting does not create profit and loss revenue");
            await claimService.ReverseReceiptAsync(historicalClaim.Id, new("Historical test reversal"), default);
            Check((await bm.SummaryAsync(claimRangeFrom, today, default)).CompanyIncome == incomeBeforeClaim.CompanyIncome,
                "reversing explicit historical posting restores Company Income");
            Check((await bm.AdvancesAsync(today, null, null, default)).Sum(x => x.Remaining) == advancesBeforeClaim,
                "historical claim posting and reversal leave advances unchanged");
            using (var claimsExport = new XLWorkbook(new MemoryStream(await claimService.ExportAsync(new FinanceClaimFilter(), default))))
                Check(claimsExport.Worksheets.Single().ColumnsUsed().Count() == 14,
                    "Claims Excel export contains exactly the fourteen business columns");
            var reportFrom = new DateOnly(today.Year, today.Month, 1);
            var profitLossBaseline = await bm.ProfitLossAsync(reportFrom, today, default);
            var profitIncome = await Create("REVENUE", 100000m); await Post(profitIncome);
            var profitExpense = await Create("PETTY", 40000m); await Post(profitExpense);
            var profitResult = await bm.ProfitLossAsync(reportFrom, today, default);
            Check(profitResult.TotalRevenue - profitLossBaseline.TotalRevenue == 100000m &&
                  profitResult.TotalExpenses - profitLossBaseline.TotalExpenses == 40000m &&
                  profitResult.NetProfitLoss - profitLossBaseline.NetProfitLoss == 60000m,
                "profit and loss reports a 100000 revenue less 40000 expense as 60000 net profit");
            var lossRevenue = await Create("REVENUE", 50000m); await Post(lossRevenue);
            var lossExpense = await Create("OTHER", 80000m); await Post(lossExpense);
            var lossResult = await bm.ProfitLossAsync(reportFrom, today, default);
            Check(lossResult.TotalRevenue - profitResult.TotalRevenue == 50000m &&
                  lossResult.TotalExpenses - profitResult.TotalExpenses == 80000m &&
                  lossResult.NetProfitLoss - profitResult.NetProfitLoss == -30000m,
                "profit and loss reports a 50000 revenue less 80000 expense as a 30000 net loss");
            Check(lossResult.RevenueCategories.Single(x => x.Category == "REVENUE").Transactions.Count(x => x.JournalNumber == profitIncome.JournalNumber) == 1,
                "a posted revenue journal is counted exactly once");
            await fr.ReverseJournalAsync(lossExpense.JournalEntryId, new("P&L test reversal"), default);
            var reversedLoss = await bm.ProfitLossAsync(reportFrom, today, default);
            Check(lossResult.TotalExpenses - reversedLoss.TotalExpenses == 80000m,
                "reversing a posted expense removes its profit and loss effect");
            var canceledJournal = new JournalEntry(MIS.Infrastructure.Persistence.Configurations.FinanceSeed.LegalEntityId,
                (await fm.GetPeriodsAsync(today.Year, default)).Single(x => x.PeriodNumber == today.Month).Id, null, "MANUAL",
                today, today, "EGP", "Cancelled P&L test", maker.Id, now);
            canceledJournal.AddLine(Account("110300"), 123m, 0, 123m, 0, 1, "Cancelled P&L test");
            canceledJournal.AddLine(Account("410100"), 0, 123m, 0, 123m, 1, "Cancelled P&L test");
            canceledJournal.Cancel(); db.JournalEntries.Add(canceledJournal); await db.SaveChangesAsync();
            var afterCancellation = await bm.ProfitLossAsync(reportFrom, today, default);
            Check(afterCancellation.TotalRevenue == reversedLoss.TotalRevenue && afterCancellation.TotalExpenses == reversedLoss.TotalExpenses,
                "cancelled journals do not affect profit and loss");
            var outsideDate = reportFrom.AddDays(-1);
            var previousPeriodRevenue = await Create("REVENUE", 700m, outsideDate); await Post(previousPeriodRevenue);
            var currentRangeAfterPriorRevenue = await bm.ProfitLossAsync(reportFrom, today, default);
            var priorDayReport = await bm.ProfitLossAsync(outsideDate, outsideDate, default);
            Check(currentRangeAfterPriorRevenue.TotalRevenue == afterCancellation.TotalRevenue &&
                  priorDayReport.TotalRevenue == 700m && priorDayReport.TotalExpenses == 0,
                "profit and loss date filters include only posted transactions in the selected range");
            var beforeCashReceipt = await bm.ProfitLossAsync(reportFrom, today, default);
            var pAndLCashReceipt = await Create("INCOME", 250000m); await Post(pAndLCashReceipt);
            var afterCashReceipt = await bm.ProfitLossAsync(reportFrom, today, default);
            Check(afterCashReceipt.TotalRevenue == beforeCashReceipt.TotalRevenue &&
                  afterCashReceipt.TotalExpenses == beforeCashReceipt.TotalExpenses &&
                  afterCashReceipt.NetProfitLoss == beforeCashReceipt.NetProfitLoss,
                "Company Income cash movements do not affect profit and loss");
            var beforeAdvance = await bm.ProfitLossAsync(reportFrom, today, default);
            var pAndLAdvance = await Create("ADVANCE", 500m); await Post(pAndLAdvance);
            var afterAdvance = await bm.ProfitLossAsync(reportFrom, today, default);
            var pAndLRepayment = await Create("ADVANCE_REPAYMENT", 200m, related: pAndLAdvance.Id); await Post(pAndLRepayment);
            var afterRepayment = await bm.ProfitLossAsync(reportFrom, today, default);
            Check(afterAdvance.TotalRevenue == beforeAdvance.TotalRevenue && afterAdvance.TotalExpenses == beforeAdvance.TotalExpenses &&
                  afterRepayment.TotalRevenue == beforeAdvance.TotalRevenue && afterRepayment.TotalExpenses == beforeAdvance.TotalExpenses,
                "employee advance creation and repayment do not affect profit and loss");
            using (var profitLossExport = new XLWorkbook(new MemoryStream(await bm.ExportAsync("profit-loss", new(From: reportFrom, To: today), default))))
                Check(profitLossExport.Worksheets.Count == 2 && profitLossExport.Worksheets.First().RowsUsed().Count() == 8 &&
                      profitLossExport.Worksheets.Last().Cell(6, 1).GetString() is "Type" or "النوع",
                    "profit and loss Excel export contains summary and transaction detail sheets");
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
