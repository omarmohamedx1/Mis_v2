using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Hr;

public sealed record EmployeeListItemDto(Guid Id, string EmployeeNumber, string FullName, Guid DepartmentId, string DepartmentName, string DepartmentCode, Guid? PositionId, string? PositionName, string? OperationalRole, bool IsActive, string Status, bool IsArchived);
public sealed record EmployeeDetailsDto(Guid Id, string EmployeeNumber, string FullName, string? NationalId, Guid DepartmentId, string DepartmentName, string DepartmentCode, Guid? PositionId, string? PositionName, string? OperationalRole, DateOnly? WorkStartDate, DateOnly? FingerprintEnrollmentDate, DateOnly? DateOfBirth, string? Address, DateOnly? WorkEndDate, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, string Status, bool IsArchived, DateTimeOffset? ArchivedAt, string? ArchiveReason);
public sealed record DepartmentOptionDto(Guid Id, string Name, string Code);
public sealed record PagedEmployeesDto(IReadOnlyCollection<EmployeeListItemDto> Items, int TotalCount, int Page, int PageSize, int TotalPages);

public sealed class SaveEmployeeRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string EmployeeNumber { get; init; } = string.Empty;
    [Required, StringLength(160, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;
    [Required, RegularExpression("^[0-9]{14}$", ErrorMessage = "National ID must contain exactly 14 digits.")]
    public string NationalId { get; init; } = string.Empty;
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
}

public sealed class ArchiveEmployeeRequest { [Required, StringLength(500, MinimumLength = 2)] public string Reason { get; init; } = string.Empty; }
