using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class ActScorePeriodConfiguration : IEntityTypeConfiguration<ActScorePeriod>
{
    public void Configure(EntityTypeBuilder<ActScorePeriod> b)
    {
        b.ToTable("ActScorePeriods");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.OrganizationId, x.PortfolioId, x.ReportingPeriod }).IsUnique();
        b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        b.Property(x => x.RulesJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.InputJson).HasColumnType("jsonb").IsRequired();
        b.HasOne<ClientOrganization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CollectionPortfolio>().WithMany().HasForeignKey(x => new { x.PortfolioId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PreparedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
