namespace MIS.Application.DTOs.Collections;

public sealed record BankPortfolioImportDto(
    Guid Id, Guid BankId, string BankNameArabic, string BankNameEnglish, string PortfolioName,
    string OriginalFileName, string FileType, long FileSize, int RowCount, string Status,
    Guid UploadedByUserId, string UploadedBy, DateTimeOffset UploadedAt, DateTimeOffset? ConfirmedAt,
    string? Notes, DateTimeOffset? UpdatedAt, Guid? PortfolioId = null,
    int ImportedRows = 0, int UpdatedRows = 0, int DuplicateRows = 0, int InvalidRows = 0);

public sealed record BankPortfolioImportPageDto(
    IReadOnlyCollection<BankPortfolioImportDto> Items, int TotalCount, int Page, int PageSize, int TotalPages);

public sealed record UpdateBankPortfolioImportRequest(string? Notes);
public sealed record BankPortfolioReplacementPreviewDto(string Token, string OriginalFileName, string FileType, long FileSize, int RowCount);
public sealed record ConfirmBankPortfolioReplacementRequest(string Token);

public sealed record BankPortfolioImportDataPreviewDto(
    Guid ImportId,
    int TotalRows,
    int ReadyRows,
    int ExistingRows,
    int InvalidRows,
    IReadOnlyCollection<BankPortfolioImportPreviewRowDto> Rows,
    int DuplicateRows = 0,
    IReadOnlyCollection<BankPortfolioImportColumnMappingDto>? DetectedColumns = null);

public sealed record BankPortfolioImportColumnMappingDto(string Field, string SourceColumn);

public sealed record BankPortfolioImportPreviewRowDto(
    int RowNumber,
    string? CustomerName,
    string? CustomerCode,
    string? AccountReference,
    decimal? OutstandingBalance,
    string Status,
    IReadOnlyCollection<string> Errors,
    string? NationalId = null,
    string? CardNumber = null,
    string? Bucket = null,
    string? ImportStatus = null,
    decimal? TotalDues = null);

public sealed record BankPortfolioImportConfirmResultDto(
    BankPortfolioImportDto Import,
    int Imported,
    int Skipped,
    int Invalid,
    int Updated = 0,
    int Duplicates = 0);
