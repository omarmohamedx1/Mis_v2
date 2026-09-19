using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class BankDcrService(ApplicationDbContext db, ICurrentUserContext user, IBankPtpService ptpService, ICollectionsClassificationContext classification) : IBankDcrService
{
    private static readonly string[] Covers = ["CALL", "VISIT", "CALL_AND_VISIT"];
    private static readonly string[] Actions = ["PTP", "RTP", "PAID", "PAID_PARTIAL", "BROKEN", "VISIT", "WILL_VISIT", "NEGOTIATION", "UNREACHABLE", "FRAUD", "ISSUE", "FOLLOW_UP", "DECEASED"];
    private bool Has(string role) => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    private bool Global => Has(SystemRoleNames.Admin) || Has(SystemRoleNames.CollectionsOperationsManager);
    private bool Manager => Global || Has(SystemRoleNames.CollectionsSupervisor);
    private bool Collector => Has(SystemRoleNames.CollectionsCollector);
    private static TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime);

    public async Task<BankDcrPageDto> GetAsync(Guid bankId, BankDcrQuery request, CancellationToken token)
    {
        await RequireBankAsync(bankId, token); ValidatePage(request.Page, request.PageSize); var q = ScopedDcrs(bankId).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search)) { if (request.Search.Length > 160) throw new HrValidationException("Search cannot exceed 160 characters."); var t=request.Search.Trim().ToLower(); q=q.Where(x=>x.Case.CaseNumber.ToLower().Contains(t)||(x.Case.Customer.FullNameArabic!=null&&x.Case.Customer.FullNameArabic.ToLower().Contains(t))||(x.Case.Customer.FullNameEnglish!=null&&x.Case.Customer.FullNameEnglish.ToLower().Contains(t))||(x.Case.Customer.PrimaryPhone!=null&&x.Case.Customer.PrimaryPhone.Contains(t))||x.Feedback.ToLower().Contains(t)); }
        if(request.Date.HasValue)q=q.Where(x=>x.DcrDate==request.Date); if(!string.IsNullOrWhiteSpace(request.ActionCover))q=q.Where(x=>x.ActionCover==Cover(request.ActionCover)); if(!string.IsNullOrWhiteSpace(request.Action))q=q.Where(x=>x.Action==Action(request.Action));
        if(request.CollectorId.HasValue){if(!Manager&&request.CollectorId!=user.UserId)throw new HrForbiddenException("Collector filter is outside your scope.");if(Manager&&!await AuthorizedCollectors(bankId).AnyAsync(x=>x.Id==request.CollectorId,token))throw new HrForbiddenException("Collector filter is outside your team scope.");q=q.Where(x=>x.CreatedByUserId==request.CollectorId);}
        var total=await q.CountAsync(token);q=Sort(q,request.SortBy,request.SortDirection);var rows=await Project(q.Skip((request.Page-1)*request.PageSize).Take(request.PageSize)).ToArrayAsync(token);
        return new(rows,total,request.Page,request.PageSize,total==0?0:(int)Math.Ceiling(total/(double)request.PageSize),new(Manager,Collector,Zone.Id,Today));
    }

    public async Task<BankDcrItemDto> GetDetailsAsync(Guid bankId, Guid dcrId, CancellationToken token)
    { await RequireBankAsync(bankId,token);return await Project(ScopedDcrs(bankId).AsNoTracking().Where(x=>x.Id==dcrId)).SingleOrDefaultAsync(token)??throw new HrNotFoundException("DCR record was not found for this bank or user scope."); }

    public async Task<BankDcrItemDto> CreateAsync(Guid bankId, CreateBankDcrRequest request, CancellationToken token)
    {
        if(!Collector)throw new HrForbiddenException("Only authorized collectors can create DCR records.");await RequireBankAsync(bankId,token);var cover=Cover(request.ActionCover);var action=Action(request.Action);Validate(request,action);
        var item=await ScopedCases(bankId).SingleOrDefaultAsync(x=>x.Id==request.CaseId,token)??throw new HrNotFoundException("Portfolio case was not found for this bank or user scope.");
        if(item.AssignedCollectorId!=user.UserId)throw new HrForbiddenException("Collectors can create DCR records only for their assigned cases.");
        if(action=="BROKEN"&&!await db.CollectionPromisesToPay.AnyAsync(x=>x.Id==request.LinkedPtpId&&x.CaseId==item.Id&&x.Case.Portfolio.OrganizationId==bankId,token))throw new HrValidationException("The selected promise does not belong to this case and bank.");
        if(request.LinkedVisitId.HasValue&&!await db.CollectionFieldVisits.AnyAsync(x=>x.Id==request.LinkedVisitId&&x.CaseId==item.Id&&x.Case.Portfolio.OrganizationId==bankId,token))throw new HrValidationException("The selected visit does not belong to this case and bank.");
        var now=DateTimeOffset.UtcNow;var record=new CollectionDcr(bankId,item.Id,user.UserId,Today,cover,action,request.Feedback,request.Comment,request.PtpDate,request.PtpAmount,request.PaidDate,request.PaidAmount,request.FollowUpAt,request.VisitDate,action=="BROKEN"?request.LinkedPtpId:null,action=="VISIT"?request.LinkedVisitId:null,now);
        await using var tx=await db.Database.BeginTransactionAsync(token);db.CollectionDcrs.Add(record);
        if(action=="PTP") { var ptp=await ptpService.CreateAsync(bankId,new CreateBankPtpRequest(item.Id,request.PtpAmount!.Value,request.PtpDate!.Value,request.Comment),token);record.LinkPtp(ptp.Id,now); }
        else if(action=="BROKEN") { await ptpService.ChangeStatusAsync(bankId,request.LinkedPtpId!.Value,new ChangeBankPtpStatusRequest("BROKEN"),token); }
        else if(action=="FOLLOW_UP") { db.CollectionActivities.Add(new CollectionActivity(item.Id,CollectionsValues.ActivityTypes.FollowUp,"SCHEDULED",request.Feedback,cover,user.UserId,now,request.FollowUpAt)); }
        else { db.CollectionActivities.Add(new CollectionActivity(item.Id,"DCR",action,request.Feedback,cover,user.UserId,now,null)); }
        await db.SaveChangesAsync(token);await tx.CommitAsync(token);return await GetDetailsAsync(bankId,record.Id,token);
    }

    public async Task<IReadOnlyCollection<BankActivityCaseLookupDto>> CasesAsync(Guid bankId,string? search,CancellationToken token)
    {await RequireBankAsync(bankId,token);var q=ScopedCases(bankId).AsNoTracking();if(!string.IsNullOrWhiteSpace(search)){if(search.Length>160)throw new HrValidationException("Search cannot exceed 160 characters.");var t=search.Trim().ToLower();q=q.Where(x=>x.CaseNumber.ToLower().Contains(t)||(x.Customer.FullNameArabic!=null&&x.Customer.FullNameArabic.ToLower().Contains(t))||(x.Customer.FullNameEnglish!=null&&x.Customer.FullNameEnglish.ToLower().Contains(t))||(x.Customer.PrimaryPhone!=null&&x.Customer.PrimaryPhone.Contains(t)));}var ar=ApiTextLocalizer.IsArabic;return await q.OrderBy(x=>x.CaseNumber).Take(50).Select(x=>new BankActivityCaseLookupDto(x.Id,x.CaseNumber,ar?x.Customer.FullNameArabic??x.Customer.FullNameEnglish!:x.Customer.FullNameEnglish??x.Customer.FullNameArabic!,x.Customer.PrimaryPhone,x.OutstandingBalance,x.Status,x.AssignedCollectorId,x.AssignedCollector==null?null:x.AssignedCollector.FullName)).ToArrayAsync(token);}
    public async Task<IReadOnlyCollection<BankDcrCollectorDto>> CollectorsAsync(Guid bankId,CancellationToken token){await RequireBankAsync(bankId,token);if(!Manager)return [];return await AuthorizedCollectors(bankId).AsNoTracking().OrderBy(x=>x.FullName).Select(x=>new BankDcrCollectorDto(x.Id,x.FullName)).ToArrayAsync(token);}

    private IQueryable<CollectionCase> ScopedCases(Guid bankId)=>ScopedCasesCore(bankId).Apply(classification);
    private IQueryable<CollectionCase> ScopedCasesCore(Guid bankId){var q=db.CollectionCases.Where(x=>x.Portfolio.OrganizationId==bankId&&!x.IsArchived);if(Global)return q;if(Collector&&!Manager)return q.Where(x=>x.AssignedCollectorId==user.UserId);if(Manager)return q.Where(x=>(x.AssignedTeam!=null&&x.AssignedTeam.SupervisorId==user.UserId)||(x.AssignedTeamId==null&&db.CollectionUserAccess.Any(a=>a.UserId==user.UserId&&a.OrganizationId==bankId&&(a.PortfolioId==null||a.PortfolioId==x.PortfolioId))));return q.Where(_=>false);}
    private IQueryable<CollectionDcr> ScopedDcrs(Guid bankId){var cases=ScopedCases(bankId);return db.CollectionDcrs.Where(x=>x.BankId==bankId&&cases.Any(c=>c.Id==x.CaseId));}
    private IQueryable<User> AuthorizedCollectors(Guid bankId)=>db.Users.EligibleCollectors().ForSupervisorScope(db,user.UserId,Global);
    private async Task RequireBankAsync(Guid bankId,CancellationToken token){var exists=await db.CollectionClientOrganizations.AsNoTracking().AnyAsync(x=>x.Id==bankId&&x.IsActive&&(x.OrganizationType==CollectionsValues.OrganizationTypes.Bank||x.OrganizationType==CollectionsValues.OrganizationTypes.ConsumerFinance),token);if(!exists||(!Global&&!await db.CollectionUserAccess.AnyAsync(x=>x.UserId==user.UserId&&x.OrganizationId==bankId,token)&&!await ScopedCases(bankId).AnyAsync(token)))throw new HrNotFoundException("Organization was not found or is outside your authorized scope.");}
    private static IQueryable<BankDcrItemDto> Project(IQueryable<CollectionDcr> q){var ar=ApiTextLocalizer.IsArabic;return q.Select(x=>new BankDcrItemDto(x.Id,x.BankId,x.CaseId,x.Case.CaseNumber,ar?x.Case.Customer.FullNameArabic??x.Case.Customer.FullNameEnglish!:x.Case.Customer.FullNameEnglish??x.Case.Customer.FullNameArabic!,x.Case.Customer.PrimaryPhone,x.DcrDate,x.ActionCover,x.Action,x.Feedback,x.Comment,x.CreatedByUserId,x.CreatedByUser.FullName,x.PtpDate,x.PtpAmount,x.PaidDate,x.PaidAmount,x.FollowUpAt,x.VisitDate,x.LinkedPtpId,x.LinkedPtp==null?null:x.LinkedPtp.Status,x.LinkedVisitId,x.CreatedAt));}
    private static IQueryable<CollectionDcr> Sort(IQueryable<CollectionDcr> q,string? field,string? direction){var d=direction?.Equals("desc",StringComparison.OrdinalIgnoreCase)!=false;return field?.Trim().ToLowerInvariant() switch{"date"=>d?q.OrderByDescending(x=>x.DcrDate):q.OrderBy(x=>x.DcrDate),"customer"=>d?q.OrderByDescending(x=>x.Case.Customer.FullNameEnglish??x.Case.Customer.FullNameArabic):q.OrderBy(x=>x.Case.Customer.FullNameEnglish??x.Case.Customer.FullNameArabic),"action"=>d?q.OrderByDescending(x=>x.Action):q.OrderBy(x=>x.Action),"createdat" or "created"=>d?q.OrderByDescending(x=>x.CreatedAt):q.OrderBy(x=>x.CreatedAt),null or ""=>q.OrderByDescending(x=>x.CreatedAt),_=>throw new HrValidationException("DCR sort field is invalid.")};}
    private static string Cover(string value){var v=value?.Trim().ToUpperInvariant()??"";if(!Covers.Contains(v))throw new HrValidationException("Action cover must be CALL, VISIT, or CALL_AND_VISIT.");return v;}
    private static string Action(string value){var v=value?.Trim().ToUpperInvariant().Replace(' ','_')??"";if(!Actions.Contains(v))throw new HrValidationException("DCR action is invalid.");return v;}
    private static void Validate(CreateBankDcrRequest r,string action){if(r.CaseId==Guid.Empty)throw new HrValidationException("Case is required.");if(string.IsNullOrWhiteSpace(r.Feedback))throw new HrValidationException("Feedback is required.");if(r.Feedback.Length>2000||r.Comment?.Length>2000)throw new HrValidationException("Feedback and comment cannot exceed 2000 characters.");if(action=="PTP"&&(r.PtpDate is null||r.PtpAmount is null or <=0))throw new HrValidationException("PTP date and a positive PTP amount are required.");if(action=="PTP"&&r.PtpDate<Today)throw new HrValidationException("PTP date cannot be in the past.");if(action is "PAID" or "PAID_PARTIAL"&&(r.PaidDate is null||r.PaidAmount is null or <=0))throw new HrValidationException("Paid date and a positive paid amount are required.");if(action=="FOLLOW_UP"&&r.FollowUpAt is null)throw new HrValidationException("Follow-up date is required.");if(action=="FOLLOW_UP"&&r.FollowUpAt<=DateTimeOffset.UtcNow)throw new HrValidationException("Follow-up date must be in the future.");if(action=="WILL_VISIT"&&r.VisitDate is null)throw new HrValidationException("Visit date is required.");if(action=="BROKEN"&&r.LinkedPtpId is null)throw new HrValidationException("Select the promise to pay being marked broken.");}
    private static void ValidatePage(int page,int size){if(page<1||size is not(20 or 50 or 100))throw new HrValidationException("Page size must be 20, 50, or 100.");}
}
