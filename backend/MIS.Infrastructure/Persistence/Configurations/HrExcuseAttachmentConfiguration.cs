using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;
namespace MIS.Infrastructure.Persistence.Configurations;
public sealed class HrExcuseAttachmentConfiguration : IEntityTypeConfiguration<HrExcuseAttachment>
{
    public void Configure(EntityTypeBuilder<HrExcuseAttachment> b)
    {
        b.ToTable("HrExcuseAttachments"); b.HasKey(x => x.Id);
        b.HasOne(x => x.Excuse).WithMany().HasForeignKey(x => x.ExcuseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FileName).HasMaxLength(255); b.Property(x => x.ContentType).HasMaxLength(160);
        b.Property(x => x.StorageKey).HasMaxLength(500); b.Property(x => x.Sha256Hash).HasMaxLength(64);
    }
}
