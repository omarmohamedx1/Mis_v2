using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Hr;

public static class HrAttendanceStatuses
{
    public const string Present = "Present";
    public const string Absent = "Absent";
    public const string Late = "Late";
    public const string Leave = "Leave";
    public const string Holiday = "Holiday";
    public const string Weekend = "Weekend";

    public static readonly IReadOnlyCollection<string> All =
    [
        Present,
        Absent,
        Late,
        Leave,
        Holiday,
        Weekend
    ];
}

public static class HrAttendanceSources
{
    public const string ExcelImport = "ExcelImport";
    public const string Manual = "Manual";
    public const string DeviceIntegration = "DeviceIntegration";
    public const string SystemProcessing = "SystemProcessing";

    public static readonly IReadOnlyCollection<string> All =
    [
        ExcelImport,
        Manual,
        DeviceIntegration,
        SystemProcessing
    ];
}

public static class HrAttendanceImportLayouts
{
    public const string CheckInCheckOutColumns = "CheckInCheckOutColumns";
    public const string PunchRows = "PunchRows";

    public static readonly IReadOnlyCollection<string> All =
    [
        CheckInCheckOutColumns,
        PunchRows
    ];
}

public static class HrAttendanceImportBatchStatuses
{
    public const string Uploaded = "Uploaded";
    public const string PreviewReady = "PreviewReady";
    public const string Confirmed = "Confirmed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyCollection<string> All =
    [
        Uploaded,
        PreviewReady,
        Confirmed,
        Failed,
        Cancelled
    ];
}

public static class HrAttendanceImportCategories
{
    public const string Valid = "Valid";
    public const string Invalid = "Invalid";
    public const string EmployeeNotFound = "EmployeeNotFound";
    public const string Duplicate = "Duplicate";
    public const string MissingCheckIn = "MissingCheckIn";
    public const string MissingCheckOut = "MissingCheckOut";

    public static readonly IReadOnlyCollection<string> All =
    [
        Valid,
        Invalid,
        EmployeeNotFound,
        Duplicate,
        MissingCheckIn,
        MissingCheckOut
    ];
}

public sealed record AttendanceListItemDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    Guid DepartmentId,
    string DepartmentName,
    Guid? BranchId,
    string? BranchName,
    DateOnly AttendanceDate,
    DateTimeOffset? CheckIn,
    DateTimeOffset? CheckOut,
    decimal WorkingHours,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeMinutes,
    string Status,
    string Source,
    bool IsManuallyAdjusted);

public sealed record AttendanceDetailsDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    Guid DepartmentId,
    string DepartmentName,
    Guid? BranchId,
    string? BranchName,
    DateOnly AttendanceDate,
    DateTimeOffset? CheckIn,
    DateTimeOffset? CheckOut,
    decimal WorkingHours,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeMinutes,
    string Status,
    string Source,
    string? Notes,
    Guid? ImportBatchId,
    bool IsManuallyAdjusted,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PagedAttendanceRecordsDto(
    IReadOnlyCollection<AttendanceListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed class AttendanceFilterDto
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

    public DateOnly? DateFrom { get; init; }

    public DateOnly? DateTo { get; init; }

    [StringLength(32)]
    public string? Status { get; init; }

    [StringLength(32)]
    public string? Source { get; init; }

    [StringLength(64)]
    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }
}

public sealed class CreateManualAttendanceRequest
{
    [Required]
    public Guid EmployeeId { get; init; }

    [Required]
    public DateOnly AttendanceDate { get; init; }

    public DateTimeOffset? CheckIn { get; init; }

    public DateTimeOffset? CheckOut { get; init; }

    [Required, StringLength(32)]
    public string Status { get; init; } = HrAttendanceStatuses.Present;

    [StringLength(2000)]
    public string? Notes { get; init; }
}

public sealed class UpdateManualAttendanceRequest
{
    [Required]
    public Guid EmployeeId { get; init; }

    [Required]
    public DateOnly AttendanceDate { get; init; }

    public DateTimeOffset? CheckIn { get; init; }

    public DateTimeOffset? CheckOut { get; init; }

    [Required, StringLength(32)]
    public string Status { get; init; } = HrAttendanceStatuses.Present;

    [StringLength(2000)]
    public string? Notes { get; init; }
}

public sealed class DeleteAttendanceRequest
{
    [StringLength(500)]
    public string? Reason { get; init; }
}

public sealed class ProcessAttendanceDayRequest
{
    public DateOnly AttendanceDate { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}

public sealed record ProcessAttendanceDayResultDto(
    DateOnly AttendanceDate,
    int CreatedRecords,
    int Absent,
    int OnLeave,
    int Holiday,
    int Weekend,
    int ExistingRecordsSkipped);

public sealed record AttendanceImportFile(
    string FileName,
    string? ContentType,
    long Length,
    Stream Content);

public sealed record AttendanceImportSheetDto(
    string? SheetName,
    int SuggestedHeaderRowNumber,
    IReadOnlyCollection<string> DetectedColumns,
    string SuggestedTimeSystem = "24-hour");

public sealed record AttendanceImportUploadDto(
    Guid BatchId,
    string FileName,
    long FileSize,
    string FileHash,
    string Status,
    IReadOnlyCollection<AttendanceImportSheetDto> Sheets,
    DateTimeOffset UploadedAt);

public sealed class AttendanceImportColumnMappingRequest
{
    [StringLength(128)]
    public string? SheetName { get; init; }

    [Range(1, 1000)]
    public int HeaderRowNumber { get; init; } = 1;

    [Range(1, 1000000)]
    public int DataStartRowNumber { get; init; } = 2;

    [Required, StringLength(32)]
    public string Layout { get; init; } = HrAttendanceImportLayouts.CheckInCheckOutColumns;

    [Required, StringLength(160)]
    public string EmployeeNumberColumn { get; init; } = string.Empty;

    [StringLength(160)]
    public string? EmployeeNameColumn { get; init; }

    [StringLength(160)]
    public string? AttendanceDateColumn { get; init; }

    [StringLength(160)]
    public string? CheckInColumn { get; init; }

    [StringLength(160)]
    public string? CheckOutColumn { get; init; }

    [StringLength(160)]
    public string? PunchDateTimeColumn { get; init; }

    [StringLength(160)]
    public string? PunchTypeColumn { get; init; }

    [StringLength(160)]
    public string? TimeColumn { get; init; }

    [StringLength(160)]
    public string? AmPmColumn { get; init; }

    [StringLength(32)]
    public string? DateFormat { get; init; }

    [StringLength(32)]
    public string? TimeFormat { get; init; }

    [Required, RegularExpression("^(24-hour|12-hour)$")]
    public string TimeSystem { get; init; } = "24-hour";

    [StringLength(20)]
    public string? CultureName { get; init; }

    [Required, StringLength(100)]
    public string TimeZoneId { get; init; } = string.Empty;
}

public sealed record AttendanceImportSummaryDto(
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int EmployeeNotFoundRows,
    int DuplicateRows,
    int MissingCheckInRows,
    int MissingCheckOutRows);

public sealed record AttendanceImportBatchDto(
    Guid BatchId,
    string FileName,
    string FileHash,
    string Status,
    AttendanceImportColumnMappingRequest? Mapping,
    AttendanceImportSummaryDto? Summary,
    string? FailureReason,
    DateTimeOffset UploadedAt,
    DateTimeOffset? PreviewedAt,
    DateTimeOffset? ConfirmedAt);

public sealed record AttendanceImportPreviewRowDto(
    Guid Id,
    Guid BatchId,
    IReadOnlyCollection<int> SourceRowNumbers,
    string? SourceEmployeeNumber,
    string? SourceEmployeeName,
    Guid? EmployeeId,
    string? EmployeeNumber,
    string? EmployeeName,
    DateOnly? AttendanceDate,
    DateTimeOffset? CheckIn,
    DateTimeOffset? CheckOut,
    IReadOnlyCollection<DateTimeOffset> Punches,
    bool CanImport,
    IReadOnlyCollection<string> Categories,
    IReadOnlyCollection<string> Errors,
    IReadOnlyCollection<Dictionary<string, string?>> SourceRows);

public sealed record PagedAttendanceImportPreviewDto(
    IReadOnlyCollection<AttendanceImportPreviewRowDto> Items,
    AttendanceImportSummaryDto Summary,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed class AttendanceImportPreviewFilterDto
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 50;

    [StringLength(32)]
    public string? Category { get; init; }

    [StringLength(160)]
    public string? Search { get; init; }
}

public sealed class ConfirmAttendanceImportRequest
{
    public bool IncludeRowsWithWarnings { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}

public sealed class CancelAttendanceImportRequest
{
    [StringLength(500)]
    public string? Notes { get; init; }
}

public sealed record AttendanceImportConfirmResultDto(
    Guid BatchId,
    int ImportedRecords,
    int SkippedRows,
    int DuplicateRows,
    int FailedRows,
    DateTimeOffset ConfirmedAt);

public sealed record AttendanceImportHistoryItemDto(
    Guid BatchId,
    string FileName,
    string FileHash,
    string Status,
    AttendanceImportSummaryDto? Summary,
    int ImportedRecords,
    Guid UploadedByUserId,
    string UploadedByUsername,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ConfirmedAt);

public sealed record PagedAttendanceImportHistoryDto(
    IReadOnlyCollection<AttendanceImportHistoryItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed class AttendanceImportHistoryFilterDto
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 20;

    [StringLength(255)]
    public string? Search { get; init; }

    [StringLength(32)]
    public string? Status { get; init; }

    public DateOnly? UploadedFrom { get; init; }

    public DateOnly? UploadedTo { get; init; }
}
