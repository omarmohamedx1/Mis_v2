using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Accounting;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class AccountingService(
    ApplicationDbContext db,
    ICurrentUserContext user,
    IHrFileStorage storage,
    IHrAuditService audit) : IAccountingService
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AttachmentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/jpeg", "image/jpg", "image/png"
    };

    private bool IsAdmin => user.Roles.Contains(SystemRoleNames.Admin, StringComparer.OrdinalIgnoreCase);
    private bool CanApprove => IsAdmin || user.Permissions.Contains(SystemPermissionCodes.AccountingPayrollApprove) || user.Permissions.Contains(SystemPermissionCodes.AccountingCommissionApprove) || user.Permissions.Contains("accounting.approve");

    public async Task<AccountingDashboardDto> GetDashboardAsync(int year, int month, CancellationToken token)
    {
        EnsureAccess(); EnsureMonth(year, month);
        var payroll = await db.AccountingEmployeePayrolls.AsNoTracking()
            .Where(x => x.Period.Year == year && x.Period.Month == month && (x.Status == AccountingValues.PayrollStatuses.Approved || x.Status == AccountingValues.PayrollStatuses.Paid))
            .SumAsync(x => (decimal?)x.NetSalary, token) ?? 0;
        var transport = await db.AccountingTransportationClaims.AsNoTracking()
            .Where(x => x.ClaimDate.Year == year && x.ClaimDate.Month == month && (x.Status == AccountingValues.TransportationStatuses.Approved || x.Status == AccountingValues.TransportationStatuses.Paid))
            .SumAsync(x => (decimal?)x.Amount, token) ?? 0;
        var collectors = await db.AccountingCollectorCommissions.AsNoTracking()
            .Where(x => x.PeriodYear == year && x.PeriodMonth == month && (x.Status == AccountingValues.CommissionStatuses.Approved || x.Status == AccountingValues.CommissionStatuses.Paid))
            .SumAsync(x => (decimal?)x.FinalCommission, token) ?? 0;
        var supervisors = await db.AccountingSupervisorCommissions.AsNoTracking()
            .Where(x => x.PeriodYear == year && x.PeriodMonth == month && (x.Status == AccountingValues.CommissionStatuses.Approved || x.Status == AccountingValues.CommissionStatuses.Paid))
            .SumAsync(x => (decimal?)x.FinalCommission, token) ?? 0;
        var pending = await db.AccountingEmployeePayrolls.CountAsync(x => x.Period.Year == year && x.Period.Month == month && x.Status == AccountingValues.PayrollStatuses.PendingReview, token)
            + await db.AccountingTransportationClaims.CountAsync(x => x.ClaimDate.Year == year && x.ClaimDate.Month == month && x.Status == AccountingValues.TransportationStatuses.Pending, token)
            + await db.AccountingCollectorCommissions.CountAsync(x => x.PeriodYear == year && x.PeriodMonth == month && x.Status == AccountingValues.CommissionStatuses.PendingReview, token)
            + await db.AccountingSupervisorCommissions.CountAsync(x => x.PeriodYear == year && x.PeriodMonth == month && x.Status == AccountingValues.CommissionStatuses.PendingReview, token);
        return new AccountingDashboardDto(year, month, payroll, transport, collectors, supervisors, pending);
    }

    public async Task<IReadOnlyList<AccountingPayrollPeriodDto>> ListPeriodsAsync(CancellationToken token)
    {
        EnsureAccess();
        return await db.AccountingPayrollPeriods.AsNoTracking().OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .Select(x => new AccountingPayrollPeriodDto(x.Id, x.Year, x.Month, x.Status, x.Notes, x.Payrolls.Count(p => p.Status != AccountingValues.PayrollStatuses.Cancelled), x.Payrolls.Where(p => p.Status != AccountingValues.PayrollStatuses.Cancelled).Sum(p => p.NetSalary), x.CreatedAt))
            .ToArrayAsync(token);
    }

    public async Task<AccountingPayrollPeriodDto> EnsurePeriodAsync(GeneratePayrollRequest request, CancellationToken token)
    {
        EnsureAccess(); EnsureMonth(request.Year, request.Month);
        var period = await db.AccountingPayrollPeriods.Include(x => x.Payrolls).SingleOrDefaultAsync(x => x.Year == request.Year && x.Month == request.Month, token);
        if (period is null)
        {
            period = new AccountingPayrollPeriod(request.Year, request.Month, user.UserId, DateTimeOffset.UtcNow, request.Notes);
            db.AccountingPayrollPeriods.Add(period);
            await WriteAudit("AccountingPayrollPeriodCreated", nameof(AccountingPayrollPeriod), period.Id.ToString(), null, null, new { period.Year, period.Month }, $"Payroll period {period.Year}-{period.Month:00} created.", token);
            await db.SaveChangesAsync(token);
        }
        return await MapPeriod(period.Id, token);
    }

    public async Task<AccountingPayrollPeriodDto> GeneratePayrollAsync(GeneratePayrollRequest request, CancellationToken token)
    {
        EnsureManage();
        var periodDto = await EnsurePeriodAsync(request, token);
        var period = await db.AccountingPayrollPeriods.SingleAsync(x => x.Id == periodDto.Id, token);
        if (period.Status is AccountingValues.PayrollStatuses.Approved or AccountingValues.PayrollStatuses.Paid)
            throw new HrConflictException("Approved or paid payroll periods cannot be regenerated.");

        var monthEnd = period.MonthEnd;
        var existing = await db.AccountingEmployeePayrolls.Where(x => x.PeriodId == period.Id).Select(x => x.EmployeeId).ToListAsync(token);
        var existingSet = existing.ToHashSet();
        var employees = await db.Employees.AsNoTracking().Include(x => x.Department).Include(x => x.Position)
            .Where(x => !x.IsArchived && x.Status == Employee.ActiveStatus)
            .ToListAsync(token);
        var comps = await db.EmployeeCompensations.AsNoTracking()
            .Where(x => x.EffectiveFrom <= monthEnd && (x.EffectiveTo == null || x.EffectiveTo >= monthEnd))
            .OrderByDescending(x => x.EffectiveFrom)
            .ToListAsync(token);
        var now = DateTimeOffset.UtcNow;
        var added = 0;
        foreach (var employee in employees)
        {
            if (existingSet.Contains(employee.Id)) continue;
            var comp = comps.FirstOrDefault(c => c.EmployeeId == employee.Id);
            if (comp is null) continue; // do not invent salary
            var name = string.IsNullOrWhiteSpace(employee.FullNameArabic) ? employee.FullName : employee.FullNameArabic!;
            db.AccountingEmployeePayrolls.Add(new AccountingEmployeePayroll(
                period.Id, employee.Id, employee.EmployeeNumber, name, employee.Department?.Name,
                employee.Position?.NameArabic ?? employee.Position?.Code, comp.BasicSalary, comp.Allowances, comp.Id, now));
            added++;
        }
        await WriteAudit("AccountingPayrollGenerated", nameof(AccountingPayrollPeriod), period.Id.ToString(), null, null, new { added }, $"Generated {added} salary rows.", token);
        await db.SaveChangesAsync(token);
        return await MapPeriod(period.Id, token);
    }

    public async Task<AccountingPayrollPageDto> ListPayrollsAsync(Guid periodId, string? search, string? status, int page, int pageSize, CancellationToken token)
    {
        EnsureAccess(); ValidatePage(page, pageSize);
        var q = db.AccountingEmployeePayrolls.AsNoTracking().Where(x => x.PeriodId == periodId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            q = q.Where(x => x.EmployeeNumber.ToLower().Contains(t) || x.EmployeeName.ToLower().Contains(t) || (x.DepartmentName != null && x.DepartmentName.ToLower().Contains(t)));
        }
        var total = await q.CountAsync(token);
        var items = await q.OrderBy(x => x.EmployeeNumber).Skip((page - 1) * pageSize).Take(pageSize).Select(MapPayrollExpr()).ToArrayAsync(token);
        return new AccountingPayrollPageDto(items, page, pageSize, total);
    }

    public async Task<AccountingEmployeePayrollDto> GetPayrollAsync(Guid id, CancellationToken token)
    {
        EnsureAccess();
        return await db.AccountingEmployeePayrolls.AsNoTracking().Where(x => x.Id == id).Select(MapPayrollExpr()).SingleOrDefaultAsync(token)
            ?? throw new HrNotFoundException("Payroll row was not found.");
    }

    public async Task<AccountingEmployeePayrollDto> UpdatePayrollAsync(Guid id, UpdateAccountingPayrollRequest request, CancellationToken token)
    {
        EnsureManage();
        var row = await db.AccountingEmployeePayrolls.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Payroll row was not found.");
        var before = Snapshot(row);
        try { row.ApplyComponents(request.Transportation, request.Commissions, request.Bonuses, request.Deductions, request.OtherAdjustments, request.Notes, DateTimeOffset.UtcNow); }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        await WriteAudit("AccountingPayrollUpdated", nameof(AccountingEmployeePayroll), row.Id.ToString(), null, before, Snapshot(row), "Payroll components updated.", token);
        await db.SaveChangesAsync(token);
        return await GetPayrollAsync(id, token);
    }

    public Task<AccountingEmployeePayrollDto> SubmitPayrollAsync(Guid id, CancellationToken token) => TransitionPayroll(id, "submit", token);
    public Task<AccountingEmployeePayrollDto> ApprovePayrollAsync(Guid id, CancellationToken token) => TransitionPayroll(id, "approve", token);
    public Task<AccountingEmployeePayrollDto> PayPayrollAsync(Guid id, CancellationToken token) => TransitionPayroll(id, "pay", token);
    public Task<AccountingEmployeePayrollDto> CancelPayrollAsync(Guid id, CancellationToken token) => TransitionPayroll(id, "cancel", token);

    private async Task<AccountingEmployeePayrollDto> TransitionPayroll(Guid id, string action, CancellationToken token)
    {
        if (action is "approve" or "pay") EnsureApprove(); else EnsureManage();
        var row = await db.AccountingEmployeePayrolls.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Payroll row was not found.");
        var before = row.Status; var now = DateTimeOffset.UtcNow;
        try
        {
            switch (action)
            {
                case "submit": row.SubmitForReview(now); break;
                case "approve": row.Approve(now); break;
                case "pay": row.MarkPaid(now); break;
                case "cancel": row.Cancel(now); break;
                default: throw new HrValidationException("Unsupported payroll action.");
            }
        }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        await WriteAudit($"AccountingPayroll{action}", nameof(AccountingEmployeePayroll), row.Id.ToString(), null, new { Status = before }, new { row.Status }, $"Payroll {action}.", token);
        await db.SaveChangesAsync(token);
        return await GetPayrollAsync(id, token);
    }

    public async Task<AccountingTransportationPageDto> ListTransportationAsync(string? search, string? status, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken token)
    {
        EnsureAccess(); ValidatePage(page, pageSize);
        var q = db.AccountingTransportationClaims.AsNoTracking().Include(x => x.Employee).ThenInclude(e => e.Department).Include(x => x.Employee).ThenInclude(e => e.Position).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status.Trim().ToUpperInvariant());
        if (from.HasValue) q = q.Where(x => x.ClaimDate >= from);
        if (to.HasValue) q = q.Where(x => x.ClaimDate <= to);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            q = q.Where(x => x.Employee.EmployeeNumber.ToLower().Contains(t) || x.Employee.FullName.ToLower().Contains(t) || x.Purpose.ToLower().Contains(t));
        }
        var total = await q.CountAsync(token);
        var rows = await q.OrderByDescending(x => x.ClaimDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(token);
        return new AccountingTransportationPageDto(rows.Select(MapTransport).ToArray(), page, pageSize, total);
    }

    public async Task<AccountingTransportationDto> GetTransportationAsync(Guid id, CancellationToken token)
    {
        EnsureAccess();
        var row = await db.AccountingTransportationClaims.AsNoTracking().Include(x => x.Employee).ThenInclude(e => e.Department).Include(x => x.Employee).ThenInclude(e => e.Position)
            .SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Transportation claim was not found.");
        return MapTransport(row);
    }

    public async Task<AccountingTransportationDto> CreateTransportationAsync(CreateTransportationRequest request, CancellationToken token)
    {
        EnsureManage();
        await EnsureEmployee(request.EmployeeId, token);
        if (request.FieldVisitId.HasValue && !await db.CollectionFieldVisits.AnyAsync(x => x.Id == request.FieldVisitId, token))
            throw new HrValidationException("Linked field visit was not found.");
        if (request.CaseId.HasValue && !await db.CollectionCases.AnyAsync(x => x.Id == request.CaseId, token))
            throw new HrValidationException("Linked case was not found.");
        var row = new AccountingTransportationClaim(request.EmployeeId, request.ClaimDate, request.Amount, request.Purpose, user.UserId, DateTimeOffset.UtcNow, request.FieldVisitId, request.CaseId, request.Notes);
        db.AccountingTransportationClaims.Add(row);
        await WriteAudit("AccountingTransportationCreated", nameof(AccountingTransportationClaim), row.Id.ToString(), null, null, new { row.Amount, row.EmployeeId }, "Transportation claim created.", token);
        await db.SaveChangesAsync(token);
        return await GetTransportationAsync(row.Id, token);
    }

    public async Task<AccountingTransportationDto> UpdateTransportationAsync(Guid id, UpdateTransportationRequest request, CancellationToken token)
    {
        EnsureManage();
        var row = await db.AccountingTransportationClaims.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Transportation claim was not found.");
        try { row.Update(request.ClaimDate, request.Amount, request.Purpose, request.Notes, request.FieldVisitId, request.CaseId, DateTimeOffset.UtcNow); }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        await WriteAudit("AccountingTransportationUpdated", nameof(AccountingTransportationClaim), row.Id.ToString(), null, null, new { row.Amount }, "Transportation claim updated.", token);
        await db.SaveChangesAsync(token);
        return await GetTransportationAsync(id, token);
    }

    public async Task<AccountingTransportationDto> TransitionTransportationAsync(Guid id, string action, CancellationToken token)
    {
        if (action is "approve" or "reject" or "pay") EnsureApprove(); else EnsureManage();
        var row = await db.AccountingTransportationClaims.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Transportation claim was not found.");
        var now = DateTimeOffset.UtcNow;
        try
        {
            switch (action.ToLowerInvariant())
            {
                case "approve": row.Approve(user.UserId, now); break;
                case "reject": row.Reject(user.UserId, now); break;
                case "pay": row.MarkPaid(now); break;
                case "cancel": row.Cancel(now); break;
                default: throw new HrValidationException("Unsupported transportation action.");
            }
        }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        await WriteAudit($"AccountingTransportation{action}", nameof(AccountingTransportationClaim), row.Id.ToString(), null, null, new { row.Status }, $"Transportation {action}.", token);
        await db.SaveChangesAsync(token);
        return await GetTransportationAsync(id, token);
    }

    public async Task<AccountingTransportationDto> UploadTransportationAttachmentAsync(Guid id, HrUploadFile file, CancellationToken token)
    {
        EnsureManage();
        var row = await db.AccountingTransportationClaims.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Transportation claim was not found.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".jpg" or ".jpeg" or ".png")) throw new HrValidationException("Only PDF, JPG, JPEG, and PNG attachments are allowed.");
        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AttachmentTypes.Contains(file.ContentType)) throw new HrValidationException("Unsupported attachment content type.");
        if (!string.IsNullOrWhiteSpace(row.AttachmentStorageKey)) await storage.DeleteAsync(row.AttachmentStorageKey, token);
        var stored = await storage.SaveAsync("accounting-transportation", file.FileName, file.ContentType, file.Content, MaxAttachmentBytes, token);
        try { row.SetAttachment(stored.OriginalFileName, stored.ContentType, stored.StorageKey, stored.Length, stored.Sha256Hash, DateTimeOffset.UtcNow); }
        catch (InvalidOperationException ex) { await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw new HrConflictException(ex.Message); }
        await db.SaveChangesAsync(token);
        return await GetTransportationAsync(id, token);
    }

    public async Task DeleteTransportationAttachmentAsync(Guid id, CancellationToken token)
    {
        EnsureManage();
        var row = await db.AccountingTransportationClaims.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Transportation claim was not found.");
        if (string.IsNullOrWhiteSpace(row.AttachmentStorageKey)) return;
        var key = row.AttachmentStorageKey;
        try { row.ClearAttachment(DateTimeOffset.UtcNow); }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        await db.SaveChangesAsync(token);
        await storage.DeleteAsync(key, token);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadTransportationAttachmentAsync(Guid id, CancellationToken token)
    {
        EnsureAccess();
        var row = await db.AccountingTransportationClaims.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Transportation claim was not found.");
        if (string.IsNullOrWhiteSpace(row.AttachmentStorageKey)) throw new HrNotFoundException("No attachment is available.");
        var stream = await storage.OpenReadAsync(row.AttachmentStorageKey, token);
        return (stream, row.AttachmentContentType ?? "application/octet-stream", row.AttachmentFileName ?? "attachment");
    }

    public async Task<IReadOnlyList<AccountingCommissionRuleDto>> ListRulesAsync(string? scope, CancellationToken token)
    {
        EnsureAccess();
        var q = db.AccountingCommissionRules.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(scope)) q = q.Where(x => x.Scope == scope.Trim().ToUpperInvariant());
        return await q.OrderByDescending(x => x.EffectiveFrom).Select(x => new AccountingCommissionRuleDto(x.Id, x.Code, x.NameArabic, x.NameEnglish, x.Scope, x.Basis, x.Percentage, x.FixedAmount, x.EffectiveFrom, x.IsActive, x.Version)).ToArrayAsync(token);
    }

    public async Task<AccountingCommissionRuleDto> CreateRuleAsync(CreateCommissionRuleRequest request, CancellationToken token)
    {
        EnsureManage();
        AccountingCommissionRule rule;
        try { rule = new AccountingCommissionRule(request.Code, request.NameArabic, request.NameEnglish, request.Scope, request.Basis, request.Percentage, request.FixedAmount, request.EffectiveFrom, user.UserId, DateTimeOffset.UtcNow); }
        catch (ArgumentException ex) { throw new HrValidationException(ex.Message); }
        db.AccountingCommissionRules.Add(rule);
        await WriteAudit("AccountingCommissionRuleCreated", nameof(AccountingCommissionRule), rule.Id.ToString(), null, null, new { rule.Code, rule.Percentage }, "Commission rule created.", token);
        await db.SaveChangesAsync(token);
        return new AccountingCommissionRuleDto(rule.Id, rule.Code, rule.NameArabic, rule.NameEnglish, rule.Scope, rule.Basis, rule.Percentage, rule.FixedAmount, rule.EffectiveFrom, rule.IsActive, rule.Version);
    }

    public async Task<int> CalculateCollectorCommissionsAsync(CalculateCommissionsRequest request, CancellationToken token)
    {
        EnsureManage(); EnsureMonth(request.Year, request.Month);
        var rule = await ResolveRuleAsync(AccountingValues.CommissionScopes.Collector, request.Year, request.Month, token);
        var start = new DateOnly(request.Year, request.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var payments = await db.CollectionPayments.AsNoTracking().Include(x => x.Case)
            .Where(x => x.Status == CollectionsValues.PaymentStatuses.Approved && x.PaymentDate >= start && x.PaymentDate <= end)
            .ToListAsync(token);
        var groups = payments.GroupBy(p => p.Case.AssignedCollectorId ?? p.SubmittedById).Where(g => g.Key != Guid.Empty).ToList();
        var existing = await db.AccountingCollectorCommissions.Where(x => x.PeriodYear == request.Year && x.PeriodMonth == request.Month && x.Status != AccountingValues.CommissionStatuses.Cancelled).ToListAsync(token);
        var users = await db.Users.AsNoTracking().Where(x => groups.Select(g => g.Key).Contains(x.Id)).ToDictionaryAsync(x => x.Id, token);
        var now = DateTimeOffset.UtcNow; var touched = 0;
        foreach (var group in groups)
        {
            var collected = group.Sum(x => x.Amount);
            var commission = rule.Calculate(collected);
            var rate = rule.Percentage ?? 0;
            var snapshot = JsonSerializer.Serialize(group.Select(p => new { PaymentId = p.Id, p.CaseId, CaseNumber = p.Case.CaseNumber, p.PaymentDate, p.Amount, p.ReferenceNumber }));
            var cases = group.Select(x => x.CaseId).Distinct().Count();
            var row = existing.FirstOrDefault(x => x.CollectorUserId == group.Key);
            users.TryGetValue(group.Key, out var collectorUser);
            if (row is null)
            {
                row = new AccountingCollectorCommission(request.Year, request.Month, group.Key, collectorUser?.EmployeeId, cases, collected, collected, rule.Id, rule.Version, rate, commission, snapshot, now);
                db.AccountingCollectorCommissions.Add(row);
                touched++;
            }
            else if (AccountingValues.CommissionStatuses.Editable.Contains(row.Status))
            {
                row.RefreshDraft(cases, collected, collected, rule.Id, rule.Version, rate, commission, snapshot, now);
                touched++;
            }
        }
        await WriteAudit("AccountingCollectorCommissionsCalculated", "AccountingCollectorCommission", $"{request.Year}-{request.Month:00}", null, null, new { touched, rule.Code }, "Collector commissions calculated from approved payments.", token);
        await db.SaveChangesAsync(token);
        return touched;
    }

    public async Task<AccountingCommissionPageDto<AccountingCollectorCommissionDto>> ListCollectorCommissionsAsync(int? year, int? month, string? search, string? status, int page, int pageSize, CancellationToken token)
    {
        EnsureAccess(); ValidatePage(page, pageSize);
        var q = db.AccountingCollectorCommissions.AsNoTracking().Include(x => x.CollectorUser).ThenInclude(u => u.Employee).Include(x => x.CommissionRule).AsQueryable();
        if (year.HasValue) q = q.Where(x => x.PeriodYear == year);
        if (month.HasValue) q = q.Where(x => x.PeriodMonth == month);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            q = q.Where(x => x.CollectorUser.FullName.ToLower().Contains(t) || (x.CollectorUser.Employee != null && x.CollectorUser.Employee.EmployeeNumber.ToLower().Contains(t)));
        }
        var total = await q.CountAsync(token);
        var rows = await q.OrderByDescending(x => x.PeriodYear).ThenByDescending(x => x.PeriodMonth).ThenBy(x => x.CollectorUser.FullName)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(token);
        return new AccountingCommissionPageDto<AccountingCollectorCommissionDto>(rows.Select(MapCollector).ToArray(), page, pageSize, total);
    }

    public async Task<AccountingCollectorCommissionDetailsDto> GetCollectorCommissionAsync(Guid id, CancellationToken token)
    {
        EnsureAccess();
        var row = await db.AccountingCollectorCommissions.AsNoTracking().Include(x => x.CollectorUser).ThenInclude(u => u.Employee).Include(x => x.CommissionRule)
            .SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Collector commission was not found.");
        var contributions = JsonSerializer.Deserialize<List<AccountingCommissionContributionDto>>(row.CalculationSnapshotJson) ?? [];
        return new AccountingCollectorCommissionDetailsDto(MapCollector(row), contributions);
    }

    public async Task<AccountingCollectorCommissionDto> AdjustCollectorCommissionAsync(Guid id, AdjustCommissionRequest request, CancellationToken token)
    {
        EnsureManage();
        var row = await db.AccountingCollectorCommissions.Include(x => x.CollectorUser).ThenInclude(u => u.Employee).Include(x => x.CommissionRule)
            .SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Collector commission was not found.");
        try { row.Adjust(request.Adjustments, request.Notes, DateTimeOffset.UtcNow); }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        await WriteAudit("AccountingCollectorCommissionAdjusted", nameof(AccountingCollectorCommission), row.Id.ToString(), null, null, new { row.Adjustments, row.FinalCommission }, "Collector commission adjusted.", token);
        await db.SaveChangesAsync(token);
        return MapCollector(row);
    }

    public async Task<AccountingCollectorCommissionDto> TransitionCollectorCommissionAsync(Guid id, string action, CancellationToken token)
    {
        if (action is "approve" or "pay") EnsureApprove(); else EnsureManage();
        var row = await db.AccountingCollectorCommissions.Include(x => x.CollectorUser).ThenInclude(u => u.Employee).Include(x => x.CommissionRule)
            .SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Collector commission was not found.");
        var now = DateTimeOffset.UtcNow;
        try
        {
            switch (action.ToLowerInvariant())
            {
                case "submit": row.Submit(now); break;
                case "approve": row.Approve(now); break;
                case "pay": row.MarkPaid(now); break;
                case "cancel": row.Cancel(now); break;
                default: throw new HrValidationException("Unsupported action.");
            }
        }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        await WriteAudit($"AccountingCollectorCommission{action}", nameof(AccountingCollectorCommission), row.Id.ToString(), null, null, new { row.Status }, $"Collector commission {action}.", token);
        await db.SaveChangesAsync(token);
        return MapCollector(row);
    }

    public async Task<int> CalculateSupervisorCommissionsAsync(CalculateCommissionsRequest request, CancellationToken token)
    {
        EnsureManage(); EnsureMonth(request.Year, request.Month);
        var rule = await ResolveRuleAsync(AccountingValues.CommissionScopes.Supervisor, request.Year, request.Month, token);
        var start = new DateOnly(request.Year, request.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var teams = await db.CollectionTeams.AsNoTracking().Where(x => x.IsActive && x.SupervisorId != null).ToListAsync(token);
        var teamIds = teams.Select(t => t.Id).ToArray();
        var members = await db.CollectionTeamMembers.AsNoTracking().Where(x => teamIds.Contains(x.TeamId) && x.IsActive).ToListAsync(token);
        var payments = await db.CollectionPayments.AsNoTracking().Include(x => x.Case)
            .Where(x => x.Status == CollectionsValues.PaymentStatuses.Approved && x.PaymentDate >= start && x.PaymentDate <= end)
            .ToListAsync(token);
        var existing = await db.AccountingSupervisorCommissions.Where(x => x.PeriodYear == request.Year && x.PeriodMonth == request.Month && x.Status != AccountingValues.CommissionStatuses.Cancelled).ToListAsync(token);
        var supervisors = teams.GroupBy(t => t.SupervisorId!.Value);
        var users = await db.Users.AsNoTracking().Where(x => supervisors.Select(g => g.Key).Contains(x.Id)).ToDictionaryAsync(x => x.Id, token);
        var now = DateTimeOffset.UtcNow; var touched = 0;
        foreach (var group in supervisors)
        {
            var groupTeamIds = group.Select(t => t.Id).ToHashSet();
            var memberIds = members.Where(m => groupTeamIds.Contains(m.TeamId)).Select(m => m.UserId).Distinct().ToHashSet();
            var teamPayments = payments.Where(p => memberIds.Contains(p.Case.AssignedCollectorId ?? p.SubmittedById)).ToList();
            var collected = teamPayments.Sum(x => x.Amount);
            var commission = rule.Calculate(collected);
            var rate = rule.Percentage ?? 0;
            var snapshot = JsonSerializer.Serialize(teamPayments.Select(p => new { PaymentId = p.Id, p.CaseId, CaseNumber = p.Case.CaseNumber, p.PaymentDate, p.Amount, p.ReferenceNumber }));
            var summary = string.Join(", ", group.Select(t => t.Code));
            users.TryGetValue(group.Key, out var supervisorUser);
            var row = existing.FirstOrDefault(x => x.SupervisorUserId == group.Key);
            if (row is null)
            {
                row = new AccountingSupervisorCommission(request.Year, request.Month, group.Key, supervisorUser?.EmployeeId, summary, memberIds.Count, collected, collected, rule.Id, rule.Version, rate, commission, snapshot, now);
                db.AccountingSupervisorCommissions.Add(row);
                touched++;
            }
            else if (AccountingValues.CommissionStatuses.Editable.Contains(row.Status))
            {
                row.RefreshDraft(summary, memberIds.Count, collected, collected, rule.Id, rule.Version, rate, commission, snapshot, now);
                touched++;
            }
        }
        await WriteAudit("AccountingSupervisorCommissionsCalculated", "AccountingSupervisorCommission", $"{request.Year}-{request.Month:00}", null, null, new { touched, rule.Code }, "Supervisor commissions calculated from approved team payments.", token);
        await db.SaveChangesAsync(token);
        return touched;
    }

    public async Task<AccountingCommissionPageDto<AccountingSupervisorCommissionDto>> ListSupervisorCommissionsAsync(int? year, int? month, string? search, string? status, int page, int pageSize, CancellationToken token)
    {
        EnsureAccess(); ValidatePage(page, pageSize);
        var q = db.AccountingSupervisorCommissions.AsNoTracking().Include(x => x.SupervisorUser).ThenInclude(u => u.Employee).Include(x => x.CommissionRule).AsQueryable();
        if (year.HasValue) q = q.Where(x => x.PeriodYear == year);
        if (month.HasValue) q = q.Where(x => x.PeriodMonth == month);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            q = q.Where(x => x.SupervisorUser.FullName.ToLower().Contains(t) || (x.SupervisorUser.Employee != null && x.SupervisorUser.Employee.EmployeeNumber.ToLower().Contains(t)));
        }
        var total = await q.CountAsync(token);
        var rows = await q.OrderByDescending(x => x.PeriodYear).ThenByDescending(x => x.PeriodMonth).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(token);
        return new AccountingCommissionPageDto<AccountingSupervisorCommissionDto>(rows.Select(MapSupervisor).ToArray(), page, pageSize, total);
    }

    public async Task<AccountingSupervisorCommissionDto> GetSupervisorCommissionAsync(Guid id, CancellationToken token)
    {
        EnsureAccess();
        var row = await db.AccountingSupervisorCommissions.AsNoTracking().Include(x => x.SupervisorUser).ThenInclude(u => u.Employee).Include(x => x.CommissionRule)
            .SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Supervisor commission was not found.");
        return MapSupervisor(row);
    }

    public async Task<AccountingSupervisorCommissionDto> AdjustSupervisorCommissionAsync(Guid id, AdjustCommissionRequest request, CancellationToken token)
    {
        EnsureManage();
        var row = await db.AccountingSupervisorCommissions.Include(x => x.SupervisorUser).ThenInclude(u => u.Employee).Include(x => x.CommissionRule)
            .SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Supervisor commission was not found.");
        try { row.Adjust(request.Adjustments, request.Notes, DateTimeOffset.UtcNow); }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        await WriteAudit("AccountingSupervisorCommissionAdjusted", nameof(AccountingSupervisorCommission), row.Id.ToString(), null, null, new { row.Adjustments, row.FinalCommission }, "Supervisor commission adjusted.", token);
        await db.SaveChangesAsync(token);
        return MapSupervisor(row);
    }

    public async Task<AccountingSupervisorCommissionDto> TransitionSupervisorCommissionAsync(Guid id, string action, CancellationToken token)
    {
        if (action is "approve" or "pay") EnsureApprove(); else EnsureManage();
        var row = await db.AccountingSupervisorCommissions.Include(x => x.SupervisorUser).ThenInclude(u => u.Employee).Include(x => x.CommissionRule)
            .SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new HrNotFoundException("Supervisor commission was not found.");
        var now = DateTimeOffset.UtcNow;
        try
        {
            switch (action.ToLowerInvariant())
            {
                case "submit": row.Submit(now); break;
                case "approve": row.Approve(now); break;
                case "pay": row.MarkPaid(now); break;
                case "cancel": row.Cancel(now); break;
                default: throw new HrValidationException("Unsupported action.");
            }
        }
        catch (InvalidOperationException ex) { throw new HrConflictException(ex.Message); }
        await WriteAudit($"AccountingSupervisorCommission{action}", nameof(AccountingSupervisorCommission), row.Id.ToString(), null, null, new { row.Status }, $"Supervisor commission {action}.", token);
        await db.SaveChangesAsync(token);
        return MapSupervisor(row);
    }

    public async Task<IReadOnlyList<AccountingLookupEmployeeDto>> LookupEmployeesAsync(string? search, CancellationToken token)
    {
        EnsureAccess();
        var q = db.Employees.AsNoTracking().Include(x => x.Department).Include(x => x.Position).Where(x => !x.IsArchived && x.Status == Employee.ActiveStatus);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            q = q.Where(x => x.EmployeeNumber.ToLower().Contains(t) || x.FullName.ToLower().Contains(t));
        }
        return await q.OrderBy(x => x.EmployeeNumber).Take(50)
            .Select(x => new AccountingLookupEmployeeDto(x.Id, x.EmployeeNumber, x.FullName, x.Department.Name, x.Position != null ? x.Position.NameArabic : null))
            .ToArrayAsync(token);
    }

    private async Task<AccountingCommissionRule> ResolveRuleAsync(string scope, int year, int month, CancellationToken token)
    {
        var on = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var rule = await db.AccountingCommissionRules
            .Where(x => x.Scope == scope && x.IsActive && x.EffectiveFrom <= on && (x.EffectiveTo == null || x.EffectiveTo >= on))
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version)
            .FirstOrDefaultAsync(token);
        if (rule is not null) return rule;

        // Seed a default configurable rule when none exists (not a hardcoded frontend %).
        var percentage = scope == AccountingValues.CommissionScopes.Collector ? 5m : 1m;
        rule = new AccountingCommissionRule(
            $"{scope}-DEFAULT",
            scope == AccountingValues.CommissionScopes.Collector ? "عمولة المحصل الافتراضية" : "عمولة المشرف الافتراضية",
            scope == AccountingValues.CommissionScopes.Collector ? "Default collector commission" : "Default supervisor commission",
            scope, AccountingValues.CommissionBases.PercentOfCollected, percentage, null, new DateOnly(2000, 1, 1), user.UserId, DateTimeOffset.UtcNow);
        db.AccountingCommissionRules.Add(rule);
        await db.SaveChangesAsync(token);
        return rule;
    }

    private async Task<AccountingPayrollPeriodDto> MapPeriod(Guid id, CancellationToken token) =>
        await db.AccountingPayrollPeriods.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new AccountingPayrollPeriodDto(x.Id, x.Year, x.Month, x.Status, x.Notes, x.Payrolls.Count(p => p.Status != AccountingValues.PayrollStatuses.Cancelled), x.Payrolls.Where(p => p.Status != AccountingValues.PayrollStatuses.Cancelled).Sum(p => p.NetSalary), x.CreatedAt))
            .SingleAsync(token);

    private static System.Linq.Expressions.Expression<Func<AccountingEmployeePayroll, AccountingEmployeePayrollDto>> MapPayrollExpr() =>
        x => new AccountingEmployeePayrollDto(x.Id, x.PeriodId, x.EmployeeId, x.EmployeeNumber, x.EmployeeName, x.DepartmentName, x.PositionName, x.BasicSalary, x.Allowances, x.Transportation, x.Commissions, x.Bonuses, x.Deductions, x.OtherAdjustments, x.NetSalary, x.Status, x.Notes, x.CreatedAt);

    private static AccountingTransportationDto MapTransport(AccountingTransportationClaim x) =>
        new(x.Id, x.EmployeeId, x.Employee.EmployeeNumber, x.Employee.FullName, x.Employee.Department?.Name, x.Employee.Position?.NameArabic, x.ClaimDate, x.Amount, x.Purpose, x.Notes, x.FieldVisitId, x.CaseId, x.Status, x.AttachmentFileName, x.CreatedAt);

    private static AccountingCollectorCommissionDto MapCollector(AccountingCollectorCommission x) =>
        new(x.Id, x.PeriodYear, x.PeriodMonth, x.CollectorUserId, x.CollectorUser.FullName, x.CollectorUser.Employee?.EmployeeNumber, x.AssignedCasesCount, x.CollectedAmount, x.EligibleAmount, x.CommissionRule.Code, x.RateApplied, x.CommissionAmount, x.Adjustments, x.FinalCommission, x.Status, x.Notes, x.CreatedAt);

    private static AccountingSupervisorCommissionDto MapSupervisor(AccountingSupervisorCommission x) =>
        new(x.Id, x.PeriodYear, x.PeriodMonth, x.SupervisorUserId, x.SupervisorUser.FullName, x.SupervisorUser.Employee?.EmployeeNumber, x.TeamSummary, x.CollectorCount, x.TeamCollectedAmount, x.EligibleAmount, x.CommissionRule.Code, x.RateApplied, x.CommissionAmount, x.Adjustments, x.FinalCommission, x.Status, x.Notes, x.CreatedAt);

    private static object Snapshot(AccountingEmployeePayroll row) => new { row.BasicSalary, row.Allowances, row.Transportation, row.Commissions, row.Bonuses, row.Deductions, row.OtherAdjustments, row.NetSalary, row.Status };


    private Task WriteAudit(string action, string entityType, string entityId, Guid? employeeId, object? oldValue, object? newValue, string description, CancellationToken token) =>
        audit.WriteAsync(new AuditWriteRequest(action, entityType, entityId, employeeId, oldValue, newValue, description), token);

    private async Task EnsureEmployee(Guid id, CancellationToken token)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == id && !x.IsArchived, token)) throw new HrNotFoundException("Employee was not found.");
    }

    private void EnsureAccess()
    {
        if (IsAdmin || user.Permissions.Contains("*") || user.Permissions.Contains(SystemPermissionCodes.FinanceAccess) || user.Permissions.Contains(SystemPermissionCodes.AccountingAccess) || user.Permissions.Contains("accounting.access"))
            return;
        throw new HrForbiddenException("Accounting access is required.");
    }

    private void EnsureManage()
    {
        EnsureAccess();
        if (IsAdmin || user.Permissions.Contains("*") || user.Permissions.Contains(SystemPermissionCodes.AccountingPayrollManage) || user.Permissions.Contains(SystemPermissionCodes.AccountingCommissionManage) || user.Permissions.Contains(SystemPermissionCodes.AccountingTransportationManage) || user.Permissions.Contains(SystemPermissionCodes.AccountingAccess) || user.Permissions.Contains("accounting.access"))
            return;
        throw new HrForbiddenException("Accounting manage permission is required.");
    }

    private void EnsureApprove()
    {
        EnsureAccess();
        if (IsAdmin || user.Permissions.Contains("*") || CanApprove || user.Permissions.Contains(SystemPermissionCodes.AccountingAccess) || user.Permissions.Contains("accounting.access"))
            return;
        throw new HrForbiddenException("Accounting approve permission is required.");
    }

    private static void EnsureMonth(int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12) throw new HrValidationException("Provide a valid payroll year and month.");
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 200) throw new HrValidationException("Invalid page parameters.");
    }
}
