namespace MIS.Domain.Entities;

public sealed class User
{
    private readonly List<UserRole> _userRoles = [];
    private readonly List<UserAccessGrant> _accessGrants = [];

    private User()
    {
    }

    public User(string username, string email, string passwordHash, string fullName, Guid departmentId, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        Id = Guid.NewGuid();
        LoginCode = $"USR-{Id:N}"[..12].ToUpperInvariant();
        Username = username.Trim();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        FullName = fullName.Trim();
        DepartmentId = departmentId;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string LoginCode { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public Guid DepartmentId { get; private set; }

    public Department Department { get; private set; } = null!;

    public bool IsActive { get; private set; } = true;

    public int AccessVersion { get; private set; } = 1;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    public IReadOnlyCollection<UserAccessGrant> AccessGrants => _accessGrants.AsReadOnly();

    public void SetPasswordHash(string passwordHash, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
        UpdatedAt = updatedAt;
    }

    public void UpdateEmail(string email, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Email = email.Trim().ToLowerInvariant();
        UpdatedAt = updatedAt;
    }

    public void MarkLoggedIn(DateTimeOffset loggedInAt)
    {
        LastLoginAt = loggedInAt;
        UpdatedAt = loggedInAt;
    }

    public void SetActive(bool isActive, DateTimeOffset updatedAt)
    {
        IsActive = isActive;
        AccessVersion++;
        UpdatedAt = updatedAt;
    }

    public void InvalidateAccess(DateTimeOffset updatedAt)
    {
        AccessVersion++;
        UpdatedAt = updatedAt;
    }

    public void UpdateIdentity(string fullName, string email, Guid departmentId, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        DepartmentId = departmentId;
        UpdatedAt = updatedAt;
    }

    public void AssignRole(Role role, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (_userRoles.Any(userRole => userRole.RoleId == role.Id))
        {
            return;
        }

        _userRoles.Add(new UserRole(Id, role.Id, createdAt));
    }
}
