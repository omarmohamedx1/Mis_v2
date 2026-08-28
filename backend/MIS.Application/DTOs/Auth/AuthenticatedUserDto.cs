namespace MIS.Application.DTOs.Auth;

public sealed record AuthenticatedUserDto(
    Guid Id,
    string Username,
    string Email,
    string LoginCode,
    string FullName,
    string Department,
    string Role,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
