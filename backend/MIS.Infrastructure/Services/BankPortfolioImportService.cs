using System.Data;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Hr;
using MIS.Domain.Services;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class BankPortfolioImportService : IBankPortfolioImportService
{
    public const long MaximumBytes = ExcelImportLimits.MaximumBytes;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserContext _user;
    private readonly IHrFileStorage _files;
    private readonly ILogger<BankPortfolioImportService> _logger;
    private readonly ICollectionsClassificationContext _classification;
    private static readonly ConcurrentDictionary<Guid, ReplacementCandidate> Replacements = new();
    private sealed record ReplacementCandidate(Guid BankId, Guid ImportId, Guid UserId, string OriginalFileName,
        string ContentType, long FileSize, string FileHash, string StorageKey, int RowCount, DateTimeOffset ExpiresAt);

    public BankPortfolioImportService(ApplicationDbContext db, ICurrentUserContext user, IHrFileStorage files,
        ILogger<BankPortfolioImportService> logger, ICollectionsClassificationContext classification)
    { _db = db; _user = user; _files = files; _logger = logger; _classification = classification; }

    public async Task<BankPortfolioImportDto> UploadAsync(Guid bankId, string fileName, string contentType, long length, Stream content, CancellationToken token)
    {
        EnsureImportPermission();
        var bank = await RequireAccessibleBankAsync(bankId, token);
        if (length <= 0 || length > MaximumBytes) throw new HrValidationException("Portfolio files must be between 1 byte and 50 MB.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".xls" or ".csv")) throw new HrValidationException("Only XLSX, XLS, and CSV portfolio files are supported.");

        var stored = await _files.SaveAsync($"bank-portfolio-imports/{bank.Id:N}", fileName, contentType, content, MaximumBytes, token);
        try
        {
            await using var storedStream = await _files.OpenReadAsync(stored.StorageKey, token);
            int rowCount;
            try { rowCount = await BankPortfolioFileInspector.CountRowsAsync(storedStream, extension, token); }
            catch (Exception exception) when (exception is not HrException and not OperationCanceledException)
            { throw new HrValidationException("The portfolio file is invalid or unreadable."); }
            var duplicate = await _db.BankPortfolioImports.AnyAsync(item => item.BankId == bankId && item.FileHash == stored.Sha256Hash && item.Status != "FAILED", token);
            if (duplicate) throw new HrConflictException("This exact file is already awaiting confirmation or has been imported for this bank.");
            var uploadedAt = DateTimeOffset.UtcNow;
            var bankName = ApiTextLocalizer.IsArabic ? bank.NameArabic : bank.NameEnglish;
            var portfolioName = $"{bankName} - {uploadedAt:dd/MM/yyyy}";
            var entity = new BankPortfolioImport(bankId, portfolioName, stored.OriginalFileName, ResolveContentType(extension), stored.Length,
                stored.Sha256Hash, stored.StorageKey, rowCount, _user.UserId, uploadedAt);
            _db.BankPortfolioImports.Add(entity);
            try { await _db.SaveChangesAsync(token); }
            catch (DbUpdateException)
            {
                _db.Entry(entity).State = EntityState.Detached;
                if (await _db.BankPortfolioImports.AsNoTracking().AnyAsync(item => item.BankId == bankId && item.FileHash == stored.Sha256Hash, token))
                    throw new HrConflictException("This exact file is already awaiting confirmation or has been imported for this bank.");
                throw;
            }
            return await GetAsync(bankId, entity.Id, token);
        }
        catch
        {
            await _files.DeleteAsync(stored.StorageKey, token);
            throw;
        }
    }

    public async Task<BankPortfolioImportDataPreviewDto> PreviewDataAsync(Guid bankId, Guid importId, CancellationToken token)
    {
        EnsureImportPermission();
        _ = await RequireAccessibleBankAsync(bankId, token);
        var entity = await _db.BankPortfolioImports.AsNoTracking().SingleOrDefaultAsync(item => item.Id == importId && item.BankId == bankId, token)
            ?? throw new HrNotFoundException("Portfolio import was not found for this bank.");
        if (entity.Status != "READY") throw new HrConflictException("Only a ready portfolio import can be previewed.");

        var portfolio = await ResolvePortfolioAsync(bankId, token);
        await using var stream = await _files.OpenReadAsync(entity.StorageKey, token);
        var extension = Path.GetExtension(entity.OriginalFileName).ToLowerInvariant();
        var parsed = await CollectionImportParser.ParseAsync(stream, extension, token);
        var rows = await BuildPreviewRowsAsync(bankId, portfolio.Id, parsed, token);
        return new BankPortfolioImportDataPreviewDto(
            entity.Id,
            rows.Count,
            rows.Count(x => x.Status == "Ready"),
            rows.Count(x => x.Status == "Existing"),
            rows.Count(x => x.Status == "Error"),
            rows.Take(200).ToArray());
    }

    public async Task<BankPortfolioImportConfirmResultDto> ConfirmAsync(Guid bankId, Guid importId, string? notes, CancellationToken token)
    {
        EnsureImportPermission();
        _ = await RequireAccessibleBankAsync(bankId, token);
        ValidateNotes(notes);
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var entity = await _db.BankPortfolioImports.SingleOrDefaultAsync(item => item.Id == importId && item.BankId == bankId, token)
            ?? throw new HrNotFoundException("Portfolio import was not found for this bank.");
        if (entity.Status == "COMPLETED") throw new HrConflictException("This portfolio import is already complete.");
        if (entity.Status != "READY") throw new HrConflictException("Only a ready portfolio import can be confirmed.");

        var portfolio = await ResolvePortfolioAsync(bankId, token);
        await using var stream = await _files.OpenReadAsync(entity.StorageKey, token);
        var extension = Path.GetExtension(entity.OriginalFileName).ToLowerInvariant();
        var parsed = await CollectionImportParser.ParseAsync(stream, extension, token);
        var previewRows = await BuildPreviewRowsAsync(bankId, portfolio.Id, parsed, token);

        var buckets = await _db.CollectionBucketDefinitions
            .Where(x => x.OrganizationId == bankId && x.IsActive && (x.PortfolioId == null || x.PortfolioId == portfolio.Id))
            .OrderByDescending(x => x.PortfolioId != null).ThenBy(x => x.SortOrder).ToArrayAsync(token);
        if (buckets.Length == 0) throw new HrConflictException("No delinquency bucket is configured for this organization.");

        var customers = await _db.CollectionCustomers.Where(x => x.OrganizationId == bankId)
            .ToDictionaryAsync(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase, token);
        var cases = await _db.CollectionCases.Where(x => x.PortfolioId == portfolio.Id)
            .ToDictionaryAsync(x => x.AccountReference, StringComparer.OrdinalIgnoreCase, token);

        var imported = 0; var skipped = 0; var invalid = 0; var now = DateTimeOffset.UtcNow;
        var org = portfolio.Organization ?? await _db.CollectionClientOrganizations.SingleAsync(x => x.Id == bankId, token);
        var collectorDirectory = await _db.Users.AsNoTracking().CollectorIdentities().SelectIdentityCandidates().ToArrayAsync(token);
        var collectorTeams = await CollectionImportAssignment.ActiveTeamIdsAsync(_db, token);

        foreach (var row in previewRows)
        {
            if (row.Status == "Error") { invalid++; continue; }

            var source = parsed.First(x => x.RowNumber == row.RowNumber);
            var file = CollectionFileRowMapper.Read(source);
            var customerCode = row.CustomerCode!;
            var account = row.AccountReference!;
            var outstanding = row.OutstandingBalance!.Value;
            var overdue = file.Overdue ?? outstanding;
            var bucketsForMatch = buckets.Select((bucket, index) => new CollectionBucketMatcher.BucketCandidate(index, bucket.Code, bucket.NameArabic, bucket.NameEnglish, bucket.MinimumDays, bucket.MaximumDays, bucket.SortOrder)).ToArray();
            var matched = CollectionBucketMatcher.Resolve(bucketsForMatch, file.DaysPastDue, file.BucketText) ?? throw new HrConflictException("No delinquency bucket is configured for this organization.");
            var bucket = buckets[matched.Index];
            var dpd = file.DaysPastDue ?? Math.Max(0, bucket.MinimumDays ?? 0);
            if (!customers.TryGetValue(customerCode, out var customer))
            {
                customer = new CollectionCustomer(bankId, customerCode, file.NameArabic ?? string.Empty, file.NameEnglish ?? string.Empty, now);
                customers[customerCode] = customer;
                _db.CollectionCustomers.Add(customer);
            }
            customer.ApplyImportedContact(file.NameArabic, file.NameEnglish, file.NationalId, file.Mobile1);
            customer.ApplyImportedProfile(file.Mobile2, file.Mobile3, file.Region, file.Area, file.City, file.Address1, file.Employer, file.JobTitle, file.Feedback, CollectionFileRowMapper.HasArabic(file.Address1 ?? file.Address2), file.Address2);

            CollectionCase collectionCase;
            if (!cases.TryGetValue(account, out collectionCase!))
            {
                collectionCase = new CollectionCase(portfolio.Id, customer.Id,
                    BuildCaseNumber(org.Code, portfolio.Code, account),
                    account, outstanding, outstanding, overdue, dpd, bucket.Id, now);
                collectionCase.ApplyImportedReferences(file.Contract, ClassifiedPortfolio.ProductTypeOrDefault(portfolio, file.Product), now);
                collectionCase.LinkImport(entity.Id);
                cases[account] = collectionCase;
                _db.CollectionCases.Add(collectionCase);
                _db.CollectionCaseBucketHistory.Add(new CaseBucketHistory(collectionCase.Id, null, bucket.Id, "Portfolio import", CollectionsValues.AssignmentSources.Import, _user.UserId, now));
                imported++;
            }
            else
            {
                var previousBucket = collectionCase.CurrentBucketId;
                collectionCase.ApplyImportedBalances(outstanding, overdue, dpd, bucket.Id, now);
                collectionCase.ApplyImportedReferences(file.Contract, ClassifiedPortfolio.ProductTypeOrDefault(portfolio, file.Product), now);
                if (previousBucket != bucket.Id)
                    _db.CollectionCaseBucketHistory.Add(new CaseBucketHistory(collectionCase.Id, previousBucket, bucket.Id, "Authoritative portfolio import", CollectionsValues.AssignmentSources.Import, _user.UserId, now));
                skipped++;
            }
            collectionCase.ApplyImportedProfile(file.CardNumber, file.StatusText, file.Stage, file.CreditLimit, file.PurchaseLimit, file.ActivationDate, file.LastPaymentAmount, CollectionFileRowMapper.ToTimestamp(file.LastPaymentDate), file.LastTransactionDate, file.LastTransactionAmount, CollectionFileRowMapper.Serialize(source.Values), now);
            collectionCase.ApplyImportedDeskFields(file.PreviousCollector, file.FileCollector, file.BucketText,
                CollectorIdentity.UniqueMatch(collectorDirectory, file.PreviousCollector),
                CollectorIdentity.UniqueMatch(collectorDirectory, file.FileCollector));
            CollectionImportAssignment.TryAssign(_db, collectionCase, collectorTeams, _user.UserId, now);
            var priority = CollectionRules.CalculatePriority(collectionCase.OutstandingBalance, collectionCase.DaysPastDue, false, false, 999);
            collectionCase.SetPriority(priority.Score, string.Join(" + ", priority.Reasons), now);
        }

        entity.Confirm(now);
        entity.UpdateNotes(notes, now);
        await _db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        var dto = await GetAsync(bankId, importId, token);
        return new BankPortfolioImportConfirmResultDto(dto, imported, skipped, invalid);
    }

    public async Task<BankPortfolioImportDto> UpdateNotesAsync(Guid bankId, Guid importId, string? notes, CancellationToken token)
    {
        EnsureImportPermission(); _ = await RequireAccessibleBankAsync(bankId, token); ValidateNotes(notes);
        var entity = await _db.BankPortfolioImports.SingleOrDefaultAsync(item => item.Id == importId && item.BankId == bankId, token)
            ?? throw new HrNotFoundException("Portfolio import was not found for this bank.");
        entity.UpdateNotes(notes, DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(token);
        return await GetAsync(bankId, importId, token);
    }

    public async Task<BankPortfolioReplacementPreviewDto> PreviewReplacementAsync(Guid bankId, Guid importId, string fileName,
        string contentType, long length, Stream content, CancellationToken token)
    {
        EnsureImportPermission(); var bank = await RequireAccessibleBankAsync(bankId, token);
        _ = await _db.BankPortfolioImports.AsNoTracking().SingleOrDefaultAsync(item => item.Id == importId && item.BankId == bankId, token)
            ?? throw new HrNotFoundException("Portfolio import was not found for this bank.");
        if (length <= 0 || length > MaximumBytes) throw new HrValidationException("Portfolio files must be between 1 byte and 50 MB.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".xls" or ".csv")) throw new HrValidationException("Only XLSX, XLS, and CSV portfolio files are supported.");
        var stored = await _files.SaveAsync($"bank-portfolio-imports/{bank.Id:N}/replacements", fileName, contentType, content, MaximumBytes, token);
        try
        {
            await using var storedStream = await _files.OpenReadAsync(stored.StorageKey, token);
            int rowCount;
            try { rowCount = await BankPortfolioFileInspector.CountRowsAsync(storedStream, extension, token); }
            catch (Exception exception) when (exception is not HrException and not OperationCanceledException)
            { throw new HrValidationException("The portfolio file is invalid or unreadable."); }
            if (await _db.BankPortfolioImports.AsNoTracking().AnyAsync(item => item.BankId == bankId && item.Id != importId && item.FileHash == stored.Sha256Hash, token))
                throw new HrConflictException("This exact file is already used by another import for this bank.");
            var id = Guid.NewGuid();
            Replacements[id] = new(bankId, importId, _user.UserId, stored.OriginalFileName, ResolveContentType(extension), stored.Length,
                stored.Sha256Hash, stored.StorageKey, rowCount, DateTimeOffset.UtcNow.AddMinutes(30));
            return new(id.ToString("N"), stored.OriginalFileName, extension[1..].ToUpperInvariant(), stored.Length, rowCount);
        }
        catch { await _files.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }
    }

    public async Task<BankPortfolioImportDto> ConfirmReplacementAsync(Guid bankId, Guid importId, string replacementToken, CancellationToken token)
    {
        EnsureImportPermission(); _ = await RequireAccessibleBankAsync(bankId, token);
        if (!Guid.TryParseExact(replacementToken, "N", out var candidateId) || !Replacements.TryRemove(candidateId, out var replacement) ||
            replacement.BankId != bankId || replacement.ImportId != importId || replacement.UserId != _user.UserId || replacement.ExpiresAt < DateTimeOffset.UtcNow)
            throw new HrValidationException("The replacement review has expired or is invalid. Select the file again.");
        var committed = false;
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var entity = await _db.BankPortfolioImports.SingleOrDefaultAsync(item => item.Id == importId && item.BankId == bankId, token)
                ?? throw new HrNotFoundException("Portfolio import was not found for this bank.");
            if (await _db.BankPortfolioImports.AnyAsync(item => item.BankId == bankId && item.Id != importId && item.FileHash == replacement.FileHash, token))
                throw new HrConflictException("This exact file is already used by another import for this bank.");
            var oldStorageKey = entity.StorageKey;
            entity.ReplaceFile(replacement.OriginalFileName, replacement.ContentType, replacement.FileSize, replacement.FileHash,
                replacement.StorageKey, replacement.RowCount, DateTimeOffset.UtcNow);
            try { await _db.SaveChangesAsync(token); await transaction.CommitAsync(token); committed = true; }
            catch (DbUpdateException) { throw new HrConflictException("This exact file is already used by another import for this bank."); }
            await _files.DeleteAsync(oldStorageKey, CancellationToken.None);
            return await GetAsync(bankId, importId, token);
        }
        catch { if (!committed) await _files.DeleteAsync(replacement.StorageKey, CancellationToken.None); throw; }
    }

    public async Task DeleteAsync(Guid bankId, Guid importId, CancellationToken token)
    {
        EnsureImportPermission(); _ = await RequireAccessibleBankAsync(bankId, token);
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var entity = await _db.BankPortfolioImports.SingleOrDefaultAsync(item => item.Id == importId && item.BankId == bankId, token)
            ?? throw new HrNotFoundException("Portfolio import was not found for this bank.");

        var caseIds = await _db.CollectionCases.Where(item => item.SourceImportId == importId).Select(item => item.Id).ToListAsync(token);
        var attachmentKeys = new List<string>();
        if (caseIds.Count > 0)
        {
            if (await _db.CollectionPaymentAllocations.AnyAsync(item => caseIds.Contains(item.CaseId), token)
                || await _db.CollectionPayments.AnyAsync(item => caseIds.Contains(item.CaseId) && item.Status != CollectionsValues.PaymentStatuses.Rejected, token))
                throw new HrConflictException("This portfolio has collection payments and cannot be deleted. Archive it instead.");

            var complaintIds = await _db.CollectionComplaints.Where(item => caseIds.Contains(item.CaseId)).Select(item => item.Id).ToListAsync(token);
            var paymentIds = await _db.CollectionPayments.Where(item => caseIds.Contains(item.CaseId)).Select(item => item.Id).ToListAsync(token);
            attachmentKeys = await _db.CollectionAttachments
                .Where(item => caseIds.Contains(item.CaseId)
                    || (item.ComplaintId != null && complaintIds.Contains(item.ComplaintId.Value))
                    || (item.PaymentId != null && paymentIds.Contains(item.PaymentId.Value)))
                .Select(item => item.StorageKey)
                .ToListAsync(token);

            if (complaintIds.Count > 0)
                await _db.CollectionComplaintNotes.Where(item => complaintIds.Contains(item.ComplaintId)).ExecuteDeleteAsync(token);
            await _db.CollectionAttachments.Where(item => caseIds.Contains(item.CaseId)
                || (item.ComplaintId != null && complaintIds.Contains(item.ComplaintId.Value))
                || (item.PaymentId != null && paymentIds.Contains(item.PaymentId.Value))).ExecuteDeleteAsync(token);
            if (complaintIds.Count > 0)
                await _db.CollectionComplaints.Where(item => complaintIds.Contains(item.Id)).ExecuteDeleteAsync(token);
            await _db.AccountingTransportationClaims
                .Where(item => item.CaseId != null && caseIds.Contains(item.CaseId.Value))
                .ExecuteUpdateAsync(set => set.SetProperty(item => item.CaseId, (Guid?)null), token);
            var visitIds = await _db.CollectionFieldVisits.Where(item => caseIds.Contains(item.CaseId)).Select(item => item.Id).ToListAsync(token);
            if (visitIds.Count > 0)
                await _db.AccountingTransportationClaims
                    .Where(item => item.FieldVisitId != null && visitIds.Contains(item.FieldVisitId.Value))
                    .ExecuteUpdateAsync(set => set.SetProperty(item => item.FieldVisitId, (Guid?)null), token);
            await _db.CollectionDcrs.Where(item => caseIds.Contains(item.CaseId)).ExecuteDeleteAsync(token);
            await _db.CollectionFieldVisits.Where(item => caseIds.Contains(item.CaseId)).ExecuteDeleteAsync(token);
            await _db.CollectionPromisesToPay.Where(item => caseIds.Contains(item.CaseId)).ExecuteDeleteAsync(token);
            await _db.CollectionActivities.Where(item => caseIds.Contains(item.CaseId)).ExecuteDeleteAsync(token);
            await _db.CollectionAssignmentHistory.Where(item => caseIds.Contains(item.CaseId)).ExecuteDeleteAsync(token);
            await _db.CollectionCaseBucketHistory.Where(item => caseIds.Contains(item.CaseId)).ExecuteDeleteAsync(token);
            if (paymentIds.Count > 0)
                await _db.CollectionPayments.Where(item => paymentIds.Contains(item.Id)).ExecuteDeleteAsync(token);
            await _db.CollectionAuditLogs.Where(item => item.CaseId != null && caseIds.Contains(item.CaseId.Value)).ExecuteDeleteAsync(token);
            await _db.CollectionCases.Where(item => item.SourceImportId == importId).ExecuteDeleteAsync(token);
        }

        var storageKey = entity.StorageKey;
        _db.CollectionAuditLogs.Add(new CollectionAuditLog(_user.UserId, "PortfolioImportDeleted", nameof(BankPortfolioImport), importId, null,
            JsonSerializer.Serialize(new { entity.PortfolioName, entity.OriginalFileName, entity.RowCount, CasesRemoved = caseIds.Count }), null, "BANK_WORKSPACE", DateTimeOffset.UtcNow));
        await _db.SaveChangesAsync(token);
        await _db.BankPortfolioImports.Where(item => item.Id == importId && item.BankId == bankId).ExecuteDeleteAsync(token);
        await transaction.CommitAsync(token);

        foreach (var key in attachmentKeys.Append(storageKey).Distinct(StringComparer.Ordinal))
        {
            try { await _files.DeleteAsync(key, CancellationToken.None); }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Portfolio import {ImportId} for bank {BankId} was deleted, but storage cleanup failed for key {StorageKey}.",
                    importId, bankId, key);
            }
        }
    }

    public async Task<BankPortfolioImportDto> GetAsync(Guid bankId, Guid importId, CancellationToken token)
    {
        EnsureImportPermission(); _ = await RequireAccessibleBankAsync(bankId, token);
        return await Project(_db.BankPortfolioImports.AsNoTracking().Where(item => item.Id == importId && item.BankId == bankId)).SingleOrDefaultAsync(token)
            ?? throw new HrNotFoundException("Portfolio import was not found for this bank.");
    }

    public async Task<BankPortfolioImportPageDto> GetHistoryAsync(Guid bankId, int page, int pageSize, string? search, CancellationToken token)
    {
        EnsureImportPermission(); _ = await RequireAccessibleBankAsync(bankId, token);
        if (page < 1 || pageSize is < 1 or > 100) throw new HrValidationException("Page must be at least 1 and page size must be between 1 and 100.");
        var query = _db.BankPortfolioImports.AsNoTracking().Where(item => item.BankId == bankId && item.Status == "COMPLETED" && !item.IsArchived);
        if (_classification.HasValue)
        {
            var classified = _db.CollectionCases.AsNoTracking().Apply(_classification);
            query = query.Where(item => classified.Any(x => x.SourceImportId == item.Id));
        }
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); query = query.Where(item => item.OriginalFileName.ToLower().Contains(term) || item.PortfolioName.ToLower().Contains(term)); }
        var total = await query.CountAsync(token);
        var items = await Project(query.OrderByDescending(item => item.UploadedAt).Skip((page - 1) * pageSize).Take(pageSize)).ToArrayAsync(token);
        return new BankPortfolioImportPageDto(items, total, page, pageSize, total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    private async Task<ClientOrganization> RequireAccessibleBankAsync(Guid bankId, CancellationToken token)
    {
        var global = _user.Roles.Any(role => role is SystemRoleNames.Admin or SystemRoleNames.CollectionsOperationsManager or SystemRoleNames.CollectionsReviewer or SystemRoleNames.CollectionsAuditor);
        var userId = _user.UserId;
        return await _db.CollectionClientOrganizations.AsNoTracking().SingleOrDefaultAsync(bank => bank.Id == bankId && bank.IsActive && (bank.OrganizationType == CollectionsValues.OrganizationTypes.Bank || bank.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance) &&
            (global || _db.CollectionUserAccess.Any(access => access.UserId == userId && access.OrganizationId == bank.Id)), token)
            ?? throw new HrNotFoundException("Bank was not found or is outside your authorized scope.");
    }

    private IQueryable<BankPortfolioImportDto> Project(IQueryable<BankPortfolioImport> query) => query.Select(item => new BankPortfolioImportDto(
        item.Id, item.BankId, item.Bank.NameArabic, item.Bank.NameEnglish, item.PortfolioName, item.OriginalFileName,
        item.ContentType == "text/csv" ? "CSV" : item.ContentType == "application/vnd.ms-excel" ? "XLS" : "XLSX",
        item.FileSize, item.RowCount, item.Status,
        item.UploadedById, item.UploadedBy.FullName, item.UploadedAt, item.ConfirmedAt, item.Notes, item.UpdatedAt));

    private void EnsureImportPermission()
    {
        if (!_user.Roles.Any(role => role.Equals(SystemRoleNames.Admin, StringComparison.OrdinalIgnoreCase) || role.Equals(SystemRoleNames.CollectionsOperationsManager, StringComparison.OrdinalIgnoreCase)))
            throw new HrForbiddenException("Only collections operations management can import bank portfolios.");
    }

    private static void ValidateNotes(string? notes)
    {
        if (notes?.Length > 1000) throw new HrValidationException("Notes cannot exceed 1000 characters.");
    }

    private static string ResolveContentType(string extension) => extension switch
    {
        ".csv" => "text/csv", ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", _ => "application/octet-stream"
    };

    private async Task<CollectionPortfolio> ResolvePortfolioAsync(Guid organizationId, CancellationToken token)
    {
        if (_classification.HasValue)
            return await ClassifiedPortfolio.ResolveAsync(_db, organizationId, _classification.Primary!, _classification.Sub!, token);

        var existing = await _db.CollectionPortfolios.Include(x => x.Organization)
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(token);
        if (existing is not null) return existing;

        var portfolio = new CollectionPortfolio(organizationId, "DEFAULT", "المحفظة الافتراضية", "Default Portfolio", "EGP", DateTimeOffset.UtcNow);
        _db.CollectionPortfolios.Add(portfolio);
        await _db.SaveChangesAsync(token);
        return await _db.CollectionPortfolios.Include(x => x.Organization).SingleAsync(x => x.Id == portfolio.Id, token);
    }

    private async Task<List<BankPortfolioImportPreviewRowDto>> BuildPreviewRowsAsync(
        Guid organizationId, Guid portfolioId, IReadOnlyCollection<ParsedCollectionRow> parsed, CancellationToken token)
    {
        var existingAccounts = (await _db.CollectionCases.AsNoTracking()
            .Where(x => x.PortfolioId == portfolioId)
            .Select(x => x.AccountReference)
            .ToArrayAsync(token)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nationalIds = await _db.CollectionCustomers.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.NationalId != null)
            .Select(x => new { x.NationalId, x.CustomerCode })
            .ToDictionaryAsync(x => x.NationalId!, x => x.CustomerCode, StringComparer.OrdinalIgnoreCase, token);
        var buckets = await _db.CollectionBucketDefinitions.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive && (x.PortfolioId == null || x.PortfolioId == portfolioId))
            .ToArrayAsync(token);
        var batchAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<BankPortfolioImportPreviewRowDto>(parsed.Count);

        var bucketCandidates = buckets.Select((bucket, index) => new CollectionBucketMatcher.BucketCandidate(index, bucket.Code, bucket.NameArabic, bucket.NameEnglish, bucket.MinimumDays, bucket.MaximumDays, bucket.SortOrder)).ToArray();
        foreach (var source in parsed)
        {
            var errors = new List<string>();
            var file = CollectionFileRowMapper.Read(source);
            var account = file.Account;
            var customerCode = file.CustomerCode;
            var national = file.NationalId ?? string.Empty;
            var phone = file.Mobile1 ?? string.Empty;
            var outstanding = file.Outstanding;
            var overdue = file.Overdue ?? outstanding;
            var dpd = file.DaysPastDue;

            if (string.IsNullOrWhiteSpace(account)) errors.Add("A case, card, or account number is required.");
            else if (!batchAccounts.Add(account)) errors.Add("Duplicate account reference in this file.");
            if (string.IsNullOrWhiteSpace(customerCode))
                customerCode = BuildCustomerCode(national, phone, file.NameArabic ?? file.NameEnglish ?? string.Empty, account);
            if (string.IsNullOrWhiteSpace(file.NameArabic) && string.IsNullOrWhiteSpace(file.NameEnglish)) errors.Add("At least one customer name is required.");
            if (!outstanding.HasValue || outstanding < 0) errors.Add("Current balance is missing or invalid.");
            if (overdue < 0) errors.Add("Total dues is invalid.");
            if (dpd.HasValue && dpd < 0) errors.Add("Days past due is invalid.");
            if (!dpd.HasValue && string.IsNullOrWhiteSpace(file.BucketText)) errors.Add("Either days past due or a bucket label is required.");
            if (bucketCandidates.Length == 0) errors.Add("No delinquency bucket is configured for this organization.");
            if (!string.IsNullOrWhiteSpace(national) && national.Length != 14) errors.Add("Egyptian national ID must contain 14 digits.");
            if (!string.IsNullOrWhiteSpace(phone) && (phone.Length != 11 || !phone.StartsWith("01", StringComparison.Ordinal)))
                errors.Add("Egyptian mobile must contain 11 digits and start with 01.");
            if (!string.IsNullOrWhiteSpace(national) && nationalIds.TryGetValue(national, out var owner)
                && !owner.Equals(customerCode, StringComparison.OrdinalIgnoreCase))
                errors.Add("National ID is already linked to another customer code.");

            var existing = !string.IsNullOrWhiteSpace(account) && existingAccounts.Contains(account);
            var status = errors.Count > 0 ? "Error" : existing ? "Existing" : "Ready";
            var name = ApiTextLocalizer.IsArabic
                ? (string.IsNullOrWhiteSpace(file.NameArabic) ? file.NameEnglish : file.NameArabic)
                : (string.IsNullOrWhiteSpace(file.NameEnglish) ? file.NameArabic : file.NameEnglish);
            rows.Add(new BankPortfolioImportPreviewRowDto(
                source.RowNumber, NullIfEmpty(name), NullIfEmpty(customerCode), NullIfEmpty(account),
                outstanding, status, errors.Select(error => ApiTextLocalizer.Localize(error) ?? error).ToArray(),
                NullIfEmpty(national), file.CardNumber, file.BucketText, file.StatusText, overdue));
        }

        return rows;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildCustomerCode(string nationalId, string mobile, string name, string account)
    {
        if (!string.IsNullOrWhiteSpace(nationalId) && nationalId.Length == 14) return "NID-" + nationalId;
        if (!string.IsNullOrWhiteSpace(mobile) && mobile.Length >= 8) return "MOB-" + mobile;
        var seed = $"{name}|{account}";
        return "CUS-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)))[..12];
    }

    private static string BuildCaseNumber(string organization, string portfolio, string account)
    {
        var safe = new string(account.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (safe.Length > 40) safe = safe[..24] + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account)))[..12];
        return $"{organization}-{portfolio}-{safe}"[..Math.Min(80, organization.Length + portfolio.Length + safe.Length + 2)];
    }
}
