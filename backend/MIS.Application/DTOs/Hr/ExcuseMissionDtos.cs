using System.ComponentModel.DataAnnotations;
namespace MIS.Application.DTOs.Hr;
public sealed class ExcuseFilter
{
    [StringLength(160)] public string? Search { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? EmployeeId { get; init; }
    public string? Type { get; init; }
    public string? Source { get; init; }
    public string? Status { get; init; }
    public DateOnly? Date { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}
public sealed record ExcuseItem(Guid Id, Guid? EmployeeId, string? EmployeeNumber, string EmployeeName, Guid? DepartmentId, string? Department, string? Position,
    string Type, string Source, DateOnly Date, TimeOnly? FromTime, TimeOnly? ToTime, string Status, Guid? VisitId, Guid? CollectorUserId,
    Guid? CaseId, string? CaseReference, string? Customer, string? Organization, string? Address, string? VisitStatus, string? VisitResult, string? VisitNotes, string? VisitCreatedBy,
    string? Reason, string? Notes, string? DecisionBy, DateTimeOffset? DecisionAt, string? RejectionReason, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool SourceChanged,
    IReadOnlyCollection<ExcuseAttachmentDto> Attachments, bool FullDay = false);
public sealed record ExcuseTypeOption(string Code, string Name, bool ReasonRequired);
public sealed record ExcusePage(IReadOnlyCollection<ExcuseItem> Items, int TotalCount, int Page, int PageSize, int TotalPages, int PendingApproval, bool CanManage, bool CanApprove);
public sealed record ExcuseAttachmentDto(Guid Id, Guid ExcuseId, string FileName, string ContentType, long Length, Guid UploadedByUserId, DateTimeOffset UploadedAt);
public sealed class ManualMissionRequest
{
    public Guid EmployeeId { get; init; }
    [Required] public string Type { get; init; } = "PersonalExcuse";
    public DateOnly Date { get; init; }
    public TimeOnly? FromTime { get; init; }
    public TimeOnly? ToTime { get; init; }
    public bool FullDay { get; init; }
    public bool ApproveImmediately { get; init; }
    public DateTimeOffset? ExpectedUpdatedAt { get; init; }
    [Required, StringLength(1000)] public string Reason { get; init; } = "";
    [StringLength(3000)] public string? Notes { get; init; }
}
public sealed record MissionDecisionRequest(bool Approve, [property: StringLength(1000)] string? Reason, DateTimeOffset ExpectedUpdatedAt, TimeOnly? ToTime = null, [property: StringLength(3000)] string? Notes = null);
public sealed record LinkCollectorEmployeeRequest(Guid EmployeeId);
public sealed record CancelMissionRequest([property: Required, StringLength(1000)] string Reason, DateTimeOffset? ExpectedUpdatedAt = null);
