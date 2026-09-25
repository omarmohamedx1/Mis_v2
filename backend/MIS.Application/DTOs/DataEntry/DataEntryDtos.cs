using MIS.Application.DTOs.Hr;

namespace MIS.Application.DTOs.DataEntry;

public sealed record DataEntryDashboardDto(int DraftBatches, int SubmittedBatches, int AcceptedBatches, int DistributedBatches, int RejectedBatches, int MyClients, int UnreadNotifications);

public sealed record DataEntryClientListItemDto(
    Guid Id,
    string CustomerNumber,
    string CustomerName,
    string? MobileNumber,
    string OrganizationName,
    string? Source,
    string? CaseNumber,
    Guid? CaseId,
    string? CaseStatus,
    string? BatchStatus,
    IReadOnlyList<string>? Phones = null,
    string? NationalId = null,
    string? Address = null,
    string? Feedback = null,
    string? Data = null,
    IReadOnlyDictionary<string, string>? Fields = null);
public sealed record DataEntryClientPageDto(IReadOnlyList<DataEntryClientListItemDto> Items, int Page, int PageSize, int TotalCount);

public sealed record DataEntryClientDetailsDto(
    Guid Id,
    string CustomerNumber,
    string? CustomerNameArabic,
    string? CustomerNameEnglish,
    string? NationalId,
    string? MobileNumber,
    string? AlternateMobile,
    string? Address,
    string? Feedback,
    string? Notes,
    string? Source,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationType,
    string? PortfolioCode,
    string? PortfolioName,
    string? PrimaryClassification,
    string? SubClassification,
    string? CaseNumber,
    Guid? CaseId,
    string? AccountNumber,
    string? ContractNumber,
    decimal? OutstandingBalance,
    decimal? OverdueBalance,
    int? DaysPastDue,
    string? CaseStatus,
    Guid? BatchId,
    string? BatchNumber,
    string? BatchStatus,
    IReadOnlyList<string>? Phones = null,
    IReadOnlyDictionary<string, string>? Fields = null);

public sealed record CreateDataEntryClientRequest(
    Guid OrganizationId,
    Guid? PortfolioId,
    string? PrimaryClassification,
    string? SubClassification,
    string CustomerName,
    string? NationalId,
    string? MobileNumber,
    string? Address,
    string? Feedback,
    string? Notes,
    string? AccountNumber,
    string? ContractNumber,
    decimal? OutstandingBalance);

public sealed record DataEntryOrganizationDto(Guid Id, string Code, string NameArabic, string NameEnglish, string OrganizationType);
public sealed record DataEntryPortfolioDto(Guid Id, string Code, string NameArabic, string NameEnglish, string? PrimaryClassification, string? SubClassification);

public sealed record DataEntryImportUploadDto(Guid UploadId, string FileName, IReadOnlyList<DataEntryImportSheetDto> Sheets);
public sealed record DataEntryImportSheetDto(string SheetName, int SuggestedHeaderRowNumber, IReadOnlyList<string> DetectedColumns);
public sealed record DataEntrySheetPreviewDto(
    string FileName,
    string SheetName,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    int TotalRows,
    bool Truncated);
public sealed record DataEntryImportMappingRequest(
    Guid OrganizationId,
    Guid? PortfolioId,
    string? PrimaryClassification,
    string? SubClassification,
    string SheetName,
    int HeaderRow,
    int FirstDataRow,
    Dictionary<string, string?> Columns,
    IReadOnlyCollection<string>? SheetNames = null);

public sealed record DataEntryImportPreviewDto(
    Guid UploadId,
    Guid PreviewId,
    int TotalRows,
    int ReadyRows,
    int ExistingCustomerRows,
    int InvalidRows,
    IReadOnlyList<DataEntryImportPreviewRowDto> Rows);

public sealed record DataEntryImportPreviewRowDto(
    int RowNumber,
    string? CustomerNumber,
    string CustomerName,
    string? NationalId,
    string? MobileNumber,
    string Status,
    string? ErrorMessage,
    Guid? CollectionCustomerId = null,
    Guid? CollectionCaseId = null);

public sealed record ConfirmDataEntryImportRequest(Guid UploadId, Guid PreviewId, IReadOnlyCollection<int>? ExcludedRowNumbers = null);

public sealed record DataEntryBatchListItemDto(
    Guid Id,
    string BatchNumber,
    string FileName,
    string UploadedBy,
    DateTimeOffset CreatedAt,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int CustomerCount,
    string OrganizationName,
    string? PrimaryClassification,
    string? SubClassification,
    string Status,
    string Source);

public sealed record DataEntryBatchPageDto(IReadOnlyList<DataEntryBatchListItemDto> Items, int Page, int PageSize, int TotalCount);

public sealed record DataEntryBatchDetailsDto(
    DataEntryBatchListItemDto Summary,
    string? RejectionReason,
    DateTimeOffset? SubmittedAt,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset? DistributedAt,
    IReadOnlyList<DataEntryImportPreviewRowDto> Rows,
    IReadOnlyList<DataEntryDocumentDto> Documents);

public sealed record DataEntryDocumentDto(
    Guid Id,
    Guid CustomerId,
    Guid? BatchId,
    Guid? CaseId,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    string? Note,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    bool CanDownload);

public sealed record DataEntryDocumentDownloadDto(Stream Content, string ContentType, string FileName);

public sealed record RejectDataEntryBatchRequest(string Reason);
public sealed record DataEntryNotificationDto(Guid Id, Guid BatchId, string BatchNumber, string Kind, string MessageArabic, string MessageEnglish, bool IsRead, DateTimeOffset CreatedAt);
