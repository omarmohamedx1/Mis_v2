using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Hr;

public sealed record EmployeeProfileDto(
    Guid Id,
    string EmployeeNumber,
    string DisplayName,
    string Status,
    bool IsActive,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    string? ArchiveReason,
    bool HasProfilePhoto,
    bool CanManageCompensation,
    EmployeePersonalInformationDto Personal,
    EmployeeContactInformationDto Contact,
    EmployeeEmploymentInformationDto Employment,
    EmployeeContractInformationDto? Contract,
    EmployeeCompensationDto? Compensation,
    EmployeeEmergencyContactDto? EmergencyContact,
    EmployeeProfileCountersDto Counters,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record EmployeePersonalInformationDto(
    string? FullNameArabic,
    string? FullNameEnglish,
    string? NationalId,
    DateOnly? DateOfBirth,
    string? Gender,
    string? MaritalStatus);

public sealed record EmployeeContactInformationDto(
    string? MobileNumber,
    string? AlternativeMobileNumber,
    string? Email,
    string? Address,
    string? City);

public sealed record EmployeeEmploymentInformationDto(
    Guid DepartmentId,
    string DepartmentName,
    string DepartmentCode,
    Guid? PositionId,
    string? PositionName,
    Guid? BranchId,
    string? BranchName,
    Guid? EmploymentTypeId,
    string? EmploymentTypeName,
    Guid? DirectManagerId,
    string? DirectManagerName,
    DateOnly? HireDate,
    string? OperationalRole,
    DateOnly? FingerprintEnrollmentDate,
    DateOnly? TerminationDate,
    string Status,
    IReadOnlyList<EmployeeOrganizationAssignmentDto> Organizations,
    string? WorkNumber = null,
    string? PackageType = null);

public sealed record EmployeeContractInformationDto(
    Guid Id,
    Guid? ContractTypeId,
    string? ContractTypeName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateOnly? ProbationStartDate,
    DateOnly? ProbationEndDate,
    string Status,
    string? Notes,
    DateTimeOffset UpdatedAt);

public sealed record EmployeeCompensationDto(
    Guid Id,
    decimal BasicSalary,
    decimal Allowances,
    decimal TotalSalary,
    DateOnly EffectiveFrom,
    string? BankName,
    string? BankAccount,
    string? Iban,
    string? Notes,
    DateTimeOffset UpdatedAt);

public sealed record EmployeeEmergencyContactDto(
    Guid Id,
    string ContactName,
    string Relationship,
    string MobileNumber,
    string? AlternativeNumber,
    string? Notes,
    DateTimeOffset UpdatedAt);

public sealed record EmployeeProfileCountersDto(
    int Documents,
    int AttendanceRecords,
    int LeaveRequests,
    int Absences,
    int Delegations);

public sealed record EmployeeReportingLineDto(
    Guid EmployeeId,
    string EmployeeName,
    Guid? DirectManagerId,
    string? DirectManagerName,
    IReadOnlyCollection<ReportingLineEmployeeDto> DirectReports);

public sealed record ReportingLineEmployeeDto(Guid Id, string EmployeeNumber, string FullName, string Status);

public sealed class UpdateEmployeePersonalRequest
{
    [StringLength(160, MinimumLength = 2)]
    public string? FullNameArabic { get; init; }

    [StringLength(160, MinimumLength = 2)]
    public string? FullNameEnglish { get; init; }

    [StringLength(32, MinimumLength = 5)]
    public string? NationalId { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    [RegularExpression("^(Male|Female)$")]
    public string? Gender { get; init; }

    [StringLength(32)]
    public string? MaritalStatus { get; init; }
}

public sealed class UpdateEmployeeContactRequest
{
    [Phone, StringLength(32)]
    public string? MobileNumber { get; init; }

    [Phone, StringLength(32)]
    public string? AlternativeMobileNumber { get; init; }

    [EmailAddress, StringLength(256)]
    public string? Email { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }

    [StringLength(100)]
    public string? City { get; init; }
}

public sealed class UpdateEmployeeEmploymentRequest
{
    [Required]
    public Guid DepartmentId { get; init; }

    public Guid? PositionId { get; init; }

    public Guid? BranchId { get; init; }

    public Guid? EmploymentTypeId { get; init; }

    public Guid? DirectManagerId { get; init; }

    public DateOnly? HireDate { get; init; }

    public IReadOnlyCollection<Guid> OrganizationIds { get; init; } = Array.Empty<Guid>();
    [StringLength(50)] public string? WorkNumber { get; init; }
    [StringLength(80)] public string? PackageType { get; init; }
}

public sealed class UpdateEmployeeContractRequest
{
    public Guid? ContractTypeId { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public DateOnly? ProbationStartDate { get; init; }

    public DateOnly? ProbationEndDate { get; init; }

    [Required, StringLength(32)]
    public string Status { get; init; } = "Draft";

    [StringLength(2000)]
    public string? Notes { get; init; }
}

public sealed class UpdateEmployeeCompensationRequest
{
    [Range(0, 9999999999999999.99)]
    public decimal BasicSalary { get; init; }

    [Range(0, 9999999999999999.99)]
    public decimal Allowances { get; init; }

    public DateOnly EffectiveFrom { get; init; }

    [StringLength(160)]
    public string? BankName { get; init; }

    [StringLength(100)]
    public string? BankAccount { get; init; }

    [StringLength(64)]
    public string? Iban { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }
}

public sealed class UpdateEmployeeEmergencyContactRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string ContactName { get; init; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 2)]
    public string Relationship { get; init; } = string.Empty;

    [Required, Phone, StringLength(32)]
    public string MobileNumber { get; init; } = string.Empty;

    [Phone, StringLength(32)]
    public string? AlternativeNumber { get; init; }

    [StringLength(1000)]
    public string? Notes { get; init; }
}

public sealed class ChangeEmployeeStatusRequest
{
    [Required, StringLength(32)]
    public string Status { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Reason { get; init; }

    public DateOnly? TerminationDate { get; init; }
}
