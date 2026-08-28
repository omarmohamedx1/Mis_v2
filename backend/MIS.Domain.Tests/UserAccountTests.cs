using MIS.Domain.Entities;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class UserAccountTests
{
    [Fact]
    public void NewUserGetsStableFormattedLoginCodeAndNormalizedEmail()
    {
        var user = new User("operator", " Operator@MIS.Local ", "hash", "Operator", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Matches("^USR-[A-F0-9]{8}$", user.LoginCode);
        Assert.Equal("operator@mis.local", user.Email);
    }

    [Fact]
    public void EmailCanChangeWithoutChangingPermanentLoginCode()
    {
        var user = new User("operator", "first@mis.local", "hash", "Operator", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var code = user.LoginCode;

        user.UpdateEmail(" Second@MIS.Local ", DateTimeOffset.UtcNow);

        Assert.Equal("second@mis.local", user.Email);
        Assert.Equal(code, user.LoginCode);
    }

    [Fact]
    public void ProvisionedUserCanBeSuspendedAndActivatedExplicitly()
    {
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var user = new User("pending.user", "pending@mis.local", "unusable", "Pending User", Guid.NewGuid(), createdAt);

        user.SetActive(false, createdAt.AddSeconds(1));
        Assert.False(user.IsActive);

        user.SetActive(true, createdAt.AddSeconds(2));
        Assert.True(user.IsActive);
        Assert.Equal(createdAt.AddSeconds(2), user.UpdatedAt);
    }

    [Fact]
    public void AccessGrantIsEffectiveOnlyAfterApprovalAndBeforeExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var grant = new UserAccessGrant(Guid.NewGuid(), "collections.case.view", "CLIENT", Guid.NewGuid(),
            "PENDING", "Approved operating need", Guid.NewGuid(), now, now.AddDays(30));

        Assert.False(grant.IsEffectiveAt(now));
        grant.Approve(Guid.NewGuid(), now.AddMinutes(1));
        Assert.True(grant.IsEffectiveAt(now.AddDays(1)));
        Assert.False(grant.IsEffectiveAt(now.AddDays(31)));
    }

    [Fact]
    public void RevokedAccessGrantStopsBeingEffectiveAndKeepsReason()
    {
        var now = DateTimeOffset.UtcNow;
        var grant = new UserAccessGrant(Guid.NewGuid(), "hr.sensitive.view", "DEPARTMENT", null,
            "ACTIVE", "Temporary compensation review", Guid.NewGuid(), now, null);
        grant.Approve(Guid.NewGuid(), now);

        grant.Revoke(Guid.NewGuid(), now.AddHours(1), "Review completed; access no longer required");

        Assert.False(grant.IsEffectiveAt(now.AddHours(2)));
        Assert.Equal("REVOKED", grant.Status);
        Assert.Equal("Review completed; access no longer required", grant.RevocationReason);
    }
}
