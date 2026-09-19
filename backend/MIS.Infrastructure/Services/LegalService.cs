using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Legal;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class LegalService(ApplicationDbContext db, ICurrentUserContext user) : ILegalService
{
    public async Task<LegalDashboardDto> GetDashboardAsync(CancellationToken token)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekEnd = today.AddDays(7);
        var monthStart = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var legalCases = OpenLegalCases();
        var openCases = await legalCases.CountAsync(token);
        var outstanding = await legalCases.SumAsync(x => (decimal?)x.OutstandingBalance, token) ?? 0;
        var intake = await legalCases.CountAsync(x => !db.LegalCaseFiles.Any(f => f.CollectionCaseId == x.Id)
            || db.LegalCaseFiles.Any(f => f.CollectionCaseId == x.Id && f.Stage == LegalValues.Stages.Intake), token);
        var hearings = await db.LegalCaseFiles.AsNoTracking()
            .CountAsync(x => x.CollectionCase.Status == CollectionsValues.CaseStatuses.Legal && !x.CollectionCase.IsArchived
                && x.NextHearingOn >= today && x.NextHearingOn <= weekEnd, token);
        var returned = await db.LegalCaseActions.AsNoTracking()
            .CountAsync(x => x.ActionType == LegalValues.Actions.Return && x.CreatedAt >= monthStart, token);

        return new LegalDashboardDto(openCases, intake, hearings, returned, outstanding, CanManage());
    }

    public async Task<IReadOnlyList<LegalOrganizationDto>> ListOrganizationsAsync(CancellationToken token)
    {
        var ar = ApiTextLocalizer.IsArabic;
        return await db.CollectionClientOrganizations.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => ar ? x.NameArabic : x.NameEnglish)
            .Select(x => new LegalOrganizationDto(x.Id, x.Code, ar ? x.NameArabic : x.NameEnglish))
            .ToArrayAsync(token);
    }

    public async Task<LegalCasePageDto> ListCasesAsync(string? search, Guid? organizationId, string? stage, int page, int pageSize, CancellationToken token)
    {
        if (page < 1) page = 1;
        if (pageSize is not (20 or 50 or 100)) pageSize = 20;
        var ar = ApiTextLocalizer.IsArabic;
        var query = OpenLegalCases();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            if (term.Length > 160) throw new HrValidationException("Search cannot exceed 160 characters.");
            query = query.Where(x => x.CaseNumber.ToLower().Contains(term)
                || x.Customer.CustomerCode.ToLower().Contains(term)
                || (x.Customer.FullNameArabic != null && x.Customer.FullNameArabic.ToLower().Contains(term))
                || (x.Customer.FullNameEnglish != null && x.Customer.FullNameEnglish.ToLower().Contains(term)));
        }
        if (organizationId.HasValue && organizationId != Guid.Empty)
            query = query.Where(x => x.Portfolio.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(stage))
        {
            var normalized = stage.Trim().ToUpperInvariant();
            if (!LegalValues.Stages.IsValid(normalized)) throw new HrValidationException("Legal stage is invalid.");
            query = normalized == LegalValues.Stages.Intake
                ? query.Where(x => !db.LegalCaseFiles.Any(f => f.CollectionCaseId == x.Id)
                    || db.LegalCaseFiles.Any(f => f.CollectionCaseId == x.Id && f.Stage == LegalValues.Stages.Intake))
                : query.Where(x => db.LegalCaseFiles.Any(f => f.CollectionCaseId == x.Id && f.Stage == normalized));
        }

        var total = await query.CountAsync(token);
        var items = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LegalCaseListItemDto(
                x.Id,
                db.LegalCaseFiles.Where(f => f.CollectionCaseId == x.Id).Select(f => (Guid?)f.Id).FirstOrDefault(),
                x.CaseNumber,
                ar ? x.Customer.FullNameArabic ?? x.Customer.FullNameEnglish ?? x.Customer.CustomerCode
                    : x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic ?? x.Customer.CustomerCode,
                ar ? x.Portfolio.Organization.NameArabic : x.Portfolio.Organization.NameEnglish,
                ar ? x.Portfolio.NameArabic : x.Portfolio.NameEnglish,
                x.OutstandingBalance,
                x.DaysPastDue,
                x.Status,
                db.LegalCaseFiles.Where(f => f.CollectionCaseId == x.Id).Select(f => f.Stage).FirstOrDefault() ?? LegalValues.Stages.Intake,
                db.LegalCaseFiles.Where(f => f.CollectionCaseId == x.Id).Select(f => f.NextHearingOn).FirstOrDefault(),
                x.UpdatedAt))
            .ToArrayAsync(token);

        return new LegalCasePageDto(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<LegalCaseDetailsDto> GetCaseAsync(Guid collectionCaseId, CancellationToken token)
    {
        var item = await TrackedCase(collectionCaseId, token);
        var file = await EnsureFileAsync(item, token);
        return await MapDetailsAsync(item, file, token);
    }

    public async Task<LegalCaseDetailsDto> SaveFileAsync(Guid collectionCaseId, SaveLegalFileRequest request, CancellationToken token)
    {
        RequireManage();
        var item = await TrackedCase(collectionCaseId, token);
        ValidateText(request.CourtName, 160, "Court name");
        ValidateText(request.CourtCaseNumber, 80, "Court case number");
        ValidateText(request.LawyerName, 160, "Lawyer name");
        ValidateText(request.Notes, 2000, "Notes");
        var file = await EnsureFileAsync(item, token);
        var now = DateTimeOffset.UtcNow;
        var before = new { file.CourtName, file.CourtCaseNumber, file.LawyerName, file.NextHearingOn, file.Notes };
        file.UpdateFile(request.CourtName, request.CourtCaseNumber, request.LawyerName, request.NextHearingOn, request.Notes, now);
        if (!string.IsNullOrWhiteSpace(request.CourtName) && file.Stage == LegalValues.Stages.Intake)
            file.SetStage(LegalValues.Stages.Court, now);
        Audit("LegalFileUpdated", item.Id, before, request);
        await db.SaveChangesAsync(token);
        return await MapDetailsAsync(item, file, token);
    }

    public async Task<LegalCaseDetailsDto> RecordActionAsync(Guid collectionCaseId, RecordLegalActionRequest request, CancellationToken token)
    {
        RequireManage();
        if (!LegalValues.Actions.IsValid(request.ActionType)) throw new HrValidationException("Legal action is invalid.");
        if (string.IsNullOrWhiteSpace(request.Notes) || request.Notes.Trim().Length is < 2 or > 2000)
            throw new HrValidationException("Action notes must contain between 2 and 2000 characters.");
        ValidateText(request.Result, 120, "Result");

        var item = await TrackedCase(collectionCaseId, token);
        var file = await EnsureFileAsync(item, token);
        var now = DateTimeOffset.UtcNow;
        var actionType = request.ActionType.Trim().ToUpperInvariant();
        var action = new LegalCaseAction(file.Id, actionType, request.Result, request.Notes, request.HappenedOn, user.UserId, now);
        db.LegalCaseActions.Add(action);

        var stage = LegalValues.StageForAction(actionType);
        if (actionType != LegalValues.Actions.Note)
            file.SetStage(stage, now);
        if (actionType == LegalValues.Actions.Hearing && request.HappenedOn.HasValue
            && request.HappenedOn.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            file.UpdateFile(file.CourtName, file.CourtCaseNumber, file.LawyerName, request.HappenedOn, file.Notes, now);

        var collectionStatus = LegalValues.CollectionStatusForAction(actionType);
        if (collectionStatus is not null)
        {
            if (item.Status != CollectionsValues.CaseStatuses.Legal)
                throw new HrValidationException("This case is no longer in the legal queue.");
            item.UpdatePortfolioCase(collectionStatus, item.NextFollowUpAt, now);
        }

        db.CollectionActivities.Add(new CollectionActivity(item.Id, CollectionsValues.ActivityTypes.Legal, actionType, request.Notes.Trim(), "LEGAL", user.UserId, now, null));
        Audit("LegalActionRecorded", item.Id, new { item.Status, file.Stage }, new { actionType, request.Notes, collectionStatus });
        await db.SaveChangesAsync(token);
        return await MapDetailsAsync(item, file, token);
    }

    private IQueryable<CollectionCase> OpenLegalCases() =>
        db.CollectionCases.Where(x => !x.IsArchived && x.Status == CollectionsValues.CaseStatuses.Legal);

    private async Task<CollectionCase> TrackedCase(Guid collectionCaseId, CancellationToken token) =>
        await db.CollectionCases
            .Include(x => x.Customer)
            .Include(x => x.Portfolio).ThenInclude(x => x.Organization)
            .SingleOrDefaultAsync(x => x.Id == collectionCaseId && !x.IsArchived
                && (x.Status == CollectionsValues.CaseStatuses.Legal || db.LegalCaseFiles.Any(f => f.CollectionCaseId == x.Id)), token)
        ?? throw new HrNotFoundException("Legal case was not found.");

    private async Task<LegalCaseFile> EnsureFileAsync(CollectionCase item, CancellationToken token)
    {
        var file = await db.LegalCaseFiles.Include(x => x.ReceivedByUser).SingleOrDefaultAsync(x => x.CollectionCaseId == item.Id, token);
        if (file is not null) return file;
        file = new LegalCaseFile(item.Id, user.UserId, DateTimeOffset.UtcNow);
        db.LegalCaseFiles.Add(file);
        db.CollectionActivities.Add(new CollectionActivity(item.Id, CollectionsValues.ActivityTypes.Legal, "OPENED", "Legal file opened.", "LEGAL", user.UserId, DateTimeOffset.UtcNow, null));
        await db.SaveChangesAsync(token);
        return await db.LegalCaseFiles.Include(x => x.ReceivedByUser).SingleAsync(x => x.Id == file.Id, token);
    }

    private async Task<LegalCaseDetailsDto> MapDetailsAsync(CollectionCase item, LegalCaseFile file, CancellationToken token)
    {
        var ar = ApiTextLocalizer.IsArabic;
        var actions = await db.LegalCaseActions.AsNoTracking().Include(x => x.CreatedByUser)
            .Where(x => x.FileId == file.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new LegalCaseActionDto(x.Id, x.ActionType, x.Result, x.Notes, x.HappenedOn, x.CreatedByUser.FullName, x.CreatedAt))
            .ToArrayAsync(token);
        var customer = ar ? item.Customer.FullNameArabic ?? item.Customer.FullNameEnglish ?? item.Customer.CustomerCode
            : item.Customer.FullNameEnglish ?? item.Customer.FullNameArabic ?? item.Customer.CustomerCode;
        return new LegalCaseDetailsDto(
            item.Id,
            file.Id,
            item.CaseNumber,
            item.AccountReference,
            customer,
            item.Customer.NationalId,
            item.Customer.PrimaryPhone,
            ar ? item.Portfolio.Organization.NameArabic : item.Portfolio.Organization.NameEnglish,
            ar ? item.Portfolio.NameArabic : item.Portfolio.NameEnglish,
            item.OriginalAmount,
            item.OutstandingBalance,
            item.DaysPastDue,
            item.Status,
            file.Stage,
            file.CourtName,
            file.CourtCaseNumber,
            file.LawyerName,
            file.NextHearingOn,
            file.Notes,
            file.ReceivedAt,
            file.ReceivedByUser.FullName,
            file.UpdatedAt,
            CanManage(),
            actions);
    }

    private bool CanManage() =>
        user.Roles.Contains(SystemRoleNames.Admin, StringComparer.OrdinalIgnoreCase)
        || user.Roles.Contains(SystemRoleNames.LegalOfficer, StringComparer.OrdinalIgnoreCase)
        || user.Permissions.Contains(SystemPermissionCodes.LegalCaseManage, StringComparer.OrdinalIgnoreCase)
        || user.Permissions.Contains("*", StringComparer.OrdinalIgnoreCase);

    private void RequireManage()
    {
        if (!CanManage()) throw new HrForbiddenException("You do not have permission to manage legal cases.");
    }

    private void Audit(string action, Guid caseId, object before, object after) =>
        db.CollectionAuditLogs.Add(new CollectionAuditLog(user.UserId, action, nameof(LegalCaseFile), caseId, caseId, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after), "LEGAL", DateTimeOffset.UtcNow));

    private static void ValidateText(string? value, int max, string name)
    {
        if (value?.Length > max) throw new HrValidationException($"{name} cannot exceed {max} characters.");
    }
}
