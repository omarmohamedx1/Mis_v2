using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Application.Scoring;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using Npgsql;

namespace MIS.Infrastructure.Services;

public sealed partial class CollectionScoreService : IBankTargetsService
{
    private bool CanApproveTargets() => HasRole(SystemRoleNames.Admin)
        || HasRole(SystemRoleNames.CollectionsOperationsManager) || HasRole(SystemRoleNames.CollectionsReviewer);

    private async Task<DateOnly> RequireTargetScope(Guid org, string type, Guid portfolio, string period, CancellationToken token)
    {
        if (type is not ("ACT" or "WO")) throw new HrValidationException("TARGETS_TYPE_INVALID");
        if (string.IsNullOrWhiteSpace(period) || !DateOnly.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var month) || month.Year is < 1900 or > 9998)
            throw new HrValidationException("TARGETS_PERIOD_INVALID");
        await RequireOrganizationAsync(org, token);
        await RequirePortfolioAsync(org, type, portfolio, token);
        return month;
    }
    private IQueryable<BankTargetConfiguration> TargetQuery(Guid org, string type, Guid portfolio, DateOnly month) =>
        _db.BankTargetConfigurations.Where(x => x.OrganizationId == org && x.PortfolioId == portfolio
            && x.PortfolioType == type && x.ReportingPeriod == month);

    public async Task<BankTargetsDto> GetTargetsAsync(Guid organizationId, string portfolioType, Guid portfolioId, string period, CancellationToken token)
    {
        var month = await RequireTargetScope(organizationId, portfolioType, portfolioId, period, token);
        var row = await TargetQuery(organizationId, portfolioType, portfolioId, month).AsNoTracking().SingleOrDefaultAsync(token);
        var history = row is null ? [] : await _db.BankTargetAudits.AsNoTracking().Where(x => x.ConfigurationId == row.Id)
            .OrderByDescending(x => x.OccurredAt).Take(100).ToArrayAsync(token);
        return new(row is null ? null : MapTarget(row), CanEdit(), CanApproveTargets(),
            await TargetScores(row, token), history.Select(x => new BankTargetAuditDto(x.Id, x.Action, x.Status,
                x.ActorId, x.OccurredAt, x.Version, ReadTargets(x.ValuesJson))).ToArray());
    }

    public async Task<BankTargetsDto> SaveTargetsAsync(Guid organizationId, string portfolioType, Guid portfolioId,
        string period, SaveBankTargetsRequest request, CancellationToken token)
    {
        EnsureCanEdit();
        var month = await RequireTargetScope(organizationId, portfolioType, portfolioId, period, token);
        BankTargetPolicy.Validate(request.Values, false);
        var row = await TargetQuery(organizationId, portfolioType, portfolioId, month).SingleOrDefaultAsync(token);
        var now = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(request.Values, JsonOptions);
        var action = "EDIT";
        if (row is null)
        {
            if (request.ExpectedVersion is not null) throw new HrConflictException("TARGETS_STALE");
            row = new(organizationId, portfolioId, portfolioType, month, json, _user.UserId, now);
            _db.BankTargetConfigurations.Add(row); action = "CREATE";
        }
        else
        {
            RequireTargetVersion(row, request.ExpectedVersion);
            row.Edit(json, _user.UserId, now);
        }
        await PersistTarget(row, action, now, token);
        return await GetTargetsAsync(organizationId, portfolioType, portfolioId, period, token);
    }

    public async Task<BankTargetsDto> CopyTargetsAsync(Guid organizationId, string portfolioType, Guid portfolioId,
        string period, CancellationToken token)
    {
        EnsureCanEdit();
        var month = await RequireTargetScope(organizationId, portfolioType, portfolioId, period, token);
        if (await TargetQuery(organizationId, portfolioType, portfolioId, month).AnyAsync(token))
            throw new HrConflictException("TARGETS_ALREADY_EXISTS");
        var previous = await TargetQuery(organizationId, portfolioType, portfolioId, month.AddMonths(-1))
            .AsNoTracking().SingleOrDefaultAsync(x => x.Status == "APPROVED", token)
            ?? throw new HrNotFoundException("TARGETS_PREVIOUS_NOT_APPROVED");
        var now = DateTimeOffset.UtcNow;
        var row = new BankTargetConfiguration(organizationId, portfolioId, portfolioType, month,
            previous.ValuesJson, _user.UserId, now, previous.Id);
        _db.BankTargetConfigurations.Add(row);
        await PersistTarget(row, "COPY", now, token);
        return await GetTargetsAsync(organizationId, portfolioType, portfolioId, period, token);
    }

    public async Task<BankTargetsDto> ReviewTargetsAsync(Guid organizationId, string portfolioType, Guid portfolioId,
        string period, BankTargetVersionRequest request, bool approve, CancellationToken token)
    {
        if (!CanApproveTargets()) throw new HrForbiddenException("TARGETS_REVIEW_FORBIDDEN");
        var month = await RequireTargetScope(organizationId, portfolioType, portfolioId, period, token);
        var row = await TargetQuery(organizationId, portfolioType, portfolioId, month).SingleOrDefaultAsync(token)
            ?? throw new HrNotFoundException("TARGETS_MISSING");
        RequireTargetVersion(row, request.ExpectedVersion);
        BankTargetPolicy.Validate(ReadTargets(row.ValuesJson), true);
        var now = DateTimeOffset.UtcNow;
        if (approve)
        {
            if (row.Status != "REVIEWED") throw new HrConflictException("TARGETS_REVIEW_REQUIRED");
            row.Approve(_user.UserId, now);
        }
        else row.Review(_user.UserId, now);
        await PersistTarget(row, approve ? "APPROVE" : "REVIEW", now, token);
        return await GetTargetsAsync(organizationId, portfolioType, portfolioId, period, token);
    }

    private static void RequireTargetVersion(BankTargetConfiguration row, Guid? version)
    {
        if (row.Status == "APPROVED") throw new HrConflictException("TARGETS_IMMUTABLE");
        if (version is null || version != row.Version) throw new HrConflictException("TARGETS_STALE");
    }
    private async Task PersistTarget(BankTargetConfiguration row, string action, DateTimeOffset now, CancellationToken token)
    {
        _db.BankTargetAudits.Add(new(row, action, _user.UserId, now));
        try { await _db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw new HrConflictException("TARGETS_STALE"); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new HrConflictException("TARGETS_ALREADY_EXISTS"); }
    }
    private static BankTargetValues ReadTargets(string json) => JsonSerializer.Deserialize<BankTargetValues>(json, SourceJsonOptions)
        ?? throw new HrValidationException("TARGETS_VALUES_REQUIRED");
    private static BankTargetConfigurationDto MapTarget(BankTargetConfiguration x) => new(x.Id, x.Status, x.Version,
        ReadTargets(x.ValuesJson), x.CreatedBy, x.CreatedAt, x.UpdatedBy, x.UpdatedAt, x.ReviewedBy, x.ReviewedAt,
        x.ApprovedBy, x.ApprovedAt, x.CopiedFromId);

    private async Task<BankTargetScoresDto> TargetScores(BankTargetConfiguration? target, CancellationToken token)
    {
        static BankTargetScoresDto Missing(string issue) => new(null, null, "UNAVAILABLE", [issue]);
        if (target is null) return Missing("TARGETS_MISSING");
        if (target.Status != "APPROVED") return Missing("TARGETS_NOT_APPROVED");
        if (target.PortfolioType != "ACT") return Missing("WO_RULES_UNCONFIRMED");
        if (!_actEnabled) return Missing("ACT_NOT_ENABLED");
        if (await _db.CollectionScoreValues.AsNoTracking().AnyAsync(x => x.OrganizationId == target.OrganizationId
            && x.PortfolioId == target.PortfolioId && x.PortfolioType == target.PortfolioType
            && x.ReportingPeriod == target.ReportingPeriod, token)) return Missing("MANUAL_PRESERVED");
        var source = await _db.ActScorePeriods.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == target.OrganizationId
            && x.PortfolioId == target.PortfolioId && x.ReportingPeriod == target.ReportingPeriod, token);
        if (source is null) return Missing("SOURCE_MISSING");
        try
        {
            var rules = JsonSerializer.Deserialize<ActScoreRules>(source.RulesJson, SourceJsonOptions);
            if (rules is null || !BankTargetPolicy.Matches(ReadTargets(target.ValuesJson), rules)) return Missing("TARGETS_SNAPSHOT_MISMATCH");
            var portfolio = await RequirePortfolioAsync(target.OrganizationId, target.PortfolioType, target.PortfolioId, token);
            var result = await CalculateAsync(target.OrganizationId, target.PortfolioId, target.PortfolioType,
                target.ReportingPeriod, portfolio.CurrencyCode, token);
            return new(result.BalanceScore, result.MoreScore, result.Status, result.Issues);
        }
        catch (JsonException) { return Missing("SOURCE_INVALID"); }
    }
}
