namespace MIS.Domain.Entities;

public sealed class HrExcuseMission
{
    private HrExcuseMission() { }
    public HrExcuseMission(Guid? employeeId, string type, DateOnly date, TimeOnly? from, TimeOnly? to, string? reason, string? notes, Guid creator, DateTimeOffset now, Guid? visitId = null)
    {
        Id = Guid.NewGuid(); EmployeeId = employeeId; CreatedByUserId = creator; CreatedAt = now;
        SourceVisitId = visitId; SourceType = visitId.HasValue ? "FieldVisit" : "Manual";
        SetValues(type, date, from, to, reason, notes, now);
    }
    public Guid Id { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Employee? Employee { get; private set; } = null!;
    public string Type { get; private set; } = "FieldVisitMission";
    public DateOnly Date { get; private set; }
    public TimeOnly? FromTime { get; private set; }
    public TimeOnly? ToTime { get; private set; }
    public bool FullDay { get; private set; }
    public string? Reason { get; private set; }
    public string? Notes { get; private set; }
    public string Status { get; private set; } = "PendingApproval";
    public string SourceType { get; private set; } = "Manual";
    public Guid? SourceVisitId { get; private set; }
    public FieldVisit? SourceVisit { get; private set; }
    public DateTimeOffset? SourceUpdatedAt { get; private set; }
    public DateTimeOffset? SourceScheduledAt { get; private set; }
    public Guid? SourceCollectorId { get; private set; }
    public bool SourceChanged { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public User? ApprovedByUser { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? RejectedByUserId { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public void SetValues(string type, DateOnly date, TimeOnly? from, TimeOnly? to, string? reason, string? notes, DateTimeOffset now)
    {
        if (EmployeeId == Guid.Empty || CreatedByUserId == Guid.Empty || date == default) throw new ArgumentException("Employee and date are required.");
        if (type is not ("FieldVisitMission" or "PersonalExcuse" or "MedicalExcuse" or "OfficialMission" or "LateArrivalExcuse" or "EarlyLeaveExcuse" or "Other")) throw new ArgumentException("Invalid mission type.");
        if (to.HasValue && (!from.HasValue || to <= from)) throw new ArgumentException("End time must be after start time on the same day.");
        if (SourceType == "Manual" && (string.IsNullOrWhiteSpace(reason) || !EmployeeId.HasValue)) throw new ArgumentException("Employee and reason are required for a manual excuse.");
        if (reason?.Length > 1000 || notes?.Length > 3000) throw new ArgumentException("Mission text exceeds the maximum length.");
        Type = type; Date = date; FromTime = from; ToTime = to; Reason = reason?.Trim(); Notes = notes?.Trim(); UpdatedAt = now;
    }
    public void Approve(Guid actor, DateTimeOffset now) { if (!EmployeeId.HasValue) throw new ArgumentException("Link the collector to an existing HR employee before approval."); if (Status != "PendingApproval") throw new InvalidOperationException("Only a pending mission can be approved."); Status = "Approved"; ApprovedByUserId = actor; ApprovedAt = now; UpdatedAt = now; }
    public void Reject(Guid actor, string reason, DateTimeOffset now) { if (Status != "PendingApproval") throw new InvalidOperationException("Only a pending mission can be rejected."); ArgumentException.ThrowIfNullOrWhiteSpace(reason); if (reason.Length > 1000) throw new ArgumentException("Rejection reason is too long."); Status = "Rejected"; RejectedByUserId = actor; RejectedAt = now; RejectionReason = reason.Trim(); UpdatedAt = now; }
    public void Cancel(DateTimeOffset now) { Status = "Cancelled"; UpdatedAt = now; }
    public void SetManualPeriod(bool fullDay, TimeOnly? from, TimeOnly? to, DateTimeOffset now)
    {
        if (SourceType != "Manual" || Status != "PendingApproval") throw new InvalidOperationException("Only pending manual excuses can be edited.");
        if (!fullDay && (!from.HasValue || !to.HasValue || to <= from)) throw new ArgumentException("Start and end times are required, with end after start.");
        FullDay = fullDay; FromTime = fullDay ? null : from; ToTime = fullDay ? null : to; UpdatedAt = now;
    }
    public void RefreshPending(FieldVisit visit, Guid? employeeId, DateOnly date, TimeOnly from, DateTimeOffset now)
    {
        if (Status != "PendingApproval") throw new InvalidOperationException("Only pending requests can be refreshed.");
        if (SourceVisitId != visit.Id) throw new InvalidOperationException("The original visit cannot be changed.");
        EmployeeId = employeeId;
        var timeChanged = SourceScheduledAt != visit.ScheduledAt;
        SetValues(Type, date, from, timeChanged ? null : ToTime, Reason, Notes, now);
        CaptureVisit(visit);
    }
    public void Reopen(FieldVisit visit, Guid? employeeId, DateOnly date, TimeOnly from, DateTimeOffset now)
    {
        Status = "PendingApproval";
        ApprovedByUserId = null; ApprovedAt = null; RejectedByUserId = null; RejectedAt = null; RejectionReason = null;
        RefreshPending(visit, employeeId, date, from, now);
        SourceChanged = true;
    }
    public void CaptureVisit(FieldVisit visit) { SourceScheduledAt = visit.ScheduledAt; SourceCollectorId = visit.CollectorId; SourceUpdatedAt = visit.UpdatedAt; SourceChanged = false; }
    public void FlagSourceChange(DateTimeOffset revision, bool cancel, DateTimeOffset now) { SourceChanged = true; SourceUpdatedAt = revision; if (cancel) Cancel(now); UpdatedAt = now; }
}
