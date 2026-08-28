using System.Security.Claims;
using MIS.Application.Common;
using MIS.Application.Interfaces;

namespace MIS.API.Authentication;

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User
        ?? throw new HrForbiddenException("An authenticated user is required.");

    public Guid UserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId)
                ? userId
                : throw new HrForbiddenException("The authenticated user identifier is invalid.");
        }
    }

    public string Username => User.Identity?.Name ?? "unknown";

    public IReadOnlyCollection<string> Roles => User.FindAll(ClaimTypes.Role)
        .Select(claim => claim.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyCollection<string> Permissions => User.FindAll(MIS.Domain.Constants.SystemPermissionCodes.ClaimType)
        .Select(claim => claim.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
