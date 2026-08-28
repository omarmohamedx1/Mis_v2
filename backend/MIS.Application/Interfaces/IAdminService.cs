using MIS.Application.DTOs.Admin;

namespace MIS.Application.Interfaces;

public interface IAdminService
{
    Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<AdminReferenceDataDto> GetReferenceDataAsync(CancellationToken cancellationToken);
    Task<AdminUserListDto> GetUsersAsync(string? search, string? department, string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminUserDto> GetUserAsync(Guid id, CancellationToken cancellationToken);
    Task<AdminUserDto> CreateUserAsync(CreateAdminUserRequest request, string? sourceIp, CancellationToken cancellationToken);
    Task<AdminUserDto> SaveAccessAsync(Guid id, SaveUserAccessRequest request, string? sourceIp, CancellationToken cancellationToken);
    Task<AdminUserDto> SetStatusAsync(Guid id, SetAdminUserStatusRequest request, string? sourceIp, CancellationToken cancellationToken);
    Task ResetPasswordAsync(Guid id, ResetAdminUserPasswordRequest request, string? sourceIp, CancellationToken cancellationToken);
    Task<AdminAuditPageDto> GetAuditAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
}
