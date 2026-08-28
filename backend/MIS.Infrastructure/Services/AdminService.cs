using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Admin;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class AdminService : IAdminService
{
    private const string ProvisionedPasswordMarker = "PROVISIONED-NO-LOGIN";
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IPasswordHashService _passwords;

    public AdminService(ApplicationDbContext db, ICurrentUserContext currentUser, IPasswordHashService passwords)
    {
        _db = db;
        _currentUser = currentUser;
        _passwords = passwords;
    }

    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var users = await _db.Users.AsNoTracking().Select(x => new { x.Id, x.IsActive, x.LastLoginAt, x.DepartmentId }).ToArrayAsync(cancellationToken);
        var activeGrants = await _db.UserAccessGrants.AsNoTracking().Where(x => x.Status == "ACTIVE" && (x.ExpiresAt == null || x.ExpiresAt > now)).ToArrayAsync(cancellationToken);
        var adminIds = await _db.UserRoles.AsNoTracking().Where(x => x.Role.Name == SystemRoleNames.Admin).Select(x => x.UserId).ToArrayAsync(cancellationToken);
        var privilegedIds = activeGrants.Where(x => AdminPermissionCatalog.IsPrivileged(x.PermissionCode)).Select(x => x.UserId).Concat(adminIds).Distinct().ToHashSet();
        var departments = await _db.Departments.AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(cancellationToken);
        var departmentSummary = departments.Select(d => new AdminDepartmentSummaryDto(d.Id, d.Code, d.NameArabic ?? d.Name, d.Name,
            users.Count(x => x.DepartmentId == d.Id), users.Count(x => x.DepartmentId == d.Id && x.IsActive),
            users.Count(x => x.DepartmentId == d.Id && privilegedIds.Contains(x.Id)))).ToArray();
        var pending = await _db.UserAccessGrants.CountAsync(x => x.Status == "PENDING", cancellationToken);
        var expiring = activeGrants.Count(x => x.ExpiresAt >= now && x.ExpiresAt <= now.AddDays(14));
        var neverLogged = users.Count(x => x.IsActive && x.LastLoginAt == null);
        var decisions = new List<AdminDecisionItemDto>();
        if (pending > 0) decisions.Add(new("PENDING_ACCESS", "HIGH", pending, "صلاحيات تنتظر قرارك", "Access awaiting review", "راجع النطاق قبل الاعتماد؛ الطلب وحده لا يمنح أي وصول.", "Review scope before approval; a request grants no access by itself."));
        if (expiring > 0) decisions.Add(new("EXPIRING_ACCESS", "MEDIUM", expiring, "صلاحيات تنتهي خلال 14 يومًا", "Access expiring in 14 days", "مدّد فقط إذا ما زالت هناك حاجة عمل موثقة.", "Extend only where a documented business need remains."));
        if (neverLogged > 0) decisions.Add(new("NEVER_LOGGED_IN", "LOW", neverLogged, "حسابات مفعلة لم تُستخدم", "Active accounts never used", "تحقق من الحاجة للحسابات لتقليل سطح المخاطر.", "Verify account need to reduce exposure."));
        var recent = await QueryAuditAsync(null).Take(8).ToArrayAsync(cancellationToken);
        return new(users.Length, users.Count(x => x.IsActive), users.Count(x => !x.IsActive), pending, privilegedIds.Count, expiring, neverLogged,
            decisions, departmentSummary, await MapAuditAsync(recent, cancellationToken));
    }

    public async Task<AdminReferenceDataDto> GetReferenceDataAsync(CancellationToken cancellationToken)
    {
        var departments = await _db.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new AdminDepartmentLookupDto(x.Id, x.Code, x.NameArabic ?? x.Name, x.Name)).ToArrayAsync(cancellationToken);
        var roles = await _db.Roles.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new AdminRoleDto(x.Id, x.Name, x.Description, x.IsSystemRole)).ToArrayAsync(cancellationToken);
        var clients = await _db.CollectionClientOrganizations.AsNoTracking().OrderBy(x => x.NameEnglish)
            .Select(x => new AdminClientLookupDto(x.Id, x.Code, x.NameArabic, x.NameEnglish, x.OrganizationType, x.IsActive)).ToArrayAsync(cancellationToken);
        return new(departments, roles, clients, AdminPermissionCatalog.All);
    }

    public async Task<AdminUserListDto> GetUsersAsync(string? search, string? department, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidatePaging(page, pageSize);
        var query = UsersQuery();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(term) || x.Username.ToLower().Contains(term) || x.Email.ToLower().Contains(term) || x.LoginCode.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(department)) query = query.Where(x => x.Department.Code == department.Trim().ToUpper());
        if (string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => x.IsActive);
        if (string.Equals(status, "INACTIVE", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => !x.IsActive);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.FullName).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return new(items.Select(MapUser).ToArray(), total, page, pageSize);
    }

    public async Task<AdminUserDto> GetUserAsync(Guid id, CancellationToken cancellationToken) =>
        MapUser(await UsersQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new HrNotFoundException("User was not found."));

    public async Task<AdminUserDto> CreateUserAsync(CreateAdminUserRequest request, string? sourceIp, CancellationToken cancellationToken)
    {
        ValidateIdentity(request.FullName, request.Username, request.Email);
        ValidatePassword(request.TemporaryPassword);
        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(x => x.Username.ToLower() == normalizedUsername || x.Email.ToLower() == normalizedEmail, cancellationToken))
            throw new HrConflictException("Username or email is already in use.");
        var department = await _db.Departments.SingleOrDefaultAsync(x => x.Id == request.DepartmentId && x.IsActive, cancellationToken)
            ?? throw new HrValidationException("Choose an active department.");
        var roles = await _db.Roles.Where(x => request.RoleIds.Contains(x.Id)).ToArrayAsync(cancellationToken);
        if (roles.Length != request.RoleIds.Distinct().Count()) throw new HrValidationException("One or more roles are invalid.");
        if (roles.Any(x => x.Name == SystemRoleNames.Admin)) throw new HrValidationException("Administrator access must be granted separately through the protected access review.");
        var now = DateTimeOffset.UtcNow;
        var user = new User(request.Username, request.Email, "temporary", request.FullName, department.Id, now);
        user.SetPasswordHash(_passwords.HashPassword(user, request.TemporaryPassword), now);
        foreach (var role in roles) user.AssignRole(role, now);
        _db.Users.Add(user);
        AddAudit("USER_CREATED", "User", user.Id, "User account created.", null, new { user.FullName, user.Username, user.Email, Department = department.Code, Roles = roles.Select(x => x.Name) }, sourceIp, now);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetUserAsync(user.Id, cancellationToken);
    }

    public async Task<AdminUserDto> SaveAccessAsync(Guid id, SaveUserAccessRequest request, string? sourceIp, CancellationToken cancellationToken)
    {
        if (id == _currentUser.UserId) throw new HrForbiddenException("An administrator cannot approve changes to their own roles or access. Ask another administrator to perform the review.");
        var user = await UsersQuery(true).SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new HrNotFoundException("User was not found.");
        var roles = await _db.Roles.Where(x => request.RoleIds.Contains(x.Id)).ToArrayAsync(cancellationToken);
        if (roles.Length != request.RoleIds.Distinct().Count()) throw new HrValidationException("One or more roles are invalid.");
        var grantsAdminAccess = roles.Any(x => x.Name == SystemRoleNames.Admin);
        if (grantsAdminAccess && !string.Equals(request.ConfirmationPhrase.Trim(), "GRANT ADMIN ACCESS", StringComparison.Ordinal))
            throw new HrValidationException("Type GRANT ADMIN ACCESS exactly to confirm administrator access.");
        var currentlyAdmin = user.UserRoles.Any(x => x.Role.Name == SystemRoleNames.Admin);
        var remainsAdmin = roles.Any(x => x.Name == SystemRoleNames.Admin);
        if (id == _currentUser.UserId && currentlyAdmin && !remainsAdmin) throw new HrForbiddenException("You cannot remove your own administrator role.");
        if (currentlyAdmin && !remainsAdmin && await ActiveAdminCountAsync(cancellationToken) <= 1) throw new HrConflictException("The last active administrator cannot lose administrator access.");
        var normalized = NormalizeGrants(request.Grants);
        var before = AccessSnapshot(user);
        var now = DateTimeOffset.UtcNow;
        var selectedRoleIds = roles.Select(x => x.Id).ToHashSet();
        var existingRoleIds = user.UserRoles.Select(x => x.RoleId).ToHashSet();
        _db.UserRoles.RemoveRange(user.UserRoles.Where(x => !selectedRoleIds.Contains(x.RoleId)));
        foreach (var role in roles.Where(x => !existingRoleIds.Contains(x.Id))) _db.UserRoles.Add(new UserRole(user.Id, role.Id, now));
        const string automaticAccessRecord = "Access configuration updated by an administrator.";
        foreach (var grant in user.AccessGrants.Where(x => x.Status is "ACTIVE" or "PENDING")) grant.Revoke(_currentUser.UserId, now, automaticAccessRecord);
        foreach (var item in normalized)
        {
            var grant = new UserAccessGrant(user.Id, item.PermissionCode, item.ScopeType, item.ClientOrganizationId, "ACTIVE", automaticAccessRecord, _currentUser.UserId, now, item.ExpiresAt);
            grant.Approve(_currentUser.UserId, now);
            _db.UserAccessGrants.Add(grant);
        }
        var collectionClientIds = normalized
            .Where(x => x.ScopeType == "CLIENT" && x.ClientOrganizationId.HasValue && x.PermissionCode.StartsWith("collections.", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ClientOrganizationId!.Value).Distinct().ToHashSet();
        var existingCollectionAccess = await _db.CollectionUserAccess.Where(x => x.UserId == user.Id).ToArrayAsync(cancellationToken);
        _db.CollectionUserAccess.RemoveRange(existingCollectionAccess);
        foreach (var clientId in collectionClientIds)
            _db.CollectionUserAccess.Add(new CollectionUserAccess(user.Id, clientId, null, now));
        user.InvalidateAccess(now);
        var after = new { Roles = roles.Select(x => x.Name), Grants = normalized.Select(x => new { x.PermissionCode, x.ScopeType, x.ClientOrganizationId, x.ExpiresAt }) };
        AddAudit("ACCESS_REVIEW_COMPLETED", "User", user.Id, $"Access updated: {roles.Length} role(s), {normalized.Count} scoped permission(s).", before, after, sourceIp, now);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetUserAsync(id, cancellationToken);
    }

    public async Task<AdminUserDto> SetStatusAsync(Guid id, SetAdminUserStatusRequest request, string? sourceIp, CancellationToken cancellationToken)
    {
        var user = await _db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new HrNotFoundException("User was not found.");
        if (id == _currentUser.UserId && !request.IsActive) throw new HrForbiddenException("You cannot suspend your own account.");
        if (request.IsActive && user.PasswordHash == ProvisionedPasswordMarker) throw new HrConflictException("Set a secure temporary password before activating this provisioned account.");
        if (!request.IsActive && user.IsActive && user.UserRoles.Any(x => x.Role.Name == SystemRoleNames.Admin) && await ActiveAdminCountAsync(cancellationToken) <= 1)
            throw new HrConflictException("The last active administrator cannot be suspended.");
        var before = new { user.IsActive };
        user.SetActive(request.IsActive, DateTimeOffset.UtcNow);
        AddAudit(request.IsActive ? "USER_ACTIVATED" : "USER_SUSPENDED", "User", user.Id,
            request.IsActive ? "User account activated." : "User account suspended.", before, new { user.IsActive }, sourceIp, DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetUserAsync(id, cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid id, ResetAdminUserPasswordRequest request, string? sourceIp, CancellationToken cancellationToken)
    {
        ValidatePassword(request.TemporaryPassword);
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new HrNotFoundException("User was not found.");
        user.SetPasswordHash(_passwords.HashPassword(user, request.TemporaryPassword), DateTimeOffset.UtcNow);
        user.InvalidateAccess(DateTimeOffset.UtcNow);
        AddAudit("PASSWORD_RESET_BY_ADMIN", "User", user.Id, "Temporary password set by an administrator.", null, new { PasswordReset = true }, sourceIp, DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminAuditPageDto> GetAuditAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidatePaging(page, pageSize);
        var query = QueryAuditAsync(search);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return new(await MapAuditAsync(rows, cancellationToken), total, page, pageSize);
    }

    private IQueryable<User> UsersQuery(bool tracking = false)
    {
        var q = _db.Users.Include(x => x.Department).Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.AccessGrants).ThenInclude(x => x.ClientOrganization).AsSplitQuery();
        return tracking ? q : q.AsNoTracking();
    }

    private static AdminUserDto MapUser(User x) => new(x.Id, x.LoginCode, x.Username, x.Email, x.FullName, x.DepartmentId,
        x.Department.Code, x.Department.NameArabic ?? x.Department.Name, x.Department.Name, x.IsActive, x.CreatedAt, x.LastLoginAt,
        x.UserRoles.Select(r => new AdminRoleDto(r.Role.Id, r.Role.Name, r.Role.Description, r.Role.IsSystemRole)).OrderBy(r => r.Name).ToArray(),
        x.AccessGrants.Where(g => g.Status is "ACTIVE" or "PENDING").Select(g => new AdminAccessGrantDto(g.Id, g.PermissionCode, g.ScopeType,
            g.ClientOrganizationId, g.ClientOrganization?.NameArabic, g.ClientOrganization?.NameEnglish, g.Status, g.RequestedAt, g.GrantedAt, g.ExpiresAt)).ToArray());

    private IReadOnlyCollection<SaveAccessGrantRequest> NormalizeGrants(IReadOnlyCollection<SaveAccessGrantRequest> grants)
    {
        var now = DateTimeOffset.UtcNow;
        var normalized = new List<SaveAccessGrantRequest>();
        foreach (var input in grants)
        {
            var code = input.PermissionCode.Trim().ToLowerInvariant();
            var scope = input.ScopeType.Trim().ToUpperInvariant();
            if (!AdminPermissionCatalog.ByCode.TryGetValue(code, out var definition)) throw new HrValidationException($"Unknown permission: {code}");
            if (!definition.AllowedScopes.Contains(scope)) throw new HrValidationException($"Scope {scope} is not allowed for {code}.");
            if (scope == "CLIENT" && input.ClientOrganizationId is null) throw new HrValidationException("A client must be selected for client-scoped access.");
            if (scope != "CLIENT" && input.ClientOrganizationId is not null) throw new HrValidationException("Client can only be set with CLIENT scope.");
            if (input.ExpiresAt <= now) throw new HrValidationException("Access expiry must be in the future.");
            normalized.Add(new SaveAccessGrantRequest { PermissionCode = code, ScopeType = scope, ClientOrganizationId = input.ClientOrganizationId, ExpiresAt = input.ExpiresAt });
        }
        if (normalized.GroupBy(x => $"{x.PermissionCode}|{x.ScopeType}|{x.ClientOrganizationId}").Any(x => x.Count() > 1)) throw new HrValidationException("Duplicate permission entries are not allowed.");
        return normalized;
    }

    private object AccessSnapshot(User user) => new { Roles = user.UserRoles.Select(x => x.Role.Name), Grants = user.AccessGrants.Where(x => x.Status is "ACTIVE" or "PENDING").Select(x => new { x.PermissionCode, x.ScopeType, x.ClientOrganizationId, x.Status, x.ExpiresAt }) };
    private void AddAudit(string action, string targetType, Guid? targetId, string reason, object? before, object? after, string? sourceIp, DateTimeOffset now) =>
        _db.AdminAuditLogs.Add(new AdminAuditLog(_currentUser.UserId, action, targetType, targetId, reason,
            before is null ? null : JsonSerializer.Serialize(before), after is null ? null : JsonSerializer.Serialize(after), sourceIp, now));
    private IQueryable<AdminAuditLog> QueryAuditAsync(string? search)
    {
        var q = _db.AdminAuditLogs.AsNoTracking().OrderByDescending(x => x.OccurredAt).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); q = q.Where(x => x.Action.ToLower().Contains(term) || x.Reason.ToLower().Contains(term) || x.TargetType.ToLower().Contains(term)); }
        return q;
    }
    private async Task<IReadOnlyCollection<AdminAuditItemDto>> MapAuditAsync(IReadOnlyCollection<AdminAuditLog> rows, CancellationToken cancellationToken)
    {
        var ids = rows.SelectMany(x => x.TargetId is null ? new[] { x.ActorUserId } : new[] { x.ActorUserId, x.TargetId.Value }).Distinct().ToArray();
        var names = await _db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        return rows.Select(x => new AdminAuditItemDto(x.Id, x.ActorUserId, names.GetValueOrDefault(x.ActorUserId, "Unknown"), x.Action, x.TargetType,
            x.TargetId, x.TargetId is Guid id ? names.GetValueOrDefault(id) : null, x.Reason, x.OccurredAt, x.SourceIp)).ToArray();
    }
    private Task<int> ActiveAdminCountAsync(CancellationToken cancellationToken) => _db.Users.CountAsync(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == SystemRoleNames.Admin), cancellationToken);
    private static void ValidatePaging(int page, int pageSize) { if (page < 1 || pageSize is < 1 or > 100) throw new HrValidationException("Page must be at least 1 and pageSize between 1 and 100."); }
    private static void ValidateIdentity(string fullName, string username, string email)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 2 || fullName.Length > 160) throw new HrValidationException("Enter a valid full name.");
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3 || username.Length > 100 || !username.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-')) throw new HrValidationException("Username must be 3-100 letters, numbers, dots, dashes, or underscores.");
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !email.Contains('@')) throw new HrValidationException("Enter a valid email address.");
    }
    private static void ValidatePassword(string value)
    {
        if (value.Length < 12 || !value.Any(char.IsUpper) || !value.Any(char.IsLower) || !value.Any(char.IsDigit) || !value.Any(c => !char.IsLetterOrDigit(c)))
            throw new HrValidationException("Temporary password must be at least 12 characters and include upper, lower, number, and symbol.");
    }
}
