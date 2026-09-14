using System.Data;
using System.Globalization;
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

public sealed class BankPortfolioCaseService(ApplicationDbContext db, ICurrentUserContext user, ICollectionsClassificationContext classification) : IBankPortfolioCaseService
{
    private static readonly string[] Statuses = [CollectionsValues.CaseStatuses.Active, CollectionsValues.CaseStatuses.OnHold,
        CollectionsValues.CaseStatuses.Settled, CollectionsValues.CaseStatuses.Closed, CollectionsValues.CaseStatuses.Legal, CollectionsValues.CaseStatuses.WriteOff];
    private bool Has(string role) => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    private bool Global => Has(SystemRoleNames.Admin) || Has(SystemRoleNames.CollectionsOperationsManager);
    private bool Manager => Global || Has(SystemRoleNames.CollectionsSupervisor);
    private bool Collector => Has(SystemRoleNames.CollectionsCollector);
    private BankPortfolioAccessDto Access() => new(Manager, Manager || Collector, Manager, Manager, Statuses);

    public async Task<BankPortfolioCasePageDto> GetAsync(Guid bankId, BankPortfolioCaseQuery request, CancellationToken token)
    {
        await RequireBankAsync(bankId, token); ValidatePage(request.Page, request.PageSize);
        var query = ApplyFilters(ScopedCases(bankId).AsNoTracking(), request);
        var total = await query.CountAsync(token);
        query = Sort(query, request.SortBy, request.SortDirection);
        var ar = ApiTextLocalizer.IsArabic;
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(x =>
            new BankPortfolioCaseListItemDto(x.Id, x.CaseNumber,
                ar ? x.Customer.FullNameArabic ?? x.Customer.FullNameEnglish! : x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic!,
                x.Customer.PrimaryPhone, x.OutstandingBalance, x.AssignedCollectorId,
                x.AssignedCollector == null ? null : x.AssignedCollector.FullName, x.Status,
                db.CollectionActivities.Where(a => a.CaseId == x.Id).Max(a => (DateTimeOffset?)a.CreatedAt), x.NextFollowUpAt)).ToArrayAsync(token);
        return new(items, total, request.Page, request.PageSize, total == 0 ? 0 : (int)Math.Ceiling(total / (double)request.PageSize), Access());
    }

    public async Task<BankPortfolioCaseDetailsDto> GetCaseAsync(Guid bankId, Guid caseId, CancellationToken token)
    {
        await RequireBankAsync(bankId, token); var ar = ApiTextLocalizer.IsArabic;
        return await ScopedCases(bankId).AsNoTracking().Where(x => x.Id == caseId).Select(x => new BankPortfolioCaseDetailsDto(
            x.Id, x.CaseNumber, ar ? x.Customer.FullNameArabic ?? x.Customer.FullNameEnglish! : x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic!,
            x.Customer.CustomerCode, x.Customer.PrimaryPhone, x.Customer.AlternatePhone, x.Customer.NationalId,
            ar ? x.Customer.AddressArabic ?? x.Customer.AddressEnglish : x.Customer.AddressEnglish ?? x.Customer.AddressArabic,
            ar ? x.Portfolio.Organization.NameArabic : x.Portfolio.Organization.NameEnglish,
            ar ? x.Portfolio.NameArabic : x.Portfolio.NameEnglish, x.AccountReference, x.ContractReference, x.ProductType,
            x.OriginalAmount, x.OutstandingBalance,
            db.CollectionPayments.Where(p => p.CaseId == x.Id && p.Status == CollectionsValues.PaymentStatuses.Approved).Sum(p => (decimal?)p.Amount) ?? 0,
            x.OutstandingBalance, x.Status, x.AssignedCollectorId, x.AssignedCollector == null ? null : x.AssignedCollector.FullName,
            db.CollectionActivities.Where(a => a.CaseId == x.Id).Max(a => (DateTimeOffset?)a.CreatedAt), x.NextFollowUpAt,
            db.CollectionActivities.Where(a => a.CaseId == x.Id).OrderByDescending(a => a.CreatedAt).Select(a => a.Notes).FirstOrDefault(),
            x.SourceImportId, x.SourceImport == null ? null : x.SourceImport.OriginalFileName,
            x.SourceImport == null ? null : x.SourceImport.UploadedAt, x.CreatedAt, x.UpdatedAt, Access())).SingleOrDefaultAsync(token)
            ?? throw new HrNotFoundException("Portfolio case was not found for this bank.");
    }

    public async Task<BankPortfolioCaseDetailsDto> UpdateAsync(Guid bankId, Guid caseId, UpdateBankPortfolioCaseRequest request, CancellationToken token)
    {
        if (!Manager && !Collector) throw new HrForbiddenException("You do not have permission to edit portfolio cases.");
        await RequireBankAsync(bankId, token); var item = await ScopedCases(bankId).Include(x => x.Customer).SingleOrDefaultAsync(x => x.Id == caseId, token)
            ?? throw new HrNotFoundException("Portfolio case was not found for this bank.");
        var status = request.Status.Trim().ToUpperInvariant(); if (!Statuses.Contains(status)) throw new HrValidationException("Case status is invalid.");
        ValidateText(request.Mobile, 40, "Mobile"); ValidateText(request.AlternativeMobile, 40, "Alternative mobile"); ValidateText(request.Address, 500, "Address");
        var before = new { item.Status, item.NextFollowUpAt, item.Customer.PrimaryPhone, item.Customer.AlternatePhone, item.Customer.AddressArabic, item.Customer.AddressEnglish };
        item.Customer.UpdatePortfolioContact(request.Mobile, request.AlternativeMobile, request.Address, ApiTextLocalizer.IsArabic);
        item.UpdatePortfolioCase(status, request.NextFollowUpAt, DateTimeOffset.UtcNow);
        AddAudit("PortfolioCaseUpdated", item.Id, before, request); await db.SaveChangesAsync(token);
        return await GetCaseAsync(bankId, caseId, token);
    }

    public async Task<IReadOnlyCollection<BankPortfolioCollectorDto>> GetCollectorsAsync(Guid bankId, CancellationToken token)
    {
        if (!Manager) throw new HrForbiddenException("You do not have permission to assign portfolio cases."); await RequireBankAsync(bankId, token);
        var query = AuthorizedCollectorUsers(bankId).AsNoTracking();
        return await query.OrderBy(x => x.FullName).Select(x => new BankPortfolioCollectorDto(x.Id, x.FullName)).ToArrayAsync(token);
    }

    public async Task<BankPortfolioAssignmentPreviewDto> PreviewAssignmentAsync(Guid bankId, AssignBankPortfolioCasesRequest request, CancellationToken token)
    {
        var (ids, collector) = await ValidateAssignmentAsync(bankId, request, token);
        return new(ids.Length, collector.Id, collector.FullName);
    }

    public async Task<BankPortfolioAssignmentPreviewDto> AssignAsync(Guid bankId, AssignBankPortfolioCasesRequest request, CancellationToken token)
    {
        var (ids, collector) = await ValidateAssignmentAsync(bankId, request, token);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var cases = await ScopedCases(bankId).Where(x => ids.Contains(x.Id)).ToArrayAsync(token);
        if (cases.Length != ids.Length) throw new HrNotFoundException("One or more portfolio cases are outside your authorized scope.");
        var teamId = Global ? null : await db.CollectionTeamMembers.Where(x => x.UserId == collector.Id && x.IsActive && x.Team.SupervisorId == user.UserId).Select(x => (Guid?)x.TeamId).FirstOrDefaultAsync(token);
        var now = DateTimeOffset.UtcNow;
        foreach (var item in cases)
        {
            var previous = item.AssignedCollectorId; item.Assign(collector.Id, teamId, now);
            db.CollectionAssignmentHistory.Add(new CollectionAssignmentHistory(item.Id, previous, collector.Id, user.UserId, teamId, request.Reason, CollectionsValues.AssignmentSources.Manual, null, now));
            AddAudit(previous.HasValue ? "PortfolioCaseReassigned" : "PortfolioCaseAssigned", item.Id, new { AssignedCollectorId = previous }, new { AssignedCollectorId = collector.Id, TeamId = teamId, request.Reason });
        }
        await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return new(ids.Length, collector.Id, collector.FullName);
    }

    public async Task<byte[]> ExportCsvAsync(Guid bankId, BankPortfolioCaseQuery request, CancellationToken token)
    {
        if (!Manager) throw new HrForbiddenException("You do not have permission to export portfolio cases."); await RequireBankAsync(bankId, token);
        var query = Sort(ApplyFilters(ScopedCases(bankId).AsNoTracking(), request), request.SortBy, request.SortDirection);
        var rows = await query.Select(x => new { x.CaseNumber, Customer = x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic!, x.Customer.PrimaryPhone, x.OutstandingBalance, Collector = x.AssignedCollector == null ? "" : x.AssignedCollector.FullName, x.Status, x.NextFollowUpAt }).ToArrayAsync(token);
        var csv = new StringBuilder("Case ID,Customer Name,Mobile,Outstanding Amount,Assigned To,Status,Next Follow-up\r\n");
        foreach (var x in rows) csv.AppendLine(string.Join(',', Csv(x.CaseNumber), Csv(x.Customer), Csv(x.PrimaryPhone), x.OutstandingBalance.ToString(CultureInfo.InvariantCulture), Csv(x.Collector), Csv(x.Status), Csv(x.NextFollowUpAt?.ToString("O"))));
        return new UTF8Encoding(true).GetBytes(csv.ToString());
    }

    private IQueryable<CollectionCase> ScopedCases(Guid bankId) => ScopedCasesCore(bankId).Apply(classification);
    private IQueryable<CollectionCase> ScopedCasesCore(Guid bankId)
    {
        var baseQuery = db.CollectionCases.Where(x => x.Portfolio.OrganizationId == bankId && !x.IsArchived); if (Global) return baseQuery;
        if (Collector) return baseQuery.Where(x => x.AssignedCollectorId == user.UserId);
        if (Manager) return baseQuery.Where(x => (x.AssignedTeam != null && x.AssignedTeam.SupervisorId == user.UserId) ||
            (x.AssignedTeamId == null && db.CollectionUserAccess.Any(a => a.UserId == user.UserId && a.OrganizationId == bankId && (a.PortfolioId == null || a.PortfolioId == x.PortfolioId))));
        return baseQuery.Where(_ => false);
    }
    private async Task RequireBankAsync(Guid bankId, CancellationToken token)
    {
        var accessible = await db.CollectionClientOrganizations.AsNoTracking().AnyAsync(x => x.Id == bankId && x.IsActive && (x.OrganizationType == CollectionsValues.OrganizationTypes.Bank || x.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance) &&
            (Global || db.CollectionUserAccess.Any(a => a.UserId == user.UserId && a.OrganizationId == bankId) || ScopedCases(bankId).Any()), token);
        if (!accessible) throw new HrNotFoundException("Bank was not found or is outside your authorized scope.");
    }
    private IQueryable<User> AuthorizedCollectorUsers(Guid bankId)
    {
        var collectors = db.Users.Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == SystemRoleNames.CollectionsCollector));
        return Global ? collectors : collectors.Where(x => db.CollectionTeamMembers.Any(m => m.UserId == x.Id && m.IsActive && m.Team.IsActive && m.Team.SupervisorId == user.UserId));
    }
    private async Task<(Guid[] Ids, User Collector)> ValidateAssignmentAsync(Guid bankId, AssignBankPortfolioCasesRequest request, CancellationToken token)
    {
        if (!Manager) throw new HrForbiddenException("You do not have permission to assign portfolio cases."); await RequireBankAsync(bankId, token);
        var ids = request.CaseIds.Where(x => x != Guid.Empty).Distinct().ToArray(); if (ids.Length is < 1 or > 500) throw new HrValidationException("Select between 1 and 500 cases.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 2 or > 500) throw new HrValidationException("Assignment reason must contain between 2 and 500 characters.");
        if (await ScopedCases(bankId).CountAsync(x => ids.Contains(x.Id), token) != ids.Length) throw new HrNotFoundException("One or more portfolio cases are outside your authorized scope.");
        var collector = await AuthorizedCollectorUsers(bankId).SingleOrDefaultAsync(x => x.Id == request.CollectorId, token) ?? throw new HrForbiddenException("The selected collector is outside your authorized scope.");
        return (ids, collector);
    }
    private static IQueryable<CollectionCase> ApplyFilters(IQueryable<CollectionCase> query, BankPortfolioCaseQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search)) { if (request.Search.Length > 160) throw new HrValidationException("Search cannot exceed 160 characters."); var term = request.Search.Trim().ToLower(); query = query.Where(x => x.CaseNumber.ToLower().Contains(term) || x.Customer.CustomerCode.ToLower().Contains(term) || (x.Customer.FullNameArabic != null && x.Customer.FullNameArabic.ToLower().Contains(term)) || (x.Customer.FullNameEnglish != null && x.Customer.FullNameEnglish.ToLower().Contains(term)) || (x.Customer.PrimaryPhone != null && x.Customer.PrimaryPhone.Contains(term)) || (x.Customer.NationalId != null && x.Customer.NationalId.Contains(term))); }
        if (!string.IsNullOrWhiteSpace(request.Status)) { var status = request.Status.Trim().ToUpperInvariant(); if (!Statuses.Contains(status)) throw new HrValidationException("Case status is invalid."); query = query.Where(x => x.Status == status); }
        if (request.CollectorId.HasValue) query = query.Where(x => x.AssignedCollectorId == request.CollectorId); return query;
    }
    private IQueryable<CollectionCase> Sort(IQueryable<CollectionCase> query, string? field, string? direction)
    {
        var desc = direction?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true;
        return field?.Trim().ToLowerInvariant() switch { "customer" => desc ? query.OrderByDescending(x => x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic) : query.OrderBy(x => x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic), "outstanding" => desc ? query.OrderByDescending(x => x.OutstandingBalance) : query.OrderBy(x => x.OutstandingBalance), "status" => desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status), "lastactivity" => desc ? query.OrderByDescending(x => db.CollectionActivities.Where(a => a.CaseId == x.Id).Max(a => (DateTimeOffset?)a.CreatedAt)) : query.OrderBy(x => db.CollectionActivities.Where(a => a.CaseId == x.Id).Max(a => (DateTimeOffset?)a.CreatedAt)), "nextfollowup" => desc ? query.OrderByDescending(x => x.NextFollowUpAt) : query.OrderBy(x => x.NextFollowUpAt), _ => query.OrderByDescending(x => x.UpdatedAt) };
    }
    private void AddAudit(string action, Guid caseId, object before, object after) => db.CollectionAuditLogs.Add(new CollectionAuditLog(user.UserId, action, nameof(CollectionCase), caseId, caseId, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after), "WEB", DateTimeOffset.UtcNow));
    private static void ValidatePage(int page, int size) { if (page < 1 || size is not (20 or 50 or 100)) throw new HrValidationException("Page size must be 20, 50, or 100."); }
    private static void ValidateText(string? value, int max, string name) { if (value?.Length > max) throw new HrValidationException($"{name} cannot exceed {max} characters."); }
    private static string Csv(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";
}
