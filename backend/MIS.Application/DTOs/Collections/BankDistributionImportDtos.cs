using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Collections;

public sealed record BankDistributionImportUpload(
    Guid Id,
    string FileName,
    IReadOnlyCollection<BankDistributionImportSheet> Sheets);

public sealed record BankDistributionImportSheet(
    string? SheetName,
    int SuggestedHeaderRowNumber,
    IReadOnlyCollection<string> DetectedColumns);

public sealed class BankDistributionImportMapping
{
    public string? SheetName { get; init; }
    [Range(1, 100)] public int HeaderRow { get; init; } = 1;
    [Range(2, 101)] public int FirstDataRow { get; init; } = 2;
    /// <summary>FILE or AUTO</summary>
    [Required, MaxLength(20)] public string Mode { get; init; } = "FILE";
    public Dictionary<string, string?> Columns { get; init; } = new();
    public IReadOnlyCollection<Guid>? CollectorIds { get; init; }
    public bool ReassignExisting { get; init; }
    [MaxLength(500)] public string? Reason { get; init; }
}

public sealed record BankDistributionImportPreview(
    Guid Id,
    Guid PreviewId,
    string Mode,
    bool ReassignExisting,
    int ReadyRows,
    int AlreadyAssignedRows,
    int InvalidRows,
    IReadOnlyCollection<BankDistributionImportRow> Rows,
    IReadOnlyCollection<AutoDistributionCollectorDto> AutoPlan);

public sealed record BankDistributionImportRow(
    int Row,
    Guid? CaseId,
    string? CaseNumber,
    string? AccountReference,
    string? CustomerName,
    string? CurrentCollectorName,
    Guid? NewCollectorId,
    string? NewCollectorName,
    string OrganizationName,
    string Status,
    IReadOnlyCollection<string> Errors);

public sealed record BankDistributionImportResult(
    int Assigned,
    int Reassigned,
    int Skipped,
    int Failed);
