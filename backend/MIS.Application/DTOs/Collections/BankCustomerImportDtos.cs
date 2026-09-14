using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Collections;

public sealed record BankCustomerImportUpload(
    Guid Id,
    string FileName,
    IReadOnlyCollection<BankCustomerImportSheet> Sheets);

public sealed record BankCustomerImportSheet(
    string? SheetName,
    int SuggestedHeaderRowNumber,
    IReadOnlyCollection<string> DetectedColumns);

public sealed class BankCustomerImportMapping
{
    public string? SheetName { get; init; }
    [Range(1, 100)] public int HeaderRow { get; init; } = 1;
    [Range(2, 101)] public int FirstDataRow { get; init; } = 2;
    public Guid? PortfolioId { get; init; }
    public Dictionary<string, string?> Columns { get; init; } = new();
}

public sealed record BankCustomerImportPreview(
    Guid Id,
    Guid PreviewId,
    Guid PortfolioId,
    string PortfolioName,
    IReadOnlyCollection<BankCustomerImportRow> Rows);

public sealed record BankCustomerImportRow(
    int Row,
    string CustomerName,
    string? Mobile,
    string? NationalId,
    string? AccountReference,
    string? ContractReference,
    decimal? Outstanding,
    decimal? Paid,
    decimal? Remaining,
    string Status,
    IReadOnlyCollection<string> Errors);

public sealed record BankCustomerImportResult(int Imported, int Skipped, int Failed);
