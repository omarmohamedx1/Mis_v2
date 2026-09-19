using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Hr;

public static class HrMasterDataCategories
{
    public const string Departments = "departments";
    public const string Positions = "positions";
    public const string Branches = "branches";
    public const string EmploymentTypes = "employment-types";
    public const string ContractTypes = "contract-types";
    public const string LeaveTypes = "leave-types";
    public const string DocumentTypes = "document-types";
    public const string DelegationTypes = "delegation-types";

    public static readonly IReadOnlyCollection<string> All =
    [
        Departments,
        Positions,
        Branches,
        EmploymentTypes,
        ContractTypes,
        LeaveTypes,
        DocumentTypes,
        DelegationTypes
    ];
}

public sealed record MasterDataItemDto(
    Guid Id,
    string Category,
    string Code,
    string NameEnglish,
    string? NameArabic,
    string? Description,
    Guid? DepartmentId,
    string? DepartmentName,
    string? Address,
    bool IsActive,
    decimal? DefaultAnnualEntitlement,
    bool? RequiresAttachment,
    bool? RequiresExpiryDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record MasterDataLookupDto(
    Guid Id,
    string Code,
    string NameEnglish,
    string? NameArabic,
    bool IsActive,
    Guid? DepartmentId = null);

public sealed record PagedMasterDataDto(
    IReadOnlyCollection<MasterDataItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed class SaveMasterDataRequest
{
    [Required, StringLength(32, MinimumLength = 1)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string NameEnglish { get; init; } = string.Empty;

    [StringLength(120)]
    public string? NameArabic { get; init; }

    [StringLength(500)]
    public string? Description { get; init; }

    public Guid? DepartmentId { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }

    [Range(0, 366)]
    public decimal? DefaultAnnualEntitlement { get; init; }

    public bool? RequiresAttachment { get; init; }

    public bool? RequiresExpiryDate { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed class SetActiveRequest
{
    public bool IsActive { get; init; }
}
