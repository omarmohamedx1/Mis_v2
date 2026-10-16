using System.Data;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Accounting;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence.Configurations;

namespace MIS.Infrastructure.Services;

public sealed partial class AccountingBookService
{
    private static string Number(DateOnly date, Guid id) => $"ACC-{date:yyyyMMdd}-{id.ToString("N")[..12].ToUpperInvariant()}";
    private async Task<AccountingPeriod> OpenPeriod(DateOnly date, CancellationToken token)
    {
        var period = await db.AccountingPeriods.SingleOrDefaultAsync(x => x.LegalEntityId == FinanceSeed.LegalEntityId && x.StartDate <= date && x.EndDate >= date, token);
        if (period is null)
        {
            period = new AccountingPeriod(FinanceSeed.LegalEntityId, date.Year, date.Month, $"{date:yyyy-MM}", new(date.Year, date.Month, 1), new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)));
            db.AccountingPeriods.Add(period);
        }
        if (period.Status != "OPEN") throw new HrConflictException("The accounting period is not open.");
        return period;
    }
    private static void RequireDeletionConfirmation(ConfirmAccountingDeletionRequest request)
    {
        if (!request.Confirmed) throw new HrValidationException("Confirm deletion before removing this accounting record.");
    }
    public async Task DeleteAsync(Guid id, ConfirmAccountingDeletionRequest request, CancellationToken token)
    {
        EnsureManage(); RequireDeletionConfirmation(request);
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        var entry = await db.AccountingBookEntries.Include(x => x.JournalEntry).ThenInclude(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Accounting entry was not found.");
        if (!entry.DeletedAt.HasValue) await DeleteEntry(entry, request.Reason, token);
        if (tx is not null) await tx.CommitAsync(token);
    }
    private async Task DeleteEntry(AccountingBookEntry entry, string? reason, CancellationToken token)
    {
        if (AccountingBookValues.IsAdvance(entry.Category) && await db.AccountingBookEntries.AnyAsync(x => x.RelatedAdvanceId == entry.Id && x.DeletedAt == null && x.JournalEntry.Status != "CANCELLED" && x.JournalEntry.Status != "REVERSED", token))
            throw new HrConflictException("Delete the advance repayments before deleting the original advance.");
        var original = entry.JournalEntry; Guid? reversalId = null;
        if (original.Status == "POSTED")
        {
            var period = await OpenPeriod(original.PostingDate, token);
            var reversal = new JournalEntry(original.LegalEntityId, period.Id, null, "ACCOUNTING_DELETE", original.TransactionDate, original.PostingDate, original.CurrencyCode,
                $"Delete {entry.DocumentNumber}", user.UserId, DateTimeOffset.UtcNow);
            reversal.LinkReversal(original.Id);
            foreach (var line in original.Lines.OrderBy(x => x.LineNumber))
                reversal.AddLine(line.AccountId, line.Credit, line.Debit, line.BaseCredit, line.BaseDebit, line.ExchangeRate, line.Description, line.ClientId, line.CollectorId, line.BranchId, line.CostCenterId);
            reversal.Post(Number(original.PostingDate, reversal.Id), user.UserId, DateTimeOffset.UtcNow, true);
            db.JournalEntries.Add(reversal); original.MarkReversed(); reversalId = reversal.Id;
        }
        else if (original.Status is "DRAFT" or "PENDING_APPROVAL" or "APPROVED") original.CancelUnrecordedAccounting();
        else if (original.Status is not ("REVERSED" or "CANCELLED")) throw new HrConflictException("This accounting entry cannot be deleted.");
        await RemoveSource(entry.SourceKey, token);
        entry.Delete(user.UserId, DateTimeOffset.UtcNow);
        Audit("AccountingEntryDeleted", entry.Id, new { entry.DocumentNumber, entry.Category, entry.Amount, entry.Description, entry.Reference, entry.TransactionDate, entry.EmployeeId, entry.AccountId, entry.CounterAccountId }, new { entry.DeletedAt, entry.DeletedById, ReversalJournalId = reversalId }, reason);
        await db.SaveChangesAsync(token);
    }
    public async Task DeleteSourceAsync(string kind, Guid id, ConfirmAccountingDeletionRequest request, CancellationToken token)
    {
        EnsureManage(); RequireDeletionConfirmation(request); kind = kind.Trim().ToUpperInvariant();
        if (kind is not ("PAYROLL" or "TRANSPORT" or "COLLECTOR" or "SUPERVISOR")) throw new HrValidationException("Unsupported accounting record type.");
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        var key = $"{kind}:{id}";
        var entry = await db.AccountingBookEntries.Include(x => x.JournalEntry).ThenInclude(x => x.Lines).SingleOrDefaultAsync(x => x.SourceKey == key, token);
        if (entry is not null && !entry.DeletedAt.HasValue) await DeleteEntry(entry, request.Reason, token);
        else { await RemoveSource(key, token, protectLegacyPeriod: entry is null); await db.SaveChangesAsync(token); }
        if (tx is not null) await tx.CommitAsync(token);
    }
    private async Task RemoveSource(string? key, CancellationToken token, bool protectLegacyPeriod = false)
    {
        if (key is null) return;
        var parts = key.Split(':'); if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id)) return;
        object? row; object? snapshot;
        switch (parts[0])
        {
            case "PAYROLL":
                var salary = await db.AccountingEmployeePayrolls.SingleOrDefaultAsync(x => x.Id == id, token);
                if (salary?.Status == "DELETED") return;
                row = salary;
                snapshot = salary is null ? null : new { salary.EmployeeId, salary.EmployeeName, salary.PeriodId, salary.BasicSalary, salary.Allowances, salary.Transportation, salary.Commissions, salary.Bonuses, salary.Deductions, salary.OtherAdjustments, salary.NetSalary, salary.Status, salary.Notes }; break;
            case "TRANSPORT":
                var claim = await db.AccountingTransportationClaims.SingleOrDefaultAsync(x => x.Id == id, token); row = claim;
                snapshot = claim is null ? null : new { claim.EmployeeId, claim.ClaimDate, claim.Amount, claim.Purpose, claim.Status, claim.Notes, claim.AttachmentStorageKey, claim.AttachmentFileName }; break;
            case "COLLECTOR":
                var collector = await db.AccountingCollectorCommissions.SingleOrDefaultAsync(x => x.Id == id, token); row = collector;
                snapshot = collector is null ? null : new { collector.CollectorUserId, collector.PeriodYear, collector.PeriodMonth, collector.CollectedAmount, collector.CommissionAmount, collector.Adjustments, collector.FinalCommission, collector.Status, collector.CalculationSnapshotJson }; break;
            case "SUPERVISOR":
                var supervisor = await db.AccountingSupervisorCommissions.SingleOrDefaultAsync(x => x.Id == id, token); row = supervisor;
                snapshot = supervisor is null ? null : new { supervisor.SupervisorUserId, supervisor.PeriodYear, supervisor.PeriodMonth, supervisor.TeamCollectedAmount, supervisor.CommissionAmount, supervisor.Adjustments, supervisor.FinalCommission, supervisor.Status, supervisor.CalculationSnapshotJson }; break;
            default: return;
        }
        if (row is null) return;
        var paidAt = row switch
        {
            AccountingEmployeePayroll { Status: "PAID" } p => p.UpdatedAt ?? p.CreatedAt,
            AccountingTransportationClaim { Status: "PAID" } p => p.UpdatedAt ?? p.CreatedAt,
            AccountingCollectorCommission { Status: "PAID" } p => p.UpdatedAt ?? p.CreatedAt,
            AccountingSupervisorCommission { Status: "PAID" } p => p.UpdatedAt ?? p.CreatedAt,
            _ => (DateTimeOffset?)null
        };
        if (protectLegacyPeriod && paidAt.HasValue)
            await OpenPeriod(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(paidAt.Value, OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo").DateTime), token);
        Audit("AccountingSourceDeleted", id, new { Kind = parts[0], Record = snapshot }, null);
        if (row is AccountingEmployeePayroll payroll) payroll.DeleteAfterConfirmation(DateTimeOffset.UtcNow);
        else db.Remove(row);
    }
}
