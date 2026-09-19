using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Admin;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Hr;
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
        var users = await _db.Users.AsNoTracking().Select(x => new { x.Id, x.IsActive, x.LastLoginAt, x.DepartmentId, x.MustChangePassword }).ToArrayAsync(cancellationToken);
        var activeGrants = await _db.UserAccessGrants.AsNoTracking().Where(x => x.Status == "ACTIVE" && (x.ExpiresAt == null || x.ExpiresAt > now)).ToArrayAsync(cancellationToken);
        var adminIds = await _db.UserRoles.AsNoTracking().Where(x => x.Role.Name == SystemRoleNames.Admin).Select(x => x.UserId).ToArrayAsync(cancellationToken);
        var privilegedIds = activeGrants.Where(x => AdminPermissionCatalog.IsPrivileged(x.PermissionCode)).Select(x => x.UserId).Concat(adminIds).Distinct().ToHashSet();
        var departments = await _db.Departments.AsNoTracking().Where(x => x.IsActive).ToArrayAsync(cancellationToken);
        var departmentSummary = departments
            .Where(d => DepartmentCodes.IsOperationalUnit(d.Code))
            .OrderBy(d => DepartmentCodes.CompanyDirectoryOrder(d.Code))
            .Select(d => new AdminDepartmentSummaryDto(d.Id, d.Code, d.NameArabic ?? d.Name, d.Name,
                users.Count(x => x.DepartmentId == d.Id), users.Count(x => x.DepartmentId == d.Id && x.IsActive),
                users.Count(x => x.DepartmentId == d.Id && privilegedIds.Contains(x.Id))))
            .ToArray();
        var pending = await _db.UserAccessGrants.CountAsync(x => x.Status == "PENDING", cancellationToken);
        var expiring = activeGrants.Count(x => x.ExpiresAt >= now && x.ExpiresAt <= now.AddDays(14));
        var neverLogged = users.Count(x => x.IsActive && x.LastLoginAt == null);
        var mustChange = users.Count(x => x.IsActive && x.MustChangePassword);
        var decisions = new List<AdminDecisionItemDto>();
        if (pending > 0) decisions.Add(new("PENDING_ACCESS", "HIGH", pending, "صلاحيات تنتظر قرارك", "Access awaiting review", "راجع النطاق قبل الاعتماد؛ الطلب وحده لا يمنح أي وصول.", "Review scope before approval; a request grants no access by itself."));
        if (expiring > 0) decisions.Add(new("EXPIRING_ACCESS", "MEDIUM", expiring, "صلاحيات تنتهي خلال 14 يومًا", "Access expiring in 14 days", "مدّد فقط إذا ما زالت هناك حاجة عمل موثقة.", "Extend only where a documented business need remains."));
        if (mustChange > 0) decisions.Add(new("MUST_CHANGE_PASSWORD", "MEDIUM", mustChange, "كلمات مرور مؤقتة لم تُغيَّر", "Temporary passwords not changed", "الموظف لم يستبدل كلمة المرور المؤقتة بعد أول دخول.", "The employee has not replaced the temporary password after first sign-in."));
        if (neverLogged > 0) decisions.Add(new("NEVER_LOGGED_IN", "LOW", neverLogged, "حسابات مفعلة لم تُستخدم", "Active accounts never used", "تحقق من الحاجة للحسابات لتقليل سطح المخاطر.", "Verify account need to reduce exposure."));
        var recent = await QueryAuditAsync(null).Take(8).ToArrayAsync(cancellationToken);
        return new(users.Length, users.Count(x => x.IsActive), users.Count(x => !x.IsActive), pending, privilegedIds.Count, expiring, neverLogged, mustChange,
            decisions, departmentSummary, await MapAuditAsync(recent, cancellationToken));
    }

    public async Task<AdminReferenceDataDto> GetReferenceDataAsync(CancellationToken cancellationToken)
    {
        var departments = (await _db.Departments.AsNoTracking().Where(x => x.IsActive).ToArrayAsync(cancellationToken))
            .Where(x => DepartmentCodes.IsOperationalUnit(x.Code))
            .OrderBy(x => DepartmentCodes.CompanyDirectoryOrder(x.Code))
            .Select(x => new AdminDepartmentLookupDto(x.Id, x.Code, x.NameArabic ?? x.Name, x.Name))
            .ToArray();
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

    public async Task<AdminCredentialIssueDto> CreateUserAsync(CreateAdminUserRequest request, string? sourceIp, CancellationToken cancellationToken)
    {
        ValidateIdentity(request.FullName, request.Username, request.Email);
        var password = ResolveTemporaryPassword(request.TemporaryPassword);
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
        user.SetPasswordHash(_passwords.HashPassword(user, password), now);
        user.RequirePasswordChange(now);
        foreach (var role in roles) user.AssignRole(role, now);
        _db.Users.Add(user);
        if (request.EmployeeId.HasValue)
        {
            var employee = await _db.Employees.Include(x => x.Department).SingleOrDefaultAsync(x => x.Id == request.EmployeeId, cancellationToken)
                ?? throw new HrNotFoundException("Employee was not found.");
            await UserEmployeeLinker.LinkAsync(_db, user, employee, now, cancellationToken);
            department = await _db.Departments.SingleAsync(x => x.Id == user.DepartmentId, cancellationToken);
        }
        AddAudit("USER_CREATED", "User", user.Id, "User account created with a one-time password.", null, new { user.FullName, user.Username, user.Email, Department = department.Code, Roles = roles.Select(x => x.Name), user.EmployeeId, MustChangePassword = true }, sourceIp, now);
        await _db.SaveChangesAsync(cancellationToken);
        return new(await GetUserAsync(user.Id, cancellationToken), password);
    }

    public async Task<IReadOnlyCollection<AdminLinkableEmployeeDto>> GetLinkableEmployeesAsync(string? search, Guid? includeEmployeeId, CancellationToken cancellationToken)
    {
        var query = _db.Employees.AsNoTracking().Include(x => x.Department)
            .Where(x => x.IsActive && !x.IsArchived
                && x.OperationalRole != EmployeeOperationalRoles.Office
                && x.Department.Code != DepartmentCodes.Office
                && (!_db.Users.Any(u => u.EmployeeId == x.Id) || x.Id == includeEmployeeId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(term)
                || (x.FullNameArabic != null && x.FullNameArabic.ToLower().Contains(term))
                || (x.FullNameEnglish != null && x.FullNameEnglish.ToLower().Contains(term))
                || x.EmployeeNumber.ToLower().Contains(term));
        }
        var rows = await query.OrderBy(x => x.EmployeeNumber).Take(200)
            .Select(x => new { x.Id, x.EmployeeNumber, x.FullName, x.DepartmentId, x.Department.Code, NameAr = x.Department.NameArabic ?? x.Department.Name, NameEn = x.Department.Name, x.OperationalRole, x.Email })
            .ToArrayAsync(cancellationToken);
        var ids = rows.Select(x => x.Id).ToArray();
        var links = await _db.Users.AsNoTracking().Where(x => x.EmployeeId != null && ids.Contains(x.EmployeeId.Value))
            .ToDictionaryAsync(x => x.EmployeeId!.Value, x => x.Id, cancellationToken);
        return rows.Select(x => new AdminLinkableEmployeeDto(x.Id, x.EmployeeNumber, x.FullName, x.DepartmentId, x.Code, x.NameAr, x.NameEn, x.OperationalRole, x.Email, links.GetValueOrDefault(x.Id))).ToArray();
    }

    public async Task<AdminUserDto> LinkEmployeeAsync(Guid id, LinkAdminEmployeeRequest request, string? sourceIp, CancellationToken cancellationToken)
    {
        var user = await _db.Users.Include(x => x.Department).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new HrNotFoundException("User was not found.");
        var now = DateTimeOffset.UtcNow;
        var before = new { user.EmployeeId, user.DepartmentId };
        if (!request.EmployeeId.HasValue)
        {
            UserEmployeeLinker.Unlink(user, now);
            AddAudit("USER_EMPLOYEE_UNLINKED", "User", user.Id, "Employee link removed from the user account.", before, new { user.EmployeeId }, sourceIp, now);
        }
        else
        {
            var employee = await _db.Employees.Include(x => x.Department).SingleOrDefaultAsync(x => x.Id == request.EmployeeId, cancellationToken)
                ?? throw new HrNotFoundException("Employee was not found.");
            await UserEmployeeLinker.LinkAsync(_db, user, employee, now, cancellationToken);
            AddAudit("USER_EMPLOYEE_LINKED", "User", user.Id, "Employee linked to the user account.", before, new { user.EmployeeId, employee.EmployeeNumber, employee.OperationalRole }, sourceIp, now);
        }
        await _db.SaveChangesAsync(cancellationToken);
        return await GetUserAsync(id, cancellationToken);
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

    public async Task DeleteUserAsync(Guid id, string? sourceIp, CancellationToken cancellationToken)
    {
        if (id == _currentUser.UserId) throw new HrForbiddenException("You cannot delete your own account.");
        var user = await _db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.AccessGrants)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new HrNotFoundException("User was not found.");
        if (user.UserRoles.Any(x => x.Role.Name == SystemRoleNames.Admin) && await _db.Users.CountAsync(x => x.UserRoles.Any(r => r.Role.Name == SystemRoleNames.Admin), cancellationToken) <= 1)
            throw new HrConflictException("The last administrator cannot be deleted.");

        var used =
            await _db.CollectionCases.AnyAsync(x => x.AssignedCollectorId == id || x.FileCollectorUserId == id || x.PreviousCollectorUserId == id, cancellationToken)
            || await _db.CollectionTeams.AnyAsync(x => x.SupervisorId == id, cancellationToken)
            || await _db.CollectionAuditLogs.AnyAsync(x => x.UserId == id, cancellationToken)
            || await _db.HrAuditLogs.AnyAsync(x => x.UserId == id, cancellationToken)
            || await _db.AdminAuditLogs.AnyAsync(x => x.ActorUserId == id, cancellationToken)
            || await _db.CollectionPayments.AnyAsync(x => x.SubmittedById == id || x.VerifiedById == id, cancellationToken)
            || await _db.AccountingCollectorCommissions.AnyAsync(x => x.CollectorUserId == id, cancellationToken)
            || await _db.AccountingSupervisorCommissions.AnyAsync(x => x.SupervisorUserId == id, cancellationToken)
            || await _db.LegalCaseFiles.AnyAsync(x => x.ReceivedByUserId == id, cancellationToken)
            || await _db.LegalCaseActions.AnyAsync(x => x.CreatedByUserId == id, cancellationToken)
            || await _db.Employees.AnyAsync(x => x.ArchivedByUserId == id, cancellationToken);

        if (used)
            throw new HrConflictException("This account has operational history and cannot be deleted. Suspend the account instead.");

        var now = DateTimeOffset.UtcNow;
        AddAudit("USER_DELETED", "User", user.Id, $"Deleted unused account {user.Username}.", new { user.Username, user.FullName, user.IsActive, user.LastLoginAt }, null, sourceIp, now);
        user.UnlinkEmployee(now);
        _db.CollectionUserAccess.RemoveRange(await _db.CollectionUserAccess.Where(x => x.UserId == id).ToListAsync(cancellationToken));
        _db.CollectionTeamMembers.RemoveRange(await _db.CollectionTeamMembers.Where(x => x.UserId == id).ToListAsync(cancellationToken));
        _db.Users.Remove(user);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new HrConflictException("This account has operational history and cannot be deleted. Suspend the account instead.");
        }
    }

    public async Task<AdminCredentialIssueDto> ResetPasswordAsync(Guid id, ResetAdminUserPasswordRequest request, string? sourceIp, CancellationToken cancellationToken)
    {
        var password = ResolveTemporaryPassword(request.TemporaryPassword);
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new HrNotFoundException("User was not found.");
        var now = DateTimeOffset.UtcNow;
        var activated = !user.IsActive;
        user.SetPasswordHash(_passwords.HashPassword(user, password), now);
        user.RequirePasswordChange(now);
        if (activated)
            user.SetActive(true, now);
        else
            user.InvalidateAccess(now);
        AddAudit("PASSWORD_RESET_BY_ADMIN", "User", user.Id, activated
            ? "Temporary password issued and the account was activated for first sign-in."
            : "Temporary password issued by an administrator. The user must change it at next sign-in.",
            null, new { PasswordReset = true, MustChangePassword = true, Activated = activated }, sourceIp, now);
        await _db.SaveChangesAsync(cancellationToken);
        return new(await GetUserAsync(id, cancellationToken), password);
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
        var q = _db.Users.Include(x => x.Department).Include(x => x.Employee)
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.AccessGrants).ThenInclude(x => x.ClientOrganization).AsSplitQuery();
        return tracking ? q : q.AsNoTracking();
    }

    private static AdminUserDto MapUser(User x) => new(x.Id, x.LoginCode, x.Username, x.Email, x.FullName, x.DepartmentId,
        x.Department.Code, x.Department.NameArabic ?? x.Department.Name, x.Department.Name, x.IsActive, x.CreatedAt, x.LastLoginAt, x.MustChangePassword,
        x.UserRoles.Select(r => new AdminRoleDto(r.Role.Id, r.Role.Name, r.Role.Description, r.Role.IsSystemRole)).OrderBy(r => r.Name).ToArray(),
        x.AccessGrants.Where(g => g.Status is "ACTIVE" or "PENDING").Select(g => new AdminAccessGrantDto(g.Id, g.PermissionCode, g.ScopeType,
            g.ClientOrganizationId, g.ClientOrganization?.NameArabic, g.ClientOrganization?.NameEnglish, g.Status, g.RequestedAt, g.GrantedAt, g.ExpiresAt)).ToArray(),
        x.EmployeeId, x.Employee?.EmployeeNumber, x.Employee?.FullName, x.Employee?.OperationalRole);

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
    private static string ResolveTemporaryPassword(string? value)
    {
        var password = string.IsNullOrWhiteSpace(value) ? TemporaryPasswordGenerator.Create() : value.Trim();
        ValidatePassword(password);
        return password;
    }
    private static void ValidatePassword(string value)
    {
        if (value.Length < 12 || !value.Any(char.IsUpper) || !value.Any(char.IsLower) || !value.Any(char.IsDigit) || !value.Any(c => !char.IsLetterOrDigit(c)))
            throw new HrValidationException("Temporary password must be at least 12 characters and include upper, lower, number, and symbol.");
    }
}
