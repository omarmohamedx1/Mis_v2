using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class HrMissionSynchronizer(ApplicationDbContext db)
{
    internal const long LockKey = 9273101;
    internal static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
    internal static DateOnly Date(DateTimeOffset value) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, Zone).DateTime);
    internal static TimeOnly Time(DateTimeOffset value) => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, Zone).DateTime);
    internal static bool Actionable(FieldVisit visit) => visit.Status is not ("CANCELLED" or "MISSED" or "FAILED");
    internal static TimeOnly? End(FieldVisit visit) => visit.CheckedOutAt.HasValue && Date(visit.CheckedOutAt.Value) == Date(visit.ScheduledAt) && Time(visit.CheckedOutAt.Value) > Time(visit.ScheduledAt) ? Time(visit.CheckedOutAt.Value) : null;
    internal void Audit(string action, string entity, Guid id, Guid? employeeId, object? before, object? after, Guid? actor = null)
        => db.HrAuditLogs.Add(new HrAuditLog(actor, action, entity, id, employeeId, before == null ? null : JsonSerializer.Serialize(before), after == null ? null : JsonSerializer.Serialize(after), action, DateTimeOffset.UtcNow));
    public async Task SyncAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({LockKey})", ct);
        db.ChangeTracker.Clear();
        var now = DateTimeOffset.UtcNow; var today = Date(now);
        var local = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone));
        var visits = await db.CollectionFieldVisits.Include(v => v.Collector)
            .Where(v => v.ScheduledAt >= start || db.HrExcuseMissions.Any(m => m.SourceVisitId == v.Id)).ToListAsync(ct);
        var missions = await db.HrExcuseMissions.Where(m => m.SourceVisitId != null).ToDictionaryAsync(m => m.SourceVisitId!.Value, ct);
        foreach (var visit in visits)
        {
            missions.TryGetValue(visit.Id, out var mission);
            if (mission == null)
            {
                if (!Actionable(visit)) continue;
                mission = new HrExcuseMission(visit.Collector.EmployeeId, "FieldVisitMission", Date(visit.ScheduledAt), Time(visit.ScheduledAt), null, null, null, visit.CreatedById, now, visit.Id);
                mission.CaptureVisit(visit); db.HrExcuseMissions.Add(mission);
                Audit("VisitRequestSubmitted", "HrExcuseMission", mission.Id, mission.EmployeeId, null, new { mission.SourceVisitId, mission.Status });
                continue;
            }
            if (mission.SourceUpdatedAt == visit.UpdatedAt && (mission.Status != "PendingApproval" || mission.EmployeeId == visit.Collector.EmployeeId)) continue;
            var before = new { mission.Status, mission.EmployeeId, mission.Date, mission.FromTime, mission.ToTime, mission.ApprovedAt, mission.ApprovedByUserId, mission.RejectedByUserId, mission.RejectedAt, mission.RejectionReason, mission.Reason, mission.Notes };
            var materialChange = mission.SourceScheduledAt != visit.ScheduledAt || mission.SourceCollectorId != visit.CollectorId;
            if (!Actionable(visit)) { mission.FlagSourceChange(visit.UpdatedAt, true, now); }
            else if (mission.Status == "PendingApproval") mission.RefreshPending(visit, visit.Collector.EmployeeId, Date(visit.ScheduledAt), Time(visit.ScheduledAt), now);
            else if (materialChange) mission.Reopen(visit, visit.Collector.EmployeeId, Date(visit.ScheduledAt), Time(visit.ScheduledAt), now);
            else mission.FlagSourceChange(visit.UpdatedAt, false, now);
            Audit("VisitExcuseUpdated", "HrExcuseMission", mission.Id, mission.EmployeeId, before, new { mission.Status, mission.Date, mission.FromTime, mission.SourceChanged });
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}

