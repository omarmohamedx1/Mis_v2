using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Constants;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class LegalCaseFileConfiguration : IEntityTypeConfiguration<LegalCaseFile>
{
    public void Configure(EntityTypeBuilder<LegalCaseFile> builder)
    {
        builder.ToTable("LegalCaseFiles", table =>
        {
            table.HasCheckConstraint("CK_LegalCaseFiles_Stage",
                $"\"Stage\" IN ('{LegalValues.Stages.Intake}','{LegalValues.Stages.Notice}','{LegalValues.Stages.Court}','{LegalValues.Stages.Hearing}','{LegalValues.Stages.Judgment}','{LegalValues.Stages.Settlement}','{LegalValues.Stages.Returned}')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Stage).HasMaxLength(24).IsRequired();
        builder.Property(x => x.CourtName).HasMaxLength(160);
        builder.Property(x => x.CourtCaseNumber).HasMaxLength(80);
        builder.Property(x => x.LawyerName).HasMaxLength(160);
        builder.Property(x => x.NextHearingOn).HasColumnType("date");
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne(x => x.CollectionCase).WithMany().HasForeignKey(x => x.CollectionCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReceivedByUser).WithMany().HasForeignKey(x => x.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CollectionCaseId).IsUnique();
        builder.HasIndex(x => x.Stage);
        builder.HasIndex(x => x.NextHearingOn);
    }
}

public sealed class LegalCaseActionConfiguration : IEntityTypeConfiguration<LegalCaseAction>
{
    public void Configure(EntityTypeBuilder<LegalCaseAction> builder)
    {
        builder.ToTable("LegalCaseActions", table =>
        {
            table.HasCheckConstraint("CK_LegalCaseActions_ActionType",
                $"\"ActionType\" IN ('{LegalValues.Actions.Note}','{LegalValues.Actions.Notice}','{LegalValues.Actions.Hearing}','{LegalValues.Actions.Judgment}','{LegalValues.Actions.Settlement}','{LegalValues.Actions.Return}')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActionType).HasMaxLength(24).IsRequired();
        builder.Property(x => x.Result).HasMaxLength(120);
        builder.Property(x => x.Notes).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.HappenedOn).HasColumnType("date");
        builder.HasOne(x => x.File).WithMany(x => x.Actions).HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FileId, x.CreatedAt });
    }
}
