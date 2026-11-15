using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class BankTargetConfigurationMapping : IEntityTypeConfiguration<BankTargetConfiguration>
{
    public void Configure(EntityTypeBuilder<BankTargetConfiguration> b)
    {
        b.ToTable("BankTargetConfigurations"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.OrganizationId, x.PortfolioId, x.PortfolioType, x.ReportingPeriod }).IsUnique();
        b.Property(x => x.PortfolioType).HasMaxLength(8).IsRequired();
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.ValuesJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne<ClientOrganization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CollectionPortfolio>().WithMany().HasForeignKey(x => new { x.PortfolioId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<BankTargetConfiguration>().WithMany().HasForeignKey(x => x.CopiedFromId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class BankTargetAuditMapping : IEntityTypeConfiguration<BankTargetAudit>
{
    public void Configure(EntityTypeBuilder<BankTargetAudit> b)
    {
        b.ToTable("BankTargetAudits"); b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(16).IsRequired();
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.ValuesJson).HasColumnType("jsonb").IsRequired();
        b.HasIndex(x => new { x.ConfigurationId, x.OccurredAt });
        b.HasOne<BankTargetConfiguration>().WithMany().HasForeignKey(x => x.ConfigurationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
