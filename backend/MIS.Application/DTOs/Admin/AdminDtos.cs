namespace MIS.Application.DTOs.Admin;

public sealed record AdminDashboardDto(
    int TotalUsers, int ActiveUsers, int InactiveUsers, int PendingAccessRequests,
    int PrivilegedUsers, int ExpiringAccessCount, int NeverLoggedInCount,
    IReadOnlyCollection<AdminDecisionItemDto> DecisionQueue,
    IReadOnlyCollection<AdminDepartmentSummaryDto> Departments,
    IReadOnlyCollection<AdminAuditItemDto> RecentActivity);

public sealed record AdminDecisionItemDto(string Type, string Severity, int Count, string TitleAr, string TitleEn, string DescriptionAr, string DescriptionEn);
public sealed record AdminDepartmentSummaryDto(Guid Id, string Code, string NameAr, string NameEn, int TotalUsers, int ActiveUsers, int PrivilegedUsers);
public sealed record AdminUserListDto(IReadOnlyCollection<AdminUserDto> Items, int Total, int Page, int PageSize);
public sealed record AdminUserDto(Guid Id, string LoginCode, string Username, string Email, string FullName, Guid DepartmentId,
    string DepartmentCode, string DepartmentNameAr, string DepartmentNameEn, bool IsActive, DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt, IReadOnlyCollection<AdminRoleDto> Roles, IReadOnlyCollection<AdminAccessGrantDto> AccessGrants);
public sealed record AdminRoleDto(Guid Id, string Name, string? Description, bool IsSystemRole);
public sealed record AdminAccessGrantDto(Guid Id, string PermissionCode, string ScopeType, Guid? ClientOrganizationId,
    string? ClientOrganizationNameAr, string? ClientOrganizationNameEn, string Status,
    DateTimeOffset RequestedAt, DateTimeOffset? GrantedAt, DateTimeOffset? ExpiresAt);
public sealed record AdminReferenceDataDto(IReadOnlyCollection<AdminDepartmentLookupDto> Departments,
    IReadOnlyCollection<AdminRoleDto> Roles, IReadOnlyCollection<AdminClientLookupDto> Clients,
    IReadOnlyCollection<AdminPermissionDefinitionDto> Permissions);
public sealed record AdminDepartmentLookupDto(Guid Id, string Code, string NameAr, string NameEn);
public sealed record AdminClientLookupDto(Guid Id, string Code, string NameAr, string NameEn, string Type, bool IsActive);
public sealed record AdminPermissionDefinitionDto(string Code, string Group, string NameAr, string NameEn,
    string DescriptionAr, string DescriptionEn, string RiskLevel, IReadOnlyCollection<string> AllowedScopes);
public sealed record AdminAuditPageDto(IReadOnlyCollection<AdminAuditItemDto> Items, int Total, int Page, int PageSize);
public sealed record AdminAuditItemDto(Guid Id, Guid ActorUserId, string ActorName, string Action, string TargetType,
    Guid? TargetId, string? TargetName, string Details, DateTimeOffset OccurredAt, string? SourceIp);

public sealed class CreateAdminUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public string TemporaryPassword { get; set; } = string.Empty;
    public IReadOnlyCollection<Guid> RoleIds { get; set; } = [];
}

public sealed class SaveUserAccessRequest
{
    public IReadOnlyCollection<Guid> RoleIds { get; set; } = [];
    public IReadOnlyCollection<SaveAccessGrantRequest> Grants { get; set; } = [];
    public string ConfirmationPhrase { get; set; } = string.Empty;
}

public sealed class SaveAccessGrantRequest
{
    public string PermissionCode { get; set; } = string.Empty;
    public string ScopeType { get; set; } = "DEPARTMENT";
    public Guid? ClientOrganizationId { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class SetAdminUserStatusRequest
{
    public bool IsActive { get; set; }
}

public sealed class ResetAdminUserPasswordRequest
{
    public string TemporaryPassword { get; set; } = string.Empty;
}
