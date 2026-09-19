using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class BankCustomerService(ApplicationDbContext db, ICurrentUserContext user, ICollectionsClassificationContext classification) : IBankCustomerService
{
    private bool Has(string role) => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    private bool Global => Has(SystemRoleNames.Admin) || Has(SystemRoleNames.CollectionsOperationsManager);
    private bool Manager => Global || Has(SystemRoleNames.CollectionsSupervisor);
    private bool Collector => Has(SystemRoleNames.CollectionsCollector);

    public async Task<BankCustomerPageDto> GetAsync(Guid organizationId, BankCustomerQuery query, CancellationToken token)
    {
        await RequireOrganizationAsync(organizationId, token);
        ValidatePage(query.Page, query.PageSize);
        var ar = ApiTextLocalizer.IsArabic;
        var cases = ScopedCases(organizationId).AsNoTracking();
        var customers = db.CollectionCustomers.AsNoTracking().Where(c => c.OrganizationId == organizationId && cases.Any(x => x.CustomerId == c.Id));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            if (query.Search.Length > 160) throw new HrValidationException("Search cannot exceed 160 characters.");
            var term = query.Search.Trim().ToLower();
            customers = customers.Where(c =>
                c.CustomerCode.ToLower().Contains(term) ||
                (c.FullNameArabic != null && c.FullNameArabic.ToLower().Contains(term)) ||
                (c.FullNameEnglish != null && c.FullNameEnglish.ToLower().Contains(term)) ||
                (c.PrimaryPhone != null && c.PrimaryPhone.Contains(term)) ||
                (c.AlternatePhone != null && c.AlternatePhone.Contains(term)) ||
                (c.NationalId != null && c.NationalId.Contains(term)) ||
                cases.Any(x => x.CustomerId == c.Id && (
                    x.AccountReference.ToLower().Contains(term) ||
                    (x.ContractReference != null && x.ContractReference.ToLower().Contains(term)) ||
                    x.CaseNumber.ToLower().Contains(term))));
        }

        var total = await customers.CountAsync(token);
        var pageCustomers = await customers
            .OrderBy(c => ar ? c.FullNameArabic ?? c.FullNameEnglish : c.FullNameEnglish ?? c.FullNameArabic)
            .ThenBy(c => c.CustomerCode)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new
            {
                c.Id,
                Name = ar ? c.FullNameArabic ?? c.FullNameEnglish! : c.FullNameEnglish ?? c.FullNameArabic!,
                c.PrimaryPhone,
                c.NationalId,
                CaseCount = cases.Count(x => x.CustomerId == c.Id),
                Outstanding = cases.Where(x => x.CustomerId == c.Id).Sum(x => (decimal?)x.OutstandingBalance) ?? 0,
                Paid = db.CollectionPayments.Where(p => p.Status == CollectionsValues.PaymentStatuses.Approved && cases.Any(x => x.Id == p.CaseId && x.CustomerId == c.Id)).Sum(p => (decimal?)p.Amount) ?? 0,
                Primary = cases.Where(x => x.CustomerId == c.Id).OrderByDescending(x => x.UpdatedAt).Select(x => new
                {
                    x.AccountReference,
                    x.ContractReference,
                    x.Status,
                    Collector = x.AssignedCollector == null ? null : x.AssignedCollector.FullName
                }).FirstOrDefault()
            }).ToArrayAsync(token);

        var items = pageCustomers.Select(c => new BankCustomerListItemDto(
            c.Id, c.Name, c.PrimaryPhone, c.NationalId,
            c.Primary?.AccountReference, c.Primary?.ContractReference,
            c.Outstanding, c.Paid, c.Outstanding, c.Primary?.Status, c.Primary?.Collector, c.CaseCount)).ToArray();

        return new BankCustomerPageDto(items, total, query.Page, query.PageSize, total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<BankCustomerDetailsDto> GetDetailsAsync(Guid organizationId, Guid customerId, CancellationToken token)
    {
        await RequireOrganizationAsync(organizationId, token);
        var ar = ApiTextLocalizer.IsArabic;
        var customer = await db.CollectionCustomers.AsNoTracking()
            .Include(c => c.Organization)
            .SingleOrDefaultAsync(c => c.Id == customerId && c.OrganizationId == organizationId, token)
            ?? throw new HrNotFoundException("Customer was not found for this organization.");

        var caseIds = await ScopedCases(organizationId).AsNoTracking().Where(x => x.CustomerId == customerId).Select(x => x.Id).ToArrayAsync(token);
        if (caseIds.Length == 0 && !Global && !Manager)
            throw new HrNotFoundException("Customer was not found for this organization.");
        if (caseIds.Length == 0 && !(await ScopedCases(organizationId).AnyAsync(x => x.CustomerId == customerId, token)) && !Global)
        {
            // Collector with no assigned cases for this customer cannot open them.
            if (Collector) throw new HrNotFoundException("Customer was not found for this organization.");
        }

        var cases = await ScopedCases(organizationId).AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new
            {
                x.Id,
                x.CaseNumber,
                x.AccountReference,
                x.ContractReference,
                x.ProductType,
                PortfolioName = ar ? x.Portfolio.NameArabic : x.Portfolio.NameEnglish,
                x.OriginalAmount,
                x.OutstandingBalance,
                x.OverdueBalance,
                Paid = db.CollectionPayments.Where(p => p.CaseId == x.Id && p.Status == CollectionsValues.PaymentStatuses.Approved).Sum(p => (decimal?)p.Amount) ?? 0,
                x.DaysPastDue,
                x.Status,
                x.AssignedCollectorId,
                Collector = x.AssignedCollector == null ? null : x.AssignedCollector.FullName,
                AssignmentDate = db.CollectionAssignmentHistory.Where(h => h.CaseId == x.Id).Max(h => (DateTimeOffset?)h.AssignedAt),
                x.LastPaymentAt,
                x.NextFollowUpAt
            }).ToArrayAsync(token);

        if (cases.Length == 0)
            throw new HrNotFoundException("Customer was not found for this organization.");

        var payments = await db.CollectionPayments.AsNoTracking()
            .Where(p => caseIds.Contains(p.CaseId) && p.Status == CollectionsValues.PaymentStatuses.Approved)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.SubmittedAt).Take(50)
            .Select(p => new BankCustomerLinkSummaryDto("Payment", p.Id, p.ReferenceNumber, p.Status, p.SubmittedAt, p.Amount))
            .ToArrayAsync(token);

        var promises = await db.CollectionPromisesToPay.AsNoTracking()
            .Where(p => caseIds.Contains(p.CaseId)).OrderByDescending(p => p.PromiseDate).Take(50)
            .Select(p => new BankCustomerLinkSummaryDto("PTP", p.Id, p.PromiseDate.ToString("yyyy-MM-dd"), p.Status, p.CreatedAt, p.PromisedAmount))
            .ToArrayAsync(token);

        var visits = await db.CollectionFieldVisits.AsNoTracking()
            .Where(v => caseIds.Contains(v.CaseId)).OrderByDescending(v => v.ScheduledAt).Take(50)
            .Select(v => new BankCustomerLinkSummaryDto("Visit", v.Id, v.Purpose ?? v.Status, v.Status, v.ScheduledAt, null))
            .ToArrayAsync(token);

        var complaints = await db.CollectionComplaints.AsNoTracking()
            .Where(c => caseIds.Contains(c.CaseId)).OrderByDescending(c => c.ReceivedAt).Take(50)
            .Select(c => new BankCustomerLinkSummaryDto("Complaint", c.Id, c.Reference + " · " + c.Category, c.Status, c.ReceivedAt, null))
            .ToArrayAsync(token);

        var activities = await db.CollectionActivities.AsNoTracking()
            .Where(a => caseIds.Contains(a.CaseId)).OrderByDescending(a => a.CreatedAt).Take(100)
            .Select(a => new BankCustomerLinkSummaryDto("Activity", a.Id, a.ActivityType, a.Result, a.CreatedAt, null))
            .ToArrayAsync(token);

        var timeline = payments.Concat(promises).Concat(visits).Concat(complaints).Concat(activities)
            .OrderByDescending(x => x.OccurredAt).Take(100).ToArray();

        var caseSummaries = cases.Select(x => new BankCustomerCaseSummaryDto(
            x.Id, x.CaseNumber, x.AccountReference, x.ContractReference, x.ProductType, x.PortfolioName,
            x.OriginalAmount, x.OutstandingBalance, x.OverdueBalance, x.Paid, x.OutstandingBalance, x.DaysPastDue,
            x.Status, x.AssignedCollectorId, x.Collector, x.AssignmentDate, x.LastPaymentAt, x.NextFollowUpAt)).ToArray();

        var (address1, address2) = CollectionFileRowMapper.SeparateAddresses(
            ar ? customer.AddressArabic ?? customer.AddressEnglish : customer.AddressEnglish ?? customer.AddressArabic,
            customer.SecondaryAddress);

        return new BankCustomerDetailsDto(
            customer.Id,
            customer.CustomerCode,
            ar ? customer.FullNameArabic ?? customer.FullNameEnglish! : customer.FullNameEnglish ?? customer.FullNameArabic!,
            customer.FullNameArabic,
            customer.FullNameEnglish,
            customer.PrimaryPhone,
            customer.AlternatePhone,
            customer.NationalId,
            address1,
            address2,
            customer.Governorate,
            customer.Area,
            ar ? customer.Organization.NameArabic : customer.Organization.NameEnglish,
            customer.Organization.OrganizationType,
            caseSummaries.Sum(x => x.OriginalAmount),
            caseSummaries.Sum(x => x.OutstandingAmount),
            caseSummaries.Sum(x => x.PaidAmount),
            caseSummaries.Sum(x => x.RemainingAmount),
            caseSummaries.Sum(x => x.OverdueAmount),
            caseSummaries,
            payments,
            promises,
            visits,
            complaints,
            activities,
            timeline);
    }

    private IQueryable<CollectionCase> ScopedCases(Guid organizationId) => ScopedCasesCore(organizationId).Apply(classification);
    private IQueryable<CollectionCase> ScopedCasesCore(Guid organizationId)
    {
        var baseQuery = db.CollectionCases.Where(x => x.Portfolio.OrganizationId == organizationId && !x.IsArchived);
        if (Global) return baseQuery;
        if (Collector) return baseQuery.Where(x => x.AssignedCollectorId == user.UserId);
        if (Manager) return baseQuery.Where(x =>
            (x.AssignedTeam != null && x.AssignedTeam.SupervisorId == user.UserId) ||
            (x.AssignedTeamId == null && db.CollectionUserAccess.Any(a => a.UserId == user.UserId && a.OrganizationId == organizationId && (a.PortfolioId == null || a.PortfolioId == x.PortfolioId))));
        return baseQuery.Where(_ => false);
    }

    private async Task RequireOrganizationAsync(Guid organizationId, CancellationToken token)
    {
        var accessible = await db.CollectionClientOrganizations.AsNoTracking().AnyAsync(x =>
            x.Id == organizationId && x.IsActive &&
            (x.OrganizationType == CollectionsValues.OrganizationTypes.Bank || x.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance) &&
            (Global || db.CollectionUserAccess.Any(a => a.UserId == user.UserId && a.OrganizationId == organizationId) || ScopedCases(organizationId).Any()), token);
        if (!accessible) throw new HrNotFoundException("Organization was not found or is outside your authorized scope.");
    }

    private static void ValidatePage(int page, int size)
    {
        if (page < 1 || size is not (20 or 50 or 100)) throw new HrValidationException("Page size must be 20, 50, or 100.");
    }
}
