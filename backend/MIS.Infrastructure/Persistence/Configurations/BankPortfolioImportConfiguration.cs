using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class BankPortfolioImportConfiguration : IEntityTypeConfiguration<BankPortfolioImport>
{
    public void Configure(EntityTypeBuilder<BankPortfolioImport> builder)
    {
        builder.ToTable("BankPortfolioImports");
        builder.HasKey(import => import.Id);
        builder.Property(import => import.PortfolioName).HasMaxLength(260).IsRequired();
        builder.Property(import => import.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(import => import.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(import => import.FileHash).HasMaxLength(64).IsRequired();
        builder.Property(import => import.StorageKey).HasMaxLength(1024).IsRequired();
        builder.Property(import => import.Status).HasMaxLength(24).IsRequired();
        builder.Property(import => import.Notes).HasMaxLength(1000);
        builder.Property(import => import.ArchiveReason).HasMaxLength(80);
        builder.Property(import => import.ArchiveNotes).HasMaxLength(1000);
        builder.Property(import => import.RestoreReason).HasMaxLength(500);
        builder.HasIndex(import => new { import.BankId, import.UploadedAt });
        builder.HasIndex(import => new { import.BankId, import.PortfolioId, import.UploadedAt });
        builder.HasIndex(import => new { import.BankId, import.IsArchived });
        builder.HasIndex(import => import.ArchivedAt);
        builder.HasIndex(import => new { import.BankId, import.PortfolioId, import.FileHash }).IsUnique();
        builder.HasOne(import => import.Bank).WithMany().HasForeignKey(import => import.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(import => import.Portfolio).WithMany()
            .HasForeignKey(import => new { import.PortfolioId, import.BankId })
            .HasPrincipalKey(portfolio => new { portfolio.Id, portfolio.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(import => import.UploadedBy).WithMany().HasForeignKey(import => import.UploadedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(import => import.ArchivedBy).WithMany().HasForeignKey(import => import.ArchivedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(import => import.RestoredBy).WithMany().HasForeignKey(import => import.RestoredById).OnDelete(DeleteBehavior.Restrict);
    }
}
