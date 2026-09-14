using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class BankCaseDistributionService(ApplicationDbContext db, ICurrentUserContext user, ICollectionsClassificationContext classification) : IBankCaseDistributionService
{
    private static readonly string[] Statuses = [CollectionsValues.CaseStatuses.Active, CollectionsValues.CaseStatuses.OnHold,
        CollectionsValues.CaseStatuses.Settled, CollectionsValues.CaseStatuses.Closed, CollectionsValues.CaseStatuses.Legal, CollectionsValues.CaseStatuses.WriteOff];
    private bool Has(string role) => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    private bool Global => Has(SystemRoleNames.Admin) || Has(SystemRoleNames.CollectionsOperationsManager);
    private bool Manager => Global || Has(SystemRoleNames.CollectionsSupervisor);

    public async Task<CaseDistributionSummaryDto> SummaryAsync(Guid bankId, CancellationToken token)
    {
        await RequireAccessAsync(bankId, token); var cases = ScopedCases(bankId).AsNoTracking();
        var total = await cases.CountAsync(token); var assigned = await cases.CountAsync(x => x.AssignedCollectorId != null, token);
        return new(total, total - assigned, assigned, await AuthorizedCollectors(bankId).CountAsync(token));
    }

    public async Task<CaseDistributionPageDto> CasesAsync(Guid bankId, bool assigned, CaseDistributionQuery request, CancellationToken token)
    {
        await RequireAccessAsync(bankId, token); ValidatePage(request.Page, request.PageSize);
        var query = Filter(ScopedCases(bankId).AsNoTracking().Where(x => assigned ? x.AssignedCollectorId != null : x.AssignedCollectorId == null), request);
        var total = await query.CountAsync(token); query = Sort(query, request.SortBy, request.SortDirection);
        var ar = ApiTextLocalizer.IsArabic;
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(x => new CaseDistributionItemDto(
            x.Id, x.CaseNumber, ar ? x.Customer.FullNameArabic ?? x.Customer.FullNameEnglish! : x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic!,
            x.Customer.PrimaryPhone, x.OutstandingBalance, x.Status, x.AssignedCollectorId,
            x.AssignedCollector == null ? null : x.AssignedCollector.FullName,
            db.CollectionAssignmentHistory.Where(h => h.CaseId == x.Id && h.AssignedToId == x.AssignedCollectorId).Max(h => (DateTimeOffset?)h.AssignedAt),
            x.SourceImportId, x.SourceImport == null ? null : x.SourceImport.OriginalFileName)).ToArrayAsync(token);
        return new(items, total, request.Page, request.PageSize, total == 0 ? 0 : (int)Math.Ceiling(total / (double)request.PageSize));
    }

    public async Task<IReadOnlyCollection<DistributionCollectorDto>> CollectorsAsync(Guid bankId, CancellationToken token)
    {
        await RequireAccessAsync(bankId, token); var scoped = ScopedCases(bankId);
        return await AuthorizedCollectors(bankId).AsNoTracking().OrderBy(x => x.FullName).Select(x => new DistributionCollectorDto(
            x.Id, x.FullName, scoped.Count(c => c.AssignedCollectorId == x.Id),
            scoped.Where(c => c.AssignedCollectorId == x.Id).Sum(c => (decimal?)c.OutstandingBalance) ?? 0)).ToArrayAsync(token);
    }

    public async Task<IReadOnlyCollection<DistributionImportDto>> ImportsAsync(Guid bankId, CancellationToken token)
    {
        await RequireAccessAsync(bankId, token); var ids = ScopedCases(bankId).Where(x => x.SourceImportId != null).Select(x => x.SourceImportId!.Value);
        return await db.BankPortfolioImports.AsNoTracking().Where(x => x.BankId == bankId && ids.Contains(x.Id)).OrderByDescending(x => x.UploadedAt)
            .Select(x => new DistributionImportDto(x.Id, x.OriginalFileName)).ToArrayAsync(token);
    }

    public Task<DistributionPreviewDto> PreviewAsync(Guid bankId, bool reassign, DistributionMutationRequest request, CancellationToken token) =>
        BuildPreviewAsync(bankId, request, reassign ? AssignmentState.Assigned : AssignmentState.Unassigned, true, token);

    public async Task<DistributionResultDto> AssignAsync(Guid bankId, bool reassign, DistributionMutationRequest request, CancellationToken token)
    {
        EnsureReason(request.Reason); if (!request.CollectorId.HasValue) throw new HrValidationException("Select a collector.");
        await RequireAccessAsync(bankId, token); var ids = Ids(request.CaseIds); var collector = await GetCollectorAsync(bankId, request.CollectorId.Value, token);
        try { await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var cases = await ScopedCases(bankId).Where(x => ids.Contains(x.Id)).ToArrayAsync(token);
            if (cases.Length != ids.Length || cases.Any(x => reassign ? x.AssignedCollectorId == null : x.AssignedCollectorId != null)) throw new HrConflictException("Some selected cases are no longer available for assignment. Refresh and try again.");
            var teamId = await TeamIdAsync(collector.Id, token); var now = DateTimeOffset.UtcNow;
            foreach (var item in cases) Apply(item, collector.Id, teamId, request.Reason, CollectionsValues.AssignmentSources.Manual, null, now);
            await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return new(ids.Length, collector.Id, collector.FullName);
        } catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure) { throw Stale(); } catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }) { throw Stale(); }
    }

    public Task<DistributionPreviewDto> PreviewUnassignAsync(Guid bankId, DistributionMutationRequest request, CancellationToken token) =>
        BuildPreviewAsync(bankId, request, AssignmentState.Assigned, false, token);

    public async Task<DistributionResultDto> UnassignAsync(Guid bankId, DistributionMutationRequest request, CancellationToken token)
    {
        EnsureReason(request.Reason); await RequireAccessAsync(bankId, token); var ids = Ids(request.CaseIds);
        try { await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var cases = await ScopedCases(bankId).Where(x => ids.Contains(x.Id)).ToArrayAsync(token);
            if (cases.Length != ids.Length || cases.Any(x => x.AssignedCollectorId == null)) throw new HrConflictException("Some selected cases are no longer assigned. Refresh and try again.");
            var now = DateTimeOffset.UtcNow;
            foreach (var item in cases) { var previous = item.AssignedCollectorId; item.Unassign(now); db.CollectionAssignmentHistory.Add(new(item.Id, previous, null, user.UserId, null, request.Reason, "UNASSIGNED", null, now)); Audit("PortfolioCaseUnassigned", item.Id, previous, null, request.Reason); }
            await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return new(ids.Length, null, null);
        } catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure) { throw Stale(); } catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }) { throw Stale(); }
    }

    public async Task<AutoDistributionPreviewDto> PreviewAutoAsync(Guid bankId, AutoDistributionRequest request, CancellationToken token)
    {
        var plan = await BuildAutoPlanAsync(bankId, request, token); return Summarize(request.Method, plan);
    }

    public async Task<DistributionResultDto> ConfirmAutoAsync(Guid bankId, AutoDistributionRequest request, CancellationToken token)
    {
        EnsureReason(request.Reason); await RequireAccessAsync(bankId, token);
        try { await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var plan = await BuildAutoPlanAsync(bankId, request, token); var now = DateTimeOffset.UtcNow; var collectorIds = plan.Select(x => x.Collector.Id).Distinct().ToArray();
            var teamIds = Global ? collectorIds.ToDictionary(x => x, _ => (Guid?)null) : await db.CollectionTeamMembers.Where(x => collectorIds.Contains(x.UserId) && x.IsActive && x.Team.IsActive && x.Team.SupervisorId == user.UserId).GroupBy(x => x.UserId).ToDictionaryAsync(x => x.Key, x => (Guid?)x.Min(m => m.TeamId), token);
            foreach (var row in plan) Apply(row.Case, row.Collector.Id, teamIds.GetValueOrDefault(row.Collector.Id), request.Reason, "AUTO", NormalizeMethod(request.Method), now);
            await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return new(plan.Count, null, null);
        } catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure) { throw Stale(); } catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }) { throw Stale(); }
    }

    private async Task<List<PlanRow>> BuildAutoPlanAsync(Guid bankId, AutoDistributionRequest request, CancellationToken token)
    {
        EnsureReason(request.Reason); await RequireAccessAsync(bankId, token); var ids = Ids(request.CaseIds);
        var collectorIds = request.CollectorIds.Where(x => x != Guid.Empty).Distinct().ToArray(); if (collectorIds.Length is < 1 or > 100) throw new HrValidationException("Select between 1 and 100 collectors.");
        var collectors = await AuthorizedCollectors(bankId).Where(x => collectorIds.Contains(x.Id)).OrderBy(x => x.FullName).ToArrayAsync(token);
        if (collectors.Length != collectorIds.Length) throw new HrForbiddenException("One or more selected collectors are outside your authorized scope.");
        var cases = await ScopedCases(bankId).Where(x => ids.Contains(x.Id) && x.AssignedCollectorId == null).OrderByDescending(x => x.OutstandingBalance).ThenBy(x => x.Id).ToArrayAsync(token);
        if (cases.Length != ids.Length) throw new HrConflictException("Some selected cases are no longer available for assignment. Refresh and try again.");
        var method = NormalizeMethod(request.Method); var rows = new List<PlanRow>(cases.Length); var counts = collectors.ToDictionary(x => x.Id, _ => 0); var totals = collectors.ToDictionary(x => x.Id, _ => 0m);
        IEnumerable<CollectionCase> orderedCases = method == "EQUAL_COUNT" ? cases.OrderBy(x => x.Id) : cases;
        foreach (var item in orderedCases)
        {
            var target = method == "EQUAL_COUNT" ? collectors.OrderBy(x => counts[x.Id]).ThenBy(x => x.FullName).First() : collectors.OrderBy(x => totals[x.Id]).ThenBy(x => counts[x.Id]).ThenBy(x => x.FullName).First();
            rows.Add(new(item, target)); counts[target.Id]++; totals[target.Id] += item.OutstandingBalance;
        }
        return rows;
    }

    private static AutoDistributionPreviewDto Summarize(string method, List<PlanRow> rows) => new(NormalizeMethod(method), rows.Count, rows.Sum(x => x.Case.OutstandingBalance), rows.GroupBy(x => x.Collector).OrderBy(x => x.Key.FullName).Select(x => new AutoDistributionCollectorDto(x.Key.Id, x.Key.FullName, x.Count(), x.Sum(y => y.Case.OutstandingBalance))).ToArray());
    private async Task<DistributionPreviewDto> BuildPreviewAsync(Guid bankId, DistributionMutationRequest request, AssignmentState state, bool requireCollector, CancellationToken token)
    {
        EnsureReason(request.Reason); await RequireAccessAsync(bankId, token); var ids = Ids(request.CaseIds); User? collector = null;
        if (requireCollector) collector = request.CollectorId.HasValue ? await GetCollectorAsync(bankId, request.CollectorId.Value, token) : throw new HrValidationException("Select a collector.");
        var query = ScopedCases(bankId).Where(x => ids.Contains(x.Id)); var rows = await query.Select(x => new { x.Id, x.OutstandingBalance, x.AssignedCollectorId, Name = x.AssignedCollector == null ? null : x.AssignedCollector.FullName }).ToArrayAsync(token);
        if (rows.Length != ids.Length || rows.Any(x => state == AssignmentState.Assigned ? x.AssignedCollectorId == null : x.AssignedCollectorId != null)) throw new HrConflictException("Some selected cases changed assignment state. Refresh and try again.");
        return new(rows.Length, rows.Sum(x => x.OutstandingBalance), collector?.Id, collector?.FullName, rows.Select(x => x.Name).Where(x => x != null).Distinct().Cast<string>().ToArray());
    }

    private void Apply(CollectionCase item, Guid collectorId, Guid? teamId, string reason, string source, string? rule, DateTimeOffset now)
    { var previous = item.AssignedCollectorId; item.Assign(collectorId, teamId, now); db.CollectionAssignmentHistory.Add(new(item.Id, previous, collectorId, user.UserId, teamId, reason, source, rule, now)); Audit(previous.HasValue ? "PortfolioCaseReassigned" : "PortfolioCaseAssigned", item.Id, previous, collectorId, reason); }
    private void Audit(string action, Guid caseId, Guid? before, Guid? after, string reason) => db.CollectionAuditLogs.Add(new(user.UserId, action, nameof(CollectionCase), caseId, caseId, JsonSerializer.Serialize(new { AssignedCollectorId = before }), JsonSerializer.Serialize(new { AssignedCollectorId = after, Reason = reason }), "WEB", DateTimeOffset.UtcNow));
    private IQueryable<CollectionCase> ScopedCases(Guid bankId) => ScopedCasesCore(bankId).Apply(classification);
    private IQueryable<CollectionCase> ScopedCasesCore(Guid bankId) { var q = db.CollectionCases.Where(x => x.Portfolio.OrganizationId == bankId && !x.IsArchived); if (Global) return q; return q.Where(x => (x.AssignedTeam != null && x.AssignedTeam.SupervisorId == user.UserId) || (x.AssignedTeamId == null && db.CollectionUserAccess.Any(a => a.UserId == user.UserId && a.OrganizationId == bankId && (a.PortfolioId == null || a.PortfolioId == x.PortfolioId)))); }
    private IQueryable<User> AuthorizedCollectors(Guid bankId) { var q = db.Users.Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == SystemRoleNames.CollectionsCollector)); return Global ? q : q.Where(x => db.CollectionTeamMembers.Any(m => m.UserId == x.Id && m.IsActive && m.Team.IsActive && m.Team.SupervisorId == user.UserId)); }
    private async Task RequireAccessAsync(Guid bankId, CancellationToken token) { if (!Manager) throw new HrForbiddenException("Case Distribution is available only to authorized Collections managers."); var bank = await db.CollectionClientOrganizations.AsNoTracking().AnyAsync(x => x.Id == bankId && x.IsActive && (x.OrganizationType == CollectionsValues.OrganizationTypes.Bank || x.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance), token); if (!bank || (!Global && !await db.CollectionUserAccess.AnyAsync(x => x.UserId == user.UserId && x.OrganizationId == bankId, token) && !await ScopedCases(bankId).AnyAsync(token))) throw new HrNotFoundException("Organization was not found or is outside your authorized scope."); }
    private async Task<User> GetCollectorAsync(Guid bankId, Guid id, CancellationToken token) => await AuthorizedCollectors(bankId).SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrForbiddenException("The selected collector is outside your authorized scope.");
    private async Task<Guid?> TeamIdAsync(Guid collectorId, CancellationToken token) => Global ? null : await db.CollectionTeamMembers.Where(x => x.UserId == collectorId && x.IsActive && x.Team.IsActive && x.Team.SupervisorId == user.UserId).Select(x => (Guid?)x.TeamId).FirstOrDefaultAsync(token);
    private static Guid[] Ids(IReadOnlyCollection<Guid> values) { var ids = values.Where(x => x != Guid.Empty).Distinct().ToArray(); if (ids.Length is < 1 or > 500) throw new HrValidationException("Select between 1 and 500 cases."); return ids; }
    private static void EnsureReason(string reason) { if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 2 or > 500) throw new HrValidationException("Reason must contain between 2 and 500 characters."); }
    private static HrConflictException Stale() => new("Some selected cases changed while this operation was being completed. Refresh and try again.");
    private static string NormalizeMethod(string value) => value.Trim().ToUpperInvariant() switch { "EQUAL_COUNT" => "EQUAL_COUNT", "BALANCED_AMOUNT" => "BALANCED_AMOUNT", _ => throw new HrValidationException("Distribution method is invalid.") };
    private static void ValidatePage(int page, int size) { if (page < 1 || size is not (20 or 50 or 100)) throw new HrValidationException("Page size must be 20, 50, or 100."); }
    private static IQueryable<CollectionCase> Filter(IQueryable<CollectionCase> q, CaseDistributionQuery r) { if (!string.IsNullOrWhiteSpace(r.Search)) { if (r.Search.Length > 160) throw new HrValidationException("Search cannot exceed 160 characters."); var t = r.Search.Trim().ToLower(); q = q.Where(x => x.CaseNumber.ToLower().Contains(t) || x.Customer.CustomerCode.ToLower().Contains(t) || (x.Customer.FullNameArabic != null && x.Customer.FullNameArabic.ToLower().Contains(t)) || (x.Customer.FullNameEnglish != null && x.Customer.FullNameEnglish.ToLower().Contains(t)) || (x.Customer.PrimaryPhone != null && x.Customer.PrimaryPhone.Contains(t)) || (x.Customer.NationalId != null && x.Customer.NationalId.Contains(t))); } if (!string.IsNullOrWhiteSpace(r.Status)) { var s = r.Status.Trim().ToUpperInvariant(); if (!Statuses.Contains(s)) throw new HrValidationException("Case status is invalid."); q = q.Where(x => x.Status == s); } if (r.CollectorId.HasValue) q = q.Where(x => x.AssignedCollectorId == r.CollectorId); if (r.ImportId.HasValue) q = q.Where(x => x.SourceImportId == r.ImportId); return q; }
    private static IQueryable<CollectionCase> Sort(IQueryable<CollectionCase> q, string? field, string? direction) { var d = direction?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true; return field?.Trim().ToLowerInvariant() switch { "customer" => d ? q.OrderByDescending(x => x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic) : q.OrderBy(x => x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic), "outstanding" => d ? q.OrderByDescending(x => x.OutstandingBalance) : q.OrderBy(x => x.OutstandingBalance), "status" => d ? q.OrderByDescending(x => x.Status) : q.OrderBy(x => x.Status), "assigned" => d ? q.OrderByDescending(x => x.UpdatedAt) : q.OrderBy(x => x.UpdatedAt), _ => q.OrderBy(x => x.CaseNumber) }; }
    private enum AssignmentState { Assigned, Unassigned }
    private sealed record PlanRow(CollectionCase Case, User Collector);
}
