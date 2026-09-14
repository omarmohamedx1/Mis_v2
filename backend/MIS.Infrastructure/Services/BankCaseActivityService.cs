using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class BankCaseActivityService(ApplicationDbContext db, ICurrentUserContext user, ICollectionsClassificationContext classification) : IBankCaseActivityService
{
    private static readonly string[] Types = [CollectionsValues.ActivityTypes.Call, CollectionsValues.ActivityTypes.Sms,
        CollectionsValues.ActivityTypes.Email, CollectionsValues.ActivityTypes.Note, CollectionsValues.ActivityTypes.FollowUp];
    private static readonly string[] FilterTypes = [.. Types, CollectionsValues.ActivityTypes.PtpCreated, CollectionsValues.ActivityTypes.PtpKept, CollectionsValues.ActivityTypes.PtpBroken, CollectionsValues.ActivityTypes.PtpCancelled, CollectionsValues.ActivityTypes.Visit, CollectionsValues.ActivityTypes.Complaint, CollectionsValues.ActivityTypes.Payment];
    private static readonly string[] CallOutcomes = ["ANSWERED", "NO_ANSWER", "BUSY", "WRONG_NUMBER", "SWITCHED_OFF", "CALLBACK_REQUESTED", "REFUSED_TO_PAY", "OTHER"];
    private bool Has(string role) => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    private bool Global => Has(SystemRoleNames.Admin) || Has(SystemRoleNames.CollectionsOperationsManager);
    private bool Manager => Global || Has(SystemRoleNames.CollectionsSupervisor);
    private bool Collector => Has(SystemRoleNames.CollectionsCollector);
    private BankCaseActivityAccessDto Access() => new(Manager, Manager || Collector, Types, CallOutcomes, FilterTypes);

    public async Task<BankCaseActivitySummaryDto> SummaryAsync(Guid bankId, CancellationToken token)
    {
        await RequireBankAsync(bankId, token); var cases = ScopedCases(bankId).AsNoTracking(); var activities = ScopedActivities(bankId).AsNoTracking();
        var now = DateTimeOffset.UtcNow; var (start, end) = CairoDayRange(now);
        return new(await activities.CountAsync(x => x.CreatedAt >= start && x.CreatedAt < end, token),
            await cases.CountAsync(x => x.NextFollowUpAt >= start && x.NextFollowUpAt < end, token),
            await cases.CountAsync(x => x.NextFollowUpAt < now, token),
            await activities.Where(x => x.CreatedAt >= start && x.CreatedAt < end && (x.ActivityType == CollectionsValues.ActivityTypes.Call || x.ActivityType == CollectionsValues.ActivityTypes.Sms || x.ActivityType == CollectionsValues.ActivityTypes.Email)).Select(x => x.CaseId).Distinct().CountAsync(token));
    }

    public async Task<BankCaseActivityPageDto> GetAsync(Guid bankId, BankCaseActivityQuery request, CancellationToken token)
    {
        await RequireBankAsync(bankId, token); ValidatePage(request.Page, request.PageSize); var query = Filter(ScopedActivities(bankId).AsNoTracking(), request);
        var followUp = NormalizeFollowUp(request.FollowUpState); var now = DateTimeOffset.UtcNow; var (start, end) = CairoDayRange(now);
        if (followUp != "ALL")
        {
            query = query.Where(x => db.CollectionActivities.Where(a => a.CaseId == x.CaseId).OrderByDescending(a => a.CreatedAt).Select(a => a.Id).First() == x.Id);
            query = followUp switch { "TODAY" => query.Where(x => x.Case.NextFollowUpAt >= start && x.Case.NextFollowUpAt < end), "UPCOMING" => query.Where(x => x.Case.NextFollowUpAt >= end), "OVERDUE" => query.Where(x => x.Case.NextFollowUpAt < now), "NONE" => query.Where(x => x.Case.NextFollowUpAt == null), _ => query };
        }
        var total = await query.CountAsync(token); var items = await Project(query.OrderByDescending(x => x.CreatedAt).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)).ToArrayAsync(token);
        return new(items, total, request.Page, request.PageSize, total == 0 ? 0 : (int)Math.Ceiling(total / (double)request.PageSize), Access());
    }

    public async Task<BankCaseActivityDetailsDto> GetDetailsAsync(Guid bankId, Guid activityId, CancellationToken token)
    {
        await RequireBankAsync(bankId, token); var ar = ApiTextLocalizer.IsArabic;
        var item = await ScopedActivities(bankId).AsNoTracking().Where(x => x.Id == activityId).Select(x => new {
            x.Id, x.CaseId, x.Case.CaseNumber, Customer = ar ? x.Case.Customer.FullNameArabic ?? x.Case.Customer.FullNameEnglish! : x.Case.Customer.FullNameEnglish ?? x.Case.Customer.FullNameArabic!,
            x.Case.Customer.PrimaryPhone, x.Case.OutstandingBalance, CaseStatus = x.Case.Status, Bank = ar ? x.Case.Portfolio.Organization.NameArabic : x.Case.Portfolio.Organization.NameEnglish,
            x.Case.AssignedCollectorId, Assigned = x.Case.AssignedCollector == null ? null : x.Case.AssignedCollector.FullName,
            x.ActivityType, Outcome = x.Result, x.Notes, ActivityAt = x.CreatedAt, ActivityFollowUp = x.NextFollowUpAt,
            PerformedById = x.CreatedById, PerformedBy = x.CreatedBy.FullName, x.CreatedAt
        }).SingleOrDefaultAsync(token) ?? throw new HrNotFoundException("Activity was not found for this bank or user scope.");
        return new(item.Id, item.CaseId, item.CaseNumber, item.Customer, item.PrimaryPhone, item.OutstandingBalance, item.CaseStatus,
            item.Bank, item.AssignedCollectorId, item.Assigned, item.ActivityType, item.Outcome, item.Notes, item.ActivityAt,
            item.ActivityFollowUp, item.PerformedById, item.PerformedBy, item.CreatedAt, await TimelineCore(item.CaseId, bankId, token), Access());
    }

    public async Task<IReadOnlyCollection<BankCaseActivityItemDto>> TimelineAsync(Guid bankId, Guid caseId, CancellationToken token)
    { await RequireBankAsync(bankId, token); if (!await ScopedCases(bankId).AnyAsync(x => x.Id == caseId, token)) throw new HrNotFoundException("Portfolio case was not found for this bank or user scope."); return await TimelineCore(caseId, bankId, token); }

    public async Task<IReadOnlyCollection<BankActivityCaseLookupDto>> CasesAsync(Guid bankId, string? search, CancellationToken token)
    {
        await RequireBankAsync(bankId, token); var query = ScopedCases(bankId).AsNoTracking(); if (!string.IsNullOrWhiteSpace(search)) { if (search.Length > 160) throw new HrValidationException("Search cannot exceed 160 characters."); var term = search.Trim().ToLower(); query = query.Where(x => x.CaseNumber.ToLower().Contains(term) || (x.Customer.FullNameArabic != null && x.Customer.FullNameArabic.ToLower().Contains(term)) || (x.Customer.FullNameEnglish != null && x.Customer.FullNameEnglish.ToLower().Contains(term)) || (x.Customer.PrimaryPhone != null && x.Customer.PrimaryPhone.Contains(term))); }
        var ar = ApiTextLocalizer.IsArabic; return await query.OrderBy(x => x.CaseNumber).Take(30).Select(x => new BankActivityCaseLookupDto(x.Id, x.CaseNumber,
            ar ? x.Customer.FullNameArabic ?? x.Customer.FullNameEnglish! : x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic!, x.Customer.PrimaryPhone,
            x.OutstandingBalance, x.Status, x.AssignedCollectorId, x.AssignedCollector == null ? null : x.AssignedCollector.FullName)).ToArrayAsync(token);
    }

    public async Task<IReadOnlyCollection<BankPortfolioCollectorDto>> CollectorsAsync(Guid bankId, CancellationToken token)
    {
        await RequireBankAsync(bankId, token); if (!Manager) return [];
        return await AuthorizedCollectors().AsNoTracking().OrderBy(x => x.FullName).Select(x => new BankPortfolioCollectorDto(x.Id, x.FullName)).ToArrayAsync(token);
    }

    public async Task<BankCaseActivityDetailsDto> CreateAsync(Guid bankId, CreateBankCaseActivityRequest request, CancellationToken token)
    {
        if (!Manager && !Collector) throw new HrForbiddenException("You do not have permission to create case activity."); await RequireBankAsync(bankId, token);
        var item = await ScopedCases(bankId).SingleOrDefaultAsync(x => x.Id == request.CaseId, token) ?? throw new HrNotFoundException("Portfolio case was not found for this bank or user scope.");
        var type = NormalizeType(request.ActivityType); var outcome = NormalizeCode(request.Outcome); var notes = Clean(request.Notes);
        if (notes?.Length > 4000) throw new HrValidationException("Notes cannot exceed 4000 characters.");
        if (outcome?.Length > 100) throw new HrValidationException("Outcome cannot exceed 100 characters.");
        if (type == CollectionsValues.ActivityTypes.Call && (outcome == null || !CallOutcomes.Contains(outcome))) throw new HrValidationException("Select a valid call outcome.");
        if (request.NextFollowUpAt.HasValue && request.NextFollowUpAt <= DateTimeOffset.UtcNow) throw new HrValidationException("Next follow-up must be in the future.");
        var now = DateTimeOffset.UtcNow; var activity = new CollectionActivity(item.Id, type, outcome, notes, TypeChannel(type), user.UserId, now, request.NextFollowUpAt);
        if (type is CollectionsValues.ActivityTypes.Call or CollectionsValues.ActivityTypes.Sms or CollectionsValues.ActivityTypes.Email) item.RecordContact(now, request.NextFollowUpAt);
        else if (type == CollectionsValues.ActivityTypes.FollowUp || request.NextFollowUpAt.HasValue) item.ScheduleNextFollowUp(request.NextFollowUpAt, now);
        db.CollectionActivities.Add(activity); db.CollectionAuditLogs.Add(new CollectionAuditLog(user.UserId, "ActivityCreated", nameof(CollectionActivity), activity.Id, item.Id, null, JsonSerializer.Serialize(new { item.Id, Type = type, Outcome = outcome, Notes = notes, request.NextFollowUpAt }), "WEB", now));
        await db.SaveChangesAsync(token); return await GetDetailsAsync(bankId, activity.Id, token);
    }

    private IQueryable<CollectionCase> ScopedCases(Guid bankId) => ScopedCasesCore(bankId).Apply(classification);
    private IQueryable<CollectionCase> ScopedCasesCore(Guid bankId) { var q = db.CollectionCases.Where(x => x.Portfolio.OrganizationId == bankId && !x.IsArchived); if (Global) return q; if (Collector) return q.Where(x => x.AssignedCollectorId == user.UserId); if (Manager) return q.Where(x => (x.AssignedTeam != null && x.AssignedTeam.SupervisorId == user.UserId) || (x.AssignedTeamId == null && db.CollectionUserAccess.Any(a => a.UserId == user.UserId && a.OrganizationId == bankId && (a.PortfolioId == null || a.PortfolioId == x.PortfolioId)))); return q.Where(_ => false); }
    private IQueryable<CollectionActivity> ScopedActivities(Guid bankId) { var cases = ScopedCases(bankId); return db.CollectionActivities.Where(x => cases.Any(c => c.Id == x.CaseId)); }
    private IQueryable<User> AuthorizedCollectors() { var q = db.Users.Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == SystemRoleNames.CollectionsCollector)); return Global ? q : q.Where(x => db.CollectionTeamMembers.Any(m => m.UserId == x.Id && m.IsActive && m.Team.IsActive && m.Team.SupervisorId == user.UserId)); }
    private async Task RequireBankAsync(Guid bankId, CancellationToken token) { var exists = await db.CollectionClientOrganizations.AsNoTracking().AnyAsync(x => x.Id == bankId && x.IsActive && (x.OrganizationType == CollectionsValues.OrganizationTypes.Bank || x.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance), token); if (!exists || (!Global && !await db.CollectionUserAccess.AnyAsync(x => x.UserId == user.UserId && x.OrganizationId == bankId, token) && !await ScopedCases(bankId).AnyAsync(token))) throw new HrNotFoundException("Organization was not found or is outside your authorized scope."); }
    private IQueryable<BankCaseActivityItemDto> Project(IQueryable<CollectionActivity> query) { var ar = ApiTextLocalizer.IsArabic; var access = Access(); return query.Select(x => new BankCaseActivityItemDto(x.Id, x.CaseId, x.Case.CaseNumber, ar ? x.Case.Customer.FullNameArabic ?? x.Case.Customer.FullNameEnglish! : x.Case.Customer.FullNameEnglish ?? x.Case.Customer.FullNameArabic!, x.ActivityType, x.Result, x.Notes, x.CreatedAt, x.Case.NextFollowUpAt, x.CreatedById, x.CreatedBy.FullName, access)); }
    private Task<BankCaseActivityItemDto[]> TimelineCore(Guid caseId, Guid bankId, CancellationToken token) { var ar = ApiTextLocalizer.IsArabic; var access = Access(); return ScopedActivities(bankId).AsNoTracking().Where(x => x.CaseId == caseId).OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new BankCaseActivityItemDto(x.Id, x.CaseId, x.Case.CaseNumber, ar ? x.Case.Customer.FullNameArabic ?? x.Case.Customer.FullNameEnglish! : x.Case.Customer.FullNameEnglish ?? x.Case.Customer.FullNameArabic!, x.ActivityType, x.Result, x.Notes, x.CreatedAt, x.NextFollowUpAt, x.CreatedById, x.CreatedBy.FullName, access)).ToArrayAsync(token); }
    private static IQueryable<CollectionActivity> Filter(IQueryable<CollectionActivity> q, BankCaseActivityQuery r) { if (!string.IsNullOrWhiteSpace(r.Search)) { if (r.Search.Length > 160) throw new HrValidationException("Search cannot exceed 160 characters."); var t = r.Search.Trim().ToLower(); q = q.Where(x => x.Case.CaseNumber.ToLower().Contains(t) || (x.Case.Customer.FullNameArabic != null && x.Case.Customer.FullNameArabic.ToLower().Contains(t)) || (x.Case.Customer.FullNameEnglish != null && x.Case.Customer.FullNameEnglish.ToLower().Contains(t)) || (x.Case.Customer.PrimaryPhone != null && x.Case.Customer.PrimaryPhone.Contains(t)) || (x.Notes != null && x.Notes.ToLower().Contains(t))); } if (!string.IsNullOrWhiteSpace(r.ActivityType)) q = q.Where(x => x.ActivityType == NormalizeFilterType(r.ActivityType)); if (!string.IsNullOrWhiteSpace(r.Outcome)) { var o = NormalizeCode(r.Outcome); q = q.Where(x => x.Result == o); } if (r.Date.HasValue) { var (s,e)=CairoDayRange(r.Date.Value); q=q.Where(x=>x.CreatedAt>=s&&x.CreatedAt<e); } if (r.CollectorId.HasValue) q=q.Where(x=>x.CreatedById==r.CollectorId); if(r.CaseId.HasValue) q=q.Where(x=>x.CaseId==r.CaseId); return q; }
    private static string NormalizeType(string value) { var type = value.Trim().ToUpperInvariant(); if (!Types.Contains(type)) throw new HrValidationException("Activity type is invalid."); return type; }
    private static string NormalizeFilterType(string value) { var type = value.Trim().ToUpperInvariant(); if (!FilterTypes.Contains(type)) throw new HrValidationException("Activity type is invalid."); return type; }
    private static string NormalizeFollowUp(string? value) => string.IsNullOrWhiteSpace(value) ? "ALL" : value.Trim().ToUpperInvariant() switch { "ALL" => "ALL", "TODAY" => "TODAY", "UPCOMING" => "UPCOMING", "OVERDUE" => "OVERDUE", "NONE" => "NONE", _ => throw new HrValidationException("Follow-up filter is invalid.") };
    private static string? NormalizeCode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string TypeChannel(string type) => type switch { CollectionsValues.ActivityTypes.Call => "PHONE", CollectionsValues.ActivityTypes.Sms => "SMS", CollectionsValues.ActivityTypes.Email => "EMAIL", _ => "INTERNAL" };
    private static void ValidatePage(int page, int size) { if (page < 1 || size is not (20 or 50 or 100)) throw new HrValidationException("Page size must be 20, 50, or 100."); }
    private static (DateTimeOffset Start, DateTimeOffset End) CairoDayRange(DateTimeOffset now) { var zone=TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); return CairoDayRange(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,zone).DateTime)); }
    private static (DateTimeOffset Start, DateTimeOffset End) CairoDayRange(DateOnly date) { var zone=TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); var start=new DateTime(date.Year,date.Month,date.Day,0,0,0,DateTimeKind.Unspecified); return (new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(start,zone)),new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(start.AddDays(1),zone))); }
}
