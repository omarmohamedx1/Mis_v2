using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class BankPtpService(ApplicationDbContext db, ICurrentUserContext user, ICollectionsClassificationContext classification) : IBankPtpService
{
    private bool Has(string role) => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    private bool Global => Has(SystemRoleNames.Admin) || Has(SystemRoleNames.CollectionsOperationsManager);
    private bool Manager => Global || Has(SystemRoleNames.CollectionsSupervisor);
    private bool Collector => Has(SystemRoleNames.CollectionsCollector);
    private BankPtpAccessDto Access() => new(Manager, Manager || Collector, Manager || Collector);

    public async Task<BankPtpSummaryDto> SummaryAsync(Guid bankId, CancellationToken token)
    {
        await RequireBankAsync(bankId, token); var q=ScopedPtps(bankId).AsNoTracking(); var today=CairoToday();
        return new(await q.CountAsync(x=>x.Status==CollectionsValues.PromiseStatuses.Active&&x.PromiseDate==today,token),
            await q.CountAsync(x=>x.Status==CollectionsValues.PromiseStatuses.Active&&x.PromiseDate>today,token),
            await q.CountAsync(x=>x.Status==CollectionsValues.PromiseStatuses.Active&&x.PromiseDate<today,token),
            await q.CountAsync(x=>x.Status==CollectionsValues.PromiseStatuses.Broken,token));
    }

    public async Task<BankPtpPageDto> GetAsync(Guid bankId, BankPtpQuery request, CancellationToken token)
    {
        await RequireBankAsync(bankId,token); ValidatePage(request.Page,request.PageSize); var q=ApplyFilters(ScopedPtps(bankId).AsNoTracking(),request); var total=await q.CountAsync(token); q=Sort(q,request.SortBy,request.SortDirection); var today=CairoToday(); var ar=ApiTextLocalizer.IsArabic;
        var rows=await q.Skip((request.Page-1)*request.PageSize).Take(request.PageSize).Select(x=>new {x.Id,x.CaseId,x.Case.CaseNumber,Customer=ar?x.Case.Customer.FullNameArabic??x.Case.Customer.FullNameEnglish!:x.Case.Customer.FullNameEnglish??x.Case.Customer.FullNameArabic!,x.PromisedAmount,x.PromiseDate,x.Status,x.CollectorId,Collector=x.Collector.FullName,x.CreatedAt,x.EvaluatedAt}).ToArrayAsync(token);
        return new(rows.Select(x=>new BankPtpItemDto(x.Id,x.CaseId,x.CaseNumber,x.Customer,x.PromisedAmount,x.PromiseDate,DisplayStatus(x.Status),Operational(x.Status,x.PromiseDate,today),x.CollectorId,x.Collector,x.CreatedAt,x.EvaluatedAt??x.CreatedAt)).ToArray(),total,request.Page,request.PageSize,total==0?0:(int)Math.Ceiling(total/(double)request.PageSize),Access());
    }

    public async Task<BankPtpDetailsDto> GetDetailsAsync(Guid bankId, Guid ptpId, CancellationToken token)
    {
        await RequireBankAsync(bankId,token); var ar=ApiTextLocalizer.IsArabic; var x=await ScopedPtps(bankId).AsNoTracking().Where(p=>p.Id==ptpId).Select(p=>new {p.Id,p.CaseId,p.Case.CaseNumber,Customer=ar?p.Case.Customer.FullNameArabic??p.Case.Customer.FullNameEnglish!:p.Case.Customer.FullNameEnglish??p.Case.Customer.FullNameArabic!,p.Case.Customer.PrimaryPhone,p.Case.OutstandingBalance,Bank=ar?p.Case.Portfolio.Organization.NameArabic:p.Case.Portfolio.Organization.NameEnglish,p.PromisedAmount,p.PromiseDate,p.Status,p.CollectorId,Collector=p.Collector.FullName,p.Notes,p.CreatedAt,p.EvaluatedAt}).SingleOrDefaultAsync(token)??throw new HrNotFoundException("Promise to pay was not found for this bank or user scope.");
        return new(x.Id,x.CaseId,x.CaseNumber,x.Customer,x.PrimaryPhone,x.OutstandingBalance,x.Bank,x.PromisedAmount,x.PromiseDate,DisplayStatus(x.Status),Operational(x.Status,x.PromiseDate,CairoToday()),x.CollectorId,x.Collector,x.Notes,x.CreatedAt,x.EvaluatedAt??x.CreatedAt,Access());
    }

    public async Task<BankPtpDetailsDto> CreateAsync(Guid bankId, CreateBankPtpRequest request, CancellationToken token)
    {
        if(!Manager&&!Collector)throw new HrForbiddenException("You do not have permission to create promises to pay."); await RequireBankAsync(bankId,token);
        if(request.PromiseAmount<=0)throw new HrValidationException("Promise amount must be positive."); if(request.PromiseDate<CairoToday())throw new HrValidationException("Promise date cannot be in the past."); if(request.Notes?.Length>2000)throw new HrValidationException("Notes cannot exceed 2000 characters.");
        var item=await ScopedCases(bankId).SingleOrDefaultAsync(x=>x.Id==request.CaseId,token)??throw new HrNotFoundException("Portfolio case was not found for this bank or user scope.");
        var collectorId=item.AssignedCollectorId??throw new HrConflictException("Assign this case to a collector before creating a promise to pay."); var now=DateTimeOffset.UtcNow;
        if(await db.CollectionPromisesToPay.AnyAsync(x=>x.CaseId==item.Id&&x.CollectorId==collectorId&&x.PromisedAmount==request.PromiseAmount&&x.PromiseDate==request.PromiseDate&&x.CreatedAt>now.AddSeconds(-15),token))throw new HrConflictException("This promise was already submitted.");
        var notes=string.IsNullOrWhiteSpace(request.Notes)?null:request.Notes.Trim(); var ptp=new PromiseToPay(item.Id,request.PromiseAmount,request.PromiseDate,collectorId,"BANK_WORKSPACE",notes,now); db.CollectionPromisesToPay.Add(ptp);
        AddActivity(item.Id,ptp,"CREATED",now); AddAudit("PromiseCreated",ptp,item.Id,null,new {ptp.PromisedAmount,ptp.PromiseDate,ptp.Status}); await db.SaveChangesAsync(token); return await GetDetailsAsync(bankId,ptp.Id,token);
    }

    public async Task<BankPtpDetailsDto> ChangeStatusAsync(Guid bankId, Guid ptpId, ChangeBankPtpStatusRequest request, CancellationToken token)
    {
        if(!Manager&&!Collector)throw new HrForbiddenException("You do not have permission to update promises to pay."); await RequireBankAsync(bankId,token); var ptp=await ScopedPtps(bankId).SingleOrDefaultAsync(x=>x.Id==ptpId,token)??throw new HrNotFoundException("Promise to pay was not found for this bank or user scope."); var target=PersistedStatus(request.Status); var before=ptp.Status; var now=DateTimeOffset.UtcNow;
        try{ptp.Transition(target,now);}catch(InvalidOperationException ex){throw new HrConflictException(ex.Message);}catch(ArgumentException ex){throw new HrValidationException(ex.Message);}
        AddActivity(ptp.CaseId,ptp,DisplayStatus(target).ToUpperInvariant(),now); AddAudit("PromiseStatusChanged",ptp,ptp.CaseId,new {Status=before},new {Status=target}); await db.SaveChangesAsync(token); return await GetDetailsAsync(bankId,ptp.Id,token);
    }

    public async Task<IReadOnlyCollection<BankActivityCaseLookupDto>> CasesAsync(Guid bankId,string? search,CancellationToken token)
    { await RequireBankAsync(bankId,token);var q=ScopedCases(bankId).AsNoTracking();if(!string.IsNullOrWhiteSpace(search)){if(search.Length>160)throw new HrValidationException("Search cannot exceed 160 characters.");var t=search.Trim().ToLower();q=q.Where(x=>x.CaseNumber.ToLower().Contains(t)||(x.Customer.FullNameArabic!=null&&x.Customer.FullNameArabic.ToLower().Contains(t))||(x.Customer.FullNameEnglish!=null&&x.Customer.FullNameEnglish.ToLower().Contains(t))||(x.Customer.PrimaryPhone!=null&&x.Customer.PrimaryPhone.Contains(t)));}var ar=ApiTextLocalizer.IsArabic;return await q.OrderBy(x=>x.CaseNumber).Take(30).Select(x=>new BankActivityCaseLookupDto(x.Id,x.CaseNumber,ar?x.Customer.FullNameArabic??x.Customer.FullNameEnglish!:x.Customer.FullNameEnglish??x.Customer.FullNameArabic!,x.Customer.PrimaryPhone,x.OutstandingBalance,x.Status,x.AssignedCollectorId,x.AssignedCollector==null?null:x.AssignedCollector.FullName)).ToArrayAsync(token); }
    public async Task<IReadOnlyCollection<BankPortfolioCollectorDto>> CollectorsAsync(Guid bankId,CancellationToken token){await RequireBankAsync(bankId,token);if(!Manager)return [];return await AuthorizedCollectors().AsNoTracking().OrderBy(x=>x.FullName).Select(x=>new BankPortfolioCollectorDto(x.Id,x.FullName)).ToArrayAsync(token);}

    private void AddActivity(Guid caseId,PromiseToPay ptp,string outcome,DateTimeOffset now){var message=$"Promise to Pay {outcome.ToLowerInvariant()} for {ptp.PromisedAmount.ToString("0.00",CultureInfo.InvariantCulture)} EGP due on {ptp.PromiseDate:yyyy-MM-dd}.";var type=outcome switch{"KEPT"=>CollectionsValues.ActivityTypes.PtpKept,"BROKEN"=>CollectionsValues.ActivityTypes.PtpBroken,"CANCELLED"=>CollectionsValues.ActivityTypes.PtpCancelled,_=>CollectionsValues.ActivityTypes.PtpCreated};db.CollectionActivities.Add(new CollectionActivity(caseId,type,outcome,message,"SYSTEM",user.UserId,now,null));}
    private void AddAudit(string action,PromiseToPay ptp,Guid caseId,object? before,object after)=>db.CollectionAuditLogs.Add(new CollectionAuditLog(user.UserId,action,nameof(PromiseToPay),ptp.Id,caseId,before==null?null:JsonSerializer.Serialize(before),JsonSerializer.Serialize(after),"WEB",DateTimeOffset.UtcNow));
    private IQueryable<CollectionCase> ScopedCases(Guid bankId)=>ScopedCasesCore(bankId).Apply(classification);
    private IQueryable<CollectionCase> ScopedCasesCore(Guid bankId){var q=db.CollectionCases.Where(x=>x.Portfolio.OrganizationId==bankId&&!x.IsArchived);if(Global)return q;if(Collector)return q.Where(x=>x.AssignedCollectorId==user.UserId);if(Manager)return q.Where(x=>(x.AssignedTeam!=null&&x.AssignedTeam.SupervisorId==user.UserId)||(x.AssignedTeamId==null&&db.CollectionUserAccess.Any(a=>a.UserId==user.UserId&&a.OrganizationId==bankId&&(a.PortfolioId==null||a.PortfolioId==x.PortfolioId))));return q.Where(_=>false);}
    private IQueryable<PromiseToPay> ScopedPtps(Guid bankId){var cases=ScopedCases(bankId);return db.CollectionPromisesToPay.Where(x=>cases.Any(c=>c.Id==x.CaseId));}
    private IQueryable<User> AuthorizedCollectors(){var q=db.Users.Where(x=>x.IsActive&&x.UserRoles.Any(r=>r.Role.Name==SystemRoleNames.CollectionsCollector));return Global?q:q.Where(x=>db.CollectionTeamMembers.Any(m=>m.UserId==x.Id&&m.IsActive&&m.Team.IsActive&&m.Team.SupervisorId==user.UserId));}
    private async Task RequireBankAsync(Guid bankId,CancellationToken token){var exists=await db.CollectionClientOrganizations.AsNoTracking().AnyAsync(x=>x.Id==bankId&&x.IsActive&&(x.OrganizationType==CollectionsValues.OrganizationTypes.Bank||x.OrganizationType==CollectionsValues.OrganizationTypes.ConsumerFinance),token);if(!exists||(!Global&&!await db.CollectionUserAccess.AnyAsync(x=>x.UserId==user.UserId&&x.OrganizationId==bankId,token)&&!await ScopedCases(bankId).AnyAsync(token)))throw new HrNotFoundException("Organization was not found or is outside your authorized scope.");}
    private static IQueryable<PromiseToPay> ApplyFilters(IQueryable<PromiseToPay> q,BankPtpQuery r){var today=CairoToday();var view=string.IsNullOrWhiteSpace(r.View)?"ALL":r.View.Trim().ToUpperInvariant();q=view switch{"TODAY"=>q.Where(x=>x.Status==CollectionsValues.PromiseStatuses.Active&&x.PromiseDate==today),"UPCOMING"=>q.Where(x=>x.Status==CollectionsValues.PromiseStatuses.Active&&x.PromiseDate>today),"OVERDUE"=>q.Where(x=>x.Status==CollectionsValues.PromiseStatuses.Active&&x.PromiseDate<today),"BROKEN"=>q.Where(x=>x.Status==CollectionsValues.PromiseStatuses.Broken),"ALL"=>q,_=>throw new HrValidationException("PTP view is invalid.")};if(!string.IsNullOrWhiteSpace(r.Search)){if(r.Search.Length>160)throw new HrValidationException("Search cannot exceed 160 characters.");var t=r.Search.Trim().ToLower();q=q.Where(x=>x.Case.CaseNumber.ToLower().Contains(t)||(x.Case.Customer.FullNameArabic!=null&&x.Case.Customer.FullNameArabic.ToLower().Contains(t))||(x.Case.Customer.FullNameEnglish!=null&&x.Case.Customer.FullNameEnglish.ToLower().Contains(t))||(x.Case.Customer.PrimaryPhone!=null&&x.Case.Customer.PrimaryPhone.Contains(t)));}if(!string.IsNullOrWhiteSpace(r.Status))q=q.Where(x=>x.Status==PersistedStatus(r.Status));if(r.PromiseDate.HasValue)q=q.Where(x=>x.PromiseDate==r.PromiseDate);if(r.CollectorId.HasValue)q=q.Where(x=>x.CollectorId==r.CollectorId);return q;}
    private static IQueryable<PromiseToPay> Sort(IQueryable<PromiseToPay> q,string? field,string? direction){var d=direction?.Equals("desc",StringComparison.OrdinalIgnoreCase)==true;return field?.Trim().ToLowerInvariant() switch{"amount"=>d?q.OrderByDescending(x=>x.PromisedAmount):q.OrderBy(x=>x.PromisedAmount),"customer"=>d?q.OrderByDescending(x=>x.Case.Customer.FullNameEnglish??x.Case.Customer.FullNameArabic):q.OrderBy(x=>x.Case.Customer.FullNameEnglish??x.Case.Customer.FullNameArabic),"status"=>d?q.OrderByDescending(x=>x.Status):q.OrderBy(x=>x.Status),"updated"=>d?q.OrderByDescending(x=>x.EvaluatedAt??x.CreatedAt):q.OrderBy(x=>x.EvaluatedAt??x.CreatedAt),_=>d?q.OrderByDescending(x=>x.PromiseDate):q.OrderBy(x=>x.PromiseDate)};}
    private static string PersistedStatus(string value)=>value.Trim().ToUpperInvariant() switch{"PENDING" or "ACTIVE"=>CollectionsValues.PromiseStatuses.Active,"KEPT" or "FULFILLED"=>CollectionsValues.PromiseStatuses.Fulfilled,"BROKEN"=>CollectionsValues.PromiseStatuses.Broken,"CANCELLED"=>CollectionsValues.PromiseStatuses.Cancelled,_=>throw new HrValidationException("PTP status is invalid.")};
    private static string DisplayStatus(string value)=>value switch{CollectionsValues.PromiseStatuses.Active=>"PENDING",CollectionsValues.PromiseStatuses.Fulfilled=>"KEPT",_=>value};
    private static string Operational(string status,DateOnly date,DateOnly today)=>status==CollectionsValues.PromiseStatuses.Broken?"BROKEN":status!=CollectionsValues.PromiseStatuses.Active?DisplayStatus(status):date==today?"TODAY":date>today?"UPCOMING":"OVERDUE";
    private static DateOnly CairoToday(){var zone=TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,zone).DateTime);}
    private static void ValidatePage(int page,int size){if(page<1||size is not(20 or 50 or 100))throw new HrValidationException("Page size must be 20, 50, or 100.");}
}
