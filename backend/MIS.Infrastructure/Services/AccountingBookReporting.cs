using Microsoft.EntityFrameworkCore;
using MIS.Application.DTOs.Accounting;
using MIS.Application.Common;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence.Configurations;

namespace MIS.Infrastructure.Services;

public sealed partial class AccountingBookService
{
    public async Task RecordPaidSourceAsync(AccountingPaidSource source, CancellationToken token)
    {
        EnsureAccess();
        if (source.Amount < 0) throw new HrValidationException("A payment amount cannot be negative.");
        if (source.Amount == 0) return;
        var key = $"{source.Kind}:{source.Id}";
        if (await db.AccountingBookEntries.AnyAsync(x => x.SourceKey == key, token)) return;
        var period = await db.AccountingPeriods.SingleOrDefaultAsync(x => x.LegalEntityId == FinanceSeed.LegalEntityId && x.StartDate <= source.PaidOn && x.EndDate >= source.PaidOn, token);
        if (period is null)
        {
            period = new AccountingPeriod(FinanceSeed.LegalEntityId, source.PaidOn.Year, source.PaidOn.Month, $"{source.PaidOn:yyyy-MM}", new(source.PaidOn.Year, source.PaidOn.Month, 1), new(source.PaidOn.Year, source.PaidOn.Month, DateTime.DaysInMonth(source.PaidOn.Year, source.PaidOn.Month)));
            db.AccountingPeriods.Add(period);
        }
        if (period.Status != "OPEN") throw new HrConflictException("The accounting period is not open.");
        var accounts = await db.FinanceAccounts.Where(x => x.LegalEntityId == FinanceSeed.LegalEntityId && x.IsActive && x.PostingAllowed).ToDictionaryAsync(x => x.Code, token);
        FinanceAccount Account(string category) => accounts.GetValueOrDefault(AccountingBookValues.AccountCode(category)) ?? throw new HrValidationException("The operating expense account is not configured.");
        var cash = accounts.Values.SingleOrDefault(x => x.Id == FinanceSeed.OperatingCashAccountId && x.IsCashAccount) ?? throw new HrValidationException("The company cash account is not configured.");
        var expense = Account(source.Category);
        var journal = new JournalEntry(FinanceSeed.LegalEntityId, period.Id, null, "ACCOUNTING_PAYMENT", source.PaidOn, source.PaidOn, "EGP", source.Description, user.UserId, DateTimeOffset.UtcNow);
        void Debit(string category, decimal amount)
        {
            if (amount == 0) return;
            journal.AddLine(Account(category).Id, Math.Max(amount, 0), Math.Max(-amount, 0), Math.Max(amount, 0), Math.Max(-amount, 0), 1, source.Description);
        }
        if (source.Kind == "PAYROLL")
        {
            Debit("SALARY", source.Amount - source.Transportation - source.Commissions - source.Bonuses);
            Debit("TRANSPORTATION", source.Transportation); Debit("PAYROLL_COMMISSION", source.Commissions); Debit("BONUS", source.Bonuses);
        }
        else Debit(source.Category, source.Amount);
        journal.AddLine(cash.Id, 0, source.Amount, 0, source.Amount, 1, source.Description);
        journal.Post($"ACC-{source.PaidOn:yyyyMMdd}-{journal.Id.ToString("N")[..12].ToUpperInvariant()}", user.UserId, DateTimeOffset.UtcNow, true);
        db.JournalEntries.Add(journal);
        var row = new AccountingBookEntry(Guid.NewGuid(), source.Category, source.PaidOn, source.Amount, expense.Id, cash.Id, source.Description, null, journal.Id, user.UserId, DateTimeOffset.UtcNow, source.EmployeeId, sourceKey: key);
        db.AccountingBookEntries.Add(row); Audit("PaidAccountingSourcePosted", row.Id, null, source);
        await db.SaveChangesAsync(token);
    }

    private sealed record LegacyExpense(string Kind, Guid Id, string Category, decimal Amount, DateTimeOffset PaidAt, string Description, decimal Transportation = 0, decimal Commissions = 0, decimal Bonuses = 0);
    private async Task<List<LegacyExpense>> LegacyExpenses(DateOnly from, DateOnly to, CancellationToken token)
    {
        var (start, end) = UtcRange(from, to);
        var payroll = await db.AccountingEmployeePayrolls.AsNoTracking().Where(x => x.Status == "PAID" && x.NetSalary > 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < end && !db.AccountingBookEntries.Any(e => e.SourceKey == "PAYROLL:" + x.Id.ToString()))
            .Select(x => new LegacyExpense("PAYROLL", x.Id, "SALARY", x.NetSalary, x.UpdatedAt ?? x.CreatedAt, x.EmployeeName, x.Transportation, x.Commissions, x.Bonuses)).ToListAsync(token);
        payroll.AddRange(await db.AccountingTransportationClaims.AsNoTracking().Where(x => x.Status == "PAID" && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < end && !db.AccountingBookEntries.Any(e => e.SourceKey == "TRANSPORT:" + x.Id.ToString()))
            .Select(x => new LegacyExpense("TRANSPORT", x.Id, "TRANSPORTATION", x.Amount, x.UpdatedAt ?? x.CreatedAt, x.Employee.FullName, 0, 0, 0)).ToListAsync(token));
        payroll.AddRange(await db.AccountingCollectorCommissions.AsNoTracking().Where(x => x.Status == "PAID" && x.FinalCommission > 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < end && !db.AccountingBookEntries.Any(e => e.SourceKey == "COLLECTOR:" + x.Id.ToString()))
            .Select(x => new LegacyExpense("COLLECTOR", x.Id, "COLLECTOR_COMMISSION", x.FinalCommission, x.UpdatedAt ?? x.CreatedAt, x.CollectorUser.FullName, 0, 0, 0)).ToListAsync(token));
        payroll.AddRange(await db.AccountingSupervisorCommissions.AsNoTracking().Where(x => x.Status == "PAID" && x.FinalCommission > 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < end && !db.AccountingBookEntries.Any(e => e.SourceKey == "SUPERVISOR:" + x.Id.ToString()))
            .Select(x => new LegacyExpense("SUPERVISOR", x.Id, "SUPERVISOR_COMMISSION", x.FinalCommission, x.UpdatedAt ?? x.CreatedAt, x.SupervisorUser.FullName, 0, 0, 0)).ToListAsync(token));
        return payroll;
    }

    public async Task<AccountingBookSummaryDto> SummaryAsync(DateOnly from, DateOnly to, CancellationToken token)
    {
        EnsureAccess(); CheckDates(from, to);
        var totals = AccountingBookValues.Categories.ToDictionary(x => x, _ => (Amount: 0m, Count: 0));
        void Add(string category, decimal amount) { var old = totals[category]; totals[category] = (old.Amount + amount, old.Count + 1); }
        decimal expenses = 0, income = 0, advances = 0, repayments = 0;
        void Include(AccountingBookEntry entry, int sign)
        {
            Add(entry.Category, sign * entry.Amount);
            switch (AccountingBookValues.Direction(entry.Category))
            {
                case "EXPENSE": expenses += sign * entry.Amount; break;
                case "INCOME": income += sign * entry.Amount; break;
                case "ADVANCE": advances += sign * entry.Amount; break;
                case "ADVANCE_REPAYMENT": repayments += sign * entry.Amount; break;
            }
            if (entry.Category == "SALARY")
                foreach (var category in new[] { "TRANSPORTATION", "BONUS", "PAYROLL_COMMISSION" })
                {
                    var amount = entry.JournalEntry.Lines.Where(x => x.Account.Code == AccountingBookValues.AccountCode(category)).Sum(x => x.BaseDebit - x.BaseCredit);
                    if (amount == 0) continue;
                    Add(category, sign * amount); var salary = totals["SALARY"]; totals["SALARY"] = (salary.Amount - sign * amount, salary.Count);
                }
        }
        var originals = await Entries().Where(x => (x.JournalEntry.Status == "POSTED" || x.JournalEntry.Status == "REVERSED") && x.JournalEntry.PostingDate >= from && x.JournalEntry.PostingDate <= to).ToListAsync(token);
        foreach (var entry in originals) Include(entry, 1);
        var reversedIds = await db.JournalEntries.Where(x => x.ReversalOfJournalId != null && (x.Status == "POSTED" || x.Status == "REVERSED") && x.PostingDate >= from && x.PostingDate <= to).Select(x => x.ReversalOfJournalId!.Value).ToArrayAsync(token);
        foreach (var entry in await Entries().Where(x => reversedIds.Contains(x.JournalEntryId)).ToListAsync(token)) Include(entry, -1);

        var legacy = await LegacyExpenses(from, to, token);
        foreach (var entry in legacy)
        {
            expenses += entry.Amount; Add(entry.Category, entry.Amount);
            if (entry.Kind == "PAYROLL")
                foreach (var (category, amount) in new[] { ("TRANSPORTATION", entry.Transportation), ("BONUS", entry.Bonuses), ("PAYROLL_COMMISSION", entry.Commissions) })
                { if (amount == 0) continue; Add(category, amount); var salary = totals["SALARY"]; totals["SALARY"] = (salary.Amount - amount, salary.Count); }
        }
        var otherLines = db.JournalEntryLines.AsNoTracking().Where(x => (x.JournalEntry.Status == "POSTED" || x.JournalEntry.Status == "REVERSED") && x.JournalEntry.PostingDate >= from && x.JournalEntry.PostingDate <= to
            && !db.AccountingBookEntries.Any(e => e.JournalEntryId == x.JournalEntryId || e.JournalEntryId == x.JournalEntry.ReversalOfJournalId));
        var otherCosts = await otherLines.Where(x => x.Account.AccountType == "EXPENSE" || x.Account.AccountType == "REVENUE")
            .GroupBy(x => new { x.Account.Code, x.Account.AccountType }).Select(g => new { g.Key.Code, g.Key.AccountType, Amount = g.Sum(x => x.BaseDebit - x.BaseCredit), Count = g.Count() }).ToListAsync(token);
        foreach (var group in otherCosts)
        {
            var category = group.AccountType == "REVENUE" ? "INCOME" : AccountingBookValues.Categories.FirstOrDefault(x => AccountingBookValues.AccountCode(x) == group.Code && AccountingBookValues.AccountType(x) == "EXPENSE") ?? "OTHER";
            var amount = group.AccountType == "REVENUE" ? -group.Amount : group.Amount;
            Add(category, amount); if (group.AccountType == "REVENUE") income += amount; else expenses += amount;
        }
        var otherCash = await otherLines.Where(x => x.Account.IsCashAccount).GroupBy(x => x.JournalEntryId).Select(g => g.Sum(x => x.BaseDebit - x.BaseCredit)).ToListAsync(token);
        var bookCashIn = originals.Where(x => AccountingBookValues.IsCashIn(x.Category)).Sum(x => x.Amount);
        var bookCashOut = originals.Where(x => AccountingBookValues.IsCashOut(x.Category)).Sum(x => x.Amount);
        var reversed = await db.AccountingBookEntries.Where(x => reversedIds.Contains(x.JournalEntryId)).ToListAsync(token);
        bookCashIn += reversed.Where(x => AccountingBookValues.IsCashOut(x.Category)).Sum(x => x.Amount);
        bookCashOut += reversed.Where(x => AccountingBookValues.IsCashIn(x.Category)).Sum(x => x.Amount);
        var cashIn = bookCashIn + otherCash.Where(x => x > 0).Sum();
        var cashOut = bookCashOut + legacy.Sum(x => x.Amount) - otherCash.Where(x => x < 0).Sum();
        var pending = db.AccountingBookEntries.Where(x => x.TransactionDate >= from && x.TransactionDate <= to && (x.JournalEntry.Status == "DRAFT" || x.JournalEntry.Status == "PENDING_APPROVAL" || x.JournalEntry.Status == "APPROVED"));
        var outstanding = await AdvancesAsync(to, null, null, token);
        return new(from, to, income, expenses, advances, repayments, cashIn, cashOut, cashIn - cashOut, await pending.SumAsync(x => (decimal?)x.Amount, token) ?? 0, await pending.CountAsync(token),
            outstanding.Sum(x => x.Remaining), outstanding.Where(x => x.Date < from || x.Carried).Sum(x => x.Remaining), legacy.Count,
            totals.Select(x => new AccountingBookCategoryTotalDto(x.Key, x.Value.Amount, x.Value.Count)).ToArray());
    }
}
