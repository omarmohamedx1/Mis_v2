using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Hr;

public sealed class EmployeeImportMapping
{
    [StringLength(128)] public string? SheetName { get; init; }
    [Range(1, 1000)] public int HeaderRow { get; init; } = 1;
    [Range(2, 2000)] public int FirstDataRow { get; init; } = 2;
    [StringLength(32)] public string? DateFormat { get; init; }
    public Dictionary<string, string> Columns { get; init; } = [];
}
public sealed record EmployeeImportUpload(Guid Id, string FileName, IReadOnlyCollection<AttendanceImportSheetDto> Sheets);
public sealed record EmployeeImportRow(int Row, SaveEmployeeRequest Employee, string Department, string Position, string Status, IReadOnlyCollection<string> Errors);
public sealed record EmployeeImportPreview(Guid Id, Guid PreviewId, IReadOnlyCollection<EmployeeImportRow> Rows);
public sealed record EmployeeImportResult(int Imported, int Skipped, int Failed);
public sealed record EmployeeImportHistory(Guid Id, string FileName, string UploadedBy, DateTimeOffset UploadedAt, int? TotalRows, EmployeeImportResult? Result);
public sealed record ConfirmEmployeeImportRequest(Guid PreviewId);
