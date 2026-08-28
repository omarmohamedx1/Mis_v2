using MIS.Application.Common;
using MIS.Application.DTOs.Auth;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;

namespace MIS.Application.Services;

public sealed class AuthService : IAuthService
{
    private const string InvalidCredentialsMessage = "Invalid username or password.";

    private readonly IPasswordHashService _passwordHashService;
    private readonly ITokenService _tokenService;
    private readonly IUserRepository _userRepository;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHashService passwordHashService,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordHashService = passwordHashService;
        _tokenService = tokenService;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var usernameOrEmail = request.Username.Trim();

        if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResult.Failure(InvalidCredentialsMessage);
        }

        var user = await _userRepository.FindByLoginIdentifierAsync(usernameOrEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return AuthResult.Failure(InvalidCredentialsMessage);
        }

        var passwordStatus = _passwordHashService.VerifyPassword(user, request.Password);

        if (passwordStatus == PasswordVerificationStatus.Failed)
        {
            return AuthResult.Failure(InvalidCredentialsMessage);
        }

        var now = DateTimeOffset.UtcNow;

        if (passwordStatus == PasswordVerificationStatus.SuccessRehashNeeded)
        {
            var updatedHash = _passwordHashService.HashPassword(user, request.Password);
            user.SetPasswordHash(updatedHash, now);
        }

        user.MarkLoggedIn(now);
        await _userRepository.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles
            .Select(userRole => userRole.Role.Name)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var permissions = ResolvePermissions(user, roles);
        var accessToken = _tokenService.GenerateAccessToken(user, roles, permissions);
        var primaryRole = roles.FirstOrDefault() ?? "User";

        var authenticatedUser = new AuthenticatedUserDto(
            user.Id,
            user.Username,
            user.Email,
            user.LoginCode,
            user.FullName,
            user.Department.Code,
            primaryRole,
            roles,
            permissions);

        return AuthResult.Success(new AuthResponse(accessToken, authenticatedUser));
    }

    private static IReadOnlyCollection<string> ResolvePermissions(MIS.Domain.Entities.User user, IReadOnlyCollection<string> roles)
    {
        var now = DateTimeOffset.UtcNow;
        var result = user.AccessGrants.Where(x => x.IsEffectiveAt(now)).Select(x => x.PermissionCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (roles.Contains(SystemRoleNames.Admin, StringComparer.OrdinalIgnoreCase)) result.Add("*");
        if (roles.Contains(SystemRoleNames.HrManager, StringComparer.OrdinalIgnoreCase))
            result.UnionWith([SystemPermissionCodes.HrAccess, SystemPermissionCodes.HrSensitiveView, "hr.employee.view", "hr.employee.manage", "hr.attendance.manage", "hr.leave.approve", "hr.report.export"]);
        if (roles.Contains(SystemRoleNames.HrOfficer, StringComparer.OrdinalIgnoreCase))
            result.UnionWith([SystemPermissionCodes.HrAccess, "hr.employee.view", "hr.employee.manage", "hr.attendance.manage"]);
        if (roles.Any(x => x.StartsWith("Collections", StringComparison.OrdinalIgnoreCase))) result.Add(SystemPermissionCodes.CollectionsAccess);
        if (roles.Contains(SystemRoleNames.CollectionsCollector, StringComparer.OrdinalIgnoreCase))
            result.UnionWith(["collections.dashboard.view", "collections.case.view", "collections.activity.manage", "collections.ptp.manage", "collections.payment.submit", "collections.visit.manage"]);
        if (roles.Contains(SystemRoleNames.CollectionsSupervisor, StringComparer.OrdinalIgnoreCase))
            result.UnionWith(["collections.dashboard.view", "collections.case.view", SystemPermissionCodes.CollectionsSensitiveView, "collections.activity.manage", SystemPermissionCodes.CollectionsAssignmentManage, "collections.ptp.manage", "collections.visit.manage", "collections.complaint.manage", "collections.report.view", SystemPermissionCodes.CollectionsReportExport]);
        if (roles.Contains(SystemRoleNames.CollectionsReviewer, StringComparer.OrdinalIgnoreCase))
            result.UnionWith(["collections.dashboard.view", "collections.case.view", SystemPermissionCodes.CollectionsPaymentApprove, "collections.report.view"]);
        if (roles.Contains(SystemRoleNames.CollectionsOperationsManager, StringComparer.OrdinalIgnoreCase))
            result.UnionWith(["collections.dashboard.view", "collections.case.view", SystemPermissionCodes.CollectionsSensitiveView, "collections.activity.manage", SystemPermissionCodes.CollectionsAssignmentManage, "collections.ptp.manage", "collections.payment.submit", SystemPermissionCodes.CollectionsPaymentApprove, "collections.visit.manage", "collections.complaint.manage", SystemPermissionCodes.CollectionsImportManage, "collections.report.view", SystemPermissionCodes.CollectionsReportExport, SystemPermissionCodes.CollectionsConfigurationManage, SystemPermissionCodes.CollectionsAuditView]);
        if (roles.Contains(SystemRoleNames.CollectionsAuditor, StringComparer.OrdinalIgnoreCase))
            result.UnionWith(["collections.dashboard.view", "collections.case.view", "collections.report.view", SystemPermissionCodes.CollectionsAuditView]);
        if (roles.Contains(SystemRoleNames.CollectionsClientViewer, StringComparer.OrdinalIgnoreCase))
            result.UnionWith(["collections.dashboard.view", "collections.case.view", "collections.report.view"]);
        return result.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
