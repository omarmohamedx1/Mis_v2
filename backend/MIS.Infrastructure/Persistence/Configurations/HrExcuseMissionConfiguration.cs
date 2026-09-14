using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class HrExcuseMissionConfiguration : IEntityTypeConfiguration<HrExcuseMission>
{
    public void Configure(EntityTypeBuilder<HrExcuseMission> b)
    {
        b.ToTable("HrExcuseMissions", t => {
            t.HasCheckConstraint("CK_HrMission_Status", "\"Status\" IN ('PendingApproval','Approved','Rejected','Cancelled')");
            t.HasCheckConstraint("CK_HrMission_Times", "\"ToTime\" IS NULL OR (\"FromTime\" IS NOT NULL AND \"ToTime\" > \"FromTime\")");
            t.HasCheckConstraint("CK_HrMission_Source", "(\"SourceType\" = 'FieldVisit' AND \"SourceVisitId\" IS NOT NULL) OR (\"SourceType\" = 'Manual' AND \"SourceVisitId\" IS NULL)");
        });
        b.HasKey(x => x.Id); b.Property(x => x.UpdatedAt).IsConcurrencyToken(); b.Property(x => x.Type).HasMaxLength(32); b.Property(x => x.Status).HasMaxLength(24); b.Property(x => x.SourceType).HasMaxLength(16);
        b.Property(x => x.Reason).HasMaxLength(1000); b.Property(x => x.Notes).HasMaxLength(3000); b.Property(x => x.RejectionReason).HasMaxLength(1000);
        b.HasIndex(x => x.SourceVisitId).IsUnique().HasFilter("\"SourceVisitId\" IS NOT NULL"); b.HasIndex(x => new { x.EmployeeId, x.Date, x.Status });
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SourceVisit).WithMany().HasForeignKey(x => x.SourceVisitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RejectedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.SourceCollectorId).OnDelete(DeleteBehavior.Restrict);
    }
}
