using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Hr;

public sealed class AbsenceImportMapping
{
    [StringLength(128)] public string? SheetName { get; init; }
    [Range(1, 1000)] public int HeaderRow { get; init; } = 1;
    [Range(2, 2000)] public int FirstDataRow { get; init; } = 2;
    [StringLength(32)] public string? DateFormat { get; init; }
    public Dictionary<string, string> Columns { get; init; } = [];
    public List<string>? SheetNames { get; init; }
}

public sealed record AbsenceImportUpload(Guid Id, string FileName, IReadOnlyCollection<AttendanceImportSheetDto> Sheets);

public sealed record AbsenceImportRecord(
    Guid EmployeeId,
    DateOnly AbsenceDate,
    string Type,
    string Status,
    string? Reason,
    string? Notes);

public sealed record AbsenceImportRow(
    int Row,
    AbsenceImportRecord Record,
    string EmployeeNumber,
    string EmployeeName,
    string? MobileNumber,
    string SourceEmployeeName,
    string Status,
    IReadOnlyCollection<string> Errors);

public sealed record AbsenceImportPreview(Guid Id, Guid PreviewId, IReadOnlyCollection<AbsenceImportRow> Rows);
public sealed record AbsenceImportResult(int Imported, int Skipped, int Failed);
public sealed record AbsenceImportHistory(Guid Id, string FileName, string UploadedBy, DateTimeOffset UploadedAt, int? TotalRows, AbsenceImportResult? Result);
public sealed record ConfirmAbsenceImportRequest(Guid PreviewId, IReadOnlyCollection<int>? ExcludedRows = null);
