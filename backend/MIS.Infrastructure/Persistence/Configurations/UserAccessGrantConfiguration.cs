using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class UserAccessGrantConfiguration : IEntityTypeConfiguration<UserAccessGrant>
{
    public void Configure(EntityTypeBuilder<UserAccessGrant> builder)
    {
        builder.ToTable("UserAccessGrants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PermissionCode).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ScopeType).HasMaxLength(24).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(24).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.RevocationReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.UserId, x.PermissionCode, x.ScopeType, x.ClientOrganizationId, x.Status });
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
        builder.HasOne(x => x.User).WithMany(x => x.AccessGrants).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ClientOrganization).WithMany().HasForeignKey(x => x.ClientOrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
