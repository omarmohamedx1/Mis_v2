namespace MIS.Domain.Entities;

public sealed class AdminAuditLog
{
    private AdminAuditLog() { }

    public AdminAuditLog(Guid actorUserId, string action, string targetType, Guid? targetId, string reason,
        string? beforeJson, string? afterJson, string? sourceIp, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        Action = action.Trim();
        TargetType = targetType.Trim();
        TargetId = targetId;
        Reason = reason.Trim();
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        SourceIp = sourceIp;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public Guid? TargetId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public string? SourceIp { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
