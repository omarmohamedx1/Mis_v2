using Microsoft.EntityFrameworkCore;
using Npgsql;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class HrExcuseMissionService(ApplicationDbContext db, ICurrentUserContext user, HrMissionSynchronizer sync, IHrFileStorage storage) : IHrExcuseMissionService
{
    private bool CanApprove => user.Roles.Contains("HrManager") || user.Permissions.Contains("hr.excuses.approve");
    private bool CanManage => CanApprove || user.Roles.Contains("HrOfficer") || user.Permissions.Contains("hr.excuses.manage");
    private void ReadAccess() { if (!CanManage && !user.Permissions.Contains("hr.excuses.view")) throw new HrForbiddenException("Excuse access is required."); }
    private void WriteAccess() { if (!CanManage) throw new HrForbiddenException("Excuse management permission is required."); }
    public IReadOnlyCollection<ExcuseTypeOption> Types()
    {
        ReadAccess(); var ar = ApiTextLocalizer.IsArabic;
        return new[] {
            new ExcuseTypeOption("PersonalExcuse", ar ? "إذن شخصي" : "Personal Permission", true),
            new ExcuseTypeOption("MedicalExcuse", ar ? "عذر طبي" : "Medical Excuse", true),
            new ExcuseTypeOption("OfficialMission", ar ? "مأمورية عمل" : "Official Mission", true),
            new ExcuseTypeOption("LateArrivalExcuse", ar ? "عذر تأخير" : "Late Arrival Excuse", true),
            new ExcuseTypeOption("EarlyLeaveExcuse", ar ? "عذر انصراف مبكر" : "Early Leave Excuse", true),
            new ExcuseTypeOption("Other", ar ? "أخرى" : "Other", true)
        };
    }
    private IQueryable<HrExcuseMission> Query() => db.HrExcuseMissions
        .Include(m => m.Employee).ThenInclude(e => e!.Department).Include(m => m.Employee).ThenInclude(e => e!.Position)
        .Include(m => m.ApprovedByUser).Include(m => m.SourceVisit).ThenInclude(v => v!.Collector)
        .Include(m => m.SourceVisit).ThenInclude(v => v!.CreatedBy)
        .Include(m => m.SourceVisit).ThenInclude(v => v!.Case).ThenInclude(c => c.Customer)
        .Include(m => m.SourceVisit).ThenInclude(v => v!.Case).ThenInclude(c => c.Portfolio).ThenInclude(p => p.Organization);
    private async Task<HrExcuseMission> Get(Guid id, CancellationToken ct) => await Query().SingleOrDefaultAsync(m => m.Id == id, ct) ?? throw new HrNotFoundException("Excuse not found.");
    public async Task<ExcusePage> ListAsync(ExcuseFilter f, CancellationToken ct)
    {
        ReadAccess(); await sync.SyncAsync(ct);
        if (f.Page < 1 || f.PageSize is < 1 or > 100) throw new HrValidationException("Invalid page.");
        var query = Query().AsNoTracking();
        var pending = await db.HrExcuseMissions.CountAsync(m => m.SourceVisitId != null && m.Status == "PendingApproval", ct);
        if (f.EmployeeId.HasValue) query = query.Where(m => m.EmployeeId == f.EmployeeId);
        if (f.DepartmentId.HasValue) query = query.Where(m => m.Employee != null && m.Employee.DepartmentId == f.DepartmentId);
        if (!string.IsNullOrEmpty(f.Source)) query = query.Where(m => m.SourceType == f.Source);
        if (!string.IsNullOrEmpty(f.Status)) query = query.Where(m => m.Status == f.Status);
        if (!string.IsNullOrEmpty(f.Type)) query = query.Where(m => m.Type == f.Type);
        if (f.Date.HasValue) query = query.Where(m => m.Date == f.Date);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = "%" + f.Search.Trim() + "%";
            query = query.Where(m => m.Employee != null && (
                EF.Functions.ILike(m.Employee.FullName, term) ||
                EF.Functions.ILike(m.Employee.FullNameArabic ?? "", term) ||
                EF.Functions.ILike(m.Employee.FullNameEnglish ?? "", term) ||
                (m.Employee.MobileNumber != null && EF.Functions.ILike(m.Employee.MobileNumber, term))));
        }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(m => m.Date).ThenBy(m => m.Id).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct);
        var ids = rows.Select(m => m.Id).ToArray();
        var files = await db.HrExcuseAttachments.AsNoTracking().Where(a => ids.Contains(a.ExcuseId)).ToListAsync(ct);
        return new(rows.Select(m => Map(m, files.Where(a => a.ExcuseId == m.Id))).ToArray(), count, f.Page, f.PageSize, (int)Math.Ceiling(count / (double)f.PageSize), pending, CanManage, CanApprove);
    }
    public async Task<ExcuseItem> OriginalVisitAsync(Guid visitId, CancellationToken ct)
    {
        ReadAccess(); await sync.SyncAsync(ct);
        var m = await Query().AsNoTracking().SingleOrDefaultAsync(m => m.SourceVisitId == visitId, ct) ?? throw new HrNotFoundException("Linked visit not found.");
        var v = m.SourceVisit!;
        return Map(m, []) with { Date = HrMissionSynchronizer.Date(v.ScheduledAt), FromTime = HrMissionSynchronizer.Time(v.ScheduledAt), ToTime = HrMissionSynchronizer.End(v) };
    }
    public async Task<ExcuseItem> DetailsAsync(Guid id, CancellationToken ct)
    {
        ReadAccess(); await sync.SyncAsync(ct);
        var m = await Query().AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct) ?? throw new HrNotFoundException("Excuse not found.");
        return Map(m, await db.HrExcuseAttachments.AsNoTracking().Where(a => a.ExcuseId == id).ToListAsync(ct));
    }
    public async Task<IReadOnlyCollection<ExcuseItem>> NotificationsAsync(CancellationToken ct)
    {
        if (!CanApprove) throw new HrForbiddenException("Excuse approval permission is required.");
        await sync.SyncAsync(ct); var today = HrMissionSynchronizer.Date(DateTimeOffset.UtcNow);
        return (await Query().AsNoTracking().Where(m => m.SourceVisitId != null && m.Status == "PendingApproval" && m.Date == today).OrderBy(m => m.FromTime).ToListAsync(ct)).Select(m => Map(m, [])).ToArray();
    }
    public async Task DecideAsync(Guid id, MissionDecisionRequest request, CancellationToken ct)
    {
        try { await DecideCoreAsync(id, request, ct); }
        catch (Exception ex) when (IsReviewConflict(ex))
        {
            throw new HrConflictException("Request changed. Refresh before reviewing.");
        }
    }
    private static bool IsReviewConflict(Exception exception)
    {
        // Npgsql may wrap a serialization failure in both DbUpdateException and
        // InvalidOperationException. Always return a review conflict, never a 500.
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException || current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected }) return true;
        return false;
    }
    private async Task DecideCoreAsync(Guid id, MissionDecisionRequest request, CancellationToken ct)
    {
        if (!CanApprove) throw new HrForbiddenException("Excuse approval permission is required.");
        await sync.SyncAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({HrMissionSynchronizer.LockKey})", ct);
        db.ChangeTracker.Clear(); var m = await Get(id, ct);
        if (m.UpdatedAt != request.ExpectedUpdatedAt) throw new HrConflictException("Request changed. Refresh before reviewing.");
        if (m.SourceVisit != null && (m.SourceUpdatedAt != m.SourceVisit.UpdatedAt || m.EmployeeId != m.SourceVisit.Collector.EmployeeId || !HrMissionSynchronizer.Actionable(m.SourceVisit))) throw new HrConflictException("Visit changed. Refresh before reviewing.");
        var before = Snapshot(m); var now = DateTimeOffset.UtcNow;
        try
        {
            if (m.Status != "PendingApproval") throw new InvalidOperationException("Only pending requests can be reviewed.");
            var end = m.FullDay ? null : request.ToTime ?? m.ToTime;
            if (m.SourceType == "Manual")
            {
                if (request.Approve)
                {
                    m.SetManualPeriod(m.FullDay, m.FromTime, end, now);
                    await EnsureNoDuplicateAsync(m.Id, m.EmployeeId!.Value, m.Date, m.FromTime, m.ToTime, ct);
                }
                m.SetValues(m.Type, m.Date, m.FromTime, m.FullDay ? null : end, m.Reason, request.Notes ?? m.Notes, now);
            }
            else m.SetValues(m.Type, m.Date, m.FromTime, end, m.Reason, request.Notes ?? m.Notes, now);
            if (request.Approve) m.Approve(user.UserId, now); else m.Reject(user.UserId, request.Reason ?? "", now);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { throw new HrValidationException(ex.Message); }
        sync.Audit(request.Approve ? "ExcuseApproved" : "ExcuseRejected", nameof(HrExcuseMission), id, m.EmployeeId, before, Snapshot(m), user.UserId);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<Guid> SaveManualAsync(Guid? id, ManualMissionRequest r, CancellationToken ct)
    {
        try { return await SaveManualCoreAsync(id, r, ct); }
        catch (Exception ex) when (IsReviewConflict(ex)) { throw new HrConflictException("Request changed. Refresh before reviewing."); }
    }
    private async Task<Guid> SaveManualCoreAsync(Guid? id, ManualMissionRequest r, CancellationToken ct)
    {
        WriteAccess();
        if (r.ApproveImmediately && !CanApprove) throw new HrForbiddenException("Excuse approval permission is required.");
        if (!Types().Any(t => t.Code == r.Type)) throw new HrValidationException("Invalid mission type.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({HrMissionSynchronizer.LockKey})", ct);
        db.ChangeTracker.Clear();
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == r.EmployeeId, ct) ?? throw new HrNotFoundException("Employee not found.");
        if (!employee.IsActive || employee.IsArchived) throw new HrValidationException("Select an active employee for a manual excuse.");
        if (employee.HireDate.HasValue && r.Date < employee.HireDate.Value) throw new HrValidationException("Excuse date cannot precede employment.");
        var m = id.HasValue ? await Get(id.Value, ct) : null; var before = m == null ? null : Snapshot(m);
        if (m != null && (m.SourceVisitId != null || m.Status != "PendingApproval" || m.EmployeeId != r.EmployeeId)) throw new HrValidationException("Only pending manual excuses can be edited, without changing employee.");
        if (m != null && m.UpdatedAt != r.ExpectedUpdatedAt) throw new HrConflictException("Request changed. Refresh before reviewing.");
        var from = r.FullDay ? null : r.FromTime; var to = r.FullDay ? null : r.ToTime;
        await EnsureNoDuplicateAsync(id, r.EmployeeId, r.Date, from, to, ct);
        try
        {
            if (m == null) { m = new(r.EmployeeId, r.Type, r.Date, from, to, r.Reason, r.Notes, user.UserId, DateTimeOffset.UtcNow); db.HrExcuseMissions.Add(m); }
            else m.SetValues(r.Type, r.Date, from, to, r.Reason, r.Notes, DateTimeOffset.UtcNow);
            m.SetManualPeriod(r.FullDay, from, to, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException ex) { throw new HrValidationException(ex.Message); }
        sync.Audit(id.HasValue ? "ExcuseEdited" : "ExcuseRequestCreated", nameof(HrExcuseMission), m.Id, m.EmployeeId, before, Snapshot(m), user.UserId);
        if (r.ApproveImmediately)
        {
            var pending = Snapshot(m); m.Approve(user.UserId, DateTimeOffset.UtcNow);
            sync.Audit("ExcuseApproved", nameof(HrExcuseMission), m.Id, m.EmployeeId, pending, Snapshot(m), user.UserId);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return m.Id;
    }
    private async Task EnsureNoDuplicateAsync(Guid? id, Guid employeeId, DateOnly date, TimeOnly? from, TimeOnly? to, CancellationToken ct)
    {
        if (await db.HrExcuseMissions.AnyAsync(m => m.Id != id && m.SourceType == "Manual" && m.EmployeeId == employeeId && m.Date == date && m.FromTime == from && m.ToTime == to && (m.Status == "PendingApproval" || m.Status == "Approved"), ct))
            throw new HrConflictException("An active manual excuse already exists for this employee, date and period.");
    }
    public async Task CancelAsync(Guid id, CancelMissionRequest r, CancellationToken ct)
    {
        WriteAccess(); if (string.IsNullOrWhiteSpace(r.Reason)) throw new HrValidationException("Cancellation reason is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({HrMissionSynchronizer.LockKey})", ct);
        db.ChangeTracker.Clear();
        var m = await Get(id, ct); if (m.Status == "Approved" && !CanApprove) throw new HrForbiddenException("Approval permission is required.");
        if (m.Status is not ("PendingApproval" or "Approved")) throw new HrValidationException("Only pending or approved excuses can be cancelled.");
        if (m.UpdatedAt != r.ExpectedUpdatedAt) throw new HrConflictException("Request changed. Refresh before reviewing.");
        var before = Snapshot(m); m.Cancel(DateTimeOffset.UtcNow);
        sync.Audit("ExcuseCancelled", nameof(HrExcuseMission), id, m.EmployeeId, before, new { r.Reason, m.Status }, user.UserId); await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task LinkEmployeeAsync(Guid collectorId, Guid employeeId, CancellationToken ct)
    {
        if (!CanApprove) throw new HrForbiddenException("Approval permission is required to link an employee.");
        if (!await db.Employees.AnyAsync(e => e.Id == employeeId, ct)) throw new HrNotFoundException("Employee not found.");
        if (await db.Users.AnyAsync(u => u.EmployeeId == employeeId && u.Id != collectorId, ct)) throw new HrConflictException("Employee is already linked.");
        var collector = await db.Users.SingleOrDefaultAsync(u => u.Id == collectorId, ct) ?? throw new HrNotFoundException("Collector not found.");
        try { collector.LinkEmployee(employeeId, DateTimeOffset.UtcNow); } catch (InvalidOperationException ex) { throw new HrValidationException(ex.Message); }
        sync.Audit("CollectorEmployeeLinked", "User", collectorId, employeeId, null, new { employeeId }, user.UserId); await db.SaveChangesAsync(ct);
    }
    public async Task UploadAsync(Guid id, Guid? attachmentId, HrUploadFile file, CancellationToken ct)
    {
        WriteAccess(); var m = await Get(id, ct);
        await using var validated = await HrEmployeeDocumentService.ValidateAndBufferAsync(file, ct);
        var a = attachmentId.HasValue ? await Attachment(id, attachmentId.Value, ct) : new HrExcuseAttachment { ExcuseId = id };
        var oldKey = a.StorageKey; var oldName = a.FileName;
        var saved = await storage.SaveAsync("excuses", file.FileName, validated.ContentType, validated.Stream, 10 * 1024 * 1024, ct);
        try
        {
            a.FileName = saved.OriginalFileName; a.ContentType = saved.ContentType; a.StorageKey = saved.StorageKey; a.Length = saved.Length; a.Sha256Hash = saved.Sha256Hash; a.UploadedByUserId = user.UserId; a.UploadedAt = DateTimeOffset.UtcNow;
            if (!attachmentId.HasValue) db.HrExcuseAttachments.Add(a);
            sync.Audit(attachmentId.HasValue ? "ExcuseAttachmentReplaced" : "ExcuseAttachmentUploaded", nameof(HrExcuseMission), id, m.EmployeeId, new { FileName = oldName }, new { a.Id, a.FileName, a.UploadedAt }, user.UserId);
            await db.SaveChangesAsync(ct);
        }
        catch { await storage.DeleteAsync(saved.StorageKey, CancellationToken.None); throw; }
        if (!string.IsNullOrEmpty(oldKey)) await storage.DeleteAsync(oldKey, ct);
    }
    private async Task<HrExcuseAttachment> Attachment(Guid id, Guid attachmentId, CancellationToken ct) => await db.HrExcuseAttachments.SingleOrDefaultAsync(a => a.ExcuseId == id && a.Id == attachmentId, ct) ?? throw new HrNotFoundException("Attachment not found.");
    public async Task<(Stream Content, string ContentType, string FileName)> DownloadAsync(Guid id, Guid attachmentId, CancellationToken ct)
    {
        ReadAccess(); var a = await Attachment(id, attachmentId, ct); return (await storage.OpenReadAsync(a.StorageKey, ct), a.ContentType, a.FileName);
    }
    public async Task DeleteAttachmentAsync(Guid id, Guid attachmentId, CancellationToken ct)
    {
        WriteAccess(); var m = await Get(id, ct); var a = await Attachment(id, attachmentId, ct);
        db.HrExcuseAttachments.Remove(a); sync.Audit("ExcuseAttachmentDeleted", nameof(HrExcuseMission), id, m.EmployeeId, new { a.Id, a.FileName }, null, user.UserId);
        await db.SaveChangesAsync(ct); await storage.DeleteAsync(a.StorageKey, ct);
    }
    private static object Snapshot(HrExcuseMission m) => new { m.Id, m.EmployeeId, m.Type, m.Date, m.FullDay, m.FromTime, m.ToTime, m.Status, m.Reason, m.Notes, m.ApprovedAt, m.ApprovedByUserId, m.RejectedAt, m.RejectedByUserId, m.RejectionReason };
    private static ExcuseItem Map(HrExcuseMission m, IEnumerable<HrExcuseAttachment> files)
    {
        var ar = ApiTextLocalizer.IsArabic; var e = m.Employee; var v = m.SourceVisit; var c = v?.Case.Customer; var o = v?.Case.Portfolio.Organization;
        return new(m.Id, m.EmployeeId, e?.EmployeeNumber, e == null ? v?.Collector.FullName ?? "" : ar ? e.FullNameArabic ?? e.FullName : e.FullNameEnglish ?? e.FullName,
            e?.DepartmentId, ar ? e?.Department.NameArabic ?? e?.Department.Name : e?.Department.Name, ar ? e?.Position?.NameArabic ?? e?.Position?.Name : e?.Position?.Name,
            m.Type, m.SourceType, m.Date, m.FromTime, m.ToTime, m.Status, m.SourceVisitId, v?.CollectorId, v?.CaseId, v?.Case.CaseNumber,
            ar ? c?.FullNameArabic ?? c?.FullNameEnglish : c?.FullNameEnglish ?? c?.FullNameArabic, ar ? o?.NameArabic : o?.NameEnglish,
            v?.Address, v?.Status, v?.Result, v?.Notes, v?.CreatedBy.FullName, m.Reason, m.Notes, m.ApprovedByUser?.FullName, m.ApprovedAt ?? m.RejectedAt, m.RejectionReason, m.CreatedAt, m.UpdatedAt, m.SourceChanged,
            files.Select(a => new ExcuseAttachmentDto(a.Id, a.ExcuseId, a.FileName, a.ContentType, a.Length, a.UploadedByUserId, a.UploadedAt)).ToArray(), m.FullDay);
    }
}
