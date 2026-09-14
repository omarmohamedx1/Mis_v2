using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Hr;

public sealed class SocialInsuranceImportMapping
{
    [StringLength(128)] public string? SheetName { get; init; }
    [Range(1, 1000)] public int HeaderRow { get; init; } = 1;
    [Range(2, 2000)] public int FirstDataRow { get; init; } = 2;
    [StringLength(32)] public string? DateFormat { get; init; }
    public Dictionary<string, string> Columns { get; init; } = [];
}
public sealed record SocialInsuranceImportUpload(Guid Id, string FileName, IReadOnlyCollection<AttendanceImportSheetDto> Sheets);
public sealed record SocialInsuranceImportRow(int Row, SocialInsuranceDto Record, string EmployeeNumber, string EmployeeName, string SourceEmployeeName, string Status, IReadOnlyCollection<string> Errors);
public sealed record SocialInsuranceImportPreview(Guid Id, Guid PreviewId, IReadOnlyCollection<SocialInsuranceImportRow> Rows);
public sealed record SocialInsuranceImportResult(int Imported, int Skipped, int Failed);
public sealed record SocialInsuranceImportHistory(Guid Id, string FileName, string UploadedBy, DateTimeOffset UploadedAt, int? TotalRows, SocialInsuranceImportResult? Result);
public sealed record ConfirmSocialInsuranceImportRequest(Guid PreviewId);
