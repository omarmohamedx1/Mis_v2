using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Hr;

public sealed record EmployeeDocumentBulkUpload(Guid Id, IReadOnlyCollection<EmployeeDocumentBulkItem> Items);
public sealed record EmployeeDocumentBulkItem(
    Guid FileId,
    string FileName,
    long Length,
    string? ContentType,
    Guid? EmployeeId,
    string? EmployeeNumber,
    string? EmployeeName,
    string? DocumentCode,
    string? DocumentName,
    string Status,
    string DuplicateAction,
    IReadOnlyCollection<string> Errors);

public sealed class EmployeeDocumentBulkCorrection
{
    public Guid FileId { get; init; }
    public Guid? EmployeeId { get; init; }
    [StringLength(64)] public string? DocumentCode { get; init; }
    [RegularExpression("^(Skip|Replace)$")] public string DuplicateAction { get; init; } = "Skip";
}

public sealed class EmployeeDocumentBulkConfirmRequest
{
    [Required, MinLength(1)] public IReadOnlyCollection<EmployeeDocumentBulkCorrection> Items { get; init; } = [];
}

public sealed record EmployeeDocumentBulkResult(int Uploaded, int Replaced, int Skipped, int Failed);
