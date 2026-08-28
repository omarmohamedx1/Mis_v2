using System.Text.RegularExpressions;
using MIS.Application.Common;
using MIS.Application.DTOs.Auth;
using MIS.Application.Interfaces;

namespace MIS.Application.Services;

public sealed partial class UserProfileService : IUserProfileService
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IUserRepository _users;
    private readonly IPasswordHashService _passwords;

    public UserProfileService(ICurrentUserContext currentUser, IUserRepository users, IPasswordHashService passwords)
    {
        _currentUser = currentUser;
        _users = users;
        _passwords = passwords;
    }

    public async Task<UserProfileDto> GetAsync(CancellationToken token) => Map(await GetCurrentAsync(token));

    public async Task<UserProfileDto> ChangeEmailAsync(ChangeMyEmailRequest request, CancellationToken token)
    {
        var user = await GetCurrentAsync(token);
        EnsureCurrentPassword(user, request.CurrentPassword);
        var email = request.NewEmail.Trim().ToLowerInvariant();
        if (await _users.EmailExistsAsync(email, user.Id, token)) throw new HrConflictException("This email address is already used by another account.");
        user.UpdateEmail(email, DateTimeOffset.UtcNow);
        await _users.SaveChangesAsync(token);
        return Map(user);
    }

    public async Task ChangePasswordAsync(ChangeMyPasswordRequest request, CancellationToken token)
    {
        var user = await GetCurrentAsync(token);
        EnsureCurrentPassword(user, request.CurrentPassword);
        if (!request.NewPassword.Equals(request.ConfirmPassword, StringComparison.Ordinal)) throw new HrValidationException("New password and confirmation do not match.");
        if (request.NewPassword.Equals(request.CurrentPassword, StringComparison.Ordinal)) throw new HrValidationException("New password must be different from the current password.");
        if (!StrongPassword().IsMatch(request.NewPassword)) throw new HrValidationException("Password must contain uppercase, lowercase, number, and special character and be at least 10 characters.");
        user.SetPasswordHash(_passwords.HashPassword(user, request.NewPassword), DateTimeOffset.UtcNow);
        user.InvalidateAccess(DateTimeOffset.UtcNow);
        await _users.SaveChangesAsync(token);
    }

    private async Task<MIS.Domain.Entities.User> GetCurrentAsync(CancellationToken token) =>
        await _users.FindByIdAsync(_currentUser.UserId, token) ?? throw new HrNotFoundException("User account was not found.");

    private void EnsureCurrentPassword(MIS.Domain.Entities.User user, string password)
    {
        if (_passwords.VerifyPassword(user, password) == PasswordVerificationStatus.Failed) throw new HrValidationException("Current password is incorrect.");
    }

    private static UserProfileDto Map(MIS.Domain.Entities.User user) => new(
        user.Id, user.LoginCode, user.Username, user.Email, user.FullName, user.Department.Code,
        user.UserRoles.Select(x => x.Role.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray(), user.LastLoginAt);

    [GeneratedRegex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{10,256}$", RegexOptions.CultureInvariant)]
    private static partial Regex StrongPassword();
}
