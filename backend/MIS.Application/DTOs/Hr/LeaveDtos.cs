using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Hr;

public static class HrLeaveRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyCollection<string> All =
    [
        Pending,
        Approved,
        Rejected,
        Cancelled
    ];
}

public sealed record LeaveRequestListItemDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    Guid DepartmentId,
    string DepartmentName,
    Guid? BranchId,
    string? BranchName,
    Guid LeaveTypeId,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal NumberOfDays,
    DateTimeOffset RequestDate,
    string Status);

public sealed record LeaveRequestDetailsDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    Guid DepartmentId,
    string DepartmentName,
    Guid? BranchId,
    string? BranchName,
    Guid LeaveTypeId,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal NumberOfDays,
    string? Reason,
    string? Notes,
    Guid? AttachmentDocumentId,
    string? AttachmentFileName,
    DateTimeOffset RequestDate,
    string Status,
    Guid CreatedByUserId,
    string CreatedByUsername,
    Guid? DecidedByUserId,
    string? DecidedByUsername,
    DateTimeOffset? DecidedAt,
    string? DecisionNotes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PagedLeaveRequestsDto(
    IReadOnlyCollection<LeaveRequestListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed class LeaveRequestFilterDto
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 20;

    [StringLength(160)]
    public string? Search { get; init; }

    public Guid? EmployeeId { get; init; }

    public Guid? DepartmentId { get; init; }

    public Guid? BranchId { get; init; }

    public Guid? LeaveTypeId { get; init; }

    [StringLength(32)]
    public string? Status { get; init; }

    public DateOnly? DateFrom { get; init; }

    public DateOnly? DateTo { get; init; }

    [StringLength(64)]
    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }
}

public sealed class CreateLeaveRequest
{
    [Required]
    public Guid EmployeeId { get; init; }

    [Required]
    public Guid LeaveTypeId { get; init; }

    [Required]
    public DateOnly StartDate { get; init; }

    [Required]
    public DateOnly EndDate { get; init; }

    [StringLength(2000)]
    public string? Reason { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

    public Guid? AttachmentDocumentId { get; init; }
}

public sealed class UpdateLeaveRequest
{
    [Required]
    public Guid EmployeeId { get; init; }

    [Required]
    public Guid LeaveTypeId { get; init; }

    [Required]
    public DateOnly StartDate { get; init; }

    [Required]
    public DateOnly EndDate { get; init; }

    [StringLength(2000)]
    public string? Reason { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

    public Guid? AttachmentDocumentId { get; init; }
}

public sealed record LeaveImportRowDto(int RowNumber, string EmployeeNumber, string? EmployeeName,
    string LeaveType, DateOnly? StartDate, DateOnly? EndDate, string? Reason, string Result,
    string? Message);

public sealed record LeaveImportReviewDto(Guid ImportId, string FileName, int TotalRows, int ValidRows,
    int WarningRows, int ErrorRows, IReadOnlyCollection<LeaveImportRowDto> Rows);

public sealed record LeaveImportResultDto(int ImportedRecords);
public sealed record ConfirmLeaveImportRequest(IReadOnlyCollection<int>? ExcludedRows = null);
public sealed record LeaveTemplateDto(byte[] Content, string FileName, string ContentType);

public sealed class ApproveLeaveRequest
{
    [StringLength(1000)]
    public string? Notes { get; init; }
}

public sealed class RejectLeaveRequest
{
    [Required, StringLength(1000, MinimumLength = 2)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class CancelLeaveRequest
{
    [Required, StringLength(1000, MinimumLength = 2)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record LeaveBalanceDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    Guid LeaveTypeId,
    string LeaveTypeName,
    int Year,
    decimal Entitled,
    decimal Used,
    decimal Pending,
    decimal Remaining,
    DateOnly AsOfDate);

public sealed record PagedLeaveBalancesDto(
    IReadOnlyCollection<LeaveBalanceDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record LeaveEntitlementDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    Guid LeaveTypeId,
    string LeaveTypeName,
    int Year,
    decimal BaseEntitlement,
    decimal Adjustment,
    decimal TotalEntitlement,
    string? Notes,
    Guid CreatedByUserId,
    string CreatedByUsername,
    Guid? UpdatedByUserId,
    string? UpdatedByUsername,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class UpsertLeaveEntitlementRequest
{
    [Range(0, 10000)]
    public decimal BaseEntitlement { get; init; }

    [Range(-10000, 10000)]
    public decimal Adjustment { get; init; }

    [StringLength(1000)]
    public string? Notes { get; init; }
}

public sealed class LeaveBalanceFilterDto
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 20;

    [StringLength(160)]
    public string? Search { get; init; }

    public Guid? EmployeeId { get; init; }

    public Guid? DepartmentId { get; init; }

    public Guid? BranchId { get; init; }

    public Guid? LeaveTypeId { get; init; }

    [Range(2000, 9999)]
    public int Year { get; init; } = DateTime.UtcNow.Year;
}
