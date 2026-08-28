using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Services;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class CollectionsService : ICollectionsService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserContext _user;
    private readonly IFinancePostingService _financePosting;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CollectionsService(ApplicationDbContext db, ICurrentUserContext user, IFinancePostingService financePosting) { _db = db; _user = user; _financePosting = financePosting; }

    public async Task<CollectionDashboardDto> GetDashboardAsync(Guid? organizationId, CancellationToken token)
    {
        await EvaluateDuePromisesAsync(token);
        var cases = AccessibleCases().Where(x => organizationId == null || x.Portfolio.OrganizationId == organizationId);
        var today = CairoToday(); var monthStart = new DateOnly(today.Year, today.Month, 1);
        var payments = _db.CollectionPayments.AsNoTracking().Where(x => x.Status == CollectionsValues.PaymentStatuses.Approved && cases.Any(c => c.Id == x.CaseId));
        var caseMetrics = await cases.GroupBy(_ => 1).Select(g => new
        {
            Count = g.Count(), Outstanding = g.Sum(x => x.OutstandingBalance), Overdue = g.Sum(x => x.OverdueBalance),
            Assigned = g.Count(x => x.AssignedCollectorId != null), Unassigned = g.Count(x => x.AssignedCollectorId == null),
            Collectors = g.Where(x => x.AssignedCollectorId != null).Select(x => x.AssignedCollectorId).Distinct().Count(),
            HighRisk = g.Count(x => x.PriorityScore >= 70)
        }).SingleOrDefaultAsync(token);
        var collectedToday = await payments.Where(x => x.PaymentDate == today).SumAsync(x => (decimal?)x.Amount, token) ?? 0;
        var collectedMtd = await payments.Where(x => x.PaymentDate >= monthStart && x.PaymentDate <= today).SumAsync(x => (decimal?)x.Amount, token) ?? 0;
        var portfolioIds = cases.Select(x => x.PortfolioId).Distinct();
        var target = await _db.CollectionPortfolios.AsNoTracking().Where(x => portfolioIds.Contains(x.Id)).SumAsync(x => x.TargetAmount ?? 0, token);
        var promiseQuery = _db.CollectionPromisesToPay.AsNoTracking().Where(x => cases.Any(c => c.Id == x.CaseId));
        var activePromises = await promiseQuery.CountAsync(x => x.Status == CollectionsValues.PromiseStatuses.Active || x.Status == CollectionsValues.PromiseStatuses.Upcoming || x.Status == CollectionsValues.PromiseStatuses.DueToday, token);
        var dueToday = await promiseQuery.CountAsync(x => x.PromiseDate == today && x.Status != CollectionsValues.PromiseStatuses.Fulfilled && x.Status != CollectionsValues.PromiseStatuses.Cancelled, token);
        var broken = await promiseQuery.CountAsync(x => x.Status == CollectionsValues.PromiseStatuses.Broken || x.Status == CollectionsValues.PromiseStatuses.PartiallyFulfilled, token);
        var (todayUtcStart, todayUtcEnd) = CairoDayUtcRange(today);
        var visits = await _db.CollectionFieldVisits.AsNoTracking().CountAsync(x => cases.Any(c => c.Id == x.CaseId) && x.ScheduledAt >= todayUtcStart && x.ScheduledAt < todayUtcEnd, token);
        var pendingReviews = await _db.CollectionPayments.AsNoTracking().CountAsync(x => cases.Any(c => c.Id == x.CaseId) && (x.Status == CollectionsValues.PaymentStatuses.Submitted || x.Status == CollectionsValues.PaymentStatuses.UnderReview), token);
        var complaints = await _db.CollectionComplaints.AsNoTracking().CountAsync(x => cases.Any(c => c.Id == x.CaseId) && x.Status != CollectionsValues.ComplaintStatuses.Closed && x.Status != CollectionsValues.ComplaintStatuses.Resolved, token);
        return new CollectionDashboardDto(caseMetrics?.Count ?? 0, caseMetrics?.Outstanding ?? 0, caseMetrics?.Overdue ?? 0, caseMetrics?.Assigned ?? 0, caseMetrics?.Unassigned ?? 0,
            caseMetrics?.Collectors ?? 0, collectedToday, collectedMtd, target <= 0 ? 0 : Math.Round(collectedMtd / target * 100, 2), activePromises, dueToday, broken, visits, pendingReviews, complaints, caseMetrics?.HighRisk ?? 0);
    }

    public async Task<PagedResultDto<ClientOrganizationCardDto>> GetClientsAsync(int page, int pageSize, string? search, string? type, bool? active, CancellationToken token)
    {
        ValidatePage(page, pageSize); var isArabic = ApiTextLocalizer.IsArabic; var allowedCases = AccessibleCases();
        var allowedOrganizationIds = allowedCases.Select(x => x.Portfolio.OrganizationId).Distinct();
        var query = _db.CollectionClientOrganizations.AsNoTracking().Where(x => allowedOrganizationIds.Contains(x.Id) || IsGlobalRole());
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); query = query.Where(x => x.Code.ToLower().Contains(term) || x.NameArabic.ToLower().Contains(term) || x.NameEnglish.ToLower().Contains(term)); }
        if (!string.IsNullOrWhiteSpace(type)) { var normalized = type.Trim().ToUpperInvariant(); query = query.Where(x => x.OrganizationType == normalized); }
        if (active.HasValue) query = query.Where(x => x.IsActive == active);
        var total = await query.CountAsync(token); var today = CairoToday(); var monthStart = new DateOnly(today.Year, today.Month, 1);
        var organizations = await query.OrderBy(x => isArabic ? x.NameArabic : x.NameEnglish).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(token);
        var ids = organizations.Select(x => x.Id).ToArray();
        var metrics = await allowedCases.Where(x => ids.Contains(x.Portfolio.OrganizationId)).GroupBy(x => x.Portfolio.OrganizationId).Select(g => new { Id = g.Key, Cases = g.Count(), Outstanding = g.Sum(x => x.OutstandingBalance), Assigned = g.Count(x => x.AssignedCollectorId != null), Unassigned = g.Count(x => x.AssignedCollectorId == null), Collectors = g.Where(x => x.AssignedCollectorId != null).Select(x => x.AssignedCollectorId).Distinct().Count(), High = g.Count(x => x.PriorityScore >= 70) }).ToDictionaryAsync(x => x.Id, token);
        var portfolios = await _db.CollectionPortfolios.AsNoTracking().Where(x => ids.Contains(x.OrganizationId) && x.IsActive).GroupBy(x => x.OrganizationId).Select(g => new { Id = g.Key, Count = g.Count(), Target = g.Sum(x => x.TargetAmount ?? 0) }).ToDictionaryAsync(x => x.Id, token);
        var paymentMetrics = await _db.CollectionPayments.AsNoTracking().Where(x => x.Status == CollectionsValues.PaymentStatuses.Approved && x.PaymentDate >= monthStart && allowedCases.Any(c => c.Id == x.CaseId) && ids.Contains(x.Case.Portfolio.OrganizationId)).GroupBy(x => x.Case.Portfolio.OrganizationId).Select(g => new { Id = g.Key, Today = g.Where(x => x.PaymentDate == today).Sum(x => x.Amount), Mtd = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, token);
        var promiseMetrics = await _db.CollectionPromisesToPay.AsNoTracking().Where(x => allowedCases.Any(c => c.Id == x.CaseId) && ids.Contains(x.Case.Portfolio.OrganizationId)).GroupBy(x => x.Case.Portfolio.OrganizationId).Select(g => new { Id = g.Key, Amount = g.Where(x => x.Status != CollectionsValues.PromiseStatuses.Cancelled).Sum(x => x.PromisedAmount), Broken = g.Where(x => x.Status == CollectionsValues.PromiseStatuses.Broken || x.Status == CollectionsValues.PromiseStatuses.PartiallyFulfilled).Sum(x => x.PromisedAmount) }).ToDictionaryAsync(x => x.Id, token);
        var items = organizations.Select(x => { metrics.TryGetValue(x.Id, out var m); portfolios.TryGetValue(x.Id, out var p); paymentMetrics.TryGetValue(x.Id, out var pay); promiseMetrics.TryGetValue(x.Id, out var ptp); var achievement = p is null || p.Target <= 0 ? 0 : Math.Round((pay?.Mtd ?? 0) / p.Target * 100, 2); var health = m is null || m.Cases == 0 ? "NO_DATA" : m.High * 100m / m.Cases >= 30 ? "AT_RISK" : achievement >= 80 ? "HEALTHY" : "WATCH"; return new ClientOrganizationCardDto(x.Id, x.Code, isArabic ? x.NameArabic : x.NameEnglish, x.OrganizationType, string.IsNullOrWhiteSpace(x.LogoStorageKey) ? null : CollectionsBrandingService.LogoUrl(x.Id), p?.Count ?? 0, m?.Cases ?? 0, m?.Outstanding ?? 0, m?.Assigned ?? 0, m?.Unassigned ?? 0, m?.Collectors ?? 0, pay?.Today ?? 0, achievement, ptp?.Amount ?? 0, ptp?.Broken ?? 0, health, x.IsActive); }).ToArray();
        return Page(items, total, page, pageSize);
    }

    public async Task<PagedResultDto<CollectionCaseListItemDto>> GetCasesAsync(CollectionFilters filters, CancellationToken token)
    {
        ValidatePage(filters.Page, filters.PageSize); var query = ApplyCaseFilters(AccessibleCases().AsNoTracking(), filters); var total = await query.CountAsync(token);
        var rows = await ProjectCases(query.OrderByDescending(x => x.PriorityScore).ThenByDescending(x => x.OverdueBalance).Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize)).ToArrayAsync(token);
        return Page(rows, total, filters.Page, filters.PageSize);
    }

    public async Task<CollectionCaseDetailsDto> GetCaseAsync(Guid caseId, bool revealSensitive, CancellationToken token)
    {
        await EvaluateDuePromisesAsync(token);
        if (revealSensitive && !CanRevealSensitive()) throw new HrForbiddenException("You do not have permission to reveal sensitive customer data.");
        var isArabic = ApiTextLocalizer.IsArabic;
        var item = await AccessibleCases().AsNoTracking().Where(x => x.Id == caseId).Select(x => new
        {
            x.Id, x.CaseNumber, Client = isArabic ? x.Portfolio.Organization.NameArabic : x.Portfolio.Organization.NameEnglish, Portfolio = isArabic ? x.Portfolio.NameArabic : x.Portfolio.NameEnglish,
            x.Customer.CustomerCode, Customer = isArabic ? x.Customer.FullNameArabic ?? x.Customer.FullNameEnglish! : x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic!, x.Customer.NationalId, x.Customer.PrimaryPhone, x.Customer.AlternatePhone,
            Address = isArabic ? x.Customer.AddressArabic ?? x.Customer.AddressEnglish : x.Customer.AddressEnglish ?? x.Customer.AddressArabic, x.Customer.Governorate, x.Customer.Area,
            x.AccountReference, x.ContractReference, x.ProductType, x.OriginalAmount, x.OutstandingBalance, x.OverdueBalance, x.Penalties, x.Fees, x.TotalDue, x.DaysPastDue,
            Bucket = isArabic ? x.CurrentBucket.NameArabic : x.CurrentBucket.NameEnglish, x.Status, x.Priority, x.PriorityScore, x.PriorityExplanation, Collector = x.AssignedCollector == null ? null : x.AssignedCollector.FullName
        }).SingleOrDefaultAsync(token) ?? throw new HrNotFoundException("Collection case was not found.");
        var activities = await _db.CollectionActivities.AsNoTracking().Where(x => x.CaseId == caseId).OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new CollectionActivityDto(x.Id, x.ActivityType, x.Result, x.Notes, x.Channel, x.CreatedBy.FullName, x.CreatedAt, x.NextFollowUpAt)).ToArrayAsync(token);
        var promises = await ProjectPromises(_db.CollectionPromisesToPay.AsNoTracking().Where(x => x.CaseId == caseId).OrderByDescending(x => x.CreatedAt)).ToArrayAsync(token);
        var payments = await ProjectPayments(_db.CollectionPayments.AsNoTracking().Where(x => x.CaseId == caseId).OrderByDescending(x => x.SubmittedAt)).ToArrayAsync(token);
        var hasBreachedComplaint = await _db.CollectionComplaints.AsNoTracking().AnyAsync(x => x.CaseId == caseId && x.SlaDueAt < DateTimeOffset.UtcNow && x.Status != CollectionsValues.ComplaintStatuses.Closed && x.Status != CollectionsValues.ComplaintStatuses.Resolved, token);
        var nextBestAction = ResolveNextBestAction(item.PriorityScore, activities, promises, payments, hasBreachedComplaint, isArabic);
        if (revealSensitive) { _db.CollectionAuditLogs.Add(new CollectionAuditLog(_user.UserId, "SensitiveDataRevealed", nameof(CollectionCustomer), caseId, caseId, null, null, "WEB", DateTimeOffset.UtcNow)); await _db.SaveChangesAsync(token); }
        return new CollectionCaseDetailsDto(item.Id, item.CaseNumber, item.Client, item.Portfolio, item.CustomerCode, item.Customer,
            revealSensitive ? item.NationalId ?? "" : CollectionRules.MaskNationalId(item.NationalId), revealSensitive ? item.PrimaryPhone ?? "" : CollectionRules.MaskPhone(item.PrimaryPhone), revealSensitive ? item.AlternatePhone ?? "" : CollectionRules.MaskPhone(item.AlternatePhone),
            revealSensitive ? item.Address ?? "" : MaskAddress(item.Address), item.Governorate, item.Area, item.AccountReference, item.ContractReference, item.ProductType, item.OriginalAmount, item.OutstandingBalance, item.OverdueBalance,
            item.Penalties, item.Fees, item.TotalDue, item.DaysPastDue, item.Bucket, item.Status, item.Priority, item.PriorityScore, LocalizePriority(item.PriorityExplanation), item.Collector, revealSensitive,
            nextBestAction.Code, nextBestAction.Reason, activities, promises, payments);
    }

    private static (string Code, string Reason) ResolveNextBestAction(
        int priorityScore,
        IReadOnlyCollection<CollectionActivityDto> activities,
        IReadOnlyCollection<PromiseToPayDto> promises,
        IReadOnlyCollection<CollectionPaymentDto> payments,
        bool hasBreachedComplaint,
        bool isArabic)
    {
        if (hasBreachedComplaint)
            return ("ESCALATE_COMPLAINT", isArabic ? "توجد شكوى مفتوحة تجاوزت مهلة اتفاقية مستوى الخدمة." : "An open complaint has breached its SLA deadline.");
        if (payments.Any(x => x.Status is CollectionsValues.PaymentStatuses.Submitted or CollectionsValues.PaymentStatuses.UnderReview))
            return ("REVIEW_PAYMENT", isArabic ? "يوجد تحصيل مقدم ينتظر المراجعة والاعتماد." : "A submitted collection is awaiting review and approval.");
        if (promises.Any(x => x.Status is CollectionsValues.PromiseStatuses.Broken or CollectionsValues.PromiseStatuses.PartiallyFulfilled))
            return ("FOLLOW_UP_BROKEN_PTP", isArabic ? "يوجد وعد سداد مكسور أو منفذ جزئيًا ويحتاج متابعة فورية." : "A broken or partially fulfilled promise requires immediate follow-up.");
        if (promises.Any(x => x.Status == CollectionsValues.PromiseStatuses.DueToday))
            return ("FOLLOW_UP_DUE_PTP", isArabic ? "يوجد وعد سداد مستحق اليوم." : "A promise to pay is due today.");
        if (activities.Any(x => x.NextFollowUpAt.HasValue && x.NextFollowUpAt <= DateTimeOffset.UtcNow))
            return ("CONTACT_CUSTOMER", isArabic ? "موعد المتابعة المسجل مستحق الآن." : "The recorded follow-up time is due.");
        if (priorityScore >= 70)
            return ("CONTACT_CUSTOMER", isArabic ? "درجة أولوية الحالة مرتفعة وتستدعي التواصل." : "The case has a high priority score and requires contact.");
        return ("MONITOR_CASE", isArabic ? "لا يوجد إجراء عاجل؛ استمر في المتابعة وفق الجدول." : "No urgent action is due; continue scheduled monitoring.");
    }

    public async Task<WorkQueueDto> GetMyWorkAsync(CancellationToken token)
    {
        await EvaluateDuePromisesAsync(token);
        var today = CairoToday(); var (_, todayUtcEnd) = CairoDayUtcRange(today); var cases = AccessibleCases().AsNoTracking();
        var calls = await ProjectCases(cases.Where(x => x.NextFollowUpAt != null && x.NextFollowUpAt < todayUtcEnd).OrderBy(x => x.NextFollowUpAt).Take(10)).ToArrayAsync(token);
        var high = await ProjectCases(cases.Where(x => x.PriorityScore >= 70).OrderByDescending(x => x.PriorityScore).Take(10)).ToArrayAsync(token);
        var promises = _db.CollectionPromisesToPay.AsNoTracking().Where(x => cases.Any(c => c.Id == x.CaseId));
        var due = await ProjectPromises(promises.Where(x => x.PromiseDate == today && x.Status != CollectionsValues.PromiseStatuses.Fulfilled && x.Status != CollectionsValues.PromiseStatuses.Cancelled).Take(10)).ToArrayAsync(token);
        var broken = await ProjectPromises(promises.Where(x => x.Status == CollectionsValues.PromiseStatuses.Broken || x.Status == CollectionsValues.PromiseStatuses.PartiallyFulfilled).OrderByDescending(x => x.PromiseDate).Take(10)).ToArrayAsync(token);
        var (todayUtcStart, _) = CairoDayUtcRange(today);
        var visits = await _db.CollectionFieldVisits.AsNoTracking().CountAsync(x => cases.Any(c => c.Id == x.CaseId) && x.ScheduledAt >= todayUtcStart && x.ScheduledAt < todayUtcEnd, token);
        var reviews = await _db.CollectionPayments.AsNoTracking().CountAsync(x => cases.Any(c => c.Id == x.CaseId) && (x.Status == CollectionsValues.PaymentStatuses.Submitted || x.Status == CollectionsValues.PaymentStatuses.UnderReview), token);
        var complaints = await _db.CollectionComplaints.AsNoTracking().CountAsync(x => cases.Any(c => c.Id == x.CaseId) && x.Status != CollectionsValues.ComplaintStatuses.Closed && x.Status != CollectionsValues.ComplaintStatuses.Resolved, token);
        return new WorkQueueDto(calls, high, due, broken, visits, reviews, complaints);
    }

    public async Task<CollectionActivityDto> CreateActivityAsync(Guid caseId, CreateActivityRequest request, CancellationToken token)
    {
        EnsureOperationalWrite();
        var collectionCase = await AccessibleCases().SingleOrDefaultAsync(x => x.Id == caseId, token) ?? throw new HrNotFoundException("Collection case was not found.");
        if (string.IsNullOrWhiteSpace(request.ActivityType)) throw new HrValidationException("Activity type is required."); var now = DateTimeOffset.UtcNow;
        var activity = new CollectionActivity(caseId, request.ActivityType, request.Result, request.Notes, request.Channel, _user.UserId, now, request.NextFollowUpAt);
        if (request.ActivityType.Equals(CollectionsValues.ActivityTypes.Call, StringComparison.OrdinalIgnoreCase) || request.ActivityType.Equals(CollectionsValues.ActivityTypes.Email, StringComparison.OrdinalIgnoreCase) || request.ActivityType.Equals(CollectionsValues.ActivityTypes.Sms, StringComparison.OrdinalIgnoreCase)) collectionCase.RecordContact(now, request.NextFollowUpAt);
        _db.CollectionActivities.Add(activity); AddAudit("ActivityCreated", activity, caseId, null, request); await _db.SaveChangesAsync(token);
        return new CollectionActivityDto(activity.Id, activity.ActivityType, activity.Result, activity.Notes, activity.Channel, _user.Username, activity.CreatedAt, activity.NextFollowUpAt);
    }

    public async Task<PromiseToPayDto> CreatePromiseAsync(Guid caseId, CreatePromiseRequest request, CancellationToken token)
    {
        EnsureOperationalWrite();
        var collectionCase = await AccessibleCases().SingleOrDefaultAsync(x => x.Id == caseId, token) ?? throw new HrNotFoundException("Collection case was not found.");
        if (request.PromisedAmount <= 0 || request.PromiseDate < CairoToday()) throw new HrValidationException("Promise amount must be positive and promise date cannot be in the past.");
        var now = DateTimeOffset.UtcNow; var promise = new PromiseToPay(caseId, request.PromisedAmount, request.PromiseDate, _user.UserId, request.Channel, request.Notes, now);
        var policy = await GetPtpPolicyAsync(caseId, token); var evaluation = CollectionRules.EvaluatePromise(promise.PromisedAmount, 0, promise.PromiseDate, CairoToday(), policy.GraceDays, policy.ToleranceAmount); promise.ApplyEvaluation(evaluation.Status, 0, now);
        _db.CollectionPromisesToPay.Add(promise); _db.CollectionActivities.Add(new CollectionActivity(caseId, CollectionsValues.ActivityTypes.PtpCreated, promise.Status, request.Notes, request.Channel, _user.UserId, now, null));
        AddAudit("PromiseCreated", promise, caseId, null, request); await _db.SaveChangesAsync(token);
        return new PromiseToPayDto(promise.Id, caseId, collectionCase.CaseNumber, await CustomerNameAsync(collectionCase.CustomerId, token), promise.PromisedAmount, promise.PromiseDate, 0, promise.Status, _user.Username, promise.Channel, promise.CreatedAt);
    }

    public async Task<PagedResultDto<PromiseToPayDto>> GetPromisesAsync(PromiseFilters filters, CancellationToken token)
    {
        await EvaluateDuePromisesAsync(token);
        ValidatePage(filters.Page, filters.PageSize); var cases = AccessibleCases(); var query = _db.CollectionPromisesToPay.AsNoTracking().Where(x => cases.Any(c => c.Id == x.CaseId));
        if (filters.OrganizationId.HasValue) query = query.Where(x => x.Case.Portfolio.OrganizationId == filters.OrganizationId);
        if (filters.CollectorId.HasValue) query = query.Where(x => x.CollectorId == filters.CollectorId); if (!string.IsNullOrWhiteSpace(filters.Status)) { var status = filters.Status.Trim().ToUpperInvariant(); query = query.Where(x => x.Status == status); }
        if (filters.From.HasValue) query = query.Where(x => x.PromiseDate >= filters.From); if (filters.To.HasValue) query = query.Where(x => x.PromiseDate <= filters.To);
        if (!string.IsNullOrWhiteSpace(filters.Search)) { var term = filters.Search.Trim().ToLower(); query = query.Where(x => x.Case.CaseNumber.ToLower().Contains(term) || x.Case.Customer.CustomerCode.ToLower().Contains(term) || (x.Case.Customer.FullNameArabic != null && x.Case.Customer.FullNameArabic.ToLower().Contains(term)) || (x.Case.Customer.FullNameEnglish != null && x.Case.Customer.FullNameEnglish.ToLower().Contains(term))); }
        var total = await query.CountAsync(token); var rows = await ProjectPromises(query.OrderBy(x => x.PromiseDate).ThenByDescending(x => x.CreatedAt).Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize)).ToArrayAsync(token); return Page(rows, total, filters.Page, filters.PageSize);
    }

    public async Task<CollectionPaymentDto> SubmitPaymentAsync(Guid caseId, SubmitPaymentRequest request, CancellationToken token)
    {
        EnsureOperationalWrite();
        var collectionCase = await AccessibleCases().SingleOrDefaultAsync(x => x.Id == caseId, token) ?? throw new HrNotFoundException("Collection case was not found.");
        if (request.Amount <= 0 || request.PaymentDate > CairoToday() || string.IsNullOrWhiteSpace(request.ReferenceNumber)) throw new HrValidationException("A positive amount, non-future payment date, and reference number are required.");
        var duplicate = await _db.CollectionPayments.AnyAsync(x => x.ReferenceNumber.ToLower() == request.ReferenceNumber.Trim().ToLower(), token); if (duplicate) throw new HrConflictException("A payment with this reference number already exists.");
        var payment = new CollectionPayment(caseId, request.Amount, request.PaymentDate, request.Method, request.ReferenceNumber, _user.UserId, null, DateTimeOffset.UtcNow, request.CurrencyCode);
        _db.CollectionPayments.Add(payment); _db.CollectionActivities.Add(new CollectionActivity(caseId, CollectionsValues.ActivityTypes.Payment, payment.Status, null, request.Method, _user.UserId, payment.SubmittedAt, null)); AddAudit("PaymentSubmitted", payment, caseId, null, request); await _db.SaveChangesAsync(token);
        return new CollectionPaymentDto(payment.Id, caseId, collectionCase.CaseNumber, await CustomerNameAsync(collectionCase.CustomerId, token), payment.Amount, payment.PaymentDate, payment.Method, payment.ReferenceNumber, payment.Status, _user.Username, payment.SubmittedAt, null, null, null);
    }

    public async Task<CollectionPaymentDto> ReviewPaymentAsync(Guid paymentId, ReviewPaymentRequest request, CancellationToken token)
    {
        if (!CanReviewPayments()) throw new HrForbiddenException("You do not have permission to review payments.");
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var payment = await _db.CollectionPayments.Include(x => x.Case).ThenInclude(x => x.Customer).Include(x => x.SubmittedBy).SingleOrDefaultAsync(x => x.Id == paymentId && AccessibleCases().Any(c => c.Id == x.CaseId), token) ?? throw new HrNotFoundException("Payment was not found.");
        var before = new { payment.Status, payment.VerifiedById, payment.RejectionReason }; payment.Review(_user.UserId, request.Approve, request.RejectionReason, true, DateTimeOffset.UtcNow);
        if (request.Approve)
        {
            await _financePosting.PostApprovedCollectionAsync(payment, payment.Case, token);
            payment.Case.RecordApprovedPayment(payment.Amount, payment.PaymentDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), DateTimeOffset.UtcNow);
            var policy = await GetPtpPolicyAsync(payment.CaseId, token); var promises = await _db.CollectionPromisesToPay.Where(x => x.CaseId == payment.CaseId && x.Status != CollectionsValues.PromiseStatuses.Cancelled && x.Status != CollectionsValues.PromiseStatuses.Rescheduled).ToArrayAsync(token);
            foreach (var promise in promises)
            {
                var start = DateOnly.FromDateTime(promise.CreatedAt.UtcDateTime); var end = promise.PromiseDate.AddDays(policy.GraceDays);
                var paidBefore = await _db.CollectionPayments.AsNoTracking().Where(x => x.CaseId == payment.CaseId && x.Id != payment.Id && x.Status == CollectionsValues.PaymentStatuses.Approved && x.PaymentDate >= start && x.PaymentDate <= end).SumAsync(x => (decimal?)x.Amount, token) ?? 0;
                var currentAmount = payment.PaymentDate >= start && payment.PaymentDate <= end ? payment.Amount : 0; var evaluation = CollectionRules.EvaluatePromise(promise.PromisedAmount, paidBefore + currentAmount, promise.PromiseDate, CairoToday(), policy.GraceDays, policy.ToleranceAmount); var previousStatus = promise.Status; var previousPaid = promise.ActualPaidAmount; promise.ApplyEvaluation(evaluation.Status, evaluation.PaidAmount, DateTimeOffset.UtcNow);
                if (previousStatus != promise.Status || previousPaid != promise.ActualPaidAmount) _db.CollectionAuditLogs.Add(new CollectionAuditLog(null, "PromiseAutomaticallyEvaluated", nameof(PromiseToPay), promise.Id, promise.CaseId, JsonSerializer.Serialize(new { Status = previousStatus, ActualPaidAmount = previousPaid }, JsonOptions), JsonSerializer.Serialize(new { promise.Status, promise.ActualPaidAmount }, JsonOptions), "AUTOMATION", DateTimeOffset.UtcNow));
            }
        }
        AddAudit(request.Approve ? "PaymentApproved" : "PaymentRejected", payment, payment.CaseId, before, new { payment.Status, payment.VerifiedById, payment.RejectionReason }); await _db.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return new CollectionPaymentDto(payment.Id, payment.CaseId, payment.Case.CaseNumber, LocalizedCustomerName(payment.Case.Customer), payment.Amount, payment.PaymentDate, payment.Method, payment.ReferenceNumber, payment.Status, payment.SubmittedBy.FullName, payment.SubmittedAt, _user.Username, payment.VerifiedAt, payment.RejectionReason);
    }

    public async Task<PagedResultDto<CollectionPaymentDto>> GetPaymentsAsync(PaymentFilters filters, CancellationToken token)
    {
        ValidatePage(filters.Page, filters.PageSize); var cases = AccessibleCases(); var query = _db.CollectionPayments.AsNoTracking().Where(x => cases.Any(c => c.Id == x.CaseId));
        if (filters.OrganizationId.HasValue) query = query.Where(x => x.Case.Portfolio.OrganizationId == filters.OrganizationId); if (!string.IsNullOrWhiteSpace(filters.Status)) { var status = filters.Status.Trim().ToUpperInvariant(); query = query.Where(x => x.Status == status); }
        if (filters.From.HasValue) query = query.Where(x => x.PaymentDate >= filters.From); if (filters.To.HasValue) query = query.Where(x => x.PaymentDate <= filters.To);
        if (!string.IsNullOrWhiteSpace(filters.Search)) { var term = filters.Search.Trim().ToLower(); query = query.Where(x => x.ReferenceNumber.ToLower().Contains(term) || x.Case.CaseNumber.ToLower().Contains(term) || x.Case.Customer.CustomerCode.ToLower().Contains(term)); }
        var total = await query.CountAsync(token); var rows = await ProjectPayments(query.OrderByDescending(x => x.SubmittedAt).Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize)).ToArrayAsync(token); return Page(rows, total, filters.Page, filters.PageSize);
    }

    public async Task<AssignmentPreviewDto> PreviewAssignmentAsync(IReadOnlyCollection<Guid> caseIds, Guid collectorId, CancellationToken token)
    {
        if (!CanAssign()) throw new HrForbiddenException("You do not have permission to assign cases."); var ids = NormalizeIds(caseIds);
        var accessibleCount = await AccessibleCases().CountAsync(x => ids.Contains(x.Id), token); if (accessibleCount != ids.Length) throw new HrForbiddenException("One or more cases are outside your data scope.");
        var collector = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == collectorId && x.IsActive && x.UserRoles.Any(r => r.Role.Name == SystemRoleNames.CollectionsCollector), token) ?? throw new HrValidationException("A valid active collector is required.");
        var workload = await _db.CollectionCases.CountAsync(x => x.AssignedCollectorId == collectorId && x.Status == CollectionsValues.CaseStatuses.Active, token);
        return new AssignmentPreviewDto(ids.Length, [new AssignmentPreviewItemDto(collector.Id, collector.FullName, workload, ids.Length, workload + ids.Length)]);
    }

    public async Task<AssignmentPreviewDto> AssignCasesAsync(BulkAssignmentRequest request, CancellationToken token)
    {
        if (!request.Confirmed) throw new HrValidationException("Assignment confirmation is required."); var preview = await PreviewAssignmentAsync(request.CaseIds, request.CollectorId, token);
        var activeMembership = await _db.CollectionTeamMembers.AsNoTracking().Where(x => x.UserId == request.CollectorId && x.IsActive && (!request.TeamId.HasValue || x.TeamId == request.TeamId)).Select(x => new { x.TeamId, x.Team.SupervisorId }).FirstOrDefaultAsync(token);
        if (request.TeamId.HasValue && activeMembership is null) throw new HrValidationException("The collector is not an active member of the selected team.");
        if (HasRole(SystemRoleNames.CollectionsSupervisor) && activeMembership?.SupervisorId != _user.UserId) throw new HrForbiddenException("Supervisors can assign only within their own team.");
        var assignedTeamId = request.TeamId ?? activeMembership?.TeamId;
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token); var ids = NormalizeIds(request.CaseIds);
        var cases = await AccessibleCases().Where(x => ids.Contains(x.Id)).ToArrayAsync(token); var now = DateTimeOffset.UtcNow;
        foreach (var item in cases) { var previous = item.AssignedCollectorId; item.Assign(request.CollectorId, assignedTeamId, now); _db.CollectionAssignmentHistory.Add(new CollectionAssignmentHistory(item.Id, previous, request.CollectorId, _user.UserId, assignedTeamId, request.Reason, CollectionsValues.AssignmentSources.Manual, null, now)); _db.CollectionActivities.Add(new CollectionActivity(item.Id, CollectionsValues.ActivityTypes.Assignment, previous.HasValue ? "REASSIGNED" : "ASSIGNED", request.Reason, null, _user.UserId, now, null)); AddAudit(previous.HasValue ? "CaseReassigned" : "CaseAssigned", item, item.Id, new { AssignedCollectorId = previous }, new { item.AssignedCollectorId, item.AssignedTeamId }); }
        await _db.SaveChangesAsync(token); await transaction.CommitAsync(token); return preview;
    }

    public Task<AutoAssignmentPreviewDto> PreviewAutomaticAssignmentAsync(AutoAssignmentRequest request, CancellationToken token)
    { if (request.Confirmed) throw new HrValidationException("Preview requests cannot be confirmed."); return BuildAutomaticPlanAsync(request, token); }

    public async Task<AutoAssignmentPreviewDto> ApplyAutomaticAssignmentAsync(AutoAssignmentRequest request, CancellationToken token)
    {
        if (!request.Confirmed) throw new HrValidationException("Automatic assignment confirmation is required."); if (!CanAssign()) throw new HrForbiddenException("You do not have permission to distribute cases."); await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token); var plan = await BuildAutomaticPlanAsync(request, token); var caseIds = plan.Assignments.Select(x => x.CaseId).ToArray(); var collectorIds = plan.Collectors.Select(x => x.CollectorId).ToArray(); var tracked = await _db.CollectionCases.Where(x => caseIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, token); var memberships = await _db.CollectionTeamMembers.AsNoTracking().Where(x => collectorIds.Contains(x.UserId) && x.IsActive && (!request.TeamId.HasValue || x.TeamId == request.TeamId)).Select(x => new { x.UserId, x.TeamId }).ToArrayAsync(token); var now = DateTimeOffset.UtcNow;
        foreach (var proposal in plan.Assignments) { var item = tracked[proposal.CaseId]; var previous = item.AssignedCollectorId; var teamId = request.TeamId ?? memberships.FirstOrDefault(x => x.UserId == proposal.CollectorId)?.TeamId; item.Assign(proposal.CollectorId, teamId, now); _db.CollectionAssignmentHistory.Add(new CollectionAssignmentHistory(item.Id, previous, proposal.CollectorId, _user.UserId, teamId, proposal.Reason, CollectionsValues.AssignmentSources.Automatic, plan.RuleCode, now)); _db.CollectionActivities.Add(new CollectionActivity(item.Id, CollectionsValues.ActivityTypes.Assignment, previous.HasValue ? "AUTO_REASSIGNED" : "AUTO_ASSIGNED", proposal.Reason, null, _user.UserId, now, null)); AddAudit(previous.HasValue ? "CaseAutomaticallyReassigned" : "CaseAutomaticallyAssigned", item, item.Id, new { AssignedCollectorId = previous }, new { item.AssignedCollectorId, item.AssignedTeamId, plan.RuleCode, proposal.Reason }); }
        await _db.SaveChangesAsync(token); await transaction.CommitAsync(token); return plan;
    }

    private async Task<AutoAssignmentPreviewDto> BuildAutomaticPlanAsync(AutoAssignmentRequest request, CancellationToken token)
    {
        if (!CanAssign()) throw new HrForbiddenException("You do not have permission to distribute cases."); if (request.MaxActiveCases is < 1 or > 5000) throw new HrValidationException("Maximum active case capacity must be between 1 and 5000."); var ids = NormalizeIds(request.CaseIds); var cases = await AccessibleCases().AsNoTracking().Where(x => ids.Contains(x.Id)).OrderByDescending(x => x.PriorityScore).ThenByDescending(x => x.OverdueBalance).Select(x => new { x.Id, x.CaseNumber, x.Customer.Governorate }).ToArrayAsync(token); if (cases.Length != ids.Length) throw new HrForbiddenException("One or more cases are outside your data scope."); var requestedCollectors = request.CollectorIds?.Where(x => x != Guid.Empty).Distinct().ToArray() ?? [];
        var users = _db.Users.AsNoTracking().Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == SystemRoleNames.CollectionsCollector)); if (requestedCollectors.Length > 0) users = users.Where(x => requestedCollectors.Contains(x.Id)); if (request.TeamId.HasValue) users = users.Where(x => _db.CollectionTeamMembers.Any(m => m.UserId == x.Id && m.TeamId == request.TeamId && m.IsActive)); if (HasRole(SystemRoleNames.CollectionsSupervisor)) users = users.Where(x => _db.CollectionTeamMembers.Any(m => m.UserId == x.Id && m.IsActive && m.Team.SupervisorId == _user.UserId)); var collectors = await users.OrderBy(x => x.FullName).Select(x => new { x.Id, x.FullName }).ToArrayAsync(token); if (collectors.Length == 0) throw new HrValidationException("No eligible active collectors match the distribution scope.");
        var collectorIds = collectors.Select(x => x.Id).ToArray(); var workloads = await _db.CollectionCases.AsNoTracking().Where(x => x.AssignedCollectorId != null && collectorIds.Contains(x.AssignedCollectorId.Value) && x.Status == CollectionsValues.CaseStatuses.Active).GroupBy(x => x.AssignedCollectorId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, token); var geography = await _db.CollectionCases.AsNoTracking().Where(x => x.AssignedCollectorId != null && collectorIds.Contains(x.AssignedCollectorId.Value) && x.Customer.Governorate != null && x.Status == CollectionsValues.CaseStatuses.Active).GroupBy(x => new { Id = x.AssignedCollectorId!.Value, x.Customer.Governorate }).Select(g => new { g.Key.Id, g.Key.Governorate, Count = g.Count() }).ToArrayAsync(token); var proposed = collectors.ToDictionary(x => x.Id, _ => 0); var assignments = new List<AutoAssignmentCaseDto>(cases.Length);
        foreach (var item in cases) { var eligible = collectors.Where(x => workloads.GetValueOrDefault(x.Id) + proposed[x.Id] < request.MaxActiveCases).OrderBy(x => workloads.GetValueOrDefault(x.Id) + proposed[x.Id]).ThenByDescending(x => string.IsNullOrWhiteSpace(item.Governorate) ? 0 : geography.Where(g => g.Id == x.Id && g.Governorate == item.Governorate).Sum(g => g.Count)).ThenBy(x => x.FullName, StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? throw new HrConflictException("Collector capacity is insufficient for the selected cases."); proposed[eligible.Id]++; var hasAreaExperience = !string.IsNullOrWhiteSpace(item.Governorate) && geography.Any(g => g.Id == eligible.Id && g.Governorate == item.Governorate); assignments.Add(new AutoAssignmentCaseDto(item.Id, item.CaseNumber, eligible.Id, eligible.FullName, hasAreaExperience ? "Lowest workload with existing governorate coverage" : "Lowest resulting active workload")); }
        var summary = collectors.Where(x => proposed[x.Id] > 0).Select(x => new AssignmentPreviewItemDto(x.Id, x.FullName, workloads.GetValueOrDefault(x.Id), proposed[x.Id], workloads.GetValueOrDefault(x.Id) + proposed[x.Id])).ToArray(); return new AutoAssignmentPreviewDto("BALANCED_GEO_V1", cases.Length, summary, assignments);
    }

    public async Task<IReadOnlyCollection<CollectorLookupDto>> GetCollectorsAsync(CancellationToken token)
    {
        if (!CanAssign()) throw new HrForbiddenException("You do not have permission to view collector workloads.");
        var userId = _user.UserId; var supervisorOnly = HasRole(SystemRoleNames.CollectionsSupervisor) && !HasRole(SystemRoleNames.Admin) && !HasRole(SystemRoleNames.CollectionsOperationsManager);
        return await _db.Users.AsNoTracking()
            .Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == SystemRoleNames.CollectionsCollector) && (!supervisorOnly || _db.CollectionTeamMembers.Any(m => m.UserId == x.Id && m.IsActive && m.Team.SupervisorId == userId)))
            .OrderBy(x => x.FullName)
            .Select(x => new CollectorLookupDto(x.Id, x.FullName, _db.CollectionCases.Count(c => c.AssignedCollectorId == x.Id && c.Status == CollectionsValues.CaseStatuses.Active), _db.CollectionTeamMembers.Where(m => m.UserId == x.Id && m.IsActive).Select(m => (Guid?)m.TeamId).FirstOrDefault(), _db.CollectionTeamMembers.Where(m => m.UserId == x.Id && m.IsActive).Select(m => ApiTextLocalizer.IsArabic ? m.Team.NameArabic : m.Team.NameEnglish).FirstOrDefault()))
            .ToArrayAsync(token);
    }

    public async Task<PagedResultDto<FieldVisitDto>> GetVisitsAsync(int page, int pageSize, string? status, DateOnly? date, CancellationToken token)
    {
        ValidatePage(page, pageSize); var cases = AccessibleCases(); var query = _db.CollectionFieldVisits.AsNoTracking().Where(x => cases.Any(c => c.Id == x.CaseId));
        if (!string.IsNullOrWhiteSpace(status)) { var value = status.Trim().ToUpperInvariant(); query = query.Where(x => x.Status == value); }
        if (date.HasValue) { var (start, end) = CairoDayUtcRange(date.Value); query = query.Where(x => x.ScheduledAt >= start && x.ScheduledAt < end); }
        var total = await query.CountAsync(token); var rows = await ProjectVisits(query.OrderBy(x => x.ScheduledAt).Skip((page - 1) * pageSize).Take(pageSize)).ToArrayAsync(token); return Page(rows, total, page, pageSize);
    }

    public async Task<FieldVisitDto> CreateVisitAsync(CreateVisitRequest request, CancellationToken token)
    {
        if (!CanAssign()) throw new HrForbiddenException("You do not have permission to plan field visits.");
        var collectionCase = await AccessibleCases().SingleOrDefaultAsync(x => x.Id == request.CaseId, token) ?? throw new HrNotFoundException("Collection case was not found.");
        if (request.ScheduledAt < DateTimeOffset.UtcNow.AddMinutes(-5) || string.IsNullOrWhiteSpace(request.Address)) throw new HrValidationException("Visit schedule and address are required, and the schedule cannot be in the past.");
        if (!await _db.Users.AnyAsync(x => x.Id == request.CollectorId && x.IsActive && x.UserRoles.Any(r => r.Role.Name == SystemRoleNames.CollectionsCollector), token)) throw new HrValidationException("A valid active collector is required.");
        var visit = new FieldVisit(request.CaseId, request.CollectorId, request.ScheduledAt, request.Address, request.Governorate, request.Area, _user.UserId, DateTimeOffset.UtcNow); _db.CollectionFieldVisits.Add(visit);
        _db.CollectionActivities.Add(new CollectionActivity(request.CaseId, CollectionsValues.ActivityTypes.Visit, CollectionsValues.VisitStatuses.Assigned, null, "FIELD", _user.UserId, DateTimeOffset.UtcNow, request.ScheduledAt)); AddAudit("VisitCreated", visit, request.CaseId, null, request); await _db.SaveChangesAsync(token);
        return await ProjectVisits(_db.CollectionFieldVisits.AsNoTracking().Where(x => x.Id == visit.Id)).SingleAsync(token);
    }

    public async Task<FieldVisitDto> CompleteVisitAsync(Guid visitId, CompleteVisitRequest request, CancellationToken token)
    {
        EnsureOperationalWrite();
        var visit = await _db.CollectionFieldVisits.Include(x => x.Case).SingleOrDefaultAsync(x => x.Id == visitId && AccessibleCases().Any(c => c.Id == x.CaseId), token) ?? throw new HrNotFoundException("Field visit was not found.");
        if (visit.CollectorId != _user.UserId && !CanAssign()) throw new HrForbiddenException("Only the assigned collector or a supervisor can complete this visit.");
        var before = new { visit.Status, visit.Result, visit.Notes }; visit.Complete(request.Result, request.Notes, DateTimeOffset.UtcNow); _db.CollectionActivities.Add(new CollectionActivity(visit.CaseId, CollectionsValues.ActivityTypes.Visit, visit.Result, visit.Notes, "FIELD", _user.UserId, DateTimeOffset.UtcNow, null)); AddAudit("VisitCompleted", visit, visit.CaseId, before, new { visit.Status, visit.Result, visit.Notes }); await _db.SaveChangesAsync(token);
        return await ProjectVisits(_db.CollectionFieldVisits.AsNoTracking().Where(x => x.Id == visit.Id)).SingleAsync(token);
    }

    public async Task<PagedResultDto<ComplaintDto>> GetComplaintsAsync(int page, int pageSize, string? search, string? status, CancellationToken token)
    {
        ValidatePage(page, pageSize); var cases = AccessibleCases(); var query = _db.CollectionComplaints.AsNoTracking().Where(x => cases.Any(c => c.Id == x.CaseId));
        if (!string.IsNullOrWhiteSpace(status)) { var value = status.Trim().ToUpperInvariant(); query = query.Where(x => x.Status == value); }
        if (!string.IsNullOrWhiteSpace(search)) { var value = search.Trim().ToLower(); query = query.Where(x => x.Reference.ToLower().Contains(value) || x.Case.CaseNumber.ToLower().Contains(value) || x.Case.Customer.CustomerCode.ToLower().Contains(value)); }
        var total = await query.CountAsync(token); var rows = await ProjectComplaints(query.OrderBy(x => x.SlaDueAt).Skip((page - 1) * pageSize).Take(pageSize)).ToArrayAsync(token); return Page(rows, total, page, pageSize);
    }

    public async Task<ComplaintDto> CreateComplaintAsync(CreateComplaintRequest request, CancellationToken token)
    {
        EnsureOperationalWrite();
        var caseItem = await AccessibleCases().SingleOrDefaultAsync(x => x.Id == request.CaseId, token) ?? throw new HrNotFoundException("Collection case was not found.");
        var now = DateTimeOffset.UtcNow; if (request.SlaDueAt <= now) throw new HrValidationException("A future SLA due time is required.");
        var collectorOnly = HasRole(SystemRoleNames.CollectionsCollector) && !CanAssign(); var ownerId = collectorOnly ? _user.UserId : request.OwnerId; if (collectorOnly && caseItem.AssignedCollectorId != _user.UserId) throw new HrForbiddenException("Collectors can create complaints only for their assigned cases.");
        if (!await _db.Users.AnyAsync(x => x.Id == ownerId && x.IsActive && x.UserRoles.Any(r => r.Role.Name == SystemRoleNames.CollectionsCollector), token)) throw new HrValidationException("A valid complaint owner is required.");
        if (HasRole(SystemRoleNames.CollectionsSupervisor) && !await _db.CollectionTeamMembers.AnyAsync(x => x.UserId == ownerId && x.IsActive && x.Team.IsActive && x.Team.SupervisorId == _user.UserId, token)) throw new HrForbiddenException("The complaint owner is outside your authorized team.");
        var id = Guid.NewGuid(); var reference = $"CMP-{now.Year}-{id:N}"[..17].ToUpperInvariant(); var complaint = new CollectionComplaint(request.CaseId, reference, request.Source, request.Category, request.Severity, request.Description, now, request.SlaDueAt, ownerId, _user.UserId, now, id); _db.CollectionComplaints.Add(complaint);
        _db.CollectionActivities.Add(new CollectionActivity(request.CaseId, CollectionsValues.ActivityTypes.Complaint, "CREATED", $"Complaint {reference} created. Complaint ID: {id}.", "COMPLAINT", _user.UserId, now, null)); AddAudit("ComplaintCreated", complaint, request.CaseId, null, new { complaint.Reference, complaint.Category, complaint.Severity, complaint.Status, complaint.OwnerId }); await _db.SaveChangesAsync(token);
        return await ProjectComplaints(_db.CollectionComplaints.AsNoTracking().Where(x => x.Id == complaint.Id)).SingleAsync(token);
    }

    public async Task<ComplaintDto> ChangeComplaintStatusAsync(Guid complaintId, ChangeComplaintStatusRequest request, CancellationToken token)
    {
        EnsureOperationalWrite();
        var complaint = await _db.CollectionComplaints.SingleOrDefaultAsync(x => x.Id == complaintId && AccessibleCases().Any(c => c.Id == x.CaseId), token) ?? throw new HrNotFoundException("Complaint was not found.");
        if (complaint.OwnerId != _user.UserId && !CanAssign() && !HasRole(SystemRoleNames.CollectionsOperationsManager)) throw new HrForbiddenException("Only the complaint owner or operations management can change its status.");
        var before = new { complaint.Status, complaint.Resolution, complaint.ClosedAt }; var now = DateTimeOffset.UtcNow; var status = request.Status.Trim().ToUpperInvariant(); try { if (status == CollectionsValues.ComplaintStatuses.InProgress) complaint.Start(now); else if (status == CollectionsValues.ComplaintStatuses.Resolved) complaint.Resolve(request.Resolution ?? string.Empty, _user.UserId, now); else if (status == CollectionsValues.ComplaintStatuses.Closed) complaint.Close(now); else if (status is CollectionsValues.ComplaintStatuses.Reopened or CollectionsValues.ComplaintStatuses.Open) complaint.Reopen(request.Resolution ?? string.Empty, now); else if (status == CollectionsValues.ComplaintStatuses.Rejected) complaint.Reject(request.Resolution ?? string.Empty, now); else throw new ArgumentException("Complaint status transition is invalid."); } catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); } catch (ArgumentException ex) { throw new HrValidationException(ex.Message); }
        if (status is CollectionsValues.ComplaintStatuses.Resolved or CollectionsValues.ComplaintStatuses.Closed or CollectionsValues.ComplaintStatuses.Reopened or CollectionsValues.ComplaintStatuses.Rejected) _db.CollectionActivities.Add(new CollectionActivity(complaint.CaseId, CollectionsValues.ActivityTypes.Complaint, status == CollectionsValues.ComplaintStatuses.Reopened ? "REOPENED" : status, $"Complaint {complaint.Reference} {status.ToLowerInvariant().Replace('_', ' ')}. Complaint ID: {complaint.Id}.", "COMPLAINT", _user.UserId, now, null));
        AddAudit("ComplaintStatusChanged", complaint, complaint.CaseId, before, new { complaint.Status, complaint.Resolution, complaint.ClosedAt }); await _db.SaveChangesAsync(token); return await ProjectComplaints(_db.CollectionComplaints.AsNoTracking().Where(x => x.Id == complaint.Id)).SingleAsync(token);
    }

    public async Task<PagedResultDto<CollectionAuditDto>> GetAuditAsync(int page, int pageSize, string? search, Guid? caseId, CancellationToken token)
    {
        ValidatePage(page, pageSize); if (!HasPermission(SystemPermissionCodes.CollectionsAuditView) && !HasRole(SystemRoleNames.Admin) && !HasRole(SystemRoleNames.CollectionsOperationsManager) && !HasRole(SystemRoleNames.CollectionsAuditor)) throw new HrForbiddenException("You do not have permission to view collection audit history.");
        var query = _db.CollectionAuditLogs.AsNoTracking(); if (caseId.HasValue) query = query.Where(x => x.CaseId == caseId);
        if (!string.IsNullOrWhiteSpace(search)) { var value = search.Trim().ToLower(); query = query.Where(x => x.Action.ToLower().Contains(value) || x.EntityType.ToLower().Contains(value) || (x.User != null && x.User.Username.ToLower().Contains(value))); }
        var total = await query.CountAsync(token); var rows = await query.OrderByDescending(x => x.OccurredAt).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new CollectionAuditDto(x.Id, x.User == null ? "System" : x.User.Username, x.Action, x.EntityType, x.EntityId, x.CaseId, x.BeforeJson, x.AfterJson, x.Source, x.OccurredAt)).ToArrayAsync(token); return Page(rows, total, page, pageSize);
    }

    public async Task<CollectionsConfigurationDto> GetConfigurationAsync(CancellationToken token)
    {
        EnsureConfigurationManage(); var clientRows = await _db.CollectionClientOrganizations.AsNoTracking().OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.NameArabic, x.NameEnglish, x.OrganizationType, x.LogoStorageKey, x.ContactEmail, x.ContactPhone, x.SettingsJson, x.IsActive }).ToArrayAsync(token); var clients = clientRows.Select(x => new ClientConfigurationDto(x.Id, x.Code, x.NameArabic, x.NameEnglish, x.OrganizationType, string.IsNullOrWhiteSpace(x.LogoStorageKey) ? null : CollectionsBrandingService.LogoUrl(x.Id), x.ContactEmail, x.ContactPhone, x.SettingsJson, x.IsActive)).ToArray(); var portfolios = await _db.CollectionPortfolios.AsNoTracking().OrderBy(x => x.OrganizationId).ThenBy(x => x.Code).Select(x => new PortfolioConfigurationDto(x.Id, x.OrganizationId, x.Code, x.NameArabic, x.NameEnglish, x.CurrencyCode, x.TargetAmount, x.SettingsJson, x.IsActive)).ToArrayAsync(token); var buckets = await _db.CollectionBucketDefinitions.AsNoTracking().OrderBy(x => x.OrganizationId).ThenBy(x => x.PortfolioId).ThenBy(x => x.SortOrder).Select(x => new BucketConfigurationDto(x.Id, x.OrganizationId, x.PortfolioId, x.Code, x.NameArabic, x.NameEnglish, x.MinimumDays, x.MaximumDays, x.SortOrder, x.IsActive)).ToArrayAsync(token); return new CollectionsConfigurationDto(clients, portfolios, buckets);
    }

    public async Task<ClientConfigurationDto> SaveClientAsync(Guid? id, SaveClientConfigurationRequest request, CancellationToken token)
    {
        EnsureConfigurationManage(); var code = request.Code.Trim().ToUpperInvariant(); var now = DateTimeOffset.UtcNow; ClientOrganization entity; object? before = null;
        if (id.HasValue) { entity = await _db.CollectionClientOrganizations.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Client organization was not found."); if (!entity.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) throw new HrConflictException("Organization code is immutable after creation."); before = new { entity.NameArabic, entity.NameEnglish, entity.OrganizationType, entity.ContactEmail, entity.ContactPhone, entity.SettingsJson, entity.IsActive }; }
        else { if (await _db.CollectionClientOrganizations.AnyAsync(x => x.Code == code, token)) throw new HrConflictException("Organization code already exists."); entity = new ClientOrganization(code, request.NameArabic, request.NameEnglish, request.OrganizationType, now); _db.CollectionClientOrganizations.Add(entity); }
        try { entity.Update(request.NameArabic, request.NameEnglish, request.OrganizationType, request.ContactEmail, request.ContactPhone, request.SettingsJson, request.IsActive, now); } catch (ArgumentException ex) { throw new HrValidationException(ex.Message); } AddAudit(id.HasValue ? "ClientOrganizationUpdated" : "ClientOrganizationCreated", entity, null, before, request); await _db.SaveChangesAsync(token); return new ClientConfigurationDto(entity.Id, entity.Code, entity.NameArabic, entity.NameEnglish, entity.OrganizationType, string.IsNullOrWhiteSpace(entity.LogoStorageKey) ? null : CollectionsBrandingService.LogoUrl(entity.Id), entity.ContactEmail, entity.ContactPhone, entity.SettingsJson, entity.IsActive);
    }

    public async Task<PortfolioConfigurationDto> SavePortfolioAsync(Guid? id, SavePortfolioConfigurationRequest request, CancellationToken token)
    {
        EnsureConfigurationManage(); if (!await _db.CollectionClientOrganizations.AnyAsync(x => x.Id == request.OrganizationId, token)) throw new HrValidationException("A valid client organization is required."); var code = request.Code.Trim().ToUpperInvariant(); CollectionPortfolio entity; object? before = null;
        if (id.HasValue) { entity = await _db.CollectionPortfolios.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Portfolio was not found."); if (entity.OrganizationId != request.OrganizationId || !entity.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) throw new HrConflictException("Portfolio organization and code are immutable after creation."); before = new { entity.NameArabic, entity.NameEnglish, entity.CurrencyCode, entity.TargetAmount, entity.SettingsJson, entity.IsActive }; }
        else { if (await _db.CollectionPortfolios.AnyAsync(x => x.OrganizationId == request.OrganizationId && x.Code == code, token)) throw new HrConflictException("Portfolio code already exists for this client."); entity = new CollectionPortfolio(request.OrganizationId, code, request.NameArabic, request.NameEnglish, request.CurrencyCode, DateTimeOffset.UtcNow); _db.CollectionPortfolios.Add(entity); }
        try { entity.Update(request.NameArabic, request.NameEnglish, request.CurrencyCode, request.TargetAmount, request.SettingsJson, request.IsActive); } catch (ArgumentException ex) { throw new HrValidationException(ex.Message); } AddAudit(id.HasValue ? "PortfolioUpdated" : "PortfolioCreated", entity, null, before, request); await _db.SaveChangesAsync(token); return new PortfolioConfigurationDto(entity.Id, entity.OrganizationId, entity.Code, entity.NameArabic, entity.NameEnglish, entity.CurrencyCode, entity.TargetAmount, entity.SettingsJson, entity.IsActive);
    }

    public async Task<BucketConfigurationDto> SaveBucketAsync(Guid? id, SaveBucketConfigurationRequest request, CancellationToken token)
    {
        EnsureConfigurationManage(); if (request.MinimumDays < 0 || request.MaximumDays < 0) throw new HrValidationException("Bucket day ranges cannot be negative."); if (request.PortfolioId.HasValue && !await _db.CollectionPortfolios.AnyAsync(x => x.Id == request.PortfolioId && x.OrganizationId == request.OrganizationId, token)) throw new HrValidationException("The selected portfolio does not belong to the client organization."); var code = request.Code.Trim().ToUpperInvariant();
        if (request.IsActive && request.MinimumDays.HasValue)
        {
            var min = request.MinimumDays.Value; var max = request.MaximumDays ?? int.MaxValue; var overlaps = await _db.CollectionBucketDefinitions.AnyAsync(x => x.Id != id && x.OrganizationId == request.OrganizationId && x.PortfolioId == request.PortfolioId && x.IsActive && x.MinimumDays != null && x.MinimumDays <= max && (x.MaximumDays == null || x.MaximumDays >= min), token); if (overlaps) throw new HrConflictException("The active bucket range overlaps another bucket in the same scope.");
        }
        DelinquencyBucketDefinition entity; object? before = null; if (id.HasValue) { entity = await _db.CollectionBucketDefinitions.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Bucket definition was not found."); if (entity.OrganizationId != request.OrganizationId || entity.PortfolioId != request.PortfolioId || !entity.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) throw new HrConflictException("Bucket scope and code are immutable after creation."); before = new { entity.NameArabic, entity.NameEnglish, entity.MinimumDays, entity.MaximumDays, entity.SortOrder, entity.IsActive }; }
        else { if (await _db.CollectionBucketDefinitions.AnyAsync(x => x.OrganizationId == request.OrganizationId && x.PortfolioId == request.PortfolioId && x.Code == code, token)) throw new HrConflictException("Bucket code already exists in this scope."); entity = new DelinquencyBucketDefinition(request.OrganizationId, request.PortfolioId, code, request.NameArabic, request.NameEnglish, request.MinimumDays, request.MaximumDays, request.SortOrder, DateTimeOffset.UtcNow); _db.CollectionBucketDefinitions.Add(entity); }
        try { entity.Update(request.NameArabic, request.NameEnglish, request.MinimumDays, request.MaximumDays, request.SortOrder, request.IsActive); } catch (ArgumentException ex) { throw new HrValidationException(ex.Message); } AddAudit(id.HasValue ? "BucketUpdated" : "BucketCreated", entity, null, before, request); await _db.SaveChangesAsync(token); return new BucketConfigurationDto(entity.Id, entity.OrganizationId, entity.PortfolioId, entity.Code, entity.NameArabic, entity.NameEnglish, entity.MinimumDays, entity.MaximumDays, entity.SortOrder, entity.IsActive);
    }

    private IQueryable<CollectionCase> AccessibleCases()
    {
        var userId = _user.UserId; if (IsGlobalRole()) return _db.CollectionCases.Where(x => !x.IsArchived);
        var isSupervisor = HasRole(SystemRoleNames.CollectionsSupervisor); var isCollector = HasRole(SystemRoleNames.CollectionsCollector); var isClientViewer = HasRole(SystemRoleNames.CollectionsClientViewer) || HasPermission("collections.case.view");
        return _db.CollectionCases.Where(x => !x.IsArchived && (
            (isCollector && x.AssignedCollectorId == userId) ||
            (isSupervisor && ((x.AssignedTeam != null && x.AssignedTeam.SupervisorId == userId) || (x.AssignedTeamId == null && _db.CollectionUserAccess.Any(a => a.UserId == userId && a.OrganizationId == x.Portfolio.OrganizationId && (a.PortfolioId == null || a.PortfolioId == x.PortfolioId))))) ||
            (isClientViewer && _db.CollectionUserAccess.Any(a => a.UserId == userId && a.OrganizationId == x.Portfolio.OrganizationId && (a.PortfolioId == null || a.PortfolioId == x.PortfolioId)))));
    }

    private IQueryable<CollectionCase> ApplyCaseFilters(IQueryable<CollectionCase> query, CollectionFilters f)
    {
        if (f.OrganizationId.HasValue) query = query.Where(x => x.Portfolio.OrganizationId == f.OrganizationId); if (f.PortfolioId.HasValue) query = query.Where(x => x.PortfolioId == f.PortfolioId); if (f.CollectorId.HasValue) query = query.Where(x => x.AssignedCollectorId == f.CollectorId);
        if (!string.IsNullOrWhiteSpace(f.Bucket)) { var value = f.Bucket.Trim().ToUpperInvariant(); query = query.Where(x => x.CurrentBucket.Code == value); } if (!string.IsNullOrWhiteSpace(f.Status)) { var value = f.Status.Trim().ToUpperInvariant(); query = query.Where(x => x.Status == value); } if (!string.IsNullOrWhiteSpace(f.Priority)) { var value = f.Priority.Trim().ToUpperInvariant(); query = query.Where(x => x.Priority == value); }
        if (!string.IsNullOrWhiteSpace(f.Search)) { if (f.Search.Length > 160) throw new HrValidationException("Search cannot exceed 160 characters."); var term = f.Search.Trim().ToLower(); query = query.Where(x => x.CaseNumber.ToLower().Contains(term) || x.AccountReference.ToLower().Contains(term) || (x.ContractReference != null && x.ContractReference.ToLower().Contains(term)) || x.Customer.CustomerCode.ToLower().Contains(term) || (x.Customer.FullNameArabic != null && x.Customer.FullNameArabic.ToLower().Contains(term)) || (x.Customer.FullNameEnglish != null && x.Customer.FullNameEnglish.ToLower().Contains(term) || (CanSearchSensitive() && ((x.Customer.NationalId != null && x.Customer.NationalId.Contains(term)) || (x.Customer.PrimaryPhone != null && x.Customer.PrimaryPhone.Contains(term)))))); }
        return query;
    }

    private IQueryable<CollectionCaseListItemDto> ProjectCases(IQueryable<CollectionCase> query)
    {
        var ar = ApiTextLocalizer.IsArabic; return query.Select(x => new CollectionCaseListItemDto(x.Id, x.CaseNumber, x.Customer.CustomerCode, ar ? x.Customer.FullNameArabic ?? x.Customer.FullNameEnglish! : x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic!, x.AccountReference, ar ? x.Portfolio.Organization.NameArabic : x.Portfolio.Organization.NameEnglish, ar ? x.Portfolio.NameArabic : x.Portfolio.NameEnglish, x.OutstandingBalance, x.OverdueBalance, x.DaysPastDue, ar ? x.CurrentBucket.NameArabic : x.CurrentBucket.NameEnglish, x.Status, x.Priority, x.PriorityScore, x.PriorityExplanation, x.AssignedCollectorId, x.AssignedCollector == null ? null : x.AssignedCollector.FullName, x.NextFollowUpAt));
    }
    private IQueryable<PromiseToPayDto> ProjectPromises(IQueryable<PromiseToPay> query) { var ar = ApiTextLocalizer.IsArabic; return query.Select(x => new PromiseToPayDto(x.Id, x.CaseId, x.Case.CaseNumber, ar ? x.Case.Customer.FullNameArabic ?? x.Case.Customer.FullNameEnglish! : x.Case.Customer.FullNameEnglish ?? x.Case.Customer.FullNameArabic!, x.PromisedAmount, x.PromiseDate, x.ActualPaidAmount, x.Status, x.Collector.FullName, x.Channel, x.CreatedAt)); }
    private IQueryable<CollectionPaymentDto> ProjectPayments(IQueryable<CollectionPayment> query) { var ar = ApiTextLocalizer.IsArabic; return query.Select(x => new CollectionPaymentDto(x.Id, x.CaseId, x.Case.CaseNumber, ar ? x.Case.Customer.FullNameArabic ?? x.Case.Customer.FullNameEnglish! : x.Case.Customer.FullNameEnglish ?? x.Case.Customer.FullNameArabic!, x.Amount, x.PaymentDate, x.Method, x.ReferenceNumber, x.Status, x.SubmittedBy.FullName, x.SubmittedAt, x.VerifiedBy == null ? null : x.VerifiedBy.FullName, x.VerifiedAt, x.RejectionReason)); }
    private IQueryable<FieldVisitDto> ProjectVisits(IQueryable<FieldVisit> query) { var ar = ApiTextLocalizer.IsArabic; return query.Select(x => new FieldVisitDto(x.Id, x.CaseId, x.Case.CaseNumber, ar ? x.Case.Customer.FullNameArabic ?? x.Case.Customer.FullNameEnglish! : x.Case.Customer.FullNameEnglish ?? x.Case.Customer.FullNameArabic!, x.CollectorId, x.Collector.FullName, x.ScheduledAt, x.Status, x.Address, x.Governorate, x.Area, x.Result, x.Notes)); }
    private IQueryable<ComplaintDto> ProjectComplaints(IQueryable<CollectionComplaint> query) { var ar = ApiTextLocalizer.IsArabic; var now = DateTimeOffset.UtcNow; return query.Select(x => new ComplaintDto(x.Id, x.CaseId, x.Case.CaseNumber, ar ? x.Case.Customer.FullNameArabic ?? x.Case.Customer.FullNameEnglish! : x.Case.Customer.FullNameEnglish ?? x.Case.Customer.FullNameArabic!, ar ? x.Case.Portfolio.Organization.NameArabic : x.Case.Portfolio.Organization.NameEnglish, x.Reference, x.Source, x.Category, x.Severity, x.Description, x.ReceivedAt, x.SlaDueAt, x.Status == CollectionsValues.ComplaintStatuses.Closed || x.Status == CollectionsValues.ComplaintStatuses.Resolved || x.SlaDueAt == null ? "ON_TIME" : x.SlaDueAt < now ? "BREACHED" : x.SlaDueAt < now.AddHours(24) ? "APPROACHING" : "ON_TIME", x.Status, x.OwnerId, x.Owner == null ? null : x.Owner.FullName, x.Resolution, x.ClosedAt)); }
    private async Task<string> CustomerNameAsync(Guid customerId, CancellationToken token) { var ar = ApiTextLocalizer.IsArabic; return await _db.CollectionCustomers.AsNoTracking().Where(x => x.Id == customerId).Select(x => ar ? x.FullNameArabic ?? x.FullNameEnglish! : x.FullNameEnglish ?? x.FullNameArabic!).SingleAsync(token); }
    private static DateOnly CairoToday() { var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, cairo).DateTime); }
    private static (DateTimeOffset Start, DateTimeOffset End) CairoDayUtcRange(DateOnly date)
    {
        var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        return (ToUtcBoundary(date.ToDateTime(TimeOnly.MinValue), cairo), ToUtcBoundary(date.AddDays(1).ToDateTime(TimeOnly.MinValue), cairo));
    }
    private static DateTimeOffset ToUtcBoundary(DateTime localTime, TimeZoneInfo timeZone)
    {
        localTime = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(localTime)) localTime = localTime.AddMinutes(1);
        if (timeZone.IsAmbiguousTime(localTime))
        {
            var earliestOffset = timeZone.GetAmbiguousTimeOffsets(localTime).Max();
            return new DateTimeOffset(localTime, earliestOffset).ToUniversalTime();
        }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone), TimeSpan.Zero);
    }
    private bool HasRole(string role) => _user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    private bool HasPermission(string permission) => _user.Permissions.Contains("*", StringComparer.OrdinalIgnoreCase) || _user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    private bool IsGlobalRole() => HasRole(SystemRoleNames.Admin) || HasRole(SystemRoleNames.CollectionsOperationsManager) || HasRole(SystemRoleNames.CollectionsReviewer) || HasRole(SystemRoleNames.CollectionsAuditor);
    private bool CanRevealSensitive() => HasPermission(SystemPermissionCodes.CollectionsSensitiveView) || HasRole(SystemRoleNames.Admin) || HasRole(SystemRoleNames.CollectionsOperationsManager) || HasRole(SystemRoleNames.CollectionsSupervisor) || HasRole(SystemRoleNames.CollectionsAuditor);
    private bool CanSearchSensitive() => CanRevealSensitive(); private bool CanAssign() => HasPermission(SystemPermissionCodes.CollectionsAssignmentManage) || HasRole(SystemRoleNames.Admin) || HasRole(SystemRoleNames.CollectionsOperationsManager) || HasRole(SystemRoleNames.CollectionsSupervisor);
    private bool CanReviewPayments() => HasPermission(SystemPermissionCodes.CollectionsPaymentApprove) || HasRole(SystemRoleNames.Admin) || HasRole(SystemRoleNames.CollectionsOperationsManager) || HasRole(SystemRoleNames.CollectionsReviewer);
    private void EnsureConfigurationManage() { if (!HasPermission(SystemPermissionCodes.CollectionsConfigurationManage) && !HasRole(SystemRoleNames.Admin) && !HasRole(SystemRoleNames.CollectionsOperationsManager)) throw new HrForbiddenException("Only Collections operations management can change client configuration."); }
    private void EnsureOperationalWrite() { if (!_user.Permissions.Any(x => x is "*" or "collections.activity.manage" or "collections.ptp.manage" or "collections.payment.submit" or "collections.visit.manage" or "collections.complaint.manage") && !HasRole(SystemRoleNames.Admin) && !HasRole(SystemRoleNames.CollectionsOperationsManager) && !HasRole(SystemRoleNames.CollectionsSupervisor) && !HasRole(SystemRoleNames.CollectionsCollector)) throw new HrForbiddenException("Your Collections access is read-only for this operation."); }
    private async Task<(int GraceDays, decimal ToleranceAmount)> GetPtpPolicyAsync(Guid caseId, CancellationToken token)
    {
        var settings = await _db.CollectionCases.AsNoTracking().Where(x => x.Id == caseId).Select(x => new { Portfolio = x.Portfolio.SettingsJson, Organization = x.Portfolio.Organization.SettingsJson }).SingleAsync(token);
        return ParsePtpSettings(settings.Portfolio) ?? ParsePtpSettings(settings.Organization) ?? (0, 0);
    }
    private async Task EvaluateDuePromisesAsync(CancellationToken token)
    {
        var today = CairoToday(); var cases = AccessibleCases(); var promises = await _db.CollectionPromisesToPay.Include(x => x.Case).ThenInclude(x => x.Portfolio).ThenInclude(x => x.Organization).Where(x => cases.Any(c => c.Id == x.CaseId) && (x.Status == CollectionsValues.PromiseStatuses.Active || x.Status == CollectionsValues.PromiseStatuses.Upcoming || x.Status == CollectionsValues.PromiseStatuses.DueToday || x.Status == CollectionsValues.PromiseStatuses.Broken || x.Status == CollectionsValues.PromiseStatuses.PartiallyFulfilled) && x.PromiseDate <= today).ToArrayAsync(token);
        if (promises.Length == 0) return; var caseIds = promises.Select(x => x.CaseId).Distinct().ToArray(); var earliest = promises.Min(x => DateOnly.FromDateTime(x.CreatedAt.UtcDateTime)); var approved = await _db.CollectionPayments.AsNoTracking().Where(x => caseIds.Contains(x.CaseId) && x.Status == CollectionsValues.PaymentStatuses.Approved && x.PaymentDate >= earliest).Select(x => new { x.CaseId, x.PaymentDate, x.Amount }).ToArrayAsync(token); var changed = false; var now = DateTimeOffset.UtcNow;
        foreach (var promise in promises)
        {
            var policy = ParsePtpSettings(promise.Case.Portfolio.SettingsJson) ?? ParsePtpSettings(promise.Case.Portfolio.Organization.SettingsJson) ?? (0, 0); var start = DateOnly.FromDateTime(promise.CreatedAt.UtcDateTime); var end = promise.PromiseDate.AddDays(policy.GraceDays); var paid = approved.Where(x => x.CaseId == promise.CaseId && x.PaymentDate >= start && x.PaymentDate <= end).Sum(x => x.Amount); var evaluation = CollectionRules.EvaluatePromise(promise.PromisedAmount, paid, promise.PromiseDate, today, policy.GraceDays, policy.ToleranceAmount);
            if (evaluation.Status == promise.Status && evaluation.PaidAmount == promise.ActualPaidAmount) continue; var previousStatus = promise.Status; var previousPaid = promise.ActualPaidAmount; promise.ApplyEvaluation(evaluation.Status, evaluation.PaidAmount, now); _db.CollectionAuditLogs.Add(new CollectionAuditLog(null, "PromiseAutomaticallyEvaluated", nameof(PromiseToPay), promise.Id, promise.CaseId, JsonSerializer.Serialize(new { Status = previousStatus, ActualPaidAmount = previousPaid }, JsonOptions), JsonSerializer.Serialize(new { promise.Status, promise.ActualPaidAmount }, JsonOptions), "AUTOMATION", now)); changed = true;
        }
        if (changed) await _db.SaveChangesAsync(token);
    }
    private static (int GraceDays, decimal ToleranceAmount)? ParsePtpSettings(string json)
    {
        try { using var document = JsonDocument.Parse(json); var root = document.RootElement; var days = 0; var amount = 0m; var hasGrace = root.TryGetProperty("ptpGraceDays", out var graceValue) && graceValue.TryGetInt32(out days); var hasTolerance = root.TryGetProperty("ptpToleranceAmount", out var toleranceValue) && toleranceValue.TryGetDecimal(out amount); if (!hasGrace && !hasTolerance) return null; return (Math.Clamp(days, 0, 30), Math.Max(0, amount)); } catch (JsonException) { return null; }
    }
    private static void ValidatePage(int page, int pageSize) { if (page < 1 || pageSize is < 1 or > 100) throw new HrValidationException("Page must be at least 1 and page size must be between 1 and 100."); }
    private static PagedResultDto<T> Page<T>(IReadOnlyCollection<T> rows, int count, int page, int pageSize) => new(rows, count, page, pageSize, count == 0 ? 0 : (int)Math.Ceiling(count / (double)pageSize));
    private static Guid[] NormalizeIds(IReadOnlyCollection<Guid> ids) { var result = ids.Where(x => x != Guid.Empty).Distinct().ToArray(); if (result.Length == 0 || result.Length > 500) throw new HrValidationException("Select between 1 and 500 cases."); return result; }
    private void AddAudit(string action, object entity, Guid? caseId, object? before, object? after) { var id = (Guid)(entity.GetType().GetProperty("Id")?.GetValue(entity) ?? throw new InvalidOperationException("Audited entity must expose an identifier.")); _db.CollectionAuditLogs.Add(new CollectionAuditLog(_user.UserId, action, entity.GetType().Name, id, caseId, before is null ? null : JsonSerializer.Serialize(before, JsonOptions), after is null ? null : JsonSerializer.Serialize(after, JsonOptions), "WEB", DateTimeOffset.UtcNow)); }
    private static string MaskAddress(string? value) => string.IsNullOrWhiteSpace(value) ? "" : value.Length <= 12 ? "********" : value[..8] + "…";
    private static string LocalizedCustomerName(CollectionCustomer customer) => ApiTextLocalizer.IsArabic ? customer.FullNameArabic ?? customer.FullNameEnglish ?? "" : customer.FullNameEnglish ?? customer.FullNameArabic ?? "";
    private static string LocalizePriority(string value) { if (!ApiTextLocalizer.IsArabic) return value; return value.Replace("HIGH_OUTSTANDING", "رصيد مرتفع").Replace("MATERIAL_OUTSTANDING", "رصيد مؤثر").Replace("SEVERE_DELINQUENCY", "تأخر شديد").Replace("HIGH_DELINQUENCY", "تأخر مرتفع").Replace("BROKEN_PTP", "وعد سداد مكسور").Replace("PTP_DUE_TODAY", "وعد مستحق اليوم").Replace("NO_RECENT_CONTACT", "لا يوجد تواصل حديث"); }
}
