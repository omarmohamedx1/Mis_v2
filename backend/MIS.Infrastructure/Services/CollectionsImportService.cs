using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Hr;
using MIS.Domain.Services;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class CollectionsImportService : ICollectionsImportService
{
    private const long MaximumBytes = ExcelImportLimits.MaximumBytes;
    private readonly ApplicationDbContext _db; private readonly ICurrentUserContext _user; private readonly IHrFileStorage _files;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public CollectionsImportService(ApplicationDbContext db, ICurrentUserContext user, IHrFileStorage files) { _db = db; _user = user; _files = files; }

    public async Task<IReadOnlyCollection<PortfolioLookupDto>> GetPortfoliosAsync(Guid? organizationId, CancellationToken token)
    {
        EnsureImportPermission(); var ar = ApiTextLocalizer.IsArabic; return await _db.CollectionPortfolios.AsNoTracking().Where(x => x.IsActive && (!organizationId.HasValue || x.OrganizationId == organizationId)).OrderBy(x => ar ? x.NameArabic : x.NameEnglish).Select(x => new PortfolioLookupDto(x.Id, x.OrganizationId, x.Code, ar ? x.NameArabic : x.NameEnglish, x.CurrencyCode, x.IsActive)).ToArrayAsync(token);
    }

    public async Task<CollectionImportBatchDto> UploadAsync(Guid organizationId, Guid portfolioId, string fileName, string contentType, long length, Stream content, CancellationToken token)
    {
        EnsureImportPermission(); var portfolio = await _db.CollectionPortfolios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == portfolioId && x.OrganizationId == organizationId && x.IsActive, token) ?? throw new HrValidationException("A valid active client portfolio is required.");
        if (length <= 0 || length > MaximumBytes) throw new HrValidationException("Collection import files must be between 1 byte and 50 MB.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".csv" or ".xlsx" or ".xls")) throw new HrValidationException("Only CSV, XLSX, and XLS collection imports are supported.");
        var stored = await _files.SaveAsync("collections-imports", fileName, contentType, content, MaximumBytes, token); if (await _db.CollectionImportBatches.AnyAsync(x => x.PortfolioId == portfolioId && x.FileHash == stored.Sha256Hash && x.Status == "COMPLETED", token)) { await _files.DeleteAsync(stored.StorageKey, token); throw new HrConflictException("This file was already imported into the selected portfolio."); }
        var now = DateTimeOffset.UtcNow; var batch = new CollectionImportBatch(organizationId, portfolioId, stored.OriginalFileName, stored.ContentType, stored.Length, stored.Sha256Hash, stored.StorageKey, _user.UserId, now); _db.CollectionImportBatches.Add(batch); await _db.SaveChangesAsync(token);
        try
        {
            await using var stream = await _files.OpenReadAsync(stored.StorageKey, token); var parsed = await CollectionImportParser.ParseAsync(stream, extension, token); var rows = await ValidateRowsAsync(batch.Id, organizationId, portfolioId, parsed, now, token); _db.CollectionImportRows.AddRange(rows); batch.SetPreview(rows.Count, rows.Count(x => x.IsValid), rows.Count(x => !x.IsValid), DateTimeOffset.UtcNow); AddAudit("ImportPreviewCreated", batch.Id, new { batch.FileName, batch.OrganizationId, batch.PortfolioId, batch.TotalRows, batch.ValidRows, batch.InvalidRows }); await _db.SaveChangesAsync(token); return await BatchDto(batch.Id, token);
        }
        catch (Exception exception) when (exception is HrException or InvalidDataException or FormatException)
        {
            batch.Fail(exception.Message, DateTimeOffset.UtcNow); AddAudit("ImportFailed", batch.Id, new { batch.FileName, Error = exception.Message }); await _db.SaveChangesAsync(token); throw;
        }
    }

    public async Task<PagedResultDto<CollectionImportBatchDto>> GetBatchesAsync(int page, int pageSize, CancellationToken token)
    {
        EnsureImportPermission(); ValidatePage(page, pageSize); var query = BatchProjection(_db.CollectionImportBatches.AsNoTracking().OrderByDescending(x => x.UploadedAt)); var total = await _db.CollectionImportBatches.CountAsync(token); var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(token); return Page(rows, total, page, pageSize);
    }

    public async Task<CollectionImportPreviewDto> GetPreviewAsync(Guid batchId, int page, int pageSize, bool? valid, CancellationToken token)
    {
        EnsureImportPermission(); ValidatePage(page, pageSize); var batch = await BatchDto(batchId, token); var query = _db.CollectionImportRows.AsNoTracking().Where(x => x.BatchId == batchId); if (valid.HasValue) query = query.Where(x => x.IsValid == valid); var total = await query.CountAsync(token); var raw = await query.OrderBy(x => x.RowNumber).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.RowNumber, x.AccountReference, x.CustomerCode, x.NameArabic, x.NameEnglish, x.OutstandingBalance, x.OverdueBalance, x.DaysPastDue, x.IsValid, x.ErrorsJson, x.RawJson }).ToArrayAsync(token); var rows = raw.Select(x => { var extras = CollectionFileRowMapper.DescribeExtras(x.RawJson); return new CollectionImportRowDto(x.Id, x.RowNumber, x.AccountReference, x.CustomerCode, ApiTextLocalizer.IsArabic ? x.NameArabic ?? x.NameEnglish : x.NameEnglish ?? x.NameArabic, x.OutstandingBalance, x.OverdueBalance, x.DaysPastDue, x.IsValid, JsonSerializer.Deserialize<string[]>(x.ErrorsJson, JsonOptions) ?? [], extras.SheetName, extras.ExtraFields); }).ToArray(); return new CollectionImportPreviewDto(batch, Page(rows, total, page, pageSize));
    }

    public async Task<CollectionImportBatchDto> ConfirmAsync(Guid batchId, ConfirmCollectionImportRequest request, CancellationToken token)
    {
        EnsureImportPermission(); await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token); var batch = await _db.CollectionImportBatches.Include(x => x.Organization).Include(x => x.Portfolio).SingleOrDefaultAsync(x => x.Id == batchId, token) ?? throw new HrNotFoundException("Collection import batch was not found."); if (batch.Status != "PREVIEW_READY") throw new HrConflictException("Only an import with a ready preview can be confirmed.");
        var excluded = (request.ExcludedRowNumbers ?? []).ToHashSet();
        var rows = await _db.CollectionImportRows.Where(x => x.BatchId == batchId && x.IsValid && !excluded.Contains(x.RowNumber)).OrderBy(x => x.RowNumber).ToArrayAsync(token); var buckets = await _db.CollectionBucketDefinitions.Where(x => x.OrganizationId == batch.OrganizationId && x.IsActive && (x.PortfolioId == null || x.PortfolioId == batch.PortfolioId)).OrderByDescending(x => x.PortfolioId != null).ThenBy(x => x.SortOrder).ToArrayAsync(token);
        var customerCodes = rows.Select(x => x.CustomerCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); var customers = await _db.CollectionCustomers.Where(x => x.OrganizationId == batch.OrganizationId && customerCodes.Contains(x.CustomerCode)).ToDictionaryAsync(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase, token); var accounts = rows.Select(x => x.AccountReference).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); var cases = await _db.CollectionCases.Where(x => x.PortfolioId == batch.PortfolioId && accounts.Contains(x.AccountReference)).ToDictionaryAsync(x => x.AccountReference, StringComparer.OrdinalIgnoreCase, token);
        var inserted = 0; var updated = 0; var now = DateTimeOffset.UtcNow;
        var candidates = buckets.Select((bucket, index) => new CollectionBucketMatcher.BucketCandidate(index, bucket.Code, bucket.NameArabic, bucket.NameEnglish, bucket.MinimumDays, bucket.MaximumDays, bucket.SortOrder)).ToArray();
        var collectorDirectory = await _db.Users.AsNoTracking().CollectorIdentities().SelectIdentityCandidates().ToArrayAsync(token);
        var collectorTeams = await CollectionImportAssignment.ActiveTeamIdsAsync(_db, token);
        foreach (var row in rows)
        {
            if (!row.OutstandingBalance.HasValue) continue;
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawJson, JsonOptions) ?? new Dictionary<string, string>();
            var file = CollectionFileRowMapper.Read(values);
            var matched = CollectionBucketMatcher.Resolve(candidates, row.DaysPastDue, file.BucketText) ?? throw new HrConflictException($"No configured delinquency bucket matches row {row.RowNumber}.");
            var bucket = buckets[matched.Index];
            var effectiveDpd = row.DaysPastDue ?? Math.Max(0, bucket.MinimumDays ?? 0);
            var overdue = row.OverdueBalance ?? row.OutstandingBalance.Value;
            if (!customers.TryGetValue(row.CustomerCode, out var customer)) { customer = new CollectionCustomer(batch.OrganizationId, row.CustomerCode, row.NameArabic ?? string.Empty, row.NameEnglish ?? string.Empty, now); customers[row.CustomerCode] = customer; _db.CollectionCustomers.Add(customer); } customer.ApplyImportedContact(row.NameArabic, row.NameEnglish, row.NationalId, row.Phone);
            customer.ApplyImportedProfile(file.Mobile2, file.Mobile3, file.Region, file.Area, file.City, file.Address1, file.Employer, file.JobTitle, file.Feedback, CollectionFileRowMapper.HasArabic(file.Address1 ?? file.Address2), file.Address2);
            if (!cases.TryGetValue(row.AccountReference, out var collectionCase)) { collectionCase = new CollectionCase(batch.PortfolioId, customer.Id, BuildCaseNumber(batch.Organization.Code, batch.Portfolio.Code, row.AccountReference), row.AccountReference, row.OutstandingBalance.Value, row.OutstandingBalance.Value, overdue, effectiveDpd, bucket.Id, now); collectionCase.ApplyImportedReferences(row.ContractReference, row.ProductType, now); cases[row.AccountReference] = collectionCase; _db.CollectionCases.Add(collectionCase); _db.CollectionCaseBucketHistory.Add(new CaseBucketHistory(collectionCase.Id, null, bucket.Id, "Initial portfolio import", CollectionsValues.AssignmentSources.Import, _user.UserId, now)); inserted++; }
            else { var previousBucket = collectionCase.CurrentBucketId; collectionCase.ApplyImportedBalances(row.OutstandingBalance.Value, overdue, effectiveDpd, bucket.Id, now); collectionCase.ApplyImportedReferences(row.ContractReference, row.ProductType, now); if (previousBucket != bucket.Id) _db.CollectionCaseBucketHistory.Add(new CaseBucketHistory(collectionCase.Id, previousBucket, bucket.Id, "Authoritative portfolio import", CollectionsValues.AssignmentSources.Import, _user.UserId, now)); updated++; }
            collectionCase.ApplyImportedProfile(file.CardNumber, file.StatusText, file.Stage, file.CreditLimit, file.PurchaseLimit, file.ActivationDate, file.LastPaymentAmount, CollectionFileRowMapper.ToTimestamp(file.LastPaymentDate), file.LastTransactionDate, file.LastTransactionAmount, row.RawJson, now);
            collectionCase.ApplyImportedDeskFields(file.PreviousCollector, file.FileCollector, file.BucketText,
                CollectorIdentity.UniqueMatch(collectorDirectory, file.PreviousCollector),
                CollectorIdentity.UniqueMatch(collectorDirectory, file.FileCollector));
            CollectionImportAssignment.TryAssign(_db, collectionCase, collectorTeams, _user.UserId, now);
            var priority = CollectionRules.CalculatePriority(collectionCase.OutstandingBalance, collectionCase.DaysPastDue, false, false, collectionCase.LastContactAt.HasValue ? (int)(now - collectionCase.LastContactAt.Value).TotalDays : 999); collectionCase.SetPriority(priority.Score, string.Join(" + ", priority.Reasons), now);
        }
        batch.Confirm(inserted, updated, batch.InvalidRows, now); AddAudit("ImportConfirmed", batch.Id, new { batch.FileName, batch.TotalRows, batch.ValidRows, batch.InvalidRows, Inserted = inserted, Updated = updated, Notes = request.Notes }); await _db.SaveChangesAsync(token); await transaction.CommitAsync(token); return await BatchDto(batch.Id, token);
    }

    public async Task<byte[]> ExportErrorsAsync(Guid batchId, CancellationToken token)
    {
        EnsureImportPermission(); _ = await BatchDto(batchId, token); var rows = await _db.CollectionImportRows.AsNoTracking().Where(x => x.BatchId == batchId && !x.IsValid).OrderBy(x => x.RowNumber).Select(x => new { x.RowNumber, x.AccountReference, x.CustomerCode, x.ErrorsJson }).ToArrayAsync(token); var builder = new StringBuilder("Row,AccountReference,CustomerCode,Errors\r\n"); foreach (var row in rows) builder.Append(Escape(row.RowNumber.ToString(CultureInfo.InvariantCulture))).Append(',').Append(Escape(row.AccountReference)).Append(',').Append(Escape(row.CustomerCode)).Append(',').Append(Escape(string.Join(" | ", JsonSerializer.Deserialize<string[]>(row.ErrorsJson, JsonOptions) ?? []))).Append("\r\n"); return new UTF8Encoding(true).GetBytes(builder.ToString());
    }

    public async Task DeleteAsync(Guid batchId, CancellationToken token)
    {
        EnsureImportPermission();
        var batch = await _db.CollectionImportBatches.SingleOrDefaultAsync(x => x.Id == batchId, token) ?? throw new HrNotFoundException("Collection import batch was not found.");
        var storageKey = batch.StorageKey;
        var rows = await _db.CollectionImportRows.Where(x => x.BatchId == batchId).ToArrayAsync(token);
        _db.CollectionImportRows.RemoveRange(rows);
        _db.CollectionImportBatches.Remove(batch);
        AddAudit("ImportBatchDeleted", batch.Id, new { batch.FileName, batch.Status, batch.OrganizationId, batch.PortfolioId, batch.TotalRows });
        await _db.SaveChangesAsync(token);
        if (!string.IsNullOrWhiteSpace(storageKey)) { try { await _files.DeleteAsync(storageKey, token); } catch { /* file already removed */ } }
    }

    private async Task<List<CollectionImportRow>> ValidateRowsAsync(Guid batchId, Guid organizationId, Guid portfolioId, IReadOnlyCollection<ParsedCollectionRow> parsed, DateTimeOffset now, CancellationToken token)
    {
        var nationalIds = await _db.CollectionCustomers.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.NationalId != null).Select(x => new { x.NationalId, x.CustomerCode }).ToDictionaryAsync(x => x.NationalId!, x => x.CustomerCode, StringComparer.OrdinalIgnoreCase, token); var hasBuckets = await _db.CollectionBucketDefinitions.AsNoTracking().AnyAsync(x => x.OrganizationId == organizationId && x.IsActive && (x.PortfolioId == null || x.PortfolioId == portfolioId), token); var accounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var result = new List<CollectionImportRow>(parsed.Count);
        foreach (var source in parsed)
        {
            var file = CollectionFileRowMapper.Read(source); var account = file.Account; var (ar, en) = (file.NameArabic, file.NameEnglish); var national = file.NationalId ?? string.Empty; var phone = file.Mobile1 ?? string.Empty; var contract = file.Contract ?? string.Empty; var product = file.Product ?? string.Empty;
            var customerCode = file.CustomerCode; if (string.IsNullOrWhiteSpace(customerCode)) customerCode = BuildCustomerCode(national, phone, $"{ar}{en}", account);
            var outstanding = file.Outstanding; var overdue = file.Overdue ?? outstanding; int? dpd = file.DaysPastDue; var bucketText = file.BucketText; var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(account)) errors.Add("A case, card, or account number is required."); else if (!accounts.Add(account)) errors.Add("Duplicate account reference in this file."); if (string.IsNullOrWhiteSpace(ar) && string.IsNullOrWhiteSpace(en)) errors.Add("At least one customer name is required.");
            if (!outstanding.HasValue || outstanding < 0) errors.Add("Current balance is missing or invalid."); if (overdue < 0) errors.Add("Total dues is invalid."); if (dpd.HasValue && dpd < 0) errors.Add("Days past due is invalid."); if (!dpd.HasValue && string.IsNullOrWhiteSpace(bucketText)) errors.Add("Either days past due or a bucket label is required."); if (!hasBuckets) errors.Add("No delinquency bucket is configured for this client.");
            if (!string.IsNullOrWhiteSpace(national) && national.Length != 14) errors.Add("Egyptian national ID must contain 14 digits."); if (!string.IsNullOrWhiteSpace(phone) && (phone.Length != 11 || !phone.StartsWith("01", StringComparison.Ordinal))) errors.Add("Egyptian mobile must contain 11 digits and start with 01."); if (!string.IsNullOrWhiteSpace(national) && nationalIds.TryGetValue(national, out var ownerCode) && !ownerCode.Equals(customerCode, StringComparison.OrdinalIgnoreCase)) errors.Add("National ID is already linked to another customer code.");
            result.Add(new CollectionImportRow(batchId, source.RowNumber, account, customerCode, ar, en, string.IsNullOrWhiteSpace(national) ? null : national, string.IsNullOrWhiteSpace(phone) ? null : phone, string.IsNullOrWhiteSpace(contract) ? null : contract, string.IsNullOrWhiteSpace(product) ? null : product, outstanding, overdue, dpd, JsonSerializer.Serialize(source.Values, JsonOptions), JsonSerializer.Serialize(errors, JsonOptions), errors.Count == 0, now));
        }
        return result;
    }

    private IQueryable<CollectionImportBatchDto> BatchProjection(IQueryable<CollectionImportBatch> query) { var ar = ApiTextLocalizer.IsArabic; return query.Select(x => new CollectionImportBatchDto(x.Id, x.OrganizationId, ar ? x.Organization.NameArabic : x.Organization.NameEnglish, x.PortfolioId, ar ? x.Portfolio.NameArabic : x.Portfolio.NameEnglish, x.FileName, x.Status, x.TotalRows, x.ValidRows, x.InvalidRows, x.InsertedRows, x.UpdatedRows, x.SkippedRows, x.UploadedBy.FullName, x.UploadedAt, x.PreviewedAt, x.ConfirmedAt, x.FailureReason)); }
    private async Task<CollectionImportBatchDto> BatchDto(Guid id, CancellationToken token) => await BatchProjection(_db.CollectionImportBatches.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(token) ?? throw new HrNotFoundException("Collection import batch was not found.");
    private void AddAudit(string action, Guid batchId, object after) => _db.CollectionAuditLogs.Add(new CollectionAuditLog(_user.UserId, action, nameof(CollectionImportBatch), batchId, null, null, JsonSerializer.Serialize(after, JsonOptions), "IMPORT", DateTimeOffset.UtcNow));
    private void EnsureImportPermission() { if (!_user.Roles.Any(x => x.Equals(SystemRoleNames.Admin, StringComparison.OrdinalIgnoreCase) || x.Equals(SystemRoleNames.CollectionsOperationsManager, StringComparison.OrdinalIgnoreCase))) throw new HrForbiddenException("Only collections operations management can manage portfolio imports."); }

    private static string BuildCustomerCode(string nationalId, string phone, string name, string account)
    {
        var national = new string(Digits(nationalId).Where(char.IsDigit).ToArray()); if (national.Length == 14) return national;
        var mobile = NormalizePhone(phone); if (mobile.Length == 11 && mobile.StartsWith("01", StringComparison.Ordinal)) return mobile;
        return "C-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{name}|{account}")))[..12];
    }

    private static string Digits(string value) { const string arabic = "٠١٢٣٤٥٦٧٨٩"; const string eastern = "۰۱۲۳۴۵۶۷۸۹"; var builder = new StringBuilder(value.Length); foreach (var c in value) { var index = arabic.IndexOf(c); if (index < 0) index = eastern.IndexOf(c); builder.Append(index >= 0 ? (char)('0' + index) : c); } return builder.ToString(); }
    private static string NormalizePhone(string value) { var digits = new string(Digits(value).Where(char.IsDigit).ToArray()); return digits.StartsWith("20") && digits.Length == 12 ? "0" + digits[2..] : digits; }
    private static string BuildCaseNumber(string organization, string portfolio, string account) { var safe = new string(account.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant(); if (safe.Length > 40) safe = safe[..24] + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account)))[..12]; return $"{organization}-{portfolio}-{safe}"[..Math.Min(80, organization.Length + portfolio.Length + safe.Length + 2)]; }
    private static string Escape(string value) { if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value; return '"' + value.Replace("\"", "\"\"") + '"'; }
    private static void ValidatePage(int page, int pageSize) { if (page < 1 || pageSize is < 1 or > 100) throw new HrValidationException("Page must be at least 1 and page size must be between 1 and 100."); }
    private static PagedResultDto<T> Page<T>(IReadOnlyCollection<T> rows, int count, int page, int pageSize) => new(rows, count, page, pageSize, count == 0 ? 0 : (int)Math.Ceiling(count / (double)pageSize));
}
