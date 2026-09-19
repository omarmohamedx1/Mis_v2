using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Hr;

public static class EmployeeDocumentExpiryFilters
{
    public const string All = "All";
    public const string Expired = "Expired";
    public const string ExpiringSoon = "ExpiringSoon";
    public const string Valid = "Valid";
    public const string NoExpiry = "NoExpiry";
}

public sealed class EmployeeDocumentFilterDto
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 200)] public int PageSize { get; init; } = 20;
    [StringLength(160)] public string? Search { get; init; }
    public Guid? EmployeeId { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? DocumentTypeId { get; init; }
    [StringLength(24)] public string? ExpiryStatus { get; init; }
    [Range(1, 365)] public int ExpiringWithinDays { get; init; } = 30;
    [StringLength(32)] public string SortBy { get; init; } = "uploadedAt";
    [StringLength(4)] public string SortDirection { get; init; } = "desc";
}

public sealed record EmployeeDocumentListItemDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    string DepartmentName,
    Guid? DocumentTypeId,
    string DocumentType,
    string FileName,
    string MimeType,
    long FileSize,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    string ExpiryStatus,
    int? DaysUntilExpiry,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    DateTimeOffset? UpdatedAt);

public sealed record EmployeeDocumentDetailsDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    Guid? DocumentTypeId,
    string DocumentType,
    string FileName,
    string MimeType,
    long FileSize,
    string? Sha256Hash,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    string ExpiryStatus,
    int? DaysUntilExpiry,
    string? Notes,
    Guid UploadedByUserId,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PagedEmployeeDocumentsDto(
    IReadOnlyCollection<EmployeeDocumentListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed class CreateEmployeeDocumentRequest
{
    public Guid EmployeeId { get; init; }
    public Guid DocumentTypeId { get; init; }
    public DateOnly? IssueDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    [StringLength(1000)] public string? Notes { get; init; }
}

public sealed class UpdateEmployeeDocumentRequest
{
    public Guid DocumentTypeId { get; init; }
    public DateOnly? IssueDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    [StringLength(1000)] public string? Notes { get; init; }
}

public sealed class DeleteEmployeeDocumentRequest
{
    [StringLength(500)] public string? Reason { get; init; }
}

public sealed record HrUploadFile(
    string FileName,
    string ContentType,
    long Length,
    Stream Content);

public sealed record EmployeeDocumentFile(
    Stream Content,
    string FileName,
    string ContentType);

public sealed record DocumentExpirySummaryDto(
    int Expired,
    int ExpiringWithin7Days,
    int ExpiringWithin15Days,
    int ExpiringWithin30Days);

public static class RequiredEmployeeDocumentCodes
{
    public const string BirthCertificate = "BIRTH_CERTIFICATE";
    public const string GraduationCertificate = "GRADUATION_CERTIFICATE";
    public const string NationalIdCopy = "NATIONAL_ID_COPY";
    public const string MilitaryStatus = "MILITARY_STATUS";
    public const string CriminalRecord = "CRIMINAL_RECORD";
    public const string EmploymentAppointmentPaper = "EMPLOYMENT_APPOINTMENT_PAPER";
    public const string LaborOfficeRegistration = "LABOR_OFFICE_REGISTRATION";
    public static readonly IReadOnlyCollection<string> All = [BirthCertificate, GraduationCertificate, NationalIdCopy, MilitaryStatus, CriminalRecord, EmploymentAppointmentPaper, LaborOfficeRegistration];
}

public static class PersonnelFileCompletionFilters
{
    public const string All = "All";
    public const string Complete = "Complete";
    public const string Incomplete = "Incomplete";
}

public sealed class PersonnelFileFilterDto
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 200)] public int PageSize { get; init; } = 20;
    [StringLength(160)] public string? Search { get; init; }
    public Guid? EmployeeId { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? PositionId { get; init; }
    public Guid? OrganizationId { get; init; }
    [StringLength(32)] public string? Gender { get; init; }
    [StringLength(40)] public string? CompletionStatus { get; init; }
    [StringLength(40)] public string? MissingDocumentCode { get; init; }
}

public sealed record PersonnelDocumentChecklistItemDto(
    string Code,
    string Name,
    string NameArabic,
    bool IsRequired,
    bool IsUploaded,
    Guid? DocumentId,
    string? FileName,
    string? MimeType,
    long? FileSize,
    string? UploadedBy,
    DateTimeOffset? UploadedAt,
    DateTimeOffset? UpdatedAt);

public sealed record EmployeePersonnelFileDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    string DepartmentName,
    Guid DepartmentId,
    string? PositionName,
    Guid? PositionId,
    string? Gender,
    string? NationalId,
    bool IsActive,
    bool IsArchived,
    int CompletedDocuments,
    int RequiredDocuments,
    int MissingDocuments,
    int CompletionPercentage,
    string Status,
    IReadOnlyCollection<PersonnelDocumentChecklistItemDto> Documents);

public sealed record PagedEmployeePersonnelFilesDto(
    IReadOnlyCollection<EmployeePersonnelFileDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record PersonnelFileSummaryDto(
    int TotalEmployees,
    int CompleteFiles,
    int IncompleteFiles,
    int MissingDocuments);
