using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using ClosedXML.Excel;
using MIS.Application.DTOs.Accounting;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class AccountingBookTests
{
    [Fact]
    public void ConfirmedAccountingRecordsByMakerWhileAdvancedManualReviewRemainsProtected()
    {
        var maker = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        JournalEntry Journal(string type)
        {
            var journal = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), null, type, new(2026, 10, 16), new(2026, 10, 16), "EGP", "Expense", maker, now);
            journal.AddLine(Guid.NewGuid(), 400, 0, 400, 0, 1, "Expense");
            journal.AddLine(Guid.NewGuid(), 0, 400, 0, 400, 1, "Cash");
            return journal;
        }
        var entry = Journal("ACCOUNTING_OPERATION");
        entry.RecordConfirmedAccounting("ACC-TEST", maker, now);
        Assert.Equal("POSTED", entry.Status); Assert.Equal(maker, entry.PostedById);
        Assert.Throws<InvalidOperationException>(() => entry.RecordConfirmedAccounting("DUPLICATE", maker, now));
        var manual = Journal("MANUAL"); manual.Submit();
        Assert.Throws<InvalidOperationException>(() => manual.Approve(maker, now));
        Assert.Throws<InvalidOperationException>(() => Journal("COLLECTION").RecordConfirmedAccounting("ACC-TEST", maker, now));
    }

    [Fact]
    public void RecurringDeletionRetiresGenerationKeyAndPreservesOriginalFinancialValues()
    {
        var id = Guid.NewGuid(); var maker = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var key = $"RECURRING:{Guid.NewGuid()}:202610";
        var entry = new AccountingBookEntry(id, "FIXED", new(2026, 10, 16), 400, Guid.NewGuid(), Guid.NewGuid(), "Rent", null, Guid.NewGuid(), maker, now, sourceKey: key);
        entry.Delete(maker, now); entry.Delete(Guid.NewGuid(), now.AddDays(1));
        Assert.Equal(now, entry.DeletedAt); Assert.Equal(maker, entry.DeletedById);
        Assert.Equal($"DELETED:{id}:{key}", entry.SourceKey);
        Assert.Equal(400, entry.Amount); Assert.Equal("Rent", entry.Description);
    }

    [Fact]
    public void DeletedSalaryCannotBeSilentlyEditedOrPaidAgain()
    {
        var now = DateTimeOffset.UtcNow;
        var salary = new AccountingEmployeePayroll(Guid.NewGuid(), Guid.NewGuid(), "EMP-1", "Employee", null, null, 500, 0, null, now);
        salary.DeleteAfterConfirmation(now);
        Assert.Throws<InvalidOperationException>(() => salary.ApplyComponents(0, 0, 0, 0, 0, null, now));
        Assert.Throws<InvalidOperationException>(() => salary.MarkPaid(now));
        Assert.Throws<InvalidOperationException>(() => salary.Approve(now));
    }

    private static AccountingRecurringExpense Template(int day = 31, DateOnly? end = null) => new("Rent", 400m, day, Guid.NewGuid(), Guid.NewGuid(), new(2024, 1, 1), end, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2025, 2, 28)]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 10, 31)]
    public void RecurringExpenses_ClampDueDayToLastDayOfMonth(int year, int month, int day) => Assert.Equal(new DateOnly(year, month, day), Template().DueDate(year, month));
    [Fact]
    public void RecurringExpenses_RespectScheduleAndActiveFlag()
    {
        var row = Template(end: new(2026, 9, 30));
        Assert.Null(row.DueDate(2023, 12)); Assert.Null(row.DueDate(2026, 10)); Assert.NotNull(row.DueDate(2026, 9));
        row.Update(row.Name, row.Amount, row.DueDay, row.AccountId, row.CashAccountId, row.StartsOn, row.EndsOn, null, false);
        Assert.Null(row.DueDate(2026, 9));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    public void Entries_RejectInvalidCurrencyAmounts(decimal amount) => Assert.Throws<ArgumentOutOfRangeException>(() => Entry("PETTY", amount));
    private static AccountingBookEntry Entry(string category, decimal amount = 400m, Guid? employee = null, Guid? related = null) => new(Guid.NewGuid(), category, new(2026, 10, 16), amount, Guid.NewGuid(), Guid.NewGuid(), "Description", null, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, employee, related);
    [Fact]
    public void Advances_RequireEmployee_AndRepaymentsRequireOriginalAdvance()
    {
        Assert.Throws<ArgumentException>(() => Entry("ADVANCE")); Assert.Throws<ArgumentException>(() => Entry("CARRIED_ADVANCE"));
        Assert.Throws<ArgumentException>(() => Entry("ADVANCE_REPAYMENT", employee: Guid.NewGuid()));
        Assert.NotNull(Entry("ADVANCE_REPAYMENT", employee: Guid.NewGuid(), related: Guid.NewGuid()));
    }
    [Fact]
    public void OpeningAdvances_DoNotCreateCashFlowOrExpense()
    {
        Assert.False(AccountingBookValues.IsCashIn("CARRIED_ADVANCE")); Assert.False(AccountingBookValues.IsCashOut("CARRIED_ADVANCE"));
        Assert.Equal("OPENING_ADVANCE", AccountingBookValues.Direction("CARRIED_ADVANCE"));
        Assert.Equal("ASSET", AccountingBookValues.AccountType("ADVANCE_REPAYMENT"));
        Assert.Equal("ADVANCE_REPAYMENT", AccountingBookValues.Direction("ADVANCE_REPAYMENT"));
    }
    [Theory]
    [InlineData("ar-EG")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void MonetaryRequests_ValidateIndependentlyOfUiCulture(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new(culture, false);
            foreach (var type in new[] { typeof(CreateAccountingBookEntryRequest), typeof(SaveRecurringExpenseRequest) })
            {
                var range = type.GetConstructors().Single().GetParameters().Single(x => x.Name == "Amount").GetCustomAttribute<RangeAttribute>()!;
                Assert.True(range.IsValid(400.25m)); Assert.True(range.IsValid("400.25")); Assert.False(range.IsValid(0m));
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
    [Fact]
    public void ExcelExport_PreservesNumericDatesAndLiteralUntrustedText()
    {
        var data = AccountingBookService.Spreadsheet([("تقرير", ["البيان", "المبلغ", "التاريخ"], [new object?[] { "=HYPERLINK(\"https://example.com\")", 400.25m, new DateOnly(2026, 10, 16) }])], "October 2026", true);
        using var workbook = new XLWorkbook(new MemoryStream(data)); var sheet = workbook.Worksheet(1);
        Assert.True(sheet.RightToLeft); Assert.Equal(400.25d, sheet.Cell(7, 2).GetDouble());
        Assert.Equal(new DateTime(2026, 10, 16), sheet.Cell(7, 3).GetDateTime()); Assert.False(sheet.Cell(7, 1).HasFormula);
        Assert.StartsWith("=HYPERLINK", sheet.Cell(7, 1).GetString()); Assert.Equal(6, sheet.SheetView.SplitRow); Assert.True(sheet.AutoFilter.IsEnabled);
    }
    [Fact]
    public void ExcelExport_RejectsOversizedReportInsteadOfSilentlyTruncating()
    {
        var rows = Enumerable.Range(0, 100001).Select(_ => new object?[] { 1 }).ToList();
        Assert.Throws<MIS.Application.Common.HrValidationException>(() => AccountingBookService.Spreadsheet([("Report", ["Value"], rows)], "", false));
    }
}
