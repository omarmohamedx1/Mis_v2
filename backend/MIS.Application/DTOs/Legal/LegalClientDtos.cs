namespace MIS.Application.DTOs.Legal;

public sealed record LegalClientDashboardDto(
    int TotalClients,
    int RecentlyAddedClients,
    int TransferredFromCollections,
    int RecentlyUpdatedClientFiles,
    IReadOnlyList<LegalClientListItemDto> RecentlyAdded,
    bool CanManage);

public sealed record LegalClientListItemDto(
    Guid CustomerId,
    string Name,
    string? NationalId,
    string? MobileNumber,
    string BankName,
    string? Address,
    string Status,
    bool TransferredFromCollections,
    DateTimeOffset UpdatedAt);

public sealed record LegalClientPageDto(
    IReadOnlyList<LegalClientListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record LegalClientDocumentDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    string UploadedBy,
    DateTimeOffset UploadedAt);

public sealed record LegalClientDetailsDto(
    LegalClientListItemDto Client,
    string CustomerCode,
    string? AlternatePhone,
    string? TertiaryPhone,
    string? Governorate,
    string? Area,
    string? City,
    string? Employer,
    string? JobTitle,
    string? SecondaryAddress,
    string? Notes,
    string? CustomerNotes,
    string? CustomerFeedback,
    string? DataEntrySource,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> CollectionCaseNumbers,
    IReadOnlyList<LegalClientDocumentDto> Documents,
    bool CanManage,
    IReadOnlyList<IReadOnlyDictionary<string, string>>? ImportedRecords);

public sealed record UpdateLegalClientRequest(string? Status, string? Notes);

public sealed record LegalClientWorkbookSheetDto(string? Name, IReadOnlyList<string[]> Rows);
public sealed record LegalClientWorkbookDto(string FileName, IReadOnlyList<LegalClientWorkbookSheetDto> Sheets);
public sealed record LegalClientColumnMapping(string? SheetName, int HeaderRow, IReadOnlyDictionary<string, string> Columns,
    IReadOnlyCollection<string>? ExtraColumns = null);

public sealed record LegalClientImportSourceRow(
    int RowNumber,
    string? Name,
    string? NameArabic,
    string? NameEnglish,
    string? NationalId,
    string? MobileNumber,
    string? BankName,
    string? Address,
    string? AddressArabic,
    string? AddressEnglish,
    string? Notes,
    string? ExternalIdentifier = null,
    IReadOnlyDictionary<string, string>? OriginalFields = null);

public sealed record LegalClientImportPreviewRowDto(
    LegalClientImportSourceRow Source,
    string Status,
    string? MatchedCustomerName,
    IReadOnlyCollection<string> Errors);

public sealed record LegalClientImportPreviewDto(
    string FileName,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int ExistingCustomersMatched,
    int DuplicateRows,
    IReadOnlyCollection<LegalClientImportPreviewRowDto> Rows);

public sealed record LegalClientImportRequest(
    string FileName,
    Guid OrganizationId,
    IReadOnlyCollection<LegalClientImportSourceRow> Rows);

public sealed record LegalClientImportRowResultDto(int RowNumber, string Status, IReadOnlyCollection<string> Errors);

public sealed record LegalClientImportResultDto(
    int TotalRows,
    int SuccessfulRows,
    int FailedRows,
    IReadOnlyCollection<LegalClientImportRowResultDto> Rows);
