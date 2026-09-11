using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.ToTable("EmployeeDocuments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RequiredDocumentCode).HasMaxLength(40);
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.MimeType).HasMaxLength(127).IsRequired();
        builder.Property(x => x.FileSize).IsRequired();
        builder.Property(x => x.Sha256Hash).HasMaxLength(64);
        builder.Property(x => x.IssueDate).HasColumnType("date");
        builder.Property(x => x.ExpiryDate).HasColumnType("date");
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.UploadedAt).IsRequired();
        builder.Property(x => x.DeleteReason).HasMaxLength(500);
        builder.HasIndex(x => x.EmployeeId);
        builder.HasIndex(x => new { x.EmployeeId, x.ExpiryDate });
        builder.HasIndex(x => x.DocumentTypeId);
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.EmployeeId, x.RequiredDocumentCode })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE AND \"RequiredDocumentCode\" IS NOT NULL");
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DocumentTypeDefinition).WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DeletedByUser).WithMany().HasForeignKey(x => x.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_EmployeeDocuments_FileSize", "\"FileSize\" > 0");
            table.HasCheckConstraint("CK_EmployeeDocuments_DateRange", "\"IssueDate\" IS NULL OR \"ExpiryDate\" IS NULL OR \"ExpiryDate\" >= \"IssueDate\"");
            table.HasCheckConstraint("CK_EmployeeDocuments_DeleteState", "NOT \"IsDeleted\" OR (\"DeletedAt\" IS NOT NULL AND \"DeletedByUserId\" IS NOT NULL)");
        });
    }
}
