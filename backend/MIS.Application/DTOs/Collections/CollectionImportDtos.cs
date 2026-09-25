namespace MIS.Application.DTOs.Collections;

public sealed record PortfolioLookupDto(Guid Id, Guid OrganizationId, string Code, string Name, string CurrencyCode, bool IsActive);
public sealed record CollectionImportBatchDto(Guid Id, Guid OrganizationId, string OrganizationName, Guid PortfolioId, string PortfolioName, string FileName, string Status, int TotalRows, int ValidRows, int InvalidRows, int InsertedRows, int UpdatedRows, int SkippedRows, string UploadedBy, DateTimeOffset UploadedAt, DateTimeOffset? PreviewedAt, DateTimeOffset? ConfirmedAt, string? FailureReason);
public sealed record CollectionImportRowDto(Guid Id, int RowNumber, string AccountReference, string CustomerCode, string? CustomerName, decimal? OutstandingBalance, decimal? OverdueBalance, int? DaysPastDue, bool IsValid, IReadOnlyCollection<string> Errors, string? SheetName = null, IReadOnlyDictionary<string, string>? ExtraFields = null);
public sealed record CollectionImportPreviewDto(CollectionImportBatchDto Batch, PagedResultDto<CollectionImportRowDto> Rows);
public sealed record ConfirmCollectionImportRequest(string? Notes, IReadOnlyCollection<int>? ExcludedRowNumbers = null);
