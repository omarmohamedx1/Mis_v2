namespace MIS.Domain.Entities;

// Persistent, role-targeted notification/review. This is not an HR excuse record.
public sealed class HrMissionReview
{
    private HrMissionReview() { }
    public HrMissionReview(FieldVisit visit, Guid? employeeId, DateOnly date, DateTimeOffset now)
    {
        Id = Guid.NewGuid(); SourceVisitId = visit.Id; CollectorUserId = visit.CollectorId; EmployeeId = employeeId;
        VisitDate = date; SourceScheduledAt = visit.ScheduledAt; SourceUpdatedAt = visit.UpdatedAt; CreatedAt = UpdatedAt = now;
    }
    public Guid Id { get; private set; }
    public Guid SourceVisitId { get; private set; }
    public FieldVisit SourceVisit { get; private set; } = null!;
    public Guid CollectorUserId { get; private set; }
    public User CollectorUser { get; private set; } = null!;
    public Guid? EmployeeId { get; private set; }
    public Employee? Employee { get; private set; }
    public DateOnly VisitDate { get; private set; }
    public DateTimeOffset SourceScheduledAt { get; private set; }
    public DateTimeOffset SourceUpdatedAt { get; private set; }
    public string Status { get; private set; } = "PendingApproval";
    public Guid? DecisionByUserId { get; private set; }
    public User? DecisionByUser { get; private set; }
    public DateTimeOffset? DecisionAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public void Refresh(FieldVisit visit, Guid? employeeId, DateTimeOffset now) { if (Status != "PendingApproval") return; SourceScheduledAt = visit.ScheduledAt; SourceUpdatedAt = visit.UpdatedAt; EmployeeId = employeeId; UpdatedAt = now; }
    public void Decide(bool approve, Guid actor, string? reason, DateTimeOffset now)
    {
        if (Status != "PendingApproval") throw new InvalidOperationException("This request has already been reviewed.");
        if (approve && !EmployeeId.HasValue) throw new ArgumentException("Link the collector to an existing HR employee first.");
        if (!approve && string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Rejection reason is required.");
        Status = approve ? "Approved" : "Rejected"; DecisionByUserId = actor; DecisionAt = UpdatedAt = now; RejectionReason = approve ? null : reason!.Trim();
    }
    public void Cancel(DateTimeOffset now) { if (Status == "PendingApproval") { Status = "Cancelled"; UpdatedAt = now; } }
}
