using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.DTOs.Admin;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthorizationPolicies.AdminAccess)]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminService _service;
    public AdminController(IAdminService service) => _service = service;
    private string? SourceIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet("dashboard")]
    public Task<AdminDashboardDto> Dashboard(CancellationToken token) => _service.GetDashboardAsync(token);

    [HttpGet("reference-data")]
    public Task<AdminReferenceDataDto> ReferenceData(CancellationToken token) => _service.GetReferenceDataAsync(token);

    [HttpGet("users")]
    public Task<AdminUserListDto> Users([FromQuery] string? search, [FromQuery] string? department, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken token = default) =>
        _service.GetUsersAsync(search, department, status, page, pageSize, token);

    [HttpGet("users/{id:guid}")]
    public Task<AdminUserDto> GetUser(Guid id, CancellationToken token) => _service.GetUserAsync(id, token);

    [HttpPost("users")]
    public async Task<ActionResult<AdminCredentialIssueDto>> Create(CreateAdminUserRequest request, CancellationToken token)
    {
        var result = await _service.CreateUserAsync(request, SourceIp, token);
        return CreatedAtAction(nameof(GetUser), new { id = result.User.Id }, result);
    }

    [HttpGet("employees")]
    public Task<IReadOnlyCollection<AdminLinkableEmployeeDto>> Employees([FromQuery] string? search, [FromQuery] Guid? includeEmployeeId, CancellationToken token) =>
        _service.GetLinkableEmployeesAsync(search, includeEmployeeId, token);

    [HttpPut("users/{id:guid}/employee")]
    public Task<AdminUserDto> LinkEmployee(Guid id, LinkAdminEmployeeRequest request, CancellationToken token) =>
        _service.LinkEmployeeAsync(id, request, SourceIp, token);

    [HttpPut("users/{id:guid}/access")]
    public Task<AdminUserDto> SaveAccess(Guid id, SaveUserAccessRequest request, CancellationToken token) => _service.SaveAccessAsync(id, request, SourceIp, token);

    [HttpPatch("users/{id:guid}/status")]
    public Task<AdminUserDto> Status(Guid id, SetAdminUserStatusRequest request, CancellationToken token) => _service.SetStatusAsync(id, request, SourceIp, token);

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token)
    {
        await _service.DeleteUserAsync(id, SourceIp, token);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/reset-password")]
    public Task<AdminCredentialIssueDto> ResetPassword(Guid id, ResetAdminUserPasswordRequest request, CancellationToken token) =>
        _service.ResetPasswordAsync(id, request, SourceIp, token);

    [HttpGet("audit")]
    public Task<AdminAuditPageDto> Audit([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken token = default) =>
        _service.GetAuditAsync(search, page, pageSize, token);
}
