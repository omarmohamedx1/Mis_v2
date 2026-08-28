namespace MIS.Domain.Entities;

public sealed class UserAccessGrant
{
    private UserAccessGrant() { }

    public UserAccessGrant(Guid userId, string permissionCode, string scopeType, Guid? clientOrganizationId,
        string status, string reason, Guid requestedByUserId, DateTimeOffset requestedAt, DateTimeOffset? expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Id = Guid.NewGuid();
        UserId = userId;
        PermissionCode = permissionCode.Trim().ToLowerInvariant();
        ScopeType = scopeType.Trim().ToUpperInvariant();
        ClientOrganizationId = clientOrganizationId;
        Status = status.Trim().ToUpperInvariant();
        Reason = reason.Trim();
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string PermissionCode { get; private set; } = string.Empty;
    public string ScopeType { get; private set; } = "DEPARTMENT";
    public Guid? ClientOrganizationId { get; private set; }
    public ClientOrganization? ClientOrganization { get; private set; }
    public string Status { get; private set; } = "PENDING";
    public string Reason { get; private set; } = string.Empty;
    public Guid RequestedByUserId { get; private set; }
    public Guid? GrantedByUserId { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? GrantedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }

    public bool IsEffectiveAt(DateTimeOffset now) => Status == "ACTIVE" && (ExpiresAt is null || ExpiresAt > now);

    public void Approve(Guid grantedByUserId, DateTimeOffset grantedAt)
    {
        Status = "ACTIVE";
        GrantedByUserId = grantedByUserId;
        GrantedAt = grantedAt;
        RevokedByUserId = null;
        RevokedAt = null;
        RevocationReason = null;
    }

    public void Revoke(Guid revokedByUserId, DateTimeOffset revokedAt, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = "REVOKED";
        RevokedByUserId = revokedByUserId;
        RevokedAt = revokedAt;
        RevocationReason = reason.Trim();
    }
}
