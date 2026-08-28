using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Finance;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Persistence.Configurations;

namespace MIS.Infrastructure.Services;

public sealed class FinanceService : IFinanceService, IFinancePostingService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserContext _user;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public FinanceService(ApplicationDbContext db, ICurrentUserContext user) { _db = db; _user = user; }

    public async Task<FinanceDashboardDto> GetDashboardAsync(CancellationToken token)
    {
        EnsureAccess(); var today = CairoToday(); var monthStart = new DateOnly(today.Year, today.Month, 1);
        var posted = _db.JournalEntryLines.AsNoTracking().Where(x => x.JournalEntry.Status == FinanceValues.JournalStatuses.Posted || x.JournalEntry.Status == FinanceValues.JournalStatuses.Reversed);
        var collectionsToday = await _db.JournalEntries.AsNoTracking().Where(x => x.EntryType == "COLLECTION" && x.Status == FinanceValues.JournalStatuses.Posted && x.PostingDate == today).SumAsync(x => x.TotalDebit, token);
        var collectionsMtd = await _db.JournalEntries.AsNoTracking().Where(x => x.EntryType == "COLLECTION" && x.Status == FinanceValues.JournalStatuses.Posted && x.PostingDate >= monthStart && x.PostingDate <= today).SumAsync(x => x.TotalDebit, token);
        var clientMoney = await AccountBalanceAsync(posted, FinanceSeed.ClientClearingAccountId, today, token) + await AccountBalanceAsync(posted, FinanceSeed.ClientPayableAccountId, today, token);
        var revenue = -await AccountBalanceAsync(posted, FinanceSeed.RevenueAccountId, today, token);
        var expenses = await posted.Where(x => x.Account.AccountType == FinanceValues.AccountTypes.Expense && x.JournalEntry.PostingDate >= monthStart && x.JournalEntry.PostingDate <= today).SumAsync(x => x.BaseDebit - x.BaseCredit, token);
        var pending = await _db.JournalEntries.CountAsync(x => x.Status == FinanceValues.JournalStatuses.Draft || x.Status == FinanceValues.JournalStatuses.PendingApproval || x.Status == FinanceValues.JournalStatuses.Approved, token);
        var failed = await _db.AccountingEvents.CountAsync(x => x.Status == FinanceValues.EventStatuses.Failed, token); var open = await _db.AccountingPeriods.CountAsync(x => x.Status == FinanceValues.PeriodStatuses.Open, token);
        return new(collectionsToday, collectionsMtd, -clientMoney, revenue, expenses, revenue - expenses, pending, failed, open, "EGP");
    }

    public async Task<IReadOnlyCollection<FinanceAccountDto>> GetAccountsAsync(CancellationToken token)
    {
        EnsureAccess(); var balances = await _db.JournalEntryLines.AsNoTracking().Where(x => x.JournalEntry.Status == FinanceValues.JournalStatuses.Posted || x.JournalEntry.Status == FinanceValues.JournalStatuses.Reversed).GroupBy(x => x.AccountId).Select(g => new { Id = g.Key, Balance = g.Sum(x => x.BaseDebit - x.BaseCredit) }).ToDictionaryAsync(x => x.Id, x => x.Balance, token);
        var accounts = await _db.FinanceAccounts.AsNoTracking().OrderBy(x => x.Code).ToArrayAsync(token);
        return accounts.Select(x => new FinanceAccountDto(x.Id, x.Code, x.NameArabic, x.NameEnglish, x.AccountType, x.NormalBalance, x.PostingAllowed, x.ControlAccountType, balances.GetValueOrDefault(x.Id))).ToArray();
    }

    public async Task<IReadOnlyCollection<AccountingPeriodDto>> GetPeriodsAsync(int? year, CancellationToken token)
    { EnsureAccess(); var query = _db.AccountingPeriods.AsNoTracking().AsQueryable(); if (year.HasValue) query = query.Where(x => x.Year == year); return await query.OrderByDescending(x => x.Year).ThenBy(x => x.PeriodNumber).Select(x => PeriodDto(x)).ToArrayAsync(token); }

    public async Task<IReadOnlyCollection<AccountingPeriodDto>> InitializeYearAsync(int year, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceConfigurationManage); if (year is < 2000 or > 2200) throw new HrValidationException("Fiscal year is invalid.");
        var legalEntity = await DefaultLegalEntityAsync(token); var existing = await _db.AccountingPeriods.Where(x => x.LegalEntityId == legalEntity.Id && x.Year == year).ToArrayAsync(token);
        for (var month = 1; month <= 12; month++) if (existing.All(x => x.PeriodNumber != month)) _db.AccountingPeriods.Add(new AccountingPeriod(legalEntity.Id, year, month, $"{year}-{month:00}", new DateOnly(year, month, 1), new DateOnly(year, month, DateTime.DaysInMonth(year, month))));
        AddAudit("FiscalYearInitialized", "AccountingPeriod", legalEntity.Id, null, new { Year = year }, null); await _db.SaveChangesAsync(token); return await GetPeriodsAsync(year, token);
    }

    public async Task<AccountingPeriodDto> ChangePeriodStatusAsync(Guid id, string action, PeriodActionRequest request, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinancePeriodClose); var period = await _db.AccountingPeriods.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Accounting period was not found."); var before = period.Status; var normalized = action.Trim().ToLowerInvariant();
        if (normalized == "soft-close") period.SoftClose(_user.UserId, DateTimeOffset.UtcNow); else if (normalized == "close") { if (await _db.JournalEntries.AnyAsync(x => x.PeriodId == id && x.Status != FinanceValues.JournalStatuses.Posted && x.Status != FinanceValues.JournalStatuses.Reversed, token)) throw new HrConflictException("The period has unposted journals."); period.Close(_user.UserId, request.Reason, DateTimeOffset.UtcNow); } else if (normalized == "reopen") period.Reopen(request.Reason); else throw new HrValidationException("Period action is invalid.");
        AddAudit($"Period{normalized}", nameof(AccountingPeriod), period.Id, new { Status = before }, new { period.Status }, request.Reason); await _db.SaveChangesAsync(token); return PeriodDto(period);
    }

    public async Task<FinancePagedResultDto<FinanceJournalListItemDto>> GetJournalsAsync(int page, int pageSize, string? status, DateOnly? from, DateOnly? to, CancellationToken token)
    {
        EnsureAccess(); ValidatePage(page, pageSize); var query = _db.JournalEntries.AsNoTracking(); if (!string.IsNullOrWhiteSpace(status)) { var s = status.Trim().ToUpperInvariant(); query = query.Where(x => x.Status == s); } if (from.HasValue) query = query.Where(x => x.PostingDate >= from); if (to.HasValue) query = query.Where(x => x.PostingDate <= to);
        var count = await query.CountAsync(token); var items = await query.OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new FinanceJournalListItemDto(x.Id, x.JournalNumber, x.EntryType, x.PostingDate, x.Description, x.TotalDebit, x.TotalCredit, x.Status, x.CreatedAt)).ToArrayAsync(token); return new(items, page, pageSize, count, (int)Math.Ceiling(count / (double)pageSize));
    }

    public async Task<JournalDto> GetJournalAsync(Guid id, CancellationToken token) { EnsureAccess(); return MapJournal(await JournalQuery().SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Journal was not found.")); }

    public async Task<JournalDto> CreateManualJournalAsync(CreateManualJournalRequest request, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceJournalCreate); if (request.Lines.Count < 2) throw new HrValidationException("At least two journal lines are required.");
        var legalEntity = await DefaultLegalEntityAsync(token); if (!string.Equals(request.CurrencyCode, legalEntity.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase)) throw new HrConflictException("An approved exchange rate is required before posting a foreign-currency journal."); var period = await GetPostingPeriodAsync(legalEntity.Id, request.PostingDate, false, token); var accountIds = request.Lines.Select(x => x.AccountId).Distinct().ToArray(); var accounts = await _db.FinanceAccounts.Where(x => accountIds.Contains(x.Id) && x.IsActive && x.PostingAllowed).ToDictionaryAsync(x => x.Id, token); if (accounts.Count != accountIds.Length) throw new HrValidationException("One or more posting accounts are invalid.");
        var journal = new JournalEntry(legalEntity.Id, period.Id, null, "MANUAL", request.TransactionDate, request.PostingDate, request.CurrencyCode, request.Description, _user.UserId, DateTimeOffset.UtcNow);
        foreach (var item in request.Lines) { if ((item.Debit > 0) == (item.Credit > 0)) throw new HrValidationException("Each line must contain either debit or credit."); var account = accounts[item.AccountId]; if (account.ControlAccountType is not null && !_user.Roles.Contains(SystemRoleNames.Admin) && !_user.Permissions.Contains(SystemPermissionCodes.FinanceConfigurationManage)) throw new HrForbiddenException("Control accounts require a specialized finance permission."); ValidateDimensions(account, item.ClientId, item.CollectorId, item.BranchId); journal.AddLine(item.AccountId, item.Debit, item.Credit, item.Debit, item.Credit, 1, item.Description, item.ClientId, item.CollectorId, item.BranchId); }
        _db.JournalEntries.Add(journal); AddAudit("ManualJournalCreated", nameof(JournalEntry), journal.Id, null, new { request.Description, request.PostingDate, Lines = request.Lines.Count }, null); await _db.SaveChangesAsync(token); return await GetJournalAsync(journal.Id, token);
    }

    public async Task<JournalDto> SubmitJournalAsync(Guid id, CancellationToken token) { EnsurePermission(SystemPermissionCodes.FinanceJournalCreate); var journal = await LoadJournalAsync(id, token); journal.Submit(); AddAudit("JournalSubmitted", nameof(JournalEntry), id, new { Status = FinanceValues.JournalStatuses.Draft }, new { journal.Status }, null); await _db.SaveChangesAsync(token); return await GetJournalAsync(id, token); }
    public async Task<JournalDto> ApproveJournalAsync(Guid id, CancellationToken token) { EnsurePermission(SystemPermissionCodes.FinanceJournalApprove); var journal = await LoadJournalAsync(id, token); journal.Approve(_user.UserId, DateTimeOffset.UtcNow); AddAudit("JournalApproved", nameof(JournalEntry), id, new { Status = FinanceValues.JournalStatuses.PendingApproval }, new { journal.Status }, null); await _db.SaveChangesAsync(token); return await GetJournalAsync(id, token); }

    public async Task<JournalDto> PostJournalAsync(Guid id, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceJournalPost); await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token); var journal = await LoadJournalAsync(id, token); await EnsurePeriodAllowsPostingAsync(journal.PeriodId, false, token); journal.Post(NewJournalNumber(journal.PostingDate, journal.Id), _user.UserId, DateTimeOffset.UtcNow); AddAudit("JournalPosted", nameof(JournalEntry), id, new { Status = FinanceValues.JournalStatuses.Approved }, new { journal.Status, journal.JournalNumber }, null); await _db.SaveChangesAsync(token); await tx.CommitAsync(token); return await GetJournalAsync(id, token);
    }

    public async Task<JournalDto> ReverseJournalAsync(Guid id, PeriodActionRequest request, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceTransactionReverse);
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        if (await _db.CollectionFinancialReceipts.AnyAsync(x => x.JournalEntryId == id || x.ClearingJournalEntryId == id, token))
            throw new HrConflictException("Collection journals must be reversed from the financial collection screen to keep the receipt, case balance, client ledger, and custody synchronized.");
        var original = await LoadJournalAsync(id, token);
        if (original.Status != FinanceValues.JournalStatuses.Posted)
            throw new HrConflictException("Only a posted journal can be reversed.");
        var reversal = await CreateReversalJournalAsync(original, $"Reversal / عكس القيد: {request.Reason}", CairoToday(), token);
        await _db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
        return await GetJournalAsync(reversal.Id, token);
    }

    public async Task<ClientLedgerDto> GetClientLedgerAsync(Guid clientId, DateOnly? from, DateOnly? to, CancellationToken token)
    {
        EnsureAccess(); var client = await _db.CollectionClientOrganizations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == clientId, token) ?? throw new HrNotFoundException("Client was not found."); var query = _db.ClientLedgerEntries.AsNoTracking().Where(x => x.ClientId == clientId && (x.JournalEntryLine.JournalEntry.Status == FinanceValues.JournalStatuses.Posted || x.JournalEntryLine.JournalEntry.Status == FinanceValues.JournalStatuses.Reversed)); if (from.HasValue) query = query.Where(x => x.TransactionDate >= from); if (to.HasValue) query = query.Where(x => x.TransactionDate <= to);
        var raw = await query.OrderBy(x => x.TransactionDate).ThenBy(x => x.CreatedAt).Select(x => new { x.Id, x.TransactionDate, x.EntryType, JournalNumber = x.JournalEntryLine.JournalEntry.JournalNumber!, x.JournalEntryLine.Description, x.Debit, x.Credit, x.CurrencyCode, x.SourceId }).ToArrayAsync(token); decimal running = 0; var rows = raw.Select(x => { running += x.Credit - x.Debit; return new ClientLedgerEntryDto(x.Id, x.TransactionDate, x.EntryType, x.JournalNumber, x.Description, x.Debit, x.Credit, running, x.CurrencyCode, x.SourceId); }).ToArray(); return new(client.Id, client.Code, client.NameArabic, client.NameEnglish, running, "EGP", rows);
    }

    public async Task<TrialBalanceDto> GetTrialBalanceAsync(DateOnly asOf, CancellationToken token)
    {
        EnsureAccess();
        var accounts = await _db.FinanceAccounts.AsNoTracking()
            .Where(x => x.PostingAllowed)
            .OrderBy(x => x.Code)
            .Select(x => new { x.Id, x.Code, x.NameArabic, x.NameEnglish, x.AccountType })
            .ToArrayAsync(token);
        var movements = await _db.JournalEntryLines.AsNoTracking()
            .Where(x => (x.JournalEntry.Status == FinanceValues.JournalStatuses.Posted
                         || x.JournalEntry.Status == FinanceValues.JournalStatuses.Reversed)
                        && x.JournalEntry.PostingDate <= asOf)
            .GroupBy(x => x.AccountId)
            .Select(group => new
            {
                AccountId = group.Key,
                Debit = group.Sum(x => x.BaseDebit),
                Credit = group.Sum(x => x.BaseCredit)
            })
            .ToDictionaryAsync(x => x.AccountId, token);
        var rows = accounts.Select(account =>
        {
            movements.TryGetValue(account.Id, out var movement);
            var debit = movement?.Debit ?? 0;
            var credit = movement?.Credit ?? 0;
            return new TrialBalanceRowDto(account.Id, account.Code, account.NameArabic, account.NameEnglish,
                account.AccountType, debit, credit, debit - credit);
        }).ToArray();
        return new TrialBalanceDto(asOf, "EGP", rows.Sum(x => x.DebitMovement), rows.Sum(x => x.CreditMovement), rows);
    }

    public async Task<FinancePagedResultDto<FinancialAuditDto>> GetAuditAsync(int page, int pageSize, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceAuditView); ValidatePage(page, pageSize); var query = _db.FinancialAuditLogs.AsNoTracking(); var count = await query.CountAsync(token); var rows = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new FinancialAuditDto(x.Id, x.Action, x.EntityType, x.EntityId, x.Actor == null ? "SYSTEM" : x.Actor.FullName, x.Reason, x.CreatedAt)).ToArrayAsync(token); return new(rows, page, pageSize, count, (int)Math.Ceiling(count / (double)pageSize));
    }

    public async Task<FinancePagedResultDto<CollectionFinanceListItemDto>> GetFinancialCollectionsAsync(
        int page, int pageSize, string? status, string? channel, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceCollectionReview);
        ValidatePage(page, pageSize);
        var query = ApplyCollectionClientScope(_db.CollectionFinancialReceipts.AsNoTracking(), SystemPermissionCodes.FinanceCollectionReview);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpperInvariant();
            query = query.Where(x => x.Status == normalized);
        }
        if (!string.IsNullOrWhiteSpace(channel))
        {
            var normalized = channel.Trim().ToUpperInvariant();
            query = query.Where(x => x.Channel == normalized);
        }

        var count = await query.CountAsync(token);
        var rows = await query.OrderByDescending(x => x.CollectionPayment.PaymentDate).ThenByDescending(x => x.PostedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new CollectionFinanceListItemDto(
                x.CollectionPaymentId, x.Id, x.CollectionPayment.ReferenceNumber, x.CollectionPayment.PaymentDate,
                x.Client.Code, x.Client.NameArabic, x.Client.NameEnglish, x.GrossAmount, x.CurrencyCode, x.Channel,
                x.Status, x.Collector == null ? "—" : x.Collector.FullName, x.JournalEntry.JournalNumber!,
                x.ClearingJournalEntry == null ? null : x.ClearingJournalEntry.JournalNumber))
            .ToArrayAsync(token);
        return new(rows, page, pageSize, count, (int)Math.Ceiling(count / (double)pageSize));
    }

    public async Task<CollectionFinanceDto> GetFinancialCollectionAsync(Guid paymentId, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceCollectionReview);
        var receipt = await ApplyCollectionClientScope(ReceiptQuery(), SystemPermissionCodes.FinanceCollectionReview).SingleOrDefaultAsync(x => x.CollectionPaymentId == paymentId, token)
            ?? throw new HrNotFoundException("The financial collection receipt was not found.");
        return MapReceipt(receipt);
    }

    public async Task<CollectionFinanceDto> ClearCollectionAsync(Guid paymentId, ClearCollectionRequest request, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceCustodyReconcile);
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var receipt = await ReceiptQuery(false).SingleOrDefaultAsync(x => x.CollectionPaymentId == paymentId, token)
            ?? throw new HrNotFoundException("The financial collection receipt was not found.");
        if (receipt.Status == FinanceValues.CollectionReceiptStatuses.Cleared)
        {
            await tx.CommitAsync(token);
            return MapReceipt(receipt);
        }
        if (receipt.Status == FinanceValues.CollectionReceiptStatuses.Reversed)
            throw new HrConflictException("A reversed collection cannot be cleared.");
        if (request.ClearedOn < receipt.CollectionPayment.PaymentDate)
            throw new HrValidationException("The clearing date cannot be earlier than the collection date.");

        var key = $"CollectionCleared:CollectionFinancialReceipt:{receipt.Id}:1";
        if (await _db.AccountingEvents.AnyAsync(x => x.IdempotencyKey == key, token))
            throw new HrConflictException("This collection already has a clearing event. Refresh the page to see its current status.");

        var legalEntity = await DefaultLegalEntityAsync(token);
        var period = await GetPostingPeriodAsync(legalEntity.Id, request.ClearedOn, false, token);
        var sourceAccountId = ResolveCollectionChannel(receipt.Channel).AssetAccountId;
        var now = DateTimeOffset.UtcNow;
        var accountingEvent = new AccountingEvent("CollectionCleared", nameof(CollectionFinancialReceipt), receipt.Id, 1,
            key, JsonSerializer.Serialize(new { receipt.Id, receipt.CollectionPaymentId, receipt.GrossAmount, receipt.Channel, request.ClearedOn, request.Reference }, JsonOptions), now);
        _db.AccountingEvents.Add(accountingEvent);

        var journal = new JournalEntry(legalEntity.Id, period.Id, accountingEvent.Id, "COLLECTION_CLEARING",
            receipt.CollectionPayment.PaymentDate, request.ClearedOn, receipt.CurrencyCode,
            $"Collection clearing / تسوية التحصيل - {receipt.CollectionPayment.ReferenceNumber}", _user.UserId, now);
        journal.AddLine(FinanceSeed.BankAccountId, receipt.GrossAmount, 0, receipt.BaseAmount, 0, receipt.ExchangeRate,
            $"Bank receipt / إيداع بنكي - {request.Reference}", receipt.ClientId);
        var sourceCredit = journal.AddLine(sourceAccountId, 0, receipt.GrossAmount, 0, receipt.BaseAmount, receipt.ExchangeRate,
            $"Clear collection channel / تسوية قناة التحصيل - {receipt.CollectionPayment.ReferenceNumber}", receipt.ClientId,
            sourceAccountId == FinanceSeed.CustodyAccountId ? receipt.CollectorId : null);
        var clearingDebit = journal.AddLine(FinanceSeed.ClientClearingAccountId, receipt.GrossAmount, 0, receipt.BaseAmount, 0,
            receipt.ExchangeRate, $"Reclass client clearing / إعادة تصنيف أموال العميل - {receipt.CollectionPayment.ReferenceNumber}", receipt.ClientId);
        var payableCredit = journal.AddLine(FinanceSeed.ClientPayableAccountId, 0, receipt.GrossAmount, 0, receipt.BaseAmount,
            receipt.ExchangeRate, $"Client funds payable / أموال العميل المستحقة - {receipt.CollectionPayment.ReferenceNumber}", receipt.ClientId);
        journal.Post(NewJournalNumber(request.ClearedOn, journal.Id), _user.UserId, now, true);
        accountingEvent.MarkPosted();
        receipt.MarkCleared(journal.Id, now);

        _db.JournalEntries.Add(journal);
        _db.CollectionClearingEvents.Add(new CollectionClearingEvent(receipt.Id, sourceAccountId, FinanceSeed.BankAccountId,
            journal.Id, receipt.GrossAmount, request.Reference, request.ClearedOn, _user.UserId, now));
        _db.ClientLedgerEntries.AddRange(
            new ClientLedgerEntry(receipt.ClientId, clearingDebit.Id, "COLLECTION_CLEARING", receipt.GrossAmount, 0,
                receipt.CurrencyCode, receipt.Id, request.ClearedOn, now),
            new ClientLedgerEntry(receipt.ClientId, payableCredit.Id, "CLIENT_FUNDS_PAYABLE", 0, receipt.GrossAmount,
                receipt.CurrencyCode, receipt.Id, request.ClearedOn, now));

        if (sourceAccountId == FinanceSeed.CustodyAccountId && receipt.CollectorId.HasValue)
        {
            var custody = await GetOrCreateCustodyAccountAsync(receipt.CollectorId.Value, receipt.CurrencyCode, now, token);
            _db.CollectorCustodyTransactions.Add(new CollectorCustodyTransaction(custody.Id, sourceCredit.Id, receipt.Id,
                FinanceValues.CustodyTransactionTypes.Handover, 0, receipt.GrossAmount, receipt.Id, request.ClearedOn, now));
        }

        AddAudit("CollectionCleared", nameof(CollectionFinancialReceipt), receipt.Id,
            new { Status = FinanceValues.CollectionReceiptStatuses.Posted },
            new { receipt.Status, ClearingJournalEntryId = journal.Id, journal.JournalNumber, request.Reference }, request.Reference);
        await _db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
        return MapReceipt(await ReceiptQuery().SingleAsync(x => x.CollectionPaymentId == paymentId, token));
    }

    public async Task<CollectionFinanceDto> ReverseCollectionAsync(Guid paymentId, PeriodActionRequest request, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceTransactionReverse);
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var receipt = await ReceiptQuery(false).SingleOrDefaultAsync(x => x.CollectionPaymentId == paymentId, token)
            ?? throw new HrNotFoundException("The financial collection receipt was not found.");
        if (receipt.Status == FinanceValues.CollectionReceiptStatuses.Reversed)
        {
            await tx.CommitAsync(token);
            return MapReceipt(receipt);
        }

        var today = CairoToday();
        if (receipt.ClearingJournalEntryId.HasValue)
        {
            var clearing = await LoadJournalAsync(receipt.ClearingJournalEntryId.Value, token);
            if (clearing.Status == FinanceValues.JournalStatuses.Posted)
                await CreateReversalJournalAsync(clearing, $"Collection clearing reversal / عكس تسوية التحصيل: {request.Reason}", today, token);
        }

        var original = await LoadJournalAsync(receipt.JournalEntryId, token);
        if (original.Status != FinanceValues.JournalStatuses.Posted)
            throw new HrConflictException("The original collection journal is not available for reversal.");
        var reversal = await CreateReversalJournalAsync(original, $"Collection reversal / عكس التحصيل: {request.Reason}", today, token);
        receipt.MarkReversed(reversal.Id, DateTimeOffset.UtcNow);
        receipt.CollectionPayment.MarkFinanciallyReversed(reversal.Id);
        foreach (var allocation in receipt.Allocations)
            allocation.Case.RestoreApprovedPayment(allocation.OutstandingBefore, allocation.OverdueBefore, DateTimeOffset.UtcNow);

        var eventKey = $"CollectionReversed:CollectionFinancialReceipt:{receipt.Id}:1";
        var reversalEvent = new AccountingEvent("CollectionReversed", nameof(CollectionFinancialReceipt), receipt.Id, 1,
            eventKey, JsonSerializer.Serialize(new { receipt.Id, receipt.CollectionPaymentId, ReversalJournalEntryId = reversal.Id, request.Reason }, JsonOptions), DateTimeOffset.UtcNow);
        reversalEvent.MarkPosted();
        _db.AccountingEvents.Add(reversalEvent);
        AddAudit("CollectionReversed", nameof(CollectionFinancialReceipt), receipt.Id, null,
            new { receipt.Status, ReversalJournalEntryId = reversal.Id, reversal.JournalNumber }, request.Reason);
        await _db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
        return MapReceipt(await ReceiptQuery().SingleAsync(x => x.CollectionPaymentId == paymentId, token));
    }

    public async Task<IReadOnlyCollection<CustodySummaryDto>> GetCustodiesAsync(CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceCustodyView);
        var accounts = await _db.CollectorCustodyAccounts.AsNoTracking().Include(x => x.Collector)
            .OrderBy(x => x.Collector.FullName).ToArrayAsync(token);
        var movements = await _db.CollectorCustodyTransactions.AsNoTracking()
            .Select(x => new { x.CustodyAccountId, x.Debit, x.Credit, x.TransactionDate }).ToArrayAsync(token);
        return accounts.Select(account =>
        {
            var accountMovements = movements.Where(x => x.CustodyAccountId == account.Id).ToArray();
            var balance = accountMovements.Sum(x => x.Debit - x.Credit);
            var oldest = balance > 0 ? accountMovements.Where(x => x.Debit > 0).Select(x => (DateOnly?)x.TransactionDate).Min() : null;
            return new CustodySummaryDto(account.Id, account.CollectorId, account.Collector.FullName, account.CurrencyCode,
                balance, account.SoftLimit, account.HardLimit, balance > account.SoftLimit, balance > account.HardLimit, oldest, account.Status);
        }).ToArray();
    }

    public async Task<CustodyDetailsDto> GetCustodyAsync(Guid collectorId, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceCustodyView);
        return await LoadCustodyAsync(collectorId, token);
    }

    private async Task<CustodyDetailsDto> LoadCustodyAsync(Guid collectorId, CancellationToken token)
    {
        var account = await _db.CollectorCustodyAccounts.AsNoTracking().Include(x => x.Collector)
            .SingleOrDefaultAsync(x => x.CollectorId == collectorId, token)
            ?? throw new HrNotFoundException("The collector custody account was not found.");
        var raw = await _db.CollectorCustodyTransactions.AsNoTracking()
            .Where(x => x.CustodyAccountId == account.Id)
            .OrderBy(x => x.TransactionDate).ThenBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id, x.TransactionDate, x.TransactionType, x.Debit, x.Credit, x.SourceId,
                PaymentId = x.Receipt == null ? (Guid?)null : x.Receipt.CollectionPaymentId,
                PaymentReference = x.Receipt == null ? null : x.Receipt.CollectionPayment.ReferenceNumber,
                JournalEntryId = x.JournalEntryLine.JournalEntryId,
                JournalNumber = x.JournalEntryLine.JournalEntry.JournalNumber!
            }).ToArrayAsync(token);
        decimal running = 0;
        var transactions = raw.Select(x =>
        {
            running += x.Debit - x.Credit;
            return new CustodyTransactionDto(x.Id, x.TransactionDate, x.TransactionType, x.Debit, x.Credit, running,
                x.SourceId, x.PaymentId, x.PaymentReference, x.JournalEntryId, x.JournalNumber);
        }).ToArray();
        var oldest = running > 0 ? raw.Where(x => x.Debit > 0).Select(x => (DateOnly?)x.TransactionDate).Min() : null;
        var summary = new CustodySummaryDto(account.Id, account.CollectorId, account.Collector.FullName, account.CurrencyCode,
            running, account.SoftLimit, account.HardLimit, running > account.SoftLimit, running > account.HardLimit, oldest, account.Status);
        return new CustodyDetailsDto(summary, transactions);
    }

    public async Task<CustodyDetailsDto> UpdateCustodyLimitsAsync(Guid collectorId, UpdateCustodyLimitsRequest request, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceConfigurationManage);
        var account = await _db.CollectorCustodyAccounts.Include(x => x.Collector)
            .SingleOrDefaultAsync(x => x.CollectorId == collectorId && x.CurrencyCode == "EGP", token)
            ?? throw new HrNotFoundException("The collector custody account was not found.");
        var before = new { account.SoftLimit, account.HardLimit };
        account.UpdateLimits(request.SoftLimit, request.HardLimit);
        AddAudit("CollectorCustodyLimitsUpdated", nameof(CollectorCustodyAccount), account.Id, before,
            new { account.SoftLimit, account.HardLimit }, request.Reason);
        await _db.SaveChangesAsync(token);
        return await LoadCustodyAsync(collectorId, token);
    }

    public async Task<JournalEntry> PostApprovedCollectionAsync(CollectionPayment payment, CollectionCase collectionCase, CancellationToken token)
    {
        var key = $"CollectionConfirmed:CollectionPayment:{payment.Id}:1";
        var tracked = _db.AccountingEvents.Local.SingleOrDefault(x => x.IdempotencyKey == key);
        var existing = tracked ?? await _db.AccountingEvents.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == key, token);
        if (existing is not null)
        {
            var existingJournal = _db.JournalEntries.Local.SingleOrDefault(x => x.AccountingEventId == existing.Id)
                ?? await _db.JournalEntries.SingleAsync(x => x.AccountingEventId == existing.Id, token);
            payment.MarkFinanciallyPosted(existingJournal.Id);
            return existingJournal;
        }

        var legalEntity = await DefaultLegalEntityAsync(token);
        if (!string.Equals(payment.CurrencyCode, legalEntity.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw new HrConflictException("Collection approval is blocked until an approved exchange rate and posting profile exist for the currency.");

        var period = await GetPostingPeriodAsync(legalEntity.Id, payment.PaymentDate, true, token);
        if (payment.Amount > collectionCase.OutstandingBalance)
            throw new HrConflictException($"The collection amount exceeds the case outstanding balance. An explicit overpayment workflow is required for the excess {payment.Amount - collectionCase.OutstandingBalance:N2} {payment.CurrencyCode}.");
        var clientId = await _db.CollectionPortfolios.Where(x => x.Id == collectionCase.PortfolioId).Select(x => x.OrganizationId).SingleAsync(token);
        var collectorId = collectionCase.AssignedCollectorId ?? payment.SubmittedById;
        var channel = ResolveCollectionChannel(payment.Method);
        if (channel.Channel == FinanceValues.CollectionChannels.DirectClient)
            throw new HrConflictException("Direct-to-client payments require the dedicated confirmation workflow and cannot be posted as company cash.");

        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new
        {
            payment.Id,
            payment.CaseId,
            payment.Amount,
            payment.CurrencyCode,
            payment.PaymentDate,
            payment.Method,
            payment.ReferenceNumber,
            Channel = channel.Channel,
            ClientId = clientId,
            CollectorId = collectorId
        }, JsonOptions);
        var accountingEvent = new AccountingEvent("CollectionConfirmed", nameof(CollectionPayment), payment.Id, 1, key, payload, payment.VerifiedAt ?? now);
        _db.AccountingEvents.Add(accountingEvent);

        var journal = new JournalEntry(legalEntity.Id, period.Id, accountingEvent.Id, "COLLECTION", payment.PaymentDate,
            payment.PaymentDate, payment.CurrencyCode, $"Collection / تحصيل - {payment.ReferenceNumber}", _user.UserId, now);
        var debit = journal.AddLine(channel.AssetAccountId, payment.Amount, 0, payment.Amount, 0, 1,
            $"Collection channel / قناة التحصيل - {payment.Method}", clientId,
            channel.AssetAccountId == FinanceSeed.CustodyAccountId ? collectorId : null);
        var credit = journal.AddLine(FinanceSeed.ClientClearingAccountId, 0, payment.Amount, 0, payment.Amount, 1,
            $"Client money / أموال العميل - {payment.ReferenceNumber}", clientId);
        journal.Post(NewJournalNumber(payment.PaymentDate, journal.Id), _user.UserId, now, true);
        accountingEvent.MarkPosted();
        payment.MarkFinanciallyPosted(journal.Id);

        var receipt = new CollectionFinancialReceipt(payment.Id, clientId, payment.Amount, payment.CurrencyCode,
            channel.Channel, channel.DestinationType, payment.ReferenceNumber, collectorId, null, now);
        receipt.AddAllocation(collectionCase.Id, payment.Amount, collectionCase.OutstandingBalance, collectionCase.OverdueBalance, now);
        receipt.LinkPostedJournal(journal.Id);

        _db.JournalEntries.Add(journal);
        _db.CollectionFinancialReceipts.Add(receipt);
        _db.ClientLedgerEntries.Add(new ClientLedgerEntry(clientId, credit.Id, "COLLECTION", 0, payment.Amount,
            payment.CurrencyCode, payment.Id, payment.PaymentDate, now));

        if (channel.AssetAccountId == FinanceSeed.CustodyAccountId)
        {
            var custody = await GetOrCreateCustodyAccountAsync(collectorId, payment.CurrencyCode, now, token);
            var existingBalance = await CustodyBalanceAsync(custody.Id, token);
            if (existingBalance + payment.Amount > custody.HardLimit)
                throw new HrConflictException($"Collector custody hard limit would be exceeded. Current: {existingBalance:N2}, limit: {custody.HardLimit:N2} {custody.CurrencyCode}.");
            _db.CollectorCustodyTransactions.Add(new CollectorCustodyTransaction(custody.Id, debit.Id, receipt.Id,
                FinanceValues.CustodyTransactionTypes.Collection, payment.Amount, 0, payment.Id, payment.PaymentDate, now));
            if (existingBalance + payment.Amount > custody.SoftLimit)
                AddAudit("CollectorCustodySoftLimitExceeded", nameof(CollectorCustodyAccount), custody.Id,
                    new { Balance = existingBalance }, new { Balance = existingBalance + payment.Amount, custody.SoftLimit }, null);
        }

        AddAudit("CollectionFinanciallyPosted", nameof(CollectionPayment), payment.Id, null,
            new { JournalEntryId = journal.Id, journal.JournalNumber, ReceiptId = receipt.Id, DebitLineId = debit.Id, Amount = payment.Amount, payment.CurrencyCode, channel.Channel }, null);
        return journal;
    }

    private async Task<FinanceLegalEntity> DefaultLegalEntityAsync(CancellationToken token) => await _db.FinanceLegalEntities.SingleOrDefaultAsync(x => x.Id == FinanceSeed.LegalEntityId, token) ?? throw new HrConflictException("The default finance legal entity is not configured.");
    private async Task<CollectorCustodyAccount> GetOrCreateCustodyAccountAsync(Guid collectorId, string currencyCode, DateTimeOffset now, CancellationToken token)
    {
        var normalized = currencyCode.Trim().ToUpperInvariant();
        var local = _db.CollectorCustodyAccounts.Local.SingleOrDefault(x => x.CollectorId == collectorId && x.CurrencyCode == normalized);
        if (local is not null) return local;
        var account = await _db.CollectorCustodyAccounts.SingleOrDefaultAsync(x => x.CollectorId == collectorId && x.CurrencyCode == normalized, token);
        if (account is not null) return account;
        account = new CollectorCustodyAccount(collectorId, normalized, null, 25_000m, 50_000m, now);
        _db.CollectorCustodyAccounts.Add(account);
        return account;
    }
    private async Task<decimal> CustodyBalanceAsync(Guid accountId, CancellationToken token)
    {
        var persisted = await _db.CollectorCustodyTransactions.AsNoTracking().Where(x => x.CustodyAccountId == accountId)
            .SumAsync(x => x.Debit - x.Credit, token);
        var pending = _db.ChangeTracker.Entries<CollectorCustodyTransaction>()
            .Where(x => x.State == EntityState.Added && x.Entity.CustodyAccountId == accountId)
            .Sum(x => x.Entity.Debit - x.Entity.Credit);
        return persisted + pending;
    }
    private async Task<JournalEntry> CreateReversalJournalAsync(JournalEntry original, string description, DateOnly postingDate, CancellationToken token)
    {
        if (original.Status != FinanceValues.JournalStatuses.Posted)
            throw new HrConflictException("Only a posted journal can be reversed.");
        var period = await GetPostingPeriodAsync(original.LegalEntityId, postingDate, false, token);
        var now = DateTimeOffset.UtcNow;
        var reversal = new JournalEntry(original.LegalEntityId, period.Id, null, "REVERSAL", original.TransactionDate,
            postingDate, original.CurrencyCode, description, _user.UserId, now);
        reversal.LinkReversal(original.Id);
        var originalLineIds = original.Lines.Select(x => x.Id).ToArray();
        var clientEntries = await _db.ClientLedgerEntries.AsNoTracking().Where(x => originalLineIds.Contains(x.JournalEntryLineId))
            .ToDictionaryAsync(x => x.JournalEntryLineId, token);
        var custodyEntries = await _db.CollectorCustodyTransactions.AsNoTracking().Where(x => originalLineIds.Contains(x.JournalEntryLineId))
            .ToDictionaryAsync(x => x.JournalEntryLineId, token);
        foreach (var line in original.Lines.OrderBy(x => x.LineNumber))
        {
            var reverseLine = reversal.AddLine(line.AccountId, line.Credit, line.Debit, line.BaseCredit, line.BaseDebit,
                line.ExchangeRate, $"{line.Description} — REVERSAL / عكس", line.ClientId, line.CollectorId, line.BranchId, line.CostCenterId);
            if (clientEntries.TryGetValue(line.Id, out var clientEntry))
                _db.ClientLedgerEntries.Add(new ClientLedgerEntry(clientEntry.ClientId, reverseLine.Id, "REVERSAL",
                    clientEntry.Credit, clientEntry.Debit, clientEntry.CurrencyCode, original.Id, postingDate, now));
            if (custodyEntries.TryGetValue(line.Id, out var custodyEntry))
                _db.CollectorCustodyTransactions.Add(new CollectorCustodyTransaction(custodyEntry.CustodyAccountId,
                    reverseLine.Id, custodyEntry.ReceiptId, FinanceValues.CustodyTransactionTypes.Reversal,
                    custodyEntry.Credit, custodyEntry.Debit, original.Id, postingDate, now));
        }
        reversal.Post(NewJournalNumber(postingDate, reversal.Id), _user.UserId, now, true);
        original.MarkReversed();
        _db.JournalEntries.Add(reversal);
        AddAudit("JournalReversed", nameof(JournalEntry), original.Id,
            new { Status = FinanceValues.JournalStatuses.Posted },
            new { original.Status, ReversalId = reversal.Id, reversal.JournalNumber }, description);
        return reversal;
    }
    private async Task<AccountingPeriod> GetPostingPeriodAsync(Guid legalEntityId, DateOnly date, bool createIfMissing, CancellationToken token) { var value = await _db.AccountingPeriods.SingleOrDefaultAsync(x => x.LegalEntityId == legalEntityId && x.StartDate <= date && x.EndDate >= date, token); if (value is null && createIfMissing) { value = new AccountingPeriod(legalEntityId, date.Year, date.Month, $"{date:yyyy-MM}", new DateOnly(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month))); _db.AccountingPeriods.Add(value); } if (value is null) throw new HrConflictException("No accounting period is configured for the posting date."); if (value.Status != FinanceValues.PeriodStatuses.Open) throw new HrConflictException("The accounting period is not open."); return value; }
    private async Task EnsurePeriodAllowsPostingAsync(Guid periodId, bool allowSoftClosed, CancellationToken token) { var status = await _db.AccountingPeriods.Where(x => x.Id == periodId).Select(x => x.Status).SingleAsync(token); if (status == FinanceValues.PeriodStatuses.Closed || (!allowSoftClosed && status == FinanceValues.PeriodStatuses.SoftClosed)) throw new HrConflictException("The accounting period does not allow this posting."); }
    private async Task<JournalEntry> LoadJournalAsync(Guid id, CancellationToken token) => await _db.JournalEntries.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Journal was not found.");
    private IQueryable<JournalEntry> JournalQuery() => _db.JournalEntries.AsNoTracking().Include(x => x.CreatedBy).Include(x => x.Lines).ThenInclude(x => x.Account);
    private IQueryable<CollectionFinancialReceipt> ReceiptQuery(bool noTracking = true)
    {
        IQueryable<CollectionFinancialReceipt> query = _db.CollectionFinancialReceipts;
        if (noTracking) query = query.AsNoTracking();
        return query
            .Include(x => x.CollectionPayment)
            .Include(x => x.Client)
            .Include(x => x.Collector)
            .Include(x => x.JournalEntry)
            .Include(x => x.ClearingJournalEntry)
            .Include(x => x.ReversalJournalEntry)
            .Include(x => x.Allocations).ThenInclude(x => x.Case);
    }
    private IQueryable<CollectionFinancialReceipt> ApplyCollectionClientScope(IQueryable<CollectionFinancialReceipt> query, string permission)
    {
        if (_user.Roles.Contains(SystemRoleNames.Admin, StringComparer.OrdinalIgnoreCase)
            || _user.Permissions.Contains("*", StringComparer.OrdinalIgnoreCase)
            || _user.Permissions.Contains("accounting.transaction.manage", StringComparer.OrdinalIgnoreCase))
            return query;
        var now = DateTimeOffset.UtcNow;
        var grants = _db.UserAccessGrants.AsNoTracking().Where(x => x.UserId == _user.UserId
            && x.PermissionCode == permission && x.Status == "ACTIVE" && (!x.ExpiresAt.HasValue || x.ExpiresAt > now));
        return query.Where(receipt => grants.Any(grant => grant.ScopeType == "ALL" || grant.ScopeType == "DEPARTMENT")
            || grants.Any(grant => grant.ScopeType == "CLIENT" && grant.ClientOrganizationId == receipt.ClientId));
    }
    private static JournalDto MapJournal(JournalEntry x) => new(x.Id, x.JournalNumber, x.EntryType, x.TransactionDate, x.PostingDate, x.CurrencyCode, x.Description, x.TotalDebit, x.TotalCredit, x.Status, x.CreatedBy.FullName, x.CreatedAt, x.ApprovedById?.ToString(), x.ApprovedAt, x.PostedById?.ToString(), x.PostedAt, x.ReversalOfJournalId, x.Lines.OrderBy(l => l.LineNumber).Select(l => new JournalLineDto(l.Id, l.LineNumber, l.AccountId, l.Account.Code, l.Account.NameArabic, l.Account.NameEnglish, l.Debit, l.Credit, l.BaseDebit, l.BaseCredit, l.ExchangeRate, l.Description, l.ClientId, l.CollectorId, l.BranchId)).ToArray());
    private static AccountingPeriodDto PeriodDto(AccountingPeriod x) => new(x.Id, x.Year, x.PeriodNumber, x.Name, x.StartDate, x.EndDate, x.Status, x.ClosedAt, x.CloseReason);
    private static CollectionFinanceDto MapReceipt(CollectionFinancialReceipt x) => new(
        x.CollectionPaymentId, x.Id, x.CollectionPayment.ReferenceNumber, x.CollectionPayment.PaymentDate,
        x.ClientId, x.Client.Code, x.Client.NameArabic, x.Client.NameEnglish, x.GrossAmount, x.BaseAmount,
        x.ExchangeRate, x.CurrencyCode, x.Channel, x.DestinationType, x.DestinationReference, x.Status,
        x.CollectorId, x.Collector?.FullName, x.JournalEntryId, x.JournalEntry.JournalNumber!,
        x.ClearingJournalEntryId, x.ClearingJournalEntry?.JournalNumber,
        x.ReversalJournalEntryId, x.ReversalJournalEntry?.JournalNumber,
        x.PostedAt, x.ClearedAt, x.ReversedAt,
        x.Allocations.OrderBy(a => a.LineNumber).Select(a => new CollectionAllocationDto(a.Id, a.CaseId,
            a.Case.CaseNumber, a.LineNumber, a.Amount, a.OutstandingBefore, a.OverdueBefore)).ToArray());
    private static void ValidateDimensions(FinanceAccount account, Guid? clientId, Guid? collectorId, Guid? branchId) { if (account.RequiresClient && !clientId.HasValue) throw new HrValidationException($"Account {account.Code} requires a client."); if (account.RequiresCollector && !collectorId.HasValue) throw new HrValidationException($"Account {account.Code} requires a collector."); if (account.RequiresBranch && !branchId.HasValue) throw new HrValidationException($"Account {account.Code} requires a branch."); }
    private static CollectionChannelResolution ResolveCollectionChannel(string method)
    {
        var value = method.Trim().ToUpperInvariant();
        if (value is FinanceValues.CollectionChannels.CashBranch || value.Contains("BRANCH_CASH"))
            return new(FinanceSeed.CashboxAccountId, FinanceValues.CollectionChannels.CashBranch, "CASHBOX");
        if (value is FinanceValues.CollectionChannels.CashCollector || value.Contains("CASH") || value.Contains("نقد"))
            return new(FinanceSeed.CustodyAccountId, FinanceValues.CollectionChannels.CashCollector, "COLLECTOR_CUSTODY");
        if (value is FinanceValues.CollectionChannels.Cheque || value.Contains("CHEQUE") || value.Contains("CHECK") || value.Contains("شيك"))
            return new(FinanceSeed.ChequesAccountId, FinanceValues.CollectionChannels.Cheque, "CHEQUES_UNDER_COLLECTION");
        if (value is FinanceValues.CollectionChannels.Gateway || value.Contains("GATEWAY") || value.Contains("WALLET") || value.Contains("POS") || value.Contains("CARD"))
            return new(FinanceSeed.GatewayAccountId, FinanceValues.CollectionChannels.Gateway, "GATEWAY_RECEIVABLE");
        if (value is FinanceValues.CollectionChannels.DirectClient || value.Contains("DIRECT_CLIENT"))
            return new(Guid.Empty, FinanceValues.CollectionChannels.DirectClient, "CLIENT_DIRECT");
        return new(FinanceSeed.BankClearingAccountId, FinanceValues.CollectionChannels.BankTransfer, "BANK_CLEARING");
    }
    private sealed record CollectionChannelResolution(Guid AssetAccountId, string Channel, string DestinationType);
    private static string NewJournalNumber(DateOnly date, Guid id) => $"JE-{date:yyyyMM}-{DateTimeOffset.UtcNow:ddHHmmssfff}-{id:N}"[..32].ToUpperInvariant();
    private static DateOnly CairoToday() { var zone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo"); return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime); }
    private static void ValidatePage(int page, int pageSize) { if (page < 1 || pageSize is < 1 or > 200) throw new HrValidationException("Page must be positive and page size must be between 1 and 200."); }
    private static Task<decimal> AccountBalanceAsync(IQueryable<JournalEntryLine> posted, Guid accountId, DateOnly asOf, CancellationToken token) => posted.Where(x => x.AccountId == accountId && x.JournalEntry.PostingDate <= asOf).SumAsync(x => x.BaseDebit - x.BaseCredit, token);
    private void AddAudit(string action, string entityType, Guid entityId, object? before, object? after, string? reason) => _db.FinancialAuditLogs.Add(new FinancialAuditLog(_user.UserId, action, entityType, entityId, JsonSerializer.Serialize(before ?? new { }, JsonOptions), JsonSerializer.Serialize(after ?? new { }, JsonOptions), reason, DateTimeOffset.UtcNow));
    private void EnsureAccess() { if (!_user.Roles.Contains(SystemRoleNames.Admin) && !_user.Permissions.Contains("*") && !_user.Permissions.Contains(SystemPermissionCodes.FinanceAccess) && !_user.Permissions.Contains("accounting.access")) throw new HrForbiddenException("You do not have access to Finance."); }
    private void EnsurePermission(string permission) { EnsureAccess(); if (!_user.Roles.Contains(SystemRoleNames.Admin) && !_user.Permissions.Contains("*") && !_user.Permissions.Contains(permission) && !LegacyPermissionMatches(permission)) throw new HrForbiddenException("You do not have the required finance permission."); }
    private bool LegacyPermissionMatches(string permission) => permission switch
    {
        SystemPermissionCodes.FinanceJournalCreate or SystemPermissionCodes.FinanceCollectionReview => _user.Permissions.Contains("accounting.transaction.manage"),
        SystemPermissionCodes.FinanceCustodyView => _user.Permissions.Contains("accounting.access"),
        SystemPermissionCodes.FinanceJournalApprove or SystemPermissionCodes.FinanceJournalPost or SystemPermissionCodes.FinancePeriodClose or SystemPermissionCodes.FinanceTransactionReverse or SystemPermissionCodes.FinanceCustodyReconcile => _user.Permissions.Contains("accounting.approve"),
        _ => false
    };
}
