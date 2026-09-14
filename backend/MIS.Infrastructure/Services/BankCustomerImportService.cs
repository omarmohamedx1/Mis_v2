using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Services;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class BankCustomerImportService(
    ApplicationDbContext db,
    IHrFileStorage storage,
    IHrAuditService audit,
    ICurrentUserContext user,
    IWorkingCalendarCalculator calendar,
    ICollectionsClassificationContext classification) : IBankCustomerImportService
{
    private const string Entity = "BankCustomerImport";
    private const long MaximumBytes = 20 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static readonly string[] Fields =
    [
        "CustomerCode", "CustomerName", "MobileNumber", "NationalId", "Address",
        "AccountNumber", "ContractNumber", "ProductType",
        "OutstandingAmount", "PaidAmount", "RemainingAmount", "OverdueAmount", "DaysPastDue"
    ];

    private sealed record Uploaded(string FileName, string StorageKey, string Extension);
    private sealed record PreviewSaved(Guid PreviewId, Guid PortfolioId, string StorageKey, int TotalRows);

    public async Task<IReadOnlyCollection<PortfolioLookupDto>> GetPortfoliosAsync(Guid organizationId, CancellationToken token)
    {
        EnsurePermission();
        await RequireOrganizationAsync(organizationId, token);
        var ar = ApiTextLocalizer.IsArabic;
        return await db.CollectionPortfolios.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .Apply(classification)
            .OrderBy(x => ar ? x.NameArabic : x.NameEnglish)
            .Select(x => new PortfolioLookupDto(x.Id, x.OrganizationId, x.Code, ar ? x.NameArabic : x.NameEnglish, x.CurrencyCode, x.IsActive))
            .ToArrayAsync(token);
    }

    public async Task<BankCustomerImportUpload> UploadAsync(Guid organizationId, HrUploadFile file, CancellationToken token)
    {
        EnsurePermission();
        await RequireOrganizationAsync(organizationId, token);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length <= 0 || file.Length > MaximumBytes || extension is not (".csv" or ".xls" or ".xlsx"))
            throw new HrValidationException("Choose a CSV, XLS, or XLSX file no larger than 20 MB.");

        var stored = await storage.SaveAsync($"bank-customer-imports/{organizationId:N}", file.FileName, file.ContentType, file.Content, MaximumBytes, token);
        try
        {
            await using var stream = await storage.OpenReadAsync(stored.StorageKey, token);
            await HrAttendanceImportService.ValidateSignatureAsync(stream, extension, token);
            var sheets = await new AttendanceImportParser(calendar).InspectAsync(stream, extension, token);
            var id = Guid.NewGuid();
            await audit.WriteAsync(new AuditWriteRequest(
                "BankCustomerImportUploaded", Entity, id.ToString(), null, null,
                new Uploaded(stored.OriginalFileName, stored.StorageKey, extension),
                $"Customer import uploaded for organization {organizationId:N}."), token);
            return new BankCustomerImportUpload(id, stored.OriginalFileName,
                sheets.Select(s => new BankCustomerImportSheet(s.SheetName, s.SuggestedHeaderRowNumber, s.DetectedColumns)).ToArray());
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<BankCustomerImportPreview> PreviewAsync(Guid organizationId, Guid id, BankCustomerImportMapping mapping, CancellationToken token)
    {
        EnsurePermission();
        await RequireOrganizationAsync(organizationId, token);
        var upload = Read<Uploaded>((await OwnedUpload(id, token)).NewValue);
        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "BankCustomerImportCompleted", token))
            throw new HrConflictException("This customer import is already complete.");

        if (mapping.Columns is null || mapping.Columns.Keys.Except(Fields).Any())
            throw new HrValidationException("Unsupported customer field mapping.");
        if (!mapping.Columns.TryGetValue("CustomerName", out var nameCol) || string.IsNullOrWhiteSpace(nameCol))
            throw new HrValidationException("Map the customer name column.");
        if (!mapping.Columns.TryGetValue("AccountNumber", out var accountCol) || string.IsNullOrWhiteSpace(accountCol))
            throw new HrValidationException("Map the account number column.");
        var hasOutstanding = mapping.Columns.TryGetValue("OutstandingAmount", out var outstandingCol) && !string.IsNullOrWhiteSpace(outstandingCol);
        var hasRemaining = mapping.Columns.TryGetValue("RemainingAmount", out var remainingCol) && !string.IsNullOrWhiteSpace(remainingCol);
        if (!hasOutstanding && !hasRemaining)
            throw new HrValidationException("Map the outstanding or remaining amount column.");

        var portfolio = await ResolvePortfolioAsync(organizationId, mapping.PortfolioId, token);
        await using var stream = await storage.OpenReadAsync(upload.StorageKey, token);
        var table = await AttendanceImportParser.ReadTableAsync(stream, upload.Extension, mapping.SheetName, mapping.HeaderRow, mapping.FirstDataRow, token);
        var indexes = new Dictionary<string, int>();
        foreach (var pair in mapping.Columns.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            var index = Array.FindIndex(table.Headers, column => column == pair.Value);
            if (index < 0) throw new HrValidationException("A mapped source column was not found.");
            indexes[pair.Key] = index;
        }

        var existingAccounts = (await db.CollectionCases.AsNoTracking()
            .Where(x => x.PortfolioId == portfolio.Id)
            .Select(x => x.AccountReference)
            .ToArrayAsync(token)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingNational = await db.CollectionCustomers.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.NationalId != null)
            .Select(x => new { x.NationalId, x.CustomerCode })
            .ToDictionaryAsync(x => x.NationalId!, x => x.CustomerCode, StringComparer.OrdinalIgnoreCase, token);
        var buckets = await db.CollectionBucketDefinitions.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive && (x.PortfolioId == null || x.PortfolioId == portfolio.Id) && x.MinimumDays != null)
            .ToArrayAsync(token);

        var batchAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<BankCustomerImportRow>();
        foreach (var cells in table.Rows)
        {
            string V(string key) => indexes.TryGetValue(key, out var i) && i < cells.Length ? cells[i].Trim() : "";
            var errors = new List<string>();
            var customerName = V("CustomerName");
            var customerCode = string.IsNullOrWhiteSpace(V("CustomerCode")) ? BuildCustomerCode(V("NationalId"), V("MobileNumber"), customerName, V("AccountNumber")) : V("CustomerCode");
            var mobile = NormalizePhone(V("MobileNumber"));
            var national = DigitsOnly(V("NationalId"));
            var account = V("AccountNumber");
            var contract = NullIfEmpty(V("ContractNumber"));
            var outstanding = ParseMoney(V("OutstandingAmount")) ?? ParseMoney(V("RemainingAmount"));
            var remaining = ParseMoney(V("RemainingAmount")) ?? outstanding;
            var paid = ParseMoney(V("PaidAmount")); // preview only — never treated as confirmed CollectionPayment
            var overdueText = V("OverdueAmount");
            var overdue = string.IsNullOrWhiteSpace(overdueText) ? outstanding : ParseMoney(overdueText);
            var dpd = ParseInt(V("DaysPastDue")) ?? 0;

            if (string.IsNullOrWhiteSpace(customerName)) errors.Add("Customer name is required. / اسم العميل مطلوب");
            if (string.IsNullOrWhiteSpace(account)) errors.Add("Account number is required. / رقم الحساب مطلوب");
            else if (!batchAccounts.Add(account)) errors.Add("Duplicate account in this file. / رقم حساب مكرر في الملف");
            if (!outstanding.HasValue || outstanding < 0) errors.Add("Outstanding amount is missing or invalid. / المديونية غير صالحة");
            if (!overdue.HasValue || overdue < 0) errors.Add("Overdue amount is missing or invalid. / المتأخر غير صالح");
            if (dpd < 0) errors.Add("Days past due is invalid. / أيام التأخر غير صالحة");
            else if (buckets.Length > 0 && !buckets.Any(x => dpd >= x.MinimumDays && (!x.MaximumDays.HasValue || dpd <= x.MaximumDays)))
                errors.Add("No configured bucket matches days past due. / لا توجد شريحة متأخرات مطابقة");
            if (!string.IsNullOrWhiteSpace(national) && national.Length != 14) errors.Add("National ID must contain 14 digits. / الرقم القومي يجب أن يكون 14 رقمًا");
            if (!string.IsNullOrWhiteSpace(mobile) && (mobile.Length != 11 || !mobile.StartsWith("01", StringComparison.Ordinal)))
                errors.Add("Mobile must be an Egyptian 01xxxxxxxxx number. / رقم الموبايل غير صالح");
            if (!string.IsNullOrWhiteSpace(national) && existingNational.TryGetValue(national, out var owner) &&
                !owner.Equals(customerCode, StringComparison.OrdinalIgnoreCase))
                errors.Add("National ID already linked to another customer. / الرقم القومي مرتبط بعميل آخر");

            var existing = !string.IsNullOrWhiteSpace(account) && existingAccounts.Contains(account);
            if (existing) errors.Add("Account already exists / الحساب موجود بالفعل");

            var status = errors.Count == 0 ? "Ready" : existing && errors.Count == 1 ? "Existing" : "Error";
            rows.Add(new BankCustomerImportRow(
                rows.Count + mapping.FirstDataRow,
                customerName,
                NullIfEmpty(mobile),
                NullIfEmpty(national),
                account,
                contract,
                outstanding,
                paid,
                remaining,
                status,
                errors));
        }

        var preview = new BankCustomerImportPreview(id, Guid.NewGuid(), portfolio.Id,
            ApiTextLocalizer.IsArabic ? portfolio.NameArabic : portfolio.NameEnglish, rows);
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new PreviewPayload(preview, mapping), Json));
        var stored = await storage.SaveAsync($"bank-customer-import-previews/{organizationId:N}", "preview.json", "application/json", content, MaximumBytes, token);
        try
        {
            await audit.WriteAsync(new AuditWriteRequest(
                "BankCustomerImportPreviewed", Entity, id.ToString(), null, null,
                new PreviewSaved(preview.PreviewId, portfolio.Id, stored.StorageKey, rows.Count),
                $"Customer import preview built with {rows.Count} row(s)."), token);
        }
        catch { await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }

        return preview with
        {
            Rows = preview.Rows.Select(row => row with { Errors = row.Errors.Select(message => ApiTextLocalizer.Localize(message)).ToArray() }).ToArray()
        };
    }

    public async Task<BankCustomerImportResult> ConfirmAsync(Guid organizationId, Guid id, Guid previewId, CancellationToken token)
    {
        EnsurePermission();
        await RequireOrganizationAsync(organizationId, token);
        await OwnedUpload(id, token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(id.ToByteArray(), 0)})", token);

        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "BankCustomerImportCompleted", token))
            throw new HrConflictException("This customer import is already complete.");

        var events = await db.HrAuditLogs.AsNoTracking()
            .Where(log => log.EntityType == Entity && log.EntityId == id && log.Action == "BankCustomerImportPreviewed")
            .OrderByDescending(log => log.Timestamp).ToArrayAsync(token);
        var saved = events.Select(log => Read<PreviewSaved>(log.NewValue)).FirstOrDefault();
        if (saved is null || saved.PreviewId != previewId)
            throw new HrValidationException("Build and review the customer preview before confirming.");

        await using var stream = await storage.OpenReadAsync(saved.StorageKey, token);
        var payload = await JsonSerializer.DeserializeAsync<PreviewPayload>(stream, Json, token)
            ?? throw new HrValidationException("The customer preview could not be read.");
        var mapping = payload.Mapping;
        var upload = Read<Uploaded>((await OwnedUpload(id, token)).NewValue);
        var portfolio = await db.CollectionPortfolios.Include(x => x.Organization)
            .SingleAsync(x => x.Id == saved.PortfolioId && x.OrganizationId == organizationId, token);
        var buckets = await db.CollectionBucketDefinitions
            .Where(x => x.OrganizationId == organizationId && x.IsActive && (x.PortfolioId == null || x.PortfolioId == portfolio.Id))
            .OrderByDescending(x => x.PortfolioId != null).ThenBy(x => x.SortOrder).ToArrayAsync(token);

        await using var source = await storage.OpenReadAsync(upload.StorageKey, token);
        var table = await AttendanceImportParser.ReadTableAsync(source, upload.Extension, mapping.SheetName, mapping.HeaderRow, mapping.FirstDataRow, token);
        var indexes = mapping.Columns.Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .ToDictionary(p => p.Key, p => Array.FindIndex(table.Headers, h => h == p.Value));
        if (indexes.Values.Any(i => i < 0)) throw new HrValidationException("A mapped source column was not found.");

        var customers = await db.CollectionCustomers.Where(x => x.OrganizationId == organizationId).ToDictionaryAsync(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase, token);
        var cases = await db.CollectionCases.Where(x => x.PortfolioId == portfolio.Id).ToDictionaryAsync(x => x.AccountReference, StringComparer.OrdinalIgnoreCase, token);
        var imported = 0; var skipped = 0; var failed = 0; var now = DateTimeOffset.UtcNow;

        foreach (var cells in table.Rows)
        {
            string V(string key) => indexes.TryGetValue(key, out var i) && i >= 0 && i < cells.Length ? cells[i].Trim() : "";
            var account = V("AccountNumber");
            if (string.IsNullOrWhiteSpace(account) || cases.ContainsKey(account)) { skipped++; continue; }

            try
            {
                var customerName = V("CustomerName");
                var customerCode = string.IsNullOrWhiteSpace(V("CustomerCode"))
                    ? BuildCustomerCode(V("NationalId"), V("MobileNumber"), customerName, account)
                    : V("CustomerCode");
                var outstanding = ParseMoney(V("OutstandingAmount")) ?? ParseMoney(V("RemainingAmount"))
                    ?? throw new HrValidationException("Outstanding required.");
                var overdue = ParseMoney(V("OverdueAmount")) ?? outstanding;
                var dpd = ParseInt(V("DaysPastDue")) ?? 0;
                var bucket = buckets.FirstOrDefault(x => x.MinimumDays.HasValue && dpd >= x.MinimumDays && (!x.MaximumDays.HasValue || dpd <= x.MaximumDays))
                    ?? buckets.OrderBy(x => x.SortOrder).FirstOrDefault()
                    ?? throw new HrConflictException("No delinquency bucket is configured for this organization.");

                if (!customers.TryGetValue(customerCode, out var customer))
                {
                    customer = new CollectionCustomer(organizationId, customerCode, customerName, customerName, now);
                    customers[customerCode] = customer;
                    db.CollectionCustomers.Add(customer);
                }
                customer.ApplyImportedContact(customerName, customerName, NullIfEmpty(DigitsOnly(V("NationalId"))), NormalizePhone(V("MobileNumber")));
                if (!string.IsNullOrWhiteSpace(V("Address")))
                    customer.UpdatePortfolioContact(customer.PrimaryPhone, customer.AlternatePhone, V("Address"), ApiTextLocalizer.IsArabic);

                var collectionCase = new CollectionCase(portfolio.Id, customer.Id,
                    BuildCaseNumber(portfolio.Organization.Code, portfolio.Code, account),
                    account, outstanding, outstanding, overdue, dpd, bucket.Id, now);
                collectionCase.ApplyImportedReferences(NullIfEmpty(V("ContractNumber")),
                    ClassifiedPortfolio.ProductTypeOrDefault(portfolio, NullIfEmpty(V("ProductType"))), now);
                cases[account] = collectionCase;
                db.CollectionCases.Add(collectionCase);
                db.CollectionCaseBucketHistory.Add(new CaseBucketHistory(collectionCase.Id, null, bucket.Id, "Customer import", CollectionsValues.AssignmentSources.Import, user.UserId, now));
                var priority = CollectionRules.CalculatePriority(collectionCase.OutstandingBalance, collectionCase.DaysPastDue, false, false, 999);
                collectionCase.SetPriority(priority.Score, string.Join(" + ", priority.Reasons), now);
                imported++;
            }
            catch
            {
                failed++;
            }
        }

        var result = new BankCustomerImportResult(imported, skipped, failed);
        await audit.WriteAsync(new AuditWriteRequest(
            "BankCustomerImportCompleted", Entity, id.ToString(), null, null, result,
            $"Customer import completed. Imported {imported}, skipped {skipped}, failed {failed}."), token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return result;
    }

    private sealed record PreviewPayload(BankCustomerImportPreview Preview, BankCustomerImportMapping Mapping);

    private async Task<CollectionPortfolio> ResolvePortfolioAsync(Guid organizationId, Guid? portfolioId, CancellationToken token)
    {
        if (classification.HasValue)
        {
            if (portfolioId.HasValue)
            {
                var requested = await db.CollectionPortfolios.Include(x => x.Organization)
                    .SingleOrDefaultAsync(x => x.Id == portfolioId && x.OrganizationId == organizationId && x.IsActive
                        && x.PrimaryClassification == classification.Primary && x.SubClassification == classification.Sub, token);
                if (requested is not null) return requested;
            }
            return await ClassifiedPortfolio.ResolveAsync(db, organizationId, classification.Primary!, classification.Sub!, token);
        }

        if (portfolioId.HasValue)
            return await db.CollectionPortfolios.Include(x => x.Organization)
                .SingleOrDefaultAsync(x => x.Id == portfolioId && x.OrganizationId == organizationId && x.IsActive, token)
                ?? throw new HrValidationException("A valid active portfolio is required.");

        var existing = await db.CollectionPortfolios.Include(x => x.Organization)
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(token);
        if (existing is not null) return existing;

        var org = await db.CollectionClientOrganizations.SingleAsync(x => x.Id == organizationId, token);
        var portfolio = new CollectionPortfolio(organizationId, "DEFAULT", "المحفظة الافتراضية", "Default Portfolio", "EGP", DateTimeOffset.UtcNow);
        db.CollectionPortfolios.Add(portfolio);
        await db.SaveChangesAsync(token);
        portfolio = await db.CollectionPortfolios.Include(x => x.Organization).SingleAsync(x => x.Id == portfolio.Id, token);
        _ = org;
        return portfolio;
    }

    private async Task RequireOrganizationAsync(Guid organizationId, CancellationToken token)
    {
        var ok = await db.CollectionClientOrganizations.AsNoTracking().AnyAsync(x =>
            x.Id == organizationId && x.IsActive &&
            (x.OrganizationType == CollectionsValues.OrganizationTypes.Bank || x.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance), token);
        if (!ok) throw new HrNotFoundException("Organization was not found.");
        if (!(user.Roles.Contains(SystemRoleNames.Admin, StringComparer.OrdinalIgnoreCase) ||
              user.Roles.Contains(SystemRoleNames.CollectionsOperationsManager, StringComparer.OrdinalIgnoreCase) ||
              await db.CollectionUserAccess.AnyAsync(a => a.UserId == user.UserId && a.OrganizationId == organizationId, token)))
            throw new HrForbiddenException("You do not have permission for this organization.");
    }

    private async Task<HrAuditLog> OwnedUpload(Guid id, CancellationToken token) =>
        await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log =>
            log.EntityType == Entity && log.EntityId == id && log.Action == "BankCustomerImportUploaded" && log.UserId == user.UserId, token)
        ?? throw new HrNotFoundException("Customer import was not found.");

    private void EnsurePermission()
    {
        if (!(user.Roles.Contains(SystemRoleNames.Admin, StringComparer.OrdinalIgnoreCase) ||
              user.Roles.Contains(SystemRoleNames.CollectionsOperationsManager, StringComparer.OrdinalIgnoreCase)))
            throw new HrForbiddenException("Only collections operations management can import customers.");
    }

    private static T Read<T>(string? value) => JsonSerializer.Deserialize<T>(value ?? "null", Json) ?? throw new HrValidationException("Import metadata is unavailable.");
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string DigitsOnly(string value) => new(Digits(value).Where(char.IsDigit).ToArray());
    private static string Digits(string value)
    {
        const string arabic = "٠١٢٣٤٥٦٧٨٩"; const string eastern = "۰۱۲۳۴۵۶۷۸۹";
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var index = arabic.IndexOf(c); if (index < 0) index = eastern.IndexOf(c);
            builder.Append(index >= 0 ? (char)('0' + index) : c);
        }
        return builder.ToString();
    }
    private static string NormalizePhone(string value)
    {
        var digits = DigitsOnly(value);
        return digits.StartsWith("20") && digits.Length == 12 ? "0" + digits[2..] : digits;
    }
    private static decimal? ParseMoney(string value)
    {
        value = Digits(value).Replace(",", string.Empty).Replace("ج.م", string.Empty).Trim();
        return decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result) ? result : null;
    }
    private static int? ParseInt(string value)
    {
        value = DigitsOnly(value);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }
    private static string BuildCustomerCode(string nationalId, string mobile, string name, string account)
    {
        if (!string.IsNullOrWhiteSpace(DigitsOnly(nationalId))) return DigitsOnly(nationalId);
        if (!string.IsNullOrWhiteSpace(NormalizePhone(mobile))) return NormalizePhone(mobile);
        var seed = $"{name}|{account}";
        return "C-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)))[..12];
    }
    private static string BuildCaseNumber(string organization, string portfolio, string account)
    {
        var safe = new string(account.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (safe.Length > 40) safe = safe[..24] + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account)))[..12];
        return $"{organization}-{portfolio}-{safe}"[..Math.Min(80, organization.Length + portfolio.Length + safe.Length + 2)];
    }
}
