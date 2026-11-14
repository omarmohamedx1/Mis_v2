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
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed partial class CollectionScoreService : ICollectionScoreService
{
    private static readonly string[] BucketCodes = ["BKT1", "BKT2", "BKT3", "BKT4", "BKT5", "BKT6"];
    private static readonly IReadOnlyDictionary<string, string[]> BucketCategories = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["BKT1"] = ["STAB", "NORM", "FWD"],
        ["BKT2"] = ["STAB", "BWD", "NORM", "FWD"],
        ["BKT3"] = ["STAB", "BWD", "NORM", "FWD"],
        ["BKT4"] = ["STAB", "BWD", "NORM", "FWD"],
        ["BKT5"] = ["STAB", "BWD", "NORM", "FWD"],
        ["BKT6"] = ["MORE"],
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserContext _user;
    private readonly IHrFileStorage _files;

    public CollectionScoreService(ApplicationDbContext db, ICurrentUserContext user, IHrFileStorage files)
    {
        _db = db;
        _user = user;
        _files = files;
    }

    public async Task<IReadOnlyCollection<CollectionScorePortfolioDto>> GetPortfoliosAsync(Guid organizationId, string portfolioType, CancellationToken token)
    {
        _ = await RequireOrganizationAsync(organizationId, token);
        var normalizedPortfolio = NormalizePortfolio(portfolioType);
        return await AccessiblePortfolios(organizationId, normalizedPortfolio)
            .OrderBy(item => item.NameEnglish)
            .Select(item => new CollectionScorePortfolioDto(
                item.Id,
                item.OrganizationId,
                item.Code,
                item.NameArabic,
                item.NameEnglish,
                item.CurrencyCode))
            .ToArrayAsync(token);
    }

    public async Task<CollectionScoreDashboardDto> GetDashboardAsync(Guid organizationId, string portfolioType, Guid portfolioId, string? reportingPeriod, CancellationToken token)
    {
        var organization = await RequireOrganizationAsync(organizationId, token);
        var normalizedPortfolio = NormalizePortfolio(portfolioType);
        var portfolio = await RequirePortfolioAsync(organizationId, normalizedPortfolio, portfolioId, token);
        var period = ParsePeriod(reportingPeriod);
        var storedValues = await _db.CollectionScoreValues.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId &&
                           item.OrganizationType == organization.OrganizationType &&
                           item.PortfolioType == normalizedPortfolio &&
                           item.PortfolioId == portfolioId &&
                           item.ReportingPeriod == period)
            .ToArrayAsync(token);
        var source = await LoadSourceAsync(organizationId, portfolioId, portfolio.SettingsJson, period, token);
        var buckets = BucketCodes.Select(code =>
        {
            var rows = storedValues.Where(item => item.Bucket == code).ToArray();
            var cells = BucketCategories[code].Select(category =>
            {
                var existing = rows.SingleOrDefault(item => item.Category == category);
                return existing is null ? EmptyCell(category) : MapCell(existing);
            }).ToArray();
            return CollectionScoreCalculator.Calculate(code, BucketVersion(rows), cells, source.Cases, source.Available)
                with { SystemCaseCount = source.SystemBuckets.GetValueOrDefault(int.Parse(code[3..], CultureInfo.InvariantCulture)) };
        }).ToArray();

        return new CollectionScoreDashboardDto(
            new CollectionScoreOrganizationDto(
                organization.Id,
                organization.Code,
                organization.NameArabic,
                organization.NameEnglish,
                organization.OrganizationType,
                string.IsNullOrWhiteSpace(organization.LogoStorageKey) ? null : CollectionsBrandingService.LogoUrl(organization.Id)),
            normalizedPortfolio,
            new CollectionScorePortfolioDto(portfolio.Id, portfolio.OrganizationId, portfolio.Code, portfolio.NameArabic, portfolio.NameEnglish, portfolio.CurrencyCode),
            FormatPeriod(period),
            CanEdit(),
            new CollectionScoreSummaryDto(
                source.Available && source.Cases.All(item => item.Dues.HasValue) ? source.Cases.Sum(item => item.Dues!.Value) : null,
                source.Available && source.Cases.All(item => item.Paid.HasValue) ? source.Cases.Sum(item => item.Paid!.Value) : null,
                null, source.SystemCount, source.Cases.Length,
                source.Cases.Count(item => item.Bucket is null || item.Movement is null && item.Bucket != 6),
                buckets.Where(item => item.Code != "BKT6").All(item => item.Score.HasValue)
                    ? buckets.Where(item => item.Code != "BKT6").Sum(item => item.Score!.Value) : null,
                source.SystemCount - source.SystemBuckets.Values.Sum()),
            buckets);
    }

    public async Task<CollectionScorePeriodsDto> GetPeriodsAsync(Guid organizationId, string portfolioType, Guid portfolioId, CancellationToken token)
    {
        _ = await RequireOrganizationAsync(organizationId, token);
        var normalizedPortfolio = NormalizePortfolio(portfolioType);
        var portfolio = await RequirePortfolioAsync(organizationId, normalizedPortfolio, portfolioId, token);
        var periods = await _db.CollectionScoreValues.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId &&
                           item.PortfolioType == normalizedPortfolio &&
                           item.PortfolioId == portfolioId)
            .Select(item => item.ReportingPeriod)
            .Distinct()
            .ToArrayAsync(token);
        var current = CairoPeriod();
        using var document = JsonDocument.Parse(portfolio.SettingsJson);
        var sourcePeriods = document.RootElement.TryGetProperty("scorePeriods", out var configured)
            ? configured.EnumerateObject().Select(item => item.Name)
                .Where(value => DateOnly.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                .Select(ParsePeriod).ToArray() : [];
        return new CollectionScorePeriodsDto(
            FormatPeriod(current),
            periods.Concat(sourcePeriods).Append(current).Distinct().OrderByDescending(period => period).Select(FormatPeriod).ToArray());
    }

    public async Task<CollectionScoreSummaryDto> GetSummaryAsync(Guid organizationId, string portfolioType, Guid portfolioId, string? reportingPeriod, CancellationToken token)
    {
        return (await GetDashboardAsync(organizationId, portfolioType, portfolioId, reportingPeriod, token)).Summary;
    }

    public async Task<IReadOnlyCollection<CollectionScoreAuditDto>> GetHistoryAsync(
        Guid organizationId, string portfolioType, Guid portfolioId, string reportingPeriod, string? bucket, int page, int pageSize, CancellationToken token)
    {
        _ = await RequireOrganizationAsync(organizationId, token);
        var normalizedPortfolio = NormalizePortfolio(portfolioType);
        _ = await RequirePortfolioAsync(organizationId, normalizedPortfolio, portfolioId, token);
        var period = ParsePeriod(reportingPeriod);
        if (page < 1 || pageSize is < 1 or > 200 || page > int.MaxValue / pageSize)
            throw new HrValidationException("History page must be positive and page size must be between 1 and 200.");
        var query = _db.CollectionScoreAudits.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId &&
                           item.PortfolioType == normalizedPortfolio &&
                           item.PortfolioId == portfolioId &&
                           item.ReportingPeriod == period);
        if (!string.IsNullOrWhiteSpace(bucket))
        {
            var normalizedBucket = NormalizeBucket(bucket);
            query = query.Where(item => item.Bucket == normalizedBucket);
        }

        var records = await query.OrderByDescending(item => item.ModifiedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new
            {
                item.Id,
                item.PortfolioType,
                item.PortfolioId,
                item.ReportingPeriod,
                item.Bucket,
                item.Category,
                item.BeforeJson,
                item.AfterJson,
                item.ModifiedByUser.FullName,
                item.ModifiedAt
            })
            .ToArrayAsync(token);
        return records.Select(item => new CollectionScoreAuditDto(
            item.Id,
            item.PortfolioType,
            item.PortfolioId,
            FormatPeriod(item.ReportingPeriod),
            item.Bucket,
            item.Category,
            item.BeforeJson,
            item.AfterJson,
            item.FullName,
            item.ModifiedAt)).ToArray();
    }

    public async Task<CollectionScoreBucketDto> SaveBucketAsync(
        Guid organizationId, string portfolioType, Guid portfolioId, string bucket, SaveCollectionScoreBucketRequest request, CancellationToken token)
    {
        EnsureCanEdit();
        var organization = await RequireOrganizationAsync(organizationId, token);
        var normalizedPortfolio = NormalizePortfolio(portfolioType);
        _ = await RequirePortfolioAsync(organizationId, normalizedPortfolio, portfolioId, token);
        var normalizedBucket = NormalizeBucket(bucket);
        var period = ParsePeriod(request.ReportingPeriod);
        ValidateRequest(normalizedBucket, request);

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token);
        var existingRows = await _db.CollectionScoreValues
            .Where(item => item.OrganizationId == organizationId &&
                           item.PortfolioType == normalizedPortfolio &&
                           item.PortfolioId == portfolioId &&
                           item.ReportingPeriod == period &&
                           item.Bucket == normalizedBucket)
            .ToArrayAsync(token);
        if (!string.Equals(BucketVersion(existingRows), request.ExpectedVersion, StringComparison.Ordinal))
            throw new HrConflictException("This BKT group changed after it was loaded. Reload it before saving your changes.");

        var modifiedAt = DateTimeOffset.UtcNow;
        var actorId = _user.UserId;
        foreach (var requested in request.Categories)
        {
            var category = NormalizeCategory(requested.Category);
            var row = existingRows.SingleOrDefault(item => item.Category == category);
            var before = row is null ? null : JsonSerializer.Serialize(MapCell(row), JsonOptions);
            if (row is null)
            {
                row = new CollectionScoreValue(
                    organizationId,
                    portfolioId,
                    organization.OrganizationType,
                    normalizedPortfolio,
                    period,
                    normalizedBucket,
                    category,
                    modifiedAt,
                    actorId);
                _db.CollectionScoreValues.Add(row);
            }

            row.Update(
                null,
                null,
                category == "STAB" ? null : requested.TargetPercent,
                null,
                null,
                null,
                category is "STAB" or "MORE" ? null : requested.Weight,
                null,
                null,
                modifiedAt,
                actorId);
            var after = JsonSerializer.Serialize(MapCell(row), JsonOptions);
            _db.CollectionScoreAudits.Add(new CollectionScoreAudit(
                row.Id,
                organizationId,
                portfolioId,
                organization.OrganizationType,
                normalizedPortfolio,
                period,
                normalizedBucket,
                category,
                actorId,
                modifiedAt,
                before,
                after));
        }

        await _db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return (await GetDashboardAsync(organizationId, normalizedPortfolio, portfolioId, FormatPeriod(period), token))
            .Buckets.Single(item => item.Code == normalizedBucket);
    }

    private async Task<ClientOrganization> RequireOrganizationAsync(Guid organizationId, CancellationToken token)
    {
        var organization = await AccessibleOrganizations()
            .SingleOrDefaultAsync(item => item.Id == organizationId && item.IsActive, token);
        if (organization is null)
            throw new HrNotFoundException("The organization was not found or is outside your access scope.");
        return organization;
    }

    private async Task<CollectionPortfolio> RequirePortfolioAsync(Guid organizationId, string portfolioType, Guid portfolioId, CancellationToken token) =>
        await AccessiblePortfolios(organizationId, portfolioType)
            .SingleOrDefaultAsync(item => item.Id == portfolioId, token)
        ?? throw new HrNotFoundException("The portfolio was not found or is outside your access scope.");

    private IQueryable<CollectionPortfolio> AccessiblePortfolios(Guid organizationId, string portfolioType)
    {
        var userId = _user.UserId;
        var global = HasRole(SystemRoleNames.Admin) ||
                     HasRole(SystemRoleNames.CollectionsOperationsManager) ||
                     HasRole(SystemRoleNames.CollectionsReviewer) ||
                     HasRole(SystemRoleNames.CollectionsAuditor);
        return _db.CollectionPortfolios.AsNoTracking().Where(portfolio =>
            portfolio.OrganizationId == organizationId &&
            portfolio.PrimaryClassification == portfolioType &&
            portfolio.IsActive &&
            (global ||
             _db.CollectionUserAccess.Any(access =>
                 access.UserId == userId &&
                 access.OrganizationId == organizationId &&
                 (access.PortfolioId == null || access.PortfolioId == portfolio.Id)) ||
             _db.CollectionCases.Any(collectionCase =>
                 collectionCase.PortfolioId == portfolio.Id &&
                 (collectionCase.AssignedCollectorId == userId ||
                  (collectionCase.AssignedTeamId != null && _db.CollectionTeamMembers.Any(member =>
                      member.TeamId == collectionCase.AssignedTeamId &&
                      member.UserId == userId &&
                      member.IsActive))))));
    }

    private IQueryable<ClientOrganization> AccessibleOrganizations()
    {
        var userId = _user.UserId;
        var global = HasRole(SystemRoleNames.Admin) ||
                     HasRole(SystemRoleNames.CollectionsOperationsManager) ||
                     HasRole(SystemRoleNames.CollectionsReviewer) ||
                     HasRole(SystemRoleNames.CollectionsAuditor);
        return _db.CollectionClientOrganizations.AsNoTracking().Where(organization =>
            (organization.OrganizationType == CollectionsValues.OrganizationTypes.Bank ||
             organization.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance) &&
            (global ||
             _db.CollectionUserAccess.Any(access => access.UserId == userId && access.OrganizationId == organization.Id) ||
             _db.CollectionCases.Any(collectionCase =>
                 collectionCase.Portfolio.OrganizationId == organization.Id &&
                 (collectionCase.AssignedCollectorId == userId ||
                  (collectionCase.AssignedTeamId != null && _db.CollectionTeamMembers.Any(member =>
                      member.TeamId == collectionCase.AssignedTeamId && member.UserId == userId && member.IsActive))))));
    }

    private bool CanEdit() =>
        HasRole(SystemRoleNames.Admin) ||
        HasRole(SystemRoleNames.CollectionsOperationsManager) ||
        HasPermission(SystemPermissionCodes.CollectionsConfigurationManage);

    private void EnsureCanEdit()
    {
        if (!CanEdit())
            throw new HrForbiddenException("You do not have permission to edit Score values.");
    }

    private bool HasRole(string role) => _user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    private bool HasPermission(string permission) =>
        _user.Permissions.Contains("*", StringComparer.OrdinalIgnoreCase) ||
        _user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static void ValidateRequest(string bucket, SaveCollectionScoreBucketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExpectedVersion) || request.ExpectedVersion.Length > 128)
            throw new HrValidationException("The BKT group version is invalid.");
        var expectedCategories = BucketCategories[bucket];
        if (request.Categories.Count != expectedCategories.Length)
            throw new HrValidationException("The request must include every category in this BKT group exactly once.");
        var categories = request.Categories.Select(cell => NormalizeCategory(cell.Category)).ToArray();
        if (categories.Distinct(StringComparer.Ordinal).Count() != categories.Length ||
            !expectedCategories.Order(StringComparer.Ordinal).SequenceEqual(categories.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new HrValidationException("The request categories do not match this BKT group.");

        foreach (var cell in request.Categories)
        {
            if (NormalizeCategory(cell.Category) != "STAB") ValidatePercentage(cell.TargetPercent, "% Target");
            if (cell.Weight < 0)
                throw new HrValidationException("Weight cannot be negative.");
            ValidatePrecision(cell.TargetPercent, 4, "% Target");
            ValidatePrecision(cell.Weight, 6, "Weight");
            EnsureDatabaseRange(cell.Weight, 12, "Weight");
        }
    }

    private static void ValidatePercentage(decimal? value, string label)
    {
        if (value is < 0 or > 100)
            throw new HrValidationException($"{label} must be between 0 and 100.");
    }

    private static void ValidatePrecision(decimal? value, int scale, string label)
    {
        if (value.HasValue && decimal.Round(value.Value, scale) != value.Value)
            throw new HrValidationException($"{label} supports at most {scale} decimal places.");
    }

    private static void EnsureDatabaseRange(decimal? value, int integerDigits, string label)
    {
        if (value.HasValue && Math.Abs(value.Value) >= (decimal)Math.Pow(10, integerDigits))
            throw new HrValidationException($"{label} is outside the supported numeric range.");
    }

    private static string NormalizePortfolio(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized is "ACT" or "WO" or "CORP"
            ? normalized
            : throw new HrValidationException("Portfolio type must be ACT, WO, or CORP.");
    }

    private static string NormalizeBucket(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return BucketCategories.ContainsKey(normalized)
            ? normalized
            : throw new HrValidationException("BKT must be between BKT1 and BKT6.");
    }

    private static string NormalizeCategory(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "STAB" or "NORM" or "FWD" or "BWD" or "MORE" => normalized,
            _ => throw new HrValidationException("The category is not supported."),
        };
    }

    private static DateOnly ParsePeriod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CairoPeriod();
        }
        if (!DateOnly.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var period))
            throw new HrValidationException("Reporting period must use the yyyy-MM format.");
        return new DateOnly(period.Year, period.Month, 1);
    }

    private static string FormatPeriod(DateOnly period) => period.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static DateOnly CairoPeriod()
    {
        var today = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Egypt Standard Time");
        return new DateOnly(today.Year, today.Month, 1);
    }

    private static CollectionScoreCellDto EmptyCell(string category) =>
        new(category, null, null, null, null, null, null, null, null, null);

    private static CollectionScoreCellDto MapCell(CollectionScoreValue item) =>
        new(item.Category, item.CaseCount, item.ActualBalance, item.TargetPercent, item.TargetBalance, item.Missing, item.Percent, item.Weight, item.ActualVsTarget, item.Score);

    private static string BucketVersion(IEnumerable<CollectionScoreValue> rows)
    {
        var content = string.Join('|', rows.OrderBy(item => item.Category, StringComparer.Ordinal)
            .Select(item => $"{item.Category}:{item.Version:N}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
