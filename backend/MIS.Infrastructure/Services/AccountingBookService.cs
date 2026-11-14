using System.Data;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Accounting;
using MIS.Application.DTOs.Finance;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Persistence.Configurations;

namespace MIS.Infrastructure.Services;

public sealed partial class AccountingBookService : IAccountingBookService
{
    private readonly ApplicationDbContext db;
    private readonly ICurrentUserContext user;
    private readonly IFinanceService finance;
    private readonly ClaimReceiptOptions claimReceiptOptions;
    public AccountingBookService(ApplicationDbContext db, ICurrentUserContext user, IFinanceService finance, IOptions<ClaimReceiptOptions>? claimReceiptOptions = null) { this.db = db; this.user = user; this.finance = finance; this.claimReceiptOptions = claimReceiptOptions?.Value ?? new(); }
    private bool IsAdmin => user.Roles.Contains(SystemRoleNames.Admin) || user.Permissions.Contains("*");
    private void EnsureAccess()
    {
        if (!IsAdmin && !user.Permissions.Contains(SystemPermissionCodes.FinanceAccess) && !user.Permissions.Contains("accounting.access")) throw new HrForbiddenException("Accounting access is required.");
    }
    private void EnsureManage()
    {
        EnsureAccess();
        if (!IsAdmin && !user.Permissions.Contains(SystemPermissionCodes.FinanceJournalCreate) && !user.Permissions.Contains("accounting.transaction.manage") && !user.Permissions.Contains(SystemPermissionCodes.AccountingPayrollManage) && !user.Permissions.Contains(SystemPermissionCodes.AccountingCommissionManage) && !user.Permissions.Contains(SystemPermissionCodes.AccountingTransportationManage) && !user.Permissions.Contains("accounting.access")) throw new HrForbiddenException("You do not have the required finance permission.");
    }
    private static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo").DateTime);
    private static (DateTimeOffset From, DateTimeOffset To) UtcRange(DateOnly from, DateOnly to)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo");
        return (new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), zone)), new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone)));
    }
    private static void CheckDates(DateOnly from, DateOnly to) { if (from.Year < 2000 || to.Year > 2100 || to < from) throw new HrValidationException("Provide a valid accounting date range."); }
    private void Audit(string action, Guid id, object? before, object? after, string? reason = null)
        => db.FinancialAuditLogs.Add(new FinancialAuditLog(user.UserId, action, nameof(AccountingBookEntry), id, JsonSerializer.Serialize(before ?? new { }), JsonSerializer.Serialize(after ?? new { }), reason, DateTimeOffset.UtcNow));
    private IQueryable<AccountingBookEntry> Entries(bool includeDeleted = false) => db.AccountingBookEntries.AsNoTracking().Where(x => includeDeleted || x.DeletedAt == null).Include(x => x.JournalEntry).ThenInclude(x => x.Lines).ThenInclude(x => x.Account)
        .Include(x => x.Account).Include(x => x.CounterAccount).Include(x => x.Employee).ThenInclude(x => x!.Department).Include(x => x.CreatedBy);
    // Keep claim journals available to receipt/audit workflows, but never fund operating reports.
    // The relationship check also protects legacy rows whose source key is missing.
    private IQueryable<AccountingBookEntry> OperatingEntries(bool includeDeleted = false)
        => Entries(includeDeleted).Where(x => x.Category != "CLAIM_RECEIPT"
            && (x.SourceKey == null || !x.SourceKey.StartsWith("CLAIM:"))
            && !db.FinanceClaimReceiptPostings.Any(p => p.AccountingBookEntryId == x.Id));

    private static AccountingBookEntryDto Map(AccountingBookEntry x) => new(x.Id, x.DocumentNumber, x.Category, AccountingBookValues.Direction(x.Category), x.TransactionDate, x.Amount,
        x.AccountId, x.Account.Code, ApiTextLocalizer.IsArabic ? x.Account.NameArabic : x.Account.NameEnglish, x.CounterAccountId,
        ApiTextLocalizer.IsArabic ? x.CounterAccount.NameArabic : x.CounterAccount.NameEnglish, x.Description, x.Reference, x.JournalEntryId, x.JournalEntry.JournalNumber,
        x.JournalEntry.Status, x.EmployeeId, x.Employee?.FullName, x.RelatedAdvanceId, x.RecurringExpenseId, x.SourceKey, x.CreatedBy.FullName, x.CreatedAt);
    private static IQueryable<AccountingBookEntry> Filter(IQueryable<AccountingBookEntry> q, AccountingBookFilter f)
    {
        if (f.From.HasValue) q = q.Where(x => x.TransactionDate >= f.From);
        if (f.To.HasValue) q = q.Where(x => x.TransactionDate <= f.To);
        if (!string.IsNullOrWhiteSpace(f.Category)) q = q.Where(x => x.Category == f.Category);
        if (!string.IsNullOrWhiteSpace(f.Status)) q = f.Status == "UNRECORDED" ? q.Where(x => x.JournalEntry.Status == "DRAFT" || x.JournalEntry.Status == "PENDING_APPROVAL" || x.JournalEntry.Status == "APPROVED") : q.Where(x => x.JournalEntry.Status == f.Status);
        if (f.EmployeeId.HasValue) q = q.Where(x => x.EmployeeId == f.EmployeeId);
        if (!string.IsNullOrWhiteSpace(f.Search)) { var s = f.Search.Trim().ToLower(); q = q.Where(x => x.DocumentNumber.ToLower().Contains(s) || x.Description.ToLower().Contains(s) || (x.Reference != null && x.Reference.ToLower().Contains(s)) || (x.Employee != null && x.Employee.FullName.ToLower().Contains(s))); }
        return q;
    }
    public async Task<AccountingBookPageDto> ListAsync(AccountingBookFilter filter, CancellationToken token)
    {
        EnsureAccess(); if (filter.Page < 1 || filter.PageSize is < 1 or > 200) throw new HrValidationException("Invalid page parameters.");
        if (filter.From.HasValue && filter.To.HasValue) CheckDates(filter.From.Value, filter.To.Value);
        var q = Filter(OperatingEntries(), filter); var count = await q.CountAsync(token); var total = await q.SumAsync(x => (decimal?)x.Amount, token) ?? 0;
        var rows = await q.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.CreatedAt).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(token);
        return new(rows.Select(Map).ToArray(), filter.Page, filter.PageSize, count, total);
    }
    private async Task<AccountingBookEntryDto> GetAsync(Guid id, CancellationToken token) => Map(await Entries().SingleAsync(x => x.Id == id, token));
    private async Task ValidateAccounts(string category, Guid accountId, Guid counterId, CancellationToken token)
    {
        var accounts = await db.FinanceAccounts.AsNoTracking().Where(x => (x.Id == accountId || x.Id == counterId) && x.IsActive && x.PostingAllowed).ToListAsync(token);
        if (accounts.Count != 2 || accounts.Any(x => x.LegalEntityId != FinanceSeed.LegalEntityId || x.ControlAccountType != null || x.RequiresClient || x.RequiresCollector || x.RequiresBranch)) throw new HrValidationException("Choose two active company posting accounts without client-money controls.");
        if (accounts.Single(x => x.Id == accountId).AccountType != AccountingBookValues.AccountType(category)) throw new HrValidationException("The selected account type does not match this operation.");
        var counterType = category == "CARRIED_ADVANCE" ? "EQUITY" : "ASSET";
        if (accounts.Single(x => x.Id == counterId).AccountType != counterType) throw new HrValidationException("Choose a company cash or bank account, or an equity account for opening advances.");
        if (category != "CARRIED_ADVANCE" && !accounts.Single(x => x.Id == counterId).IsCashAccount) throw new HrValidationException("An advance account cannot be used as a cash account.");
    }
    public async Task<AccountingBookEntryDto> CreateAsync(CreateAccountingBookEntryRequest request, CancellationToken token)
    {
        EnsureManage();
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        if (!AccountingBookValues.ManualCategories.Contains(request.Category)) throw new HrValidationException("Use the payroll, transportation or commission screen for this expense category.");
        if (request.TransactionDate.Year < 2000 || request.TransactionDate > Today()) throw new HrValidationException("Choose an accounting date no later than today.");
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount) throw new HrValidationException("Use a positive amount with at most two decimal places.");
        if (request.PaymentMethod?.Length > 80 || request.Notes?.Length > 1000) throw new HrValidationException("Payment method or notes exceed the allowed length.");
        if (request.RelatedAdvanceId.HasValue && request.Category != "ADVANCE_REPAYMENT") throw new HrValidationException("Only an advance repayment can reference an original advance.");
        if (request.EmployeeId.HasValue && !await db.Employees.AnyAsync(x => x.Id == request.EmployeeId && !x.IsArchived, token)) throw new HrNotFoundException("Employee was not found.");
        if (AccountingBookValues.IsAdvance(request.Category) && request.EmployeeId is null) throw new HrValidationException("An employee is required for an advance.");
        var source = request.RequestId.HasValue ? $"MANUAL:{request.RequestId}" : null;
        var existing = source is null ? null : await db.AccountingBookEntries.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKey == source, token);
        if (existing is not null)
        {
            if (existing.DeletedAt.HasValue) throw new HrConflictException("This entry was deleted. Start a new entry to record it again.");
            if (existing.Amount != request.Amount || existing.Category != request.Category || existing.TransactionDate != request.TransactionDate || existing.AccountId != request.AccountId || existing.CounterAccountId != request.CounterAccountId || existing.Description != request.Description.Trim() || existing.EmployeeId != request.EmployeeId || existing.RelatedAdvanceId != request.RelatedAdvanceId || existing.Reference != (string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim()) || existing.PaymentMethod != (string.IsNullOrWhiteSpace(request.PaymentMethod) ? null : request.PaymentMethod.Trim()) || existing.Notes != (string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim())) throw new HrConflictException("This request was already used with different accounting data.");
            return await GetAsync(existing.Id, token);
        }
        if (request.Category == "CARRIED_ADVANCE")
        {
            var reference = request.Reference?.Trim();
            if (string.IsNullOrWhiteSpace(reference)) throw new HrValidationException("A reference is required for a carried advance.");
            var duplicate = await db.AccountingBookEntries.AnyAsync(x =>
                x.Category == "CARRIED_ADVANCE"
                && x.EmployeeId == request.EmployeeId
                && x.DeletedAt == null
                && x.Reference != null
                && x.Reference.ToLower() == reference.ToLower(), token);
            if (duplicate) throw new HrConflictException("A carried advance with this employee and reference already exists.");
        }
        if (request.Category == "ADVANCE_REPAYMENT") await ValidateRepayment(request.RelatedAdvanceId, request.Amount, request.EmployeeId, request.AccountId, request.TransactionDate, null, token);
        await ValidateAccounts(request.Category, request.AccountId, request.CounterAccountId, token);
        var result = await CreateRecordedEntry(request, null, source, token);
        if (tx is not null) await tx.CommitAsync(token);
        return result;
    }

    public async Task<AccountingBookEntryDto> RecordClaimReceiptAsync(Guid claimId, Guid receiptPostingId, DateOnly receiptDate, decimal amount, CancellationToken token)
    {
        EnsureAccess();
        if (!IsAdmin && !user.Permissions.Contains(SystemPermissionCodes.FinanceClaimsConfirmReceipt))
            throw new HrForbiddenException("Claim receipt confirmation permission is required.");
        if (claimId == Guid.Empty || receiptPostingId == Guid.Empty || amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new HrValidationException("The claim receipt details are invalid.");
        if (receiptDate.Year is < 2000 or > 2100 || receiptDate > Today())
            throw new HrValidationException("Choose a valid bank receipt date no later than today.");

        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        var source = $"CLAIM:{claimId}:{receiptPostingId}";
        var existing = await db.AccountingBookEntries.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKey == source, token);
        if (existing is not null)
        {
            if (existing.Amount != amount || existing.TransactionDate != receiptDate || existing.Category is not ("INCOME" or "CLAIM_RECEIPT") || existing.DeletedAt.HasValue)
                throw new HrConflictException("This claim receipt key already exists with different or reversed accounting data.");
            var result = await GetAsync(existing.Id, token);
            if (tx is not null) await tx.CommitAsync(token);
            return result;
        }

        var request = new CreateAccountingBookEntryRequest(
            "CLAIM_RECEIPT", receiptDate, amount, claimReceiptOptions.ClearingAccountId, claimReceiptOptions.AssetAccountId,
            $"Claim bank receipt {claimId:N}", claimId.ToString("N"));
        var accounts = await db.FinanceAccounts.AsNoTracking().Where(x => x.Id == request.AccountId || x.Id == request.CounterAccountId).ToListAsync(token);
        ClaimReceiptOptions.Validate(accounts, claimReceiptOptions);
        var entry = await CreateRecordedEntry(request, null, source, token);
        if (tx is not null) await tx.CommitAsync(token);
        return entry;
    }
    private async Task<AccountingBookEntryDto> CreateRecordedEntry(CreateAccountingBookEntryRequest request, Guid? recurringId, string? source, CancellationToken token)
    {
        var incoming = request.Category == "CLAIM_RECEIPT" || AccountingBookValues.IsCashIn(request.Category);
        var debitId = incoming ? request.CounterAccountId : request.AccountId;
        var creditId = incoming ? request.AccountId : request.CounterAccountId;
        var period = await OpenPeriod(request.TransactionDate, token);
        var now = DateTimeOffset.UtcNow;
        var entryType = request.Category switch
        {
            "CLAIM_RECEIPT" => "CLAIM_RECEIPT",
            "INCOME" => "COMPANY_INCOME",
            "REVENUE" => "REVENUE",
            _ => "ACCOUNTING_OPERATION"
        };
        var journal = new JournalEntry(FinanceSeed.LegalEntityId, period.Id, null, entryType, request.TransactionDate, request.TransactionDate, "EGP", request.Description, user.UserId, now);
        journal.AddLine(debitId, request.Amount, 0, request.Amount, 0, 1, request.Description);
        journal.AddLine(creditId, 0, request.Amount, 0, request.Amount, 1, request.Description);
        journal.RecordConfirmedAccounting(Number(request.TransactionDate, journal.Id), user.UserId, now);
        db.JournalEntries.Add(journal);
        var row = new AccountingBookEntry(Guid.NewGuid(), request.Category, request.TransactionDate, request.Amount, request.AccountId, request.CounterAccountId, request.Description, request.Reference,
            journal.Id, user.UserId, DateTimeOffset.UtcNow, request.EmployeeId, request.RelatedAdvanceId, recurringId, source, request.PaymentMethod, request.Notes);
        db.AccountingBookEntries.Add(row); Audit("AccountingEntryCreated", row.Id, null, request);
        await db.SaveChangesAsync(token); return await GetAsync(row.Id, token);
    }
    private async Task ValidateRepayment(Guid? advanceId, decimal amount, Guid? employeeId, Guid accountId, DateOnly date, Guid? excludeId, CancellationToken token)
    {
        var advance = await db.AccountingBookEntries.AsNoTracking().Include(x => x.JournalEntry).SingleOrDefaultAsync(x => x.Id == advanceId, token);
        if (advance is null || !AccountingBookValues.IsAdvance(advance.Category) || advance.JournalEntry.Status != "POSTED") throw new HrValidationException("Choose a posted employee advance to settle.");
        if (advance.EmployeeId != employeeId || advance.AccountId != accountId || date < advance.TransactionDate) throw new HrValidationException("Repayment must use the same employee and advance account, on or after the advance date.");
        var paid = await db.AccountingBookEntries.Where(x => x.Category == "ADVANCE_REPAYMENT" && x.RelatedAdvanceId == advanceId && x.Id != excludeId && x.JournalEntry.Status == "POSTED").SumAsync(x => (decimal?)x.Amount, token) ?? 0;
        if (amount > advance.Amount - paid) throw new HrConflictException("Repayment exceeds the outstanding advance balance.");
    }
    public async Task<AccountingBookEntryDto> TransitionAsync(Guid id, string action, string? reason, CancellationToken token)
    {
        EnsureAccess(); await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        var entry = await db.AccountingBookEntries.Include(x => x.JournalEntry).SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Accounting entry was not found.");
        if (entry.SourceKey is not null && !entry.SourceKey.StartsWith("MANUAL:") && !entry.SourceKey.StartsWith("RECURRING:")) throw new HrConflictException("Use the original payroll or commission record to manage this payment.");
        try
        {
            if (entry.DeletedAt.HasValue) throw new HrConflictException("This entry was deleted. Start a new entry to record it again.");
            switch (action)
            {
                case "record":
                    EnsureManage();
                    if (entry.JournalEntry.Status == "POSTED") break;
                    if (entry.TransactionDate > Today()) throw new HrConflictException("Future accounting entries cannot be posted before their date.");
                    await OpenPeriod(entry.TransactionDate, token);
                    await ValidateAccounts(entry.Category, entry.AccountId, entry.CounterAccountId, token);
                    if (entry.Category == "ADVANCE_REPAYMENT") await ValidateRepayment(entry.RelatedAdvanceId, entry.Amount, entry.EmployeeId, entry.AccountId, entry.TransactionDate, entry.Id, token);
                    await db.Entry(entry.JournalEntry).Collection(x => x.Lines).LoadAsync(token);
                    entry.JournalEntry.RecordConfirmedAccounting(Number(entry.TransactionDate, entry.JournalEntryId), user.UserId, DateTimeOffset.UtcNow);
                    Audit("AccountingEntryRecorded", entry.Id, null, new { entry.Amount, entry.JournalEntry.Status }); await db.SaveChangesAsync(token); break;
                case "submit": EnsureManage(); await finance.SubmitJournalAsync(entry.JournalEntryId, token); break;
                case "approve": await finance.ApproveJournalAsync(entry.JournalEntryId, token); break;
                case "post":
                    if (entry.TransactionDate > Today()) throw new HrConflictException("Future accounting entries cannot be posted before their date.");
                    if (entry.Category == "ADVANCE_REPAYMENT") await ValidateRepayment(entry.RelatedAdvanceId, entry.Amount, entry.EmployeeId, entry.AccountId, entry.TransactionDate, entry.Id, token);
                    await ValidateAccounts(entry.Category, entry.AccountId, entry.CounterAccountId, token);
                    await finance.PostJournalAsync(entry.JournalEntryId, token); break;
                case "cancel": EnsureManage(); entry.JournalEntry.Cancel(); Audit("AccountingEntryCancelled", entry.Id, null, new { entry.JournalEntry.Status }, reason); await db.SaveChangesAsync(token); break;
                case "reverse":
                    if (AccountingBookValues.IsAdvance(entry.Category) && await db.AccountingBookEntries.AnyAsync(x => x.Category == "ADVANCE_REPAYMENT" && x.RelatedAdvanceId == id && x.JournalEntry.Status == "POSTED", token)) throw new HrConflictException("Reverse the advance repayments before reversing the original advance.");
                    await finance.ReverseJournalAsync(entry.JournalEntryId, new PeriodActionRequest(reason ?? ""), token); break;
                default: throw new HrValidationException("Unsupported accounting action.");
            }
        }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        if (tx is not null) await tx.CommitAsync(token);
        return await GetAsync(id, token);
    }
    private IQueryable<AccountingBookEntry> ActiveAdvancesQuery()
    {
        return Entries().Where(x => (x.Category == "ADVANCE" || x.Category == "CARRIED_ADVANCE") && x.JournalEntry.Status == "POSTED");
    }
    private static AccountingAdvanceDto MapAdvance(AccountingBookEntry x, decimal repaid)
    {
        var remaining = x.Amount - repaid;
        var method = x.PaymentMethod ?? (ApiTextLocalizer.IsArabic ? x.CounterAccount.NameArabic : x.CounterAccount.NameEnglish);
        return new(x.Id, x.DocumentNumber, x.EmployeeId!.Value, x.Employee!.EmployeeNumber,
            ApiTextLocalizer.IsArabic ? x.Employee.FullNameArabic ?? x.Employee.FullName : x.Employee.FullNameEnglish ?? x.Employee.FullName,
            x.Employee.Department is null ? null : ApiTextLocalizer.IsArabic ? x.Employee.Department.NameArabic ?? x.Employee.Department.Name : x.Employee.Department.Name,
            x.Employee.DepartmentId,
            x.TransactionDate, x.Amount, repaid, remaining, x.Description, method, x.Reference, x.Notes,
            remaining <= 0 ? "SETTLED" : repaid > 0 ? "PARTIAL" : "OPEN", x.JournalEntryId, x.AccountId, x.CreatedBy.FullName, x.CreatedAt, x.Category == "CARRIED_ADVANCE");
    }
    private async Task<Dictionary<Guid, decimal>> AdvanceRepayments(Guid[] ids, CancellationToken token)
    {
        if (ids.Length == 0) return [];
        return await db.AccountingBookEntries.Where(x => x.RelatedAdvanceId != null && ids.Contains(x.RelatedAdvanceId.Value) && x.Category == "ADVANCE_REPAYMENT" && x.JournalEntry.Status == "POSTED")
            .GroupBy(x => x.RelatedAdvanceId!.Value).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount, token);
    }
    public async Task<AccountingAdvancesResultDto> AdvancesAsync(AccountingAdvancesFilter filter, CancellationToken token)
    {
        EnsureAccess();
        if (filter.From.HasValue && filter.To.HasValue) CheckDates(filter.From.Value, filter.To.Value);
        if (filter.From.HasValue) CheckDates(filter.From.Value, filter.From.Value);
        if (filter.To.HasValue) CheckDates(filter.To.Value, filter.To.Value);
        var asOf = filter.AsOf ?? (filter.CarriedOnly ? Today() : (DateOnly?)null);
        if (asOf.HasValue) CheckDates(asOf.Value, asOf.Value);
        var q = asOf.HasValue
            ? Entries().Where(x =>
                (x.Category == "ADVANCE" || x.Category == "CARRIED_ADVANCE")
                && x.TransactionDate <= asOf.Value
                && (x.JournalEntry.Status == "POSTED" || x.JournalEntry.Status == "REVERSED")
                && !db.JournalEntries.Any(reversal =>
                    reversal.ReversalOfJournalId == x.JournalEntryId
                    && reversal.PostingDate <= asOf.Value
                    && (reversal.Status == "POSTED" || reversal.Status == "REVERSED")))
            : ActiveAdvancesQuery();
        if (!asOf.HasValue && filter.From.HasValue) q = q.Where(x => x.TransactionDate >= filter.From);
        if (!asOf.HasValue && filter.To.HasValue) q = q.Where(x => x.TransactionDate <= filter.To);
        if (filter.EmployeeId.HasValue) q = q.Where(x => x.EmployeeId == filter.EmployeeId);
        if (filter.DepartmentId.HasValue) q = q.Where(x => x.Employee!.DepartmentId == filter.DepartmentId);
        if (!string.IsNullOrWhiteSpace(filter.EmployeeName))
        {
            var search = filter.EmployeeName.Trim().ToLower();
            q = q.Where(x => x.Employee!.FullName.ToLower().Contains(search)
                || (x.Employee.FullNameArabic != null && x.Employee.FullNameArabic.ToLower().Contains(search))
                || (x.Employee.FullNameEnglish != null && x.Employee.FullNameEnglish.ToLower().Contains(search)));
        }
        if (!string.IsNullOrWhiteSpace(filter.EmployeeNumber))
        {
            var number = filter.EmployeeNumber.Trim().ToLower();
            q = q.Where(x => x.Employee!.EmployeeNumber.ToLower().Contains(number));
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            q = q.Where(x =>
                x.DocumentNumber.ToLower().Contains(search)
                || (x.Reference != null && x.Reference.ToLower().Contains(search))
                || x.Description.ToLower().Contains(search)
                || x.Employee!.EmployeeNumber.ToLower().Contains(search)
                || x.Employee.FullName.ToLower().Contains(search)
                || (x.Employee.FullNameArabic != null && x.Employee.FullNameArabic.ToLower().Contains(search))
                || (x.Employee.FullNameEnglish != null && x.Employee.FullNameEnglish.ToLower().Contains(search)));
        }
        var rows = await q.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.CreatedAt).ToListAsync(token);
        var ids = rows.Select(x => x.Id).ToArray();
        Dictionary<Guid, decimal> repayments;
        if (asOf.HasValue && ids.Length > 0)
        {
            repayments = await db.AccountingBookEntries.AsNoTracking()
                .Where(x => x.RelatedAdvanceId != null
                    && ids.Contains(x.RelatedAdvanceId.Value)
                    && x.Category == "ADVANCE_REPAYMENT"
                    && x.TransactionDate <= asOf.Value
                    && (x.JournalEntry.Status == "POSTED" || x.JournalEntry.Status == "REVERSED")
                    && !db.JournalEntries.Any(reversal =>
                        reversal.ReversalOfJournalId == x.JournalEntryId
                        && reversal.PostingDate <= asOf.Value
                        && (reversal.Status == "POSTED" || reversal.Status == "REVERSED")))
                .GroupBy(x => x.RelatedAdvanceId!.Value)
                .Select(group => new { Id = group.Key, Amount = group.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.Id, x => x.Amount, token);
        }
        else
        {
            repayments = await AdvanceRepayments(ids, token);
        }

        var monthStart = asOf.HasValue ? new DateOnly(asOf.Value.Year, asOf.Value.Month, 1) : default;
        var advances = rows.Select(x =>
        {
            var mapped = MapAdvance(x, repayments.GetValueOrDefault(x.Id));
            return asOf.HasValue
                ? mapped with { Carried = x.Category == "CARRIED_ADVANCE" || x.TransactionDate < monthStart }
                : mapped;
        }).ToArray();
        if (filter.CarriedOnly) advances = advances.Where(x => x.Carried).ToArray();
        if (!string.IsNullOrWhiteSpace(filter.Status))
            advances = advances.Where(x => x.Status == filter.Status).ToArray();
        return new(advances, advances.Sum(x => x.Amount), advances.Sum(x => x.Repaid), advances.Sum(x => x.Remaining), advances.Count(x => x.Remaining > 0));
    }
    public async Task<IReadOnlyList<AccountingAdvanceDto>> AdvancesAsync(DateOnly asOf, string? search, Guid? employeeId, CancellationToken token)
    {
        EnsureAccess(); CheckDates(asOf, asOf);
        var q = Entries().Where(x => (x.Category == "ADVANCE" || x.Category == "CARRIED_ADVANCE") && x.TransactionDate <= asOf &&
            (x.JournalEntry.Status == "POSTED" || x.JournalEntry.Status == "REVERSED") &&
            !db.JournalEntries.Any(r => r.ReversalOfJournalId == x.JournalEntryId && r.PostingDate <= asOf && (r.Status == "POSTED" || r.Status == "REVERSED")));
        if (employeeId.HasValue) q = q.Where(x => x.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => x.Employee!.FullName.Contains(search) || x.DocumentNumber.Contains(search));
        var rows = await q.OrderBy(x => x.TransactionDate).ToListAsync(token);
        var ids = rows.Select(x => x.Id).ToArray();
        var repayments = ids.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await db.AccountingBookEntries.Where(x => x.Category == "ADVANCE_REPAYMENT" && x.RelatedAdvanceId != null && ids.Contains(x.RelatedAdvanceId.Value) &&
                    (x.JournalEntry.Status == "POSTED" || x.JournalEntry.Status == "REVERSED") && x.TransactionDate <= asOf &&
                    !db.JournalEntries.Any(r => r.ReversalOfJournalId == x.JournalEntryId && r.PostingDate <= asOf && (r.Status == "POSTED" || r.Status == "REVERSED")))
                .GroupBy(x => x.RelatedAdvanceId!.Value).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.Id, x => x.Amount, token);
        var monthStart = new DateOnly(asOf.Year, asOf.Month, 1);
        return rows.Select(x => MapAdvance(x, repayments.GetValueOrDefault(x.Id)) with
        {
            Carried = x.Category == "CARRIED_ADVANCE" || x.TransactionDate < monthStart,
        }).ToArray();
    }
    public async Task<AccountingAdvanceDetailsDto> AdvanceDetailsAsync(Guid id, CancellationToken token)
    {
        EnsureAccess();
        var advance = await ActiveAdvancesQuery().SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Advance was not found.");
        var relatedAdvances = await ActiveAdvancesQuery().Where(x => x.EmployeeId == advance.EmployeeId).ToListAsync(token);
        var relatedIds = relatedAdvances.Select(x => x.Id).ToArray();
        var allRepayments = await AdvanceRepayments(relatedIds, token);
        var payments = await db.AccountingBookEntries.AsNoTracking().Where(x => x.RelatedAdvanceId == id && x.Category == "ADVANCE_REPAYMENT" &&
                (x.JournalEntry.Status == "POSTED" || x.JournalEntry.Status == "REVERSED" || x.JournalEntry.Status == "CANCELLED"))
            .Include(x => x.CounterAccount).Include(x => x.CreatedBy).OrderBy(x => x.TransactionDate).ThenBy(x => x.CreatedAt).ToListAsync(token);
        var totalOutstanding = relatedAdvances.Sum(x => x.Amount - allRepayments.GetValueOrDefault(x.Id));
        var paid = allRepayments.GetValueOrDefault(id);
        var paymentRows = new List<AccountingAdvancePaymentDto>(payments.Count);
        var cumulative = 0m;
        foreach (var payment in payments)
        {
            if (payment.JournalEntry.Status is "POSTED" or "REVERSED") cumulative += payment.Amount;
            paymentRows.Add(new(payment.Id, payment.TransactionDate, payment.Amount, payment.JournalEntry.Status,
                payment.PaymentMethod ?? (ApiTextLocalizer.IsArabic ? payment.CounterAccount.NameArabic : payment.CounterAccount.NameEnglish),
                payment.Reference, payment.Notes, payment.CreatedBy.FullName, payment.CreatedAt, advance.Amount - cumulative));
        }
        return new(MapAdvance(advance, paid), totalOutstanding, paymentRows);
    }
    public async Task<IReadOnlyList<AccountingRecurringExpenseDto>> RecurringAsync(CancellationToken token)
    {
        EnsureAccess(); return await db.AccountingRecurringExpenses.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new AccountingRecurringExpenseDto(x.Id, x.Name, x.Amount, x.DueDay, x.AccountId, x.CashAccountId, x.StartsOn, x.EndsOn, x.Notes, x.IsActive)).ToArrayAsync(token);
    }
    public async Task<AccountingRecurringExpenseDto> SaveRecurringAsync(Guid? id, SaveRecurringExpenseRequest request, CancellationToken token)
    {
        EnsureManage(); CheckDates(request.StartsOn, request.EndsOn ?? request.StartsOn); await ValidateAccounts("FIXED", request.AccountId, request.CashAccountId, token);
        AccountingRecurringExpense row;
        try
        {
        row = id.HasValue ? await db.AccountingRecurringExpenses.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Recurring expense was not found.")
            : new AccountingRecurringExpense(request.Name, request.Amount, request.DueDay, request.AccountId, request.CashAccountId, request.StartsOn, request.EndsOn, request.Notes, user.UserId, DateTimeOffset.UtcNow);
        row.Update(request.Name, request.Amount, request.DueDay, request.AccountId, request.CashAccountId, request.StartsOn, request.EndsOn, request.Notes, request.IsActive);
        }
        catch (ArgumentException ex) { throw new HrValidationException(ex.Message); }
        if (!id.HasValue) db.AccountingRecurringExpenses.Add(row);
        Audit("RecurringExpenseSaved", row.Id, null, request); await db.SaveChangesAsync(token);
        return (await RecurringAsync(token)).Single(x => x.Id == row.Id);
    }
    public async Task<GenerateRecurringExpensesResult> GenerateRecurringAsync(GenerateRecurringExpensesRequest request, CancellationToken token)
    {
        EnsureManage(); await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        var templates = await db.AccountingRecurringExpenses.Where(x => x.IsActive).ToListAsync(token); int created = 0, exists = 0, outside = 0, notDue = 0;
        foreach (var template in templates)
        {
            var date = template.DueDate(request.Year, request.Month);
            if (!date.HasValue) { outside++; continue; }
            if (date.Value > Today()) { notDue++; continue; }
            var key = $"RECURRING:{template.Id}:{request.Year:D4}{request.Month:D2}";
            if (await db.AccountingBookEntries.AnyAsync(x => x.SourceKey == key, token)) { exists++; continue; }
            await ValidateAccounts("FIXED", template.AccountId, template.CashAccountId, token);
            await CreateRecordedEntry(new("FIXED", date.Value, template.Amount, template.AccountId, template.CashAccountId, template.Name, $"RECURRING-{request.Year:D4}{request.Month:D2}"), template.Id, key, token); created++;
        }
        if (tx is not null) await tx.CommitAsync(token);
        return new(created, exists, outside, notDue);
    }
}
