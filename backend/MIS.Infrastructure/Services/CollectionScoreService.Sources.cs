using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Domain.Constants;

namespace MIS.Infrastructure.Services;

public sealed partial class CollectionScoreService
{
    private static readonly Dictionary<string, string> DefaultFields = new()
    {
        ["openingBucket"] = "Current BKT", ["openingBalance"] = "Current Balance", ["startingDues"] = "StartingTotal Dues",
        ["payment"] = "Payment", ["dues0"] = "BKT0", ["dues1"] = "BKT1", ["dues2"] = "BKT2",
        ["dues3"] = "BKT3", ["dues4"] = "BKT4", ["dues5"] = "BKT5"
    };
    private static string SourceVersion(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    private static string Fingerprint(IEnumerable<(Guid Id, string? Raw)> rows) => SourceVersion(string.Join('|',
        rows.OrderBy(item => item.Id).Select(item => $"{item.Id:N}:{item.Raw}")));

    private sealed record OpeningRow(Guid Id, IReadOnlyDictionary<string, string> Values);
    private async Task<OpeningRow[]?> ReadOpeningAsync(Guid organizationId, Guid portfolioId, Guid importId, CancellationToken token)
    {
        var import = await _db.BankPortfolioImports.AsNoTracking().SingleOrDefaultAsync(item => item.Id == importId &&
            item.BankId == organizationId && item.PortfolioId == portfolioId && item.Status == "COMPLETED" && !item.IsArchived, token);
        if (import is null) return null;
        IReadOnlyCollection<ParsedCollectionRow> parsed;
        try
        {
            await using var stream = await _files.OpenReadAsync(import.StorageKey, token);
            parsed = await CollectionImportParser.ParseAsync(stream, Path.GetExtension(import.OriginalFileName).ToLowerInvariant(), token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { return null; }
        var cases = await _db.CollectionCases.AsNoTracking().Where(item => item.PortfolioId == portfolioId &&
                item.Portfolio.OrganizationId == organizationId && !item.IsArchived)
            .Select(item => new { item.Id, item.AccountReference }).ToArrayAsync(token);
        var byAccount = cases.GroupBy(item => item.AccountReference, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<Guid, OpeningRow>();
        foreach (var row in parsed)
        {
            var account = CollectionFileRowMapper.Read(row).Account;
            if (string.IsNullOrWhiteSpace(account) || !byAccount.TryGetValue(account, out var matches) || matches.Length != 1) return null;
            var item = new OpeningRow(matches[0].Id, row.Values);
            if (result.TryGetValue(item.Id, out var duplicate))
            {
                // Identical duplicate accounts count once; conflicting opening rows must be reconciled.
                if (CollectionFileRowMapper.Serialize(duplicate.Values) != CollectionFileRowMapper.Serialize(item.Values)) return null;
            }
            else result[item.Id] = item;
        }
        return result.Values.ToArray();
    }

    private static string OpeningFingerprint(IEnumerable<OpeningRow> rows) =>
        Fingerprint(rows.Select(item => (item.Id, (string?)CollectionFileRowMapper.Serialize(item.Values))));

    public async Task<CollectionScoreSourceDto> GetSourceAsync(Guid organizationId, string portfolioType, Guid portfolioId,
        string reportingPeriod, CancellationToken token)
    {
        _ = await RequireOrganizationAsync(organizationId, token);
        var portfolio = await RequirePortfolioAsync(organizationId, NormalizePortfolio(portfolioType), portfolioId, token);
        var period = ParsePeriod(reportingPeriod);
        var imports = await _db.BankPortfolioImports.AsNoTracking()
            .Where(item => item.BankId == organizationId && item.PortfolioId == portfolioId && item.Status == "COMPLETED" && !item.IsArchived)
            .OrderByDescending(item => item.UploadedAt)
            .Select(item => new CollectionScoreSourceImportDto(item.Id, item.OriginalFileName, item.ImportedRows + item.UpdatedRows))
            .ToArrayAsync(token);
        var json = JsonNode.Parse(portfolio.SettingsJson);
        var source = json?["scorePeriods"]?[FormatPeriod(period)];
        return new(SourceVersion(portfolio.SettingsJson), imports, source?["openingImportId"]?.GetValue<Guid>(),
            source?["paymentSource"]?.GetValue<string>() ?? "SYSTEM",
            source?["fields"]?.Deserialize<Dictionary<string, string>>() ?? DefaultFields);
    }

    public async Task SaveSourceAsync(Guid organizationId, string portfolioType, Guid portfolioId,
        SaveCollectionScoreSourceRequest request, CancellationToken token)
    {
        EnsureCanEdit();
        _ = await RequireOrganizationAsync(organizationId, token);
        _ = await RequirePortfolioAsync(organizationId, NormalizePortfolio(portfolioType), portfolioId, token);
        var period = ParsePeriod(request.ReportingPeriod);
        if (!request.CompleteOpeningPopulation || !request.PaymentsReconciled || request.PaymentSource is not ("SYSTEM" or "IMPORT"))
            throw new HrValidationException("Declare a complete opening population and reconciled period payment source.");
        if (request.Fields.Count != DefaultFields.Count || request.Fields.Any(item => !DefaultFields.ContainsKey(item.Key) ||
                string.IsNullOrWhiteSpace(item.Value) || item.Value.Length > 200))
            throw new HrValidationException("Supply the opening bucket, balance, starting dues, payment and six dues-band column names.");
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token);
        var portfolio = await _db.CollectionPortfolios.SingleAsync(item => item.Id == portfolioId, token);
        if (SourceVersion(portfolio.SettingsJson) != request.ExpectedVersion)
            throw new HrConflictException("Source settings changed. Reload before saving.");
        if (!await _db.BankPortfolioImports.AnyAsync(item => item.Id == request.OpeningImportId && item.BankId == organizationId &&
                item.PortfolioId == portfolioId && item.Status == "COMPLETED" && !item.IsArchived, token))
            throw new HrValidationException("Choose a completed import belonging to this bank and portfolio.");
        var rows = await ReadOpeningAsync(organizationId, portfolioId, request.OpeningImportId, token);
        if (rows is null || rows.Length == 0) throw new HrValidationException("The opening file is unavailable or its accounts do not match unique system cases in this portfolio.");
        var settings = JsonNode.Parse(portfolio.SettingsJson)!.AsObject();
        var periods = settings["scorePeriods"] as JsonObject ?? new JsonObject();
        if (settings["scorePeriods"] is null) settings["scorePeriods"] = periods;
        periods[FormatPeriod(period)] = JsonSerializer.SerializeToNode(new {
            openingImportId = request.OpeningImportId, completeOpeningPopulation = true, paymentsReconciled = true,
            paymentSource = request.PaymentSource, fields = request.Fields,
            fingerprint = OpeningFingerprint(rows),
            configuredBy = _user.UserId, configuredAt = DateTimeOffset.UtcNow
        });
        portfolio.Update(portfolio.NameArabic, portfolio.NameEnglish, portfolio.CurrencyCode, portfolio.TargetAmount, settings.ToJsonString(), portfolio.IsActive);
        await _db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    private sealed record SourceData(CollectionScoreCalculator.CaseInput[] Cases, bool Available,
        int SystemCount, Dictionary<int, int> SystemBuckets);

    private async Task<SourceData> LoadSourceAsync(Guid organizationId, Guid portfolioId, string settingsJson,
        DateOnly period, CancellationToken token)
    {
        var systemCases = await _db.CollectionCases.AsNoTracking()
            .Where(item => item.PortfolioId == portfolioId && item.Portfolio.OrganizationId == organizationId && !item.IsArchived)
            .Select(item => new { item.ImportBucketLabel, item.CurrentBucket.Code })
            .ToArrayAsync(token);
        var systemBuckets = systemCases.Select(item => CollectionScoreCalculator.ParseBucket(item.ImportBucketLabel)
                ?? CollectionScoreCalculator.ParseBucket(item.Code))
            .Where(item => item is >= 1 and <= 6).GroupBy(item => item!.Value).ToDictionary(group => group.Key, group => group.Count());
        var unavailable = new SourceData([], false, systemCases.Length, systemBuckets);
        using var document = JsonDocument.Parse(settingsJson);
        if (!document.RootElement.TryGetProperty("scorePeriods", out var periods) ||
            !periods.TryGetProperty(FormatPeriod(period), out var source) ||
            !source.TryGetProperty("completeOpeningPopulation", out var complete) || complete.ValueKind != JsonValueKind.True ||
            !source.TryGetProperty("openingImportId", out var importValue) || !importValue.TryGetGuid(out var importId))
            return unavailable;
        // Import identity, bank and classification/portfolio are all checked; upload date never defines the score month.
        if (!await _db.BankPortfolioImports.AsNoTracking().AnyAsync(item => item.Id == importId &&
                item.BankId == organizationId && item.PortfolioId == portfolioId && item.Status == "COMPLETED" && !item.IsArchived, token))
            return unavailable;
        var cohort = await ReadOpeningAsync(organizationId, portfolioId, importId, token);
        if (cohort is null || cohort.Length == 0) return unavailable;
        if (!source.TryGetProperty("fingerprint", out var fingerprint) ||
            fingerprint.GetString() != OpeningFingerprint(cohort)) return unavailable;
        var caseIds = cohort.Select(item => item.Id).ToArray();
        var reconciled = source.TryGetProperty("paymentsReconciled", out var reconciliation) && reconciliation.ValueKind == JsonValueKind.True;
        var paymentSource = source.TryGetProperty("paymentSource", out var paymentSourceValue) ? paymentSourceValue.GetString() : null;
        var payments = new Dictionary<Guid, decimal>();
        if (reconciled && paymentSource == "SYSTEM")
        {
            payments = await _db.CollectionPayments.AsNoTracking().Where(item =>
                    item.Case.PortfolioId == portfolioId && item.Case.Portfolio.OrganizationId == organizationId &&
                    caseIds.Contains(item.CaseId) && item.Status == CollectionsValues.PaymentStatuses.Approved &&
                    item.FinancialReversalJournalEntryId == null && item.PaymentDate >= period && item.PaymentDate < period.AddMonths(1))
                .GroupBy(item => item.CaseId).Select(group => new { Id = group.Key, Amount = group.Sum(item => item.Amount) })
                .ToDictionaryAsync(item => item.Id, item => item.Amount, token);
        }
        string Field(string key, string fallback) => source.TryGetProperty("fields", out var fields) &&
            fields.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : fallback;
        var inputs = cohort.Select(item =>
        {
            var values = item.Values;
            string Read(string key, string fallback) => CollectionFileRowMapper.SnapshotValue(values, Field(key, fallback));
            decimal? Money(string value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount >= 0 ? amount : null;
            var bucket = CollectionScoreCalculator.ParseBucket(Read("openingBucket", "Current BKT"));
            if (bucket == 0) bucket = null;
            var balance = Money(Read("openingBalance", "Current Balance"));
            var dues = Money(Read("startingDues", "StartingTotal Dues"));
            decimal? paid = !reconciled ? null : paymentSource == "SYSTEM" ? payments.GetValueOrDefault(item.Id)
                : paymentSource == "IMPORT" ? Money(Read("payment", "Payment")) : null;
            var bands = Enumerable.Range(0, 6).Select(band => Money(Read($"dues{band}", $"BKT{band}"))).ToArray();
            string? movement = bucket.HasValue && paid.HasValue && bands.All(band => band.HasValue)
                ? CollectionScoreCalculator.Movement(bucket.Value, CollectionScoreCalculator.EndingBucket(bucket.Value, paid.Value, bands.Select(band => band!.Value).ToArray()))
                : null;
            return new CollectionScoreCalculator.CaseInput(bucket, balance, movement, dues, paid);
        }).ToArray();
        return new(inputs, true, systemCases.Length, systemBuckets);
    }
}
