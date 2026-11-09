using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExcelDataReader;
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

public sealed class FinanceClaimsService(
    ApplicationDbContext db,
    ICurrentUserContext user,
    IAccountingBookService accountingBook,
    IFinanceService finance) : IFinanceClaimsService
{
    private const int MaximumImportBytes = 50 * 1024 * 1024;
    private const int MaximumImportRows = 20_000;
    private const int ExportLimit = 100_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    static FinanceClaimsService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private bool IsAdmin => user.Roles.Contains(SystemRoleNames.Admin) || user.Permissions.Contains("*");
    private static DateOnly CairoToday() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
        DateTimeOffset.UtcNow, OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo").DateTime);

    private void EnsurePermission(string permission)
    {
        if (!IsAdmin && !user.Permissions.Contains(permission))
            throw new HrForbiddenException($"Permission {permission} is required.");
    }

    private void Audit(string action, Guid id, object? before, object? after, string? reason = null)
        => db.FinancialAuditLogs.Add(new FinancialAuditLog(user.UserId, action, nameof(FinanceClaim), id,
            JsonSerializer.Serialize(before ?? new { }), JsonSerializer.Serialize(after ?? new { }), reason, DateTimeOffset.UtcNow));

    private static object Snapshot(FinanceClaim claim) => new
    {
        claim.Serial, claim.BankOrganizationId, claim.BankName, claim.ClaimType, claim.Month, claim.ClaimDate,
        claim.AmountWithoutVat, claim.VatPercent, claim.Vat, claim.TotalWithVat, claim.TaxAtSourcePercent,
        claim.TaxAtSource, claim.Total, claim.TotalInBank, claim.DateInBank, claim.Status, claim.IsHistoricalImport
    };

    private static FinanceClaimListItem Map(FinanceClaim claim, string createdBy, string? updatedBy, bool posted)
        => new(claim.Id, claim.Serial, claim.BankOrganizationId, claim.BankName, claim.ClaimType, claim.Month, claim.ClaimDate,
            claim.AmountWithoutVat, claim.VatPercent, claim.Vat, claim.TotalWithVat, claim.TaxAtSourcePercent, claim.TaxAtSource,
            claim.Total, claim.TotalInBank, claim.DateInBank, claim.Status, claim.IsHistoricalImport, posted,
            createdBy, claim.CreatedAt, updatedBy, claim.UpdatedAt);

    private IQueryable<FinanceClaim> Filter(FinanceClaimFilter filter)
    {
        var query = db.FinanceClaims.AsNoTracking().AsQueryable();
        if (filter.BankOrganizationId.HasValue) query = query.Where(x => x.BankOrganizationId == filter.BankOrganizationId);
        if (!string.IsNullOrWhiteSpace(filter.ClaimType))
        {
            var claimType = filter.ClaimType.Trim().ToLowerInvariant();
            query = query.Where(x => x.ClaimType.ToLower() == claimType);
        }
        if (!string.IsNullOrWhiteSpace(filter.Month))
        {
            var month = filter.Month.Trim().ToLowerInvariant();
            query = query.Where(x => x.Month.ToLower() == month);
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim().ToUpperInvariant();
            if (status is not ("PENDING" or "RECEIVED" or "CANCELLED")) throw new HrValidationException("Invalid claim status.");
            query = query.Where(x => x.Status == status);
        }
        if (filter.ClaimDateFrom.HasValue) query = query.Where(x => x.ClaimDate >= filter.ClaimDateFrom.Value);
        if (filter.ClaimDateTo.HasValue) query = query.Where(x => x.ClaimDate <= filter.ClaimDateTo.Value);
        if (filter.BankDateFrom.HasValue) query = query.Where(x => x.DateInBank >= filter.BankDateFrom.Value);
        if (filter.BankDateTo.HasValue) query = query.Where(x => x.DateInBank <= filter.BankDateTo.Value);
        if (filter.AmountFrom.HasValue) query = query.Where(x => x.TotalInBank >= filter.AmountFrom.Value);
        if (filter.AmountTo.HasValue) query = query.Where(x => x.TotalInBank <= filter.AmountTo.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.BankName.ToLower().Contains(search) || x.ClaimType.ToLower().Contains(search)
                || x.Month.ToLower().Contains(search) || (x.Serial != null && x.Serial.ToLower().Contains(search)));
        }
        return query;
    }

    private static void ValidateFilter(FinanceClaimFilter filter)
    {
        if (filter.Page < 1 || filter.PageSize is < 1 or > 200) throw new HrValidationException("Invalid page parameters.");
        if (filter.ClaimDateFrom > filter.ClaimDateTo || filter.BankDateFrom > filter.BankDateTo || filter.AmountFrom > filter.AmountTo)
            throw new HrValidationException("A filter range is invalid.");
        foreach (var date in new[] { filter.ClaimDateFrom, filter.ClaimDateTo, filter.BankDateFrom, filter.BankDateTo })
            if (date.HasValue && date.Value.Year is < 2000 or > 2100) throw new HrValidationException("A filter date is invalid.");
        if (filter.AmountFrom < 0 || filter.AmountTo < 0) throw new HrValidationException("Amount filters cannot be negative.");
    }

    public async Task<FinanceClaimPage> ListAsync(FinanceClaimFilter filter, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceClaimsView);
        ValidateFilter(filter);
        var query = Filter(filter);
        var count = await query.CountAsync(token);
        var data = await query.OrderByDescending(x => x.ClaimDate).ThenByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(x => new
            {
                Claim = x,
                CreatedBy = x.CreatedBy.FullName,
                UpdatedBy = x.UpdatedBy == null ? null : x.UpdatedBy.FullName,
                Posted = db.FinanceClaimReceiptPostings.Any(p => p.ClaimId == x.Id && p.ReversedAt == null)
            }).ToArrayAsync(token);
        var summary = await query.GroupBy(_ => 1).Select(g => new FinanceClaimSummary(
            g.Count(), g.Count(x => x.Status == "PENDING"), g.Count(x => x.Status == "RECEIVED"),
            g.Sum(x => x.TotalWithVat), g.Where(x => x.Status == "RECEIVED").Sum(x => x.TotalWithVat),
            g.Where(x => x.Status == "RECEIVED").Sum(x => x.TotalInBank), g.Sum(x => x.Vat), g.Sum(x => x.TaxAtSource)))
            .SingleOrDefaultAsync(token) ?? new FinanceClaimSummary(0, 0, 0, 0, 0, 0, 0, 0);
        return new(data.Select(x => Map(x.Claim, x.CreatedBy, x.UpdatedBy, x.Posted)).ToArray(), filter.Page, filter.PageSize, count, summary);
    }

    private async Task<FinanceClaim> ClaimAsync(Guid id, bool tracking, CancellationToken token)
    {
        var query = tracking ? db.FinanceClaims.AsQueryable() : db.FinanceClaims.AsNoTracking();
        return await query.Include(x => x.CreatedBy).Include(x => x.UpdatedBy).SingleOrDefaultAsync(x => x.Id == id, token)
            ?? throw new HrNotFoundException("Claim was not found.");
    }

    private async Task<FinanceClaimDetails> DetailsCoreAsync(Guid id, CancellationToken token)
    {
        var claim = await db.FinanceClaims.AsNoTracking()
            .Include(x => x.CreatedBy).Include(x => x.UpdatedBy).Include(x => x.ReceiptConfirmedBy)
            .Include(x => x.ImportBatch).ThenInclude(x => x!.CreatedBy)
            .Include(x => x.ReceiptPostings).ThenInclude(x => x.AccountingBookEntry).ThenInclude(x => x.JournalEntry)
            .Include(x => x.ReceiptPostings).ThenInclude(x => x.CreatedBy)
            .Include(x => x.ReceiptPostings).ThenInclude(x => x.ReversedBy)
            .SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Claim was not found.");
        var audit = await db.FinancialAuditLogs.AsNoTracking().Where(x => x.EntityType == nameof(FinanceClaim) && x.EntityId == id)
            .Include(x => x.Actor).OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new FinanceClaimAuditDto(x.Action, x.Actor == null ? null : x.Actor.FullName, x.Reason, x.CreatedAt, x.BeforeJson, x.AfterJson))
            .ToArrayAsync(token);
        var receipts = claim.ReceiptPostings.OrderBy(x => x.CreatedAt).Select(x => new FinanceClaimReceiptDto(
            x.Id, x.Amount, x.ReceiptDate, x.IsHistoricalPosting, x.CreatedBy.FullName, x.CreatedAt,
            x.AccountingBookEntry.DocumentNumber, x.AccountingBookEntry.JournalEntry.Status, x.ReversedAt,
            x.ReversedBy == null ? null : x.ReversedBy.FullName, x.ReversalJournalId)).ToArray();
        var item = Map(claim, claim.CreatedBy.FullName, claim.UpdatedBy?.FullName, claim.ReceiptPostings.Any(x => x.ReversedAt == null));
        return new(item, receipts, audit, claim.ImportBatch?.FileName, claim.ImportBatch?.CreatedBy.FullName,
            claim.ImportBatch?.CreatedAt, claim.ReceiptConfirmedBy?.FullName, claim.ReceiptConfirmedAt);
    }

    public async Task<FinanceClaimDetails> DetailsAsync(Guid id, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceClaimsView);
        return await DetailsCoreAsync(id, token);
    }

    public async Task<IReadOnlyList<FinanceClaimOrganization>> OrganizationsAsync(CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceClaimsView);
        return await db.CollectionClientOrganizations.AsNoTracking()
            .Where(x => x.IsActive && (x.OrganizationType == CollectionsValues.OrganizationTypes.Bank
                || x.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance
                || x.OrganizationType == CollectionsValues.OrganizationTypes.FinancialInstitution))
            .OrderBy(x => x.NameEnglish)
            .Select(x => new FinanceClaimOrganization(x.Id, x.Code, x.NameArabic, x.NameEnglish, x.OrganizationType)).ToArrayAsync(token);
    }

    private async Task ValidateOrganizationAsync(Guid? organizationId, string organizationName, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(organizationName) || organizationName.Trim().Length > 200)
            throw new HrValidationException("Enter a valid bank or installment company name.");
        if (!organizationId.HasValue) return;
        if (!await db.CollectionClientOrganizations.AnyAsync(x => x.Id == organizationId && x.IsActive
            && (x.OrganizationType == CollectionsValues.OrganizationTypes.Bank
                || x.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance
                || x.OrganizationType == CollectionsValues.OrganizationTypes.FinancialInstitution), token))
            throw new HrValidationException("Choose an active bank or installment company from the Collections directory.");
    }

    internal static bool IsClaimOrganizationType(string organizationType) =>
        organizationType is CollectionsValues.OrganizationTypes.Bank
            or CollectionsValues.OrganizationTypes.ConsumerFinance
            or CollectionsValues.OrganizationTypes.FinancialInstitution;

    private static void ValidateRequest(SaveFinanceClaimRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BankName) || string.IsNullOrWhiteSpace(request.ClaimType) || string.IsNullOrWhiteSpace(request.Month))
            throw new HrValidationException("Bank / installment company, claim type, and month are required.");
        if (request.BankName.Trim().Length > 200 || request.ClaimType.Trim().Length > 200 || request.Month.Trim().Length > 80)
            throw new HrValidationException("One or more claim fields exceed their maximum length.");
        if (request.AmountWithoutVat <= 0 || decimal.Round(request.AmountWithoutVat, 2) != request.AmountWithoutVat)
            throw new HrValidationException("Enter a positive claim amount with at most two decimals.");
        if (request.VatPercent is < 0 or > 100 || decimal.Round(request.VatPercent, 4) != request.VatPercent
            || request.TaxAtSourcePercent is < 0 or > 100 || decimal.Round(request.TaxAtSourcePercent, 4) != request.TaxAtSourcePercent)
            throw new HrValidationException("Tax percentages must be between 0 and 100 with at most four decimals.");
        if (request.ClaimDate.Year is < 2000 or > 2100 || request.DateInBank is { Year: < 2000 or > 2100 })
            throw new HrValidationException("Claim dates must be between 2000 and 2100.");
    }

    public async Task<FinanceClaimListItem> CreateAsync(SaveFinanceClaimRequest request, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceClaimsCreate);
        ValidateRequest(request);
        await ValidateOrganizationAsync(request.BankOrganizationId, request.BankName, token);
        var now = DateTimeOffset.UtcNow;
        var claim = new FinanceClaim(request.Serial, request.BankOrganizationId, request.BankName, request.ClaimType, request.Month,
            request.ClaimDate, request.AmountWithoutVat, request.VatPercent, request.TaxAtSourcePercent,
            request.DateInBank, false, null, user.UserId, now);
        db.FinanceClaims.Add(claim);
        Audit("FinanceClaimCreated", claim.Id, null, Snapshot(claim));
        await db.SaveChangesAsync(token);
        return Map(claim, await UserNameAsync(user.UserId, token), null, false);
    }

    public async Task<FinanceClaimListItem> UpdateAsync(Guid id, SaveFinanceClaimRequest request, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceClaimsEdit);
        ValidateRequest(request);
        await ValidateOrganizationAsync(request.BankOrganizationId, request.BankName, token);
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        var claim = await ClaimAsync(id, true, token);
        var before = Snapshot(claim);
        if (claim.Status == "RECEIVED")
        {
            var active = await db.FinanceClaimReceiptPostings.Include(x => x.AccountingBookEntry)
                .Where(x => x.ClaimId == id && x.ReversedAt == null)
                .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(token);
            if (active is not null)
            {
                var reversal = await finance.ReverseClaimReceiptAsync(active.AccountingBookEntry.JournalEntryId,
                    new PeriodActionRequest("Claim edited; receipt reversed before updating claim"), token);
                active.MarkReversed(user.UserId, DateTimeOffset.UtcNow, reversal.Id);
            }
            claim.ReopenAfterReceiptReversal(user.UserId, DateTimeOffset.UtcNow);
        }
        try
        {
            claim.UpdateDetails(request.Serial, request.BankOrganizationId, request.BankName, request.ClaimType, request.Month,
                request.ClaimDate, request.AmountWithoutVat, request.VatPercent, request.TaxAtSourcePercent, request.DateInBank,
                user.UserId, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        Audit("FinanceClaimUpdated", id, before, Snapshot(claim));
        await db.SaveChangesAsync(token);
        if (tx is not null) await tx.CommitAsync(token);
        return Map(claim, claim.CreatedBy.FullName, await UserNameAsync(user.UserId, token), false);
    }

    private async Task<string> UserNameAsync(Guid id, CancellationToken token)
        => await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => x.FullName).SingleOrDefaultAsync(token) ?? "SYSTEM";

    public async Task<FinanceClaimDetails> ConfirmReceiptAsync(Guid id, ConfirmFinanceClaimReceiptRequest request, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceClaimsConfirmReceipt);
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        var claim = await db.FinanceClaims.Include(x => x.CreatedBy).Include(x => x.UpdatedBy)
            .SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Claim was not found.");
        var active = await db.FinanceClaimReceiptPostings.Include(x => x.AccountingBookEntry).ThenInclude(x => x.JournalEntry)
            .Where(x => x.ClaimId == id && x.ReversedAt == null).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(token);
        if (claim.Status == "RECEIVED" && active is not null)
        {
            if (tx is not null) await tx.CommitAsync(token);
            return await DetailsCoreAsync(id, token);
        }
        if (claim.Status == "RECEIVED" && claim.IsHistoricalImport)
            throw new HrConflictException("This historical receipt must use the explicit historical posting action.");
        if (claim.Status != "PENDING") throw new HrConflictException("Only a pending claim can be received.");
        var date = request.DateInBank ?? claim.DateInBank ?? throw new HrValidationException("Enter the date the amount entered the bank.");
        if (date > CairoToday() || date.Year is < 2000 or > 2100)
            throw new HrValidationException("Choose a valid bank receipt date.");
        var before = Snapshot(claim);
        var postingId = Guid.NewGuid();
        var entry = await accountingBook.RecordClaimReceiptAsync(claim.Id, postingId, date, claim.TotalInBank, token);
        var now = DateTimeOffset.UtcNow;
        claim.ConfirmReceipt(date, user.UserId, now);
        db.FinanceClaimReceiptPostings.Add(new FinanceClaimReceiptPosting(postingId, claim.Id, entry.Id, claim.TotalInBank,
            date, false, user.UserId, now));
        Audit("FinanceClaimReceiptConfirmed", id, before, new { Amount = claim.TotalInBank, DateInBank = date, entry.Id });
        await db.SaveChangesAsync(token);
        if (tx is not null) await tx.CommitAsync(token);
        return await DetailsCoreAsync(id, token);
    }

    public async Task<FinanceClaimDetails> PostHistoricalReceiptAsync(Guid id, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceClaimsConfirmReceipt);
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        var claim = await db.FinanceClaims.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Claim was not found.");
        if (claim.Status != "RECEIVED" || !claim.IsHistoricalImport || !claim.DateInBank.HasValue)
            throw new HrConflictException("Only an imported historical claim with a bank receipt date can be posted this way.");
        var active = await db.FinanceClaimReceiptPostings.AnyAsync(x => x.ClaimId == id && x.ReversedAt == null, token);
        if (active)
        {
            if (tx is not null) await tx.CommitAsync(token);
            return await DetailsCoreAsync(id, token);
        }
        var postingId = Guid.NewGuid();
        var entry = await accountingBook.RecordClaimReceiptAsync(claim.Id, postingId, claim.DateInBank.Value, claim.TotalInBank, token);
        var now = DateTimeOffset.UtcNow;
        db.FinanceClaimReceiptPostings.Add(new FinanceClaimReceiptPosting(postingId, claim.Id, entry.Id, claim.TotalInBank,
            claim.DateInBank.Value, true, user.UserId, now));
        Audit("FinanceClaimHistoricalReceiptPosted", id, null, new { Amount = claim.TotalInBank, claim.DateInBank, entry.Id });
        await db.SaveChangesAsync(token);
        if (tx is not null) await tx.CommitAsync(token);
        return await DetailsCoreAsync(id, token);
    }

    public async Task<FinanceClaimDetails> ReverseReceiptAsync(Guid id, ReverseFinanceClaimReceiptRequest request, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceClaimsReverseReceipt);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new HrValidationException("A reversal reason is required.");
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        var claim = await db.FinanceClaims.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Claim was not found.");
        if (claim.Status != "RECEIVED") throw new HrConflictException("Only a received claim can be reversed.");
        var before = Snapshot(claim);
        var active = await db.FinanceClaimReceiptPostings.Include(x => x.AccountingBookEntry)
            .Where(x => x.ClaimId == id && x.ReversedAt == null).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(token);
        Guid? reversalId = null;
        if (active is not null)
        {
            var reversal = await finance.ReverseClaimReceiptAsync(active.AccountingBookEntry.JournalEntryId, new PeriodActionRequest(request.Reason), token);
            reversalId = reversal.Id;
            active.MarkReversed(user.UserId, DateTimeOffset.UtcNow, reversalId);
        }
        claim.ReopenAfterReceiptReversal(user.UserId, DateTimeOffset.UtcNow);
        Audit(active is null ? "FinanceClaimHistoricalReceiptReopened" : "FinanceClaimReceiptReversed", id, before,
            new { Claim = Snapshot(claim), ReversalJournalId = reversalId }, request.Reason);
        await db.SaveChangesAsync(token);
        if (tx is not null) await tx.CommitAsync(token);
        return await DetailsCoreAsync(id, token);
    }

    public async Task<FinanceClaimDetails> CancelAsync(Guid id, string? reason, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceClaimsCancel);
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        var claim = await db.FinanceClaims.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Claim was not found.");
        if (claim.Status == "CANCELLED")
        {
            if (tx is not null) await tx.CommitAsync(token);
            return await DetailsCoreAsync(id, token);
        }
        var before = Snapshot(claim);
        if (claim.Status == "RECEIVED")
        {
            var active = await db.FinanceClaimReceiptPostings.Include(x => x.AccountingBookEntry)
                .Where(x => x.ClaimId == id && x.ReversedAt == null).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(token);
            if (active is not null)
            {
                var reversal = await finance.ReverseClaimReceiptAsync(active.AccountingBookEntry.JournalEntryId,
                    new PeriodActionRequest(string.IsNullOrWhiteSpace(reason) ? "Claim cancelled" : reason), token);
                active.MarkReversed(user.UserId, DateTimeOffset.UtcNow, reversal.Id);
            }
        }
        try { claim.Cancel(user.UserId, DateTimeOffset.UtcNow); }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        Audit("FinanceClaimCancelled", id, before, Snapshot(claim), reason);
        await db.SaveChangesAsync(token);
        if (tx is not null) await tx.CommitAsync(token);
        return await DetailsCoreAsync(id, token);
    }

    private sealed record ParsedClaimRow(
        int RowNumber, string? Serial, string? BankName, string? ClaimType, string? Month, DateOnly? ClaimDate,
        decimal? AmountWithoutVat, decimal? VatPercent, decimal? ExcelVat, decimal? ExcelTotalWithVat,
        decimal? TaxAtSourcePercent, decimal? ExcelTaxAtSource, decimal? ExcelTotal, decimal? ExcelTotalInBank,
        DateOnly? DateInBank);

    public async Task<FinanceClaimImportPreview> PreviewImportAsync(string fileName, Stream stream, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceClaimsImport);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".xls")) throw new HrValidationException("Only XLSX and XLS Claims workbooks are supported.");
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, token)) > 0)
        {
            if (buffer.Length + read > MaximumImportBytes) throw new HrValidationException("Claims workbooks must not exceed 50 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), token);
        }
        if (buffer.Length == 0) throw new HrValidationException("The selected workbook is empty.");
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (await db.FinanceClaimImportBatches.AnyAsync(x => x.FileHash == hash, token))
            throw new HrConflictException("This exact workbook has already been previewed or imported.");
        buffer.Position = 0;
        List<ParsedClaimRow> parsed;
        try { parsed = ParseWorkbook(buffer); }
        catch (Exception ex) when (ex is not OperationCanceledException and not HrException)
        {
            throw new HrValidationException("The Claims workbook could not be read. Verify its format and column headers.");
        }
        if (parsed.Count > MaximumImportRows) throw new HrValidationException("Claims workbooks may contain at most 20000 data rows.");
        var rows = new List<FinanceClaimImportRow>(parsed.Count);
        var inFileKeys = new HashSet<string>(StringComparer.Ordinal);
        var existingClaims = await db.FinanceClaims.AsNoTracking()
            .Select(x => new { x.BankName, x.ClaimType, x.Month, x.ClaimDate, x.AmountWithoutVat }).ToArrayAsync(token);
        var existingKeys = existingClaims.Select(x => DuplicateKey(x.BankName, x.ClaimType, x.Month, x.ClaimDate, x.AmountWithoutVat))
            .Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal);
        foreach (var row in parsed)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(row.BankName)) errors.Add("Bank / Installment Company is required.");
            if (string.IsNullOrWhiteSpace(row.ClaimType)) errors.Add("The type of Claim is required.");
            if (string.IsNullOrWhiteSpace(row.Month)) errors.Add("Month is required.");
            if (!row.ClaimDate.HasValue) errors.Add("Date of claim is missing or invalid.");
            if (!row.AmountWithoutVat.HasValue || row.AmountWithoutVat <= 0) errors.Add("Total claim without VAT must be positive.");
            if (!row.VatPercent.HasValue || row.VatPercent is < 0 or > 100) errors.Add("VAT % must be between 0 and 100.");
            if (!row.TaxAtSourcePercent.HasValue || row.TaxAtSourcePercent is < 0 or > 100) errors.Add("Tax at source % must be between 0 and 100.");
            if (row.ClaimDate is { Year: < 2000 or > 2100 })
                errors.Add("Date of claim is outside the supported date range.");
            if (row.AmountWithoutVat.HasValue && decimal.Round(row.AmountWithoutVat.Value, 2) != row.AmountWithoutVat.Value)
                errors.Add("Total claim without VAT must have at most two decimal places.");
            if (row.VatPercent.HasValue && decimal.Round(row.VatPercent.Value, 4) != row.VatPercent.Value
                || row.TaxAtSourcePercent.HasValue && decimal.Round(row.TaxAtSourcePercent.Value, 4) != row.TaxAtSourcePercent.Value)
                errors.Add("Tax percentages must have at most four decimal places.");
            if (row.DateInBank is { Year: < 2000 or > 2100 })
                errors.Add("Date In Bank is outside the supported date range.");
            if (row.DateInBank is DateOnly bankDate && bankDate > CairoToday())
                errors.Add("Date In Bank cannot be in the future.");

            decimal? vat = row.AmountWithoutVat.HasValue && row.VatPercent.HasValue
                ? Percent(row.AmountWithoutVat.Value, row.VatPercent.Value) : null;
            decimal? gross = vat is decimal vatValue && row.AmountWithoutVat is decimal baseAmount
                ? decimal.Round(baseAmount + vatValue, 2, MidpointRounding.AwayFromZero) : null;
            decimal? withholding = row.AmountWithoutVat.HasValue && row.TaxAtSourcePercent.HasValue
                ? Percent(row.AmountWithoutVat.Value, row.TaxAtSourcePercent.Value) : null;
            decimal? net = gross is decimal grossValue && withholding is decimal withholdingValue
                ? decimal.Round(grossValue - withholdingValue, 2, MidpointRounding.AwayFromZero) : null;
            AddDiscrepancy(row.ExcelVat, vat, "VAT", warnings);
            AddDiscrepancy(row.ExcelTotalWithVat, gross, "Total claim with VAT", warnings);
            AddDiscrepancy(row.ExcelTaxAtSource, withholding, "Tax at source", warnings);
            AddDiscrepancy(row.ExcelTotal, net, "Total", warnings);
            AddDiscrepancy(row.ExcelTotalInBank, net, "Total in bank", warnings);

            var key = DuplicateKey(row.BankName, row.ClaimType, row.Month, row.ClaimDate, row.AmountWithoutVat);
            var duplicateInFile = key.Length > 0 && !inFileKeys.Add(key);
            var duplicateInDb = key.Length > 0 && existingKeys.Contains(key);
            var duplicate = duplicateInFile || duplicateInDb;
            if (duplicate) warnings.Add("Possible duplicate claim; it will be skipped during import for review.");
            rows.Add(new FinanceClaimImportRow(row.RowNumber, row.Serial, row.BankName, row.ClaimType, row.Month, row.ClaimDate,
                row.AmountWithoutVat, row.VatPercent, row.ExcelVat, row.ExcelTotalWithVat, row.TaxAtSourcePercent,
                row.ExcelTaxAtSource, row.ExcelTotal, row.ExcelTotalInBank, row.DateInBank, vat, gross, withholding, net, net,
                errors.Count == 0, duplicate, errors, warnings));
        }
        var now = DateTimeOffset.UtcNow;
        var batch = new FinanceClaimImportBatch(Path.GetFileName(fileName), hash, JsonSerializer.Serialize(rows, JsonOptions), rows.Count, user.UserId, now);
        db.FinanceClaimImportBatches.Add(batch);
        await db.SaveChangesAsync(token);
        return new(batch.Id, batch.FileName, rows.Count, rows.Count(x => x.IsValid && !x.PossibleDuplicate),
            rows.Count(x => x.Warnings.Count > 0), rows.Count(x => !x.IsValid), rows.Take(200).ToArray());
    }

    public async Task<FinanceClaimImportResult> ConfirmImportAsync(FinanceClaimImportConfirmRequest request, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceClaimsImport);
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        var batch = await db.FinanceClaimImportBatches.SingleOrDefaultAsync(x => x.Id == request.PreviewId, token)
            ?? throw new HrNotFoundException("Claims import preview was not found.");
        var now = DateTimeOffset.UtcNow;
        try { batch.Confirm(user.UserId, now); }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        var rows = JsonSerializer.Deserialize<List<FinanceClaimImportRow>>(batch.PreviewJson, JsonOptions)
            ?? throw new HrValidationException("The saved Claims preview is invalid.");
        var imported = 0;
        var skipped = 0;
        var failed = 0;
        var existingClaims = await db.FinanceClaims.AsNoTracking()
            .Select(x => new { x.BankName, x.ClaimType, x.Month, x.ClaimDate, x.AmountWithoutVat }).ToArrayAsync(token);
        var seen = existingClaims.Select(x => DuplicateKey(x.BankName, x.ClaimType, x.Month, x.ClaimDate, x.AmountWithoutVat))
            .Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!row.IsValid) { failed++; continue; }
            var key = DuplicateKey(row.BankName, row.ClaimType, row.Month, row.ClaimDate, row.AmountWithoutVat);
            if (row.PossibleDuplicate || key.Length == 0 || !seen.Add(key))
            {
                skipped++;
                continue;
            }
            var bank = await FindOrganizationAsync(row.BankName!, token);
            try
            {
                var claim = new FinanceClaim(row.Serial, bank?.Id, row.BankName!, row.ClaimType!, row.Month!, row.ClaimDate!.Value,
                    row.AmountWithoutVat!.Value, row.VatPercent!.Value, row.TaxAtSourcePercent!.Value, row.DateInBank,
                    true, batch.Id, user.UserId, now);
                db.FinanceClaims.Add(claim);
                Audit("FinanceClaimImported", claim.Id, null, new { Snapshot = Snapshot(claim), batch.Id, batch.FileName, row.RowNumber });
                imported++;
            }
            catch (ArgumentException)
            {
                failed++;
            }
        }
        Audit("FinanceClaimsImportConfirmed", batch.Id, null, new { batch.FileName, batch.TotalRows, Imported = imported, Skipped = skipped, Failed = failed });
        await db.SaveChangesAsync(token);
        if (tx is not null) await tx.CommitAsync(token);
        return new(batch.TotalRows, imported, skipped, failed);
    }

    private async Task<ClientOrganization?> FindOrganizationAsync(string name, CancellationToken token)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var matches = await db.CollectionClientOrganizations.Where(x => x.IsActive
            && (x.OrganizationType == CollectionsValues.OrganizationTypes.Bank
                || x.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance
                || x.OrganizationType == CollectionsValues.OrganizationTypes.FinancialInstitution) &&
            (x.NameEnglish.ToLower() == normalized || x.NameArabic.ToLower() == normalized || x.Code.ToLower() == normalized))
            .Take(2).ToArrayAsync(token);
        return matches.Length == 1 ? matches[0] : null;
    }

    private static List<ParsedClaimRow> ParseWorkbook(Stream stream)
    {
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var parsed = new List<ParsedClaimRow>();
        var headersRead = false;
        var rowNumber = 0;
        do
        {
            while (reader.Read())
            {
                rowNumber++;
                if (!headersRead)
                {
                    if (Enumerable.Range(0, reader.FieldCount).All(index => string.IsNullOrWhiteSpace(Value(reader.GetValue(index))))) continue;
                    for (var index = 0; index < reader.FieldCount; index++)
                    {
                        var header = NormalizeHeader(Value(reader.GetValue(index)));
                        if (!string.IsNullOrEmpty(header)) columns[header] = index;
                    }
                    headersRead = true;
                    continue;
                }
                if (Enumerable.Range(0, reader.FieldCount).All(index => string.IsNullOrWhiteSpace(Value(reader.GetValue(index))))) continue;
                string? Text(params string[] names)
                {
                    var index = names.Select(NormalizeHeader).Select(name => columns.GetValueOrDefault(name, -1)).FirstOrDefault(x => x >= 0);
                    return index >= 0 && index < reader.FieldCount ? Value(reader.GetValue(index)) : null;
                }
                parsed.Add(new ParsedClaimRow(rowNumber, Text("S", "Serial"),
                    Text("Bank Name", "Bank / Installment Company", "Bank/Installment Company", "Bank / Finance Company",
                        "Installment Company", "Finance Company", "Organization", "Organization Name", "Company", "Company Name",
                        "البنك / شركة التقسيط", "البنك", "شركة التقسيط"),
                    Text("The type of Claim", "Type of Claim", "Claim Type"),
                    Text("Month"), ParseDate(Text("Date of claim", "Claim Date")), ParseMoney(Text("total claim without vat", "Amount without VAT")),
                    ParseMoney(Text("Vat%", "VAT %", "VAT Percent")), ParseMoney(Text("Vat", "VAT")),
                    ParseMoney(Text("total claim with vat", "Total claim with VAT")), ParseMoney(Text("Tax at source %", "Tax at source percent")),
                    ParseMoney(Text("Tax at source")), ParseMoney(Text("Total")), ParseMoney(Text("Total in bank")),
                    ParseDate(Text("Date In Bank"))));
                if (parsed.Count > MaximumImportRows + 1) throw new HrValidationException("Claims workbooks may contain at most 20000 data rows.");
            }
            if (headersRead) break;
        } while (reader.NextResult());
        if (!headersRead) throw new HrValidationException("The workbook does not contain a header row.");
        return parsed;
    }

    private static string NormalizeHeader(string? value)
        => string.IsNullOrWhiteSpace(value) ? "" : new(value.Trim().ToLowerInvariant().Replace("%", "percent", StringComparison.Ordinal).Where(char.IsLetterOrDigit).ToArray());

    private static string? Value(object? value)
        => value switch
        {
            null => null,
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim()
        };

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date)
            || DateTime.TryParse(value, CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.AllowWhiteSpaces, out date))
            return DateOnly.FromDateTime(date);
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 1 and <= 2_958_465)
            return DateOnly.FromDateTime(DateTime.FromOADate(serial));
        return null;
    }

    private static decimal? ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)) return decimal.Round(parsed, 4, MidpointRounding.AwayFromZero);
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("en-GB"), out parsed)) return decimal.Round(parsed, 4, MidpointRounding.AwayFromZero);
        return null;
    }

    private static decimal Percent(decimal amount, decimal percent)
        => decimal.Round(amount * percent / 100m, 2, MidpointRounding.AwayFromZero);

    private static void AddDiscrepancy(decimal? excel, decimal? calculated, string field, ICollection<string> warnings)
    {
        if (excel.HasValue && calculated.HasValue && Math.Abs(excel.Value - calculated.Value) > 0.01m)
            warnings.Add($"{field} in the workbook ({excel.Value:0.00}) differs from the recalculated amount ({calculated.Value:0.00}); the recalculated value will be imported.");
    }

    private static string DuplicateKey(string? bank, string? type, string? month, DateOnly? date, decimal? amount)
        => string.IsNullOrWhiteSpace(bank) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(month) || !date.HasValue || !amount.HasValue
            ? ""
            : string.Join("|", bank.Trim().ToUpperInvariant(), type.Trim().ToUpperInvariant(), month.Trim().ToUpperInvariant(),
                date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), amount.Value.ToString("0.00", CultureInfo.InvariantCulture));

    public async Task<byte[]> ExportAsync(FinanceClaimFilter filter, CancellationToken token)
    {
        EnsurePermission(SystemPermissionCodes.FinanceClaimsExport);
        ValidateFilter(filter);
        var records = await Filter(filter).OrderBy(x => x.ClaimDate).ThenBy(x => x.Serial).Take(ExportLimit + 1).ToArrayAsync(token);
        if (records.Length > ExportLimit) throw new HrValidationException("The export exceeds 100000 claims. Narrow the filters and try again.");
        var ar = ApiTextLocalizer.IsArabic;
        string T(string arabic, string english) => ar ? arabic : english;
        var headers = new[]
        {
            T("مسلسل", "S"), T("البنك / شركة التقسيط", "Bank / Installment Company"), T("نوع المطالبة", "The type of Claim"), T("الشهر", "Month"),
            T("تاريخ المطالبة", "Date of claim"), T("إجمالي المطالبة بدون ضريبة القيمة المضافة", "total claim without vat"),
            T("نسبة ضريبة القيمة المضافة", "Vat%"), T("قيمة ضريبة القيمة المضافة", "Vat"),
            T("إجمالي المطالبة شامل ضريبة القيمة المضافة", "total claim with vat"), T("نسبة ضريبة المنبع", "Tax at source %"),
            T("قيمة ضريبة المنبع", "Tax at source"), T("الإجمالي", "Total"), T("صافي المبلغ بالبنك", "Total in bank"),
            T("تاريخ دخول المبلغ البنك", "Date In Bank")
        };
        var rows = records.Select(x => (object?[])[x.Serial, x.BankName, x.ClaimType, x.Month, x.ClaimDate, x.AmountWithoutVat,
            x.VatPercent, x.Vat, x.TotalWithVat, x.TaxAtSourcePercent, x.TaxAtSource, x.Total, x.TotalInBank, x.DateInBank]).ToList();
        return AccountingBookService.Spreadsheet([(T("المطالبات", "Claims"), headers, rows)], T("تقرير المطالبات", "Claims report"), ar);
    }
}
