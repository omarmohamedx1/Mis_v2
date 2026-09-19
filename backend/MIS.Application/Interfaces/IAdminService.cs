using MIS.Application.DTOs.Admin;

namespace MIS.Application.Interfaces;

public interface IAdminService
{
    Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<AdminReferenceDataDto> GetReferenceDataAsync(CancellationToken cancellationToken);
    Task<AdminUserListDto> GetUsersAsync(string? search, string? department, string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminUserDto> GetUserAsync(Guid id, CancellationToken cancellationToken);
    Task<AdminCredentialIssueDto> CreateUserAsync(CreateAdminUserRequest request, string? sourceIp, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AdminLinkableEmployeeDto>> GetLinkableEmployeesAsync(string? search, Guid? includeEmployeeId, CancellationToken cancellationToken);
    Task<AdminUserDto> LinkEmployeeAsync(Guid id, LinkAdminEmployeeRequest request, string? sourceIp, CancellationToken cancellationToken);
    Task<AdminUserDto> SaveAccessAsync(Guid id, SaveUserAccessRequest request, string? sourceIp, CancellationToken cancellationToken);
    Task<AdminUserDto> SetStatusAsync(Guid id, SetAdminUserStatusRequest request, string? sourceIp, CancellationToken cancellationToken);
    Task DeleteUserAsync(Guid id, string? sourceIp, CancellationToken cancellationToken);
    Task<AdminCredentialIssueDto> ResetPasswordAsync(Guid id, ResetAdminUserPasswordRequest request, string? sourceIp, CancellationToken cancellationToken);
    Task<AdminAuditPageDto> GetAuditAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
}
