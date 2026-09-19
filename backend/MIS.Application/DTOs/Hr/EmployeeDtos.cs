using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Hr;

public sealed record EmployeeOrganizationAssignmentDto(Guid Id, string Code, string NameArabic, string NameEnglish, string OrganizationType);
public sealed record EmployeeListItemDto(Guid Id, string EmployeeNumber, string FullName, Guid DepartmentId, string DepartmentName, string DepartmentCode, Guid? PositionId, string? PositionName, string? OperationalRole, bool IsActive, string Status, bool IsArchived, IReadOnlyList<EmployeeOrganizationAssignmentDto> Organizations, string? NationalId = null, string? MobileNumber = null, DateOnly? WorkStartDate = null, string? FullNameArabic = null, string? FullNameEnglish = null);
public sealed record EmployeeDetailsDto(Guid Id, string EmployeeNumber, string FullName, string? NationalId, Guid DepartmentId, string DepartmentName, string DepartmentCode, Guid? PositionId, string? PositionName, string? OperationalRole, DateOnly? WorkStartDate, DateOnly? FingerprintEnrollmentDate, DateOnly? DateOfBirth, string? Address, DateOnly? WorkEndDate, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, string Status, bool IsArchived, DateTimeOffset? ArchivedAt, string? ArchiveReason, string? MobileNumber = null, string? FullNameArabic = null, string? FullNameEnglish = null, IReadOnlyList<EmployeeOrganizationAssignmentDto>? Organizations = null, decimal? BasicSalary = null, decimal? Allowances = null, string? WorkNumber = null, string? PackageType = null);
public sealed record DepartmentOptionDto(Guid Id, string Name, string Code);
public sealed record PositionLookupDto(Guid Id, string Code, string Name, string? NameArabic, Guid? DepartmentId);
public sealed record PagedEmployeesDto(IReadOnlyCollection<EmployeeListItemDto> Items, int TotalCount, int Page, int PageSize, int TotalPages);

public sealed class SaveEmployeeRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string EmployeeNumber { get; init; } = string.Empty;
    [Required, StringLength(160, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;
    [StringLength(160)] public string? FullNameArabic { get; init; }
    [StringLength(160)] public string? FullNameEnglish { get; init; }
    [Required, StringLength(20)]
    public string NationalId { get; init; } = string.Empty;
    private string? mobileNumber;
    [StringLength(32)]
    public string? MobileNumber { get => mobileNumber; init => mobileNumber = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
    [Required]
    public Guid DepartmentId { get; init; }
    public bool IsActive { get; init; } = true;
    [RegularExpression("^(Male|Female)$")]
    public string? Gender { get; init; }
    [RegularExpression("^(Active|Inactive|OnLeave|Suspended|Terminated)$")]
    public string? Status { get; init; }
    public Guid? PositionId { get; init; }
    [StringLength(24)] public string? OperationalRole { get; init; }
    public DateOnly? WorkStartDate { get; init; }
    public DateOnly? FingerprintEnrollmentDate { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    [StringLength(500)] public string? Address { get; init; }
    public DateOnly? WorkEndDate { get; init; }
    public IReadOnlyCollection<Guid> OrganizationIds { get; init; } = Array.Empty<Guid>();
    [Range(0, 9999999999999999.99)]
    public decimal? BasicSalary { get; init; }
    [Range(0, 9999999999999999.99)]
    public decimal? Allowances { get; init; }
    [StringLength(50)] public string? WorkNumber { get; init; }
    [StringLength(80)] public string? PackageType { get; init; }
}

public sealed record EmployeeIdentityAvailabilityDto(bool EmployeeNumberTaken, bool NationalIdTaken);
public sealed class ArchiveEmployeeRequest { [Required, StringLength(500, MinimumLength = 2)] public string Reason { get; init; } = string.Empty; }
public sealed class RemoveEmployeesRequest
{
    [Required]
    public IReadOnlyCollection<Guid> Ids { get; init; } = Array.Empty<Guid>();
    public bool KeepData { get; init; } = true;
    [StringLength(500)]
    public string? Reason { get; init; }
}
public sealed record RemoveEmployeesResultDto(int Kept, int Deleted, int Skipped);
