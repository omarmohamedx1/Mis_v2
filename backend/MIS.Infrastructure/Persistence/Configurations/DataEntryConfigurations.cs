using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class DataEntryBatchConfiguration : IEntityTypeConfiguration<DataEntryBatch>
{
    public void Configure(EntityTypeBuilder<DataEntryBatch> b)
    {
        b.ToTable("DataEntryBatches");
        b.HasKey(x => x.Id);
        b.Property(x => x.BatchNumber).HasMaxLength(40).IsRequired();
        b.Property(x => x.PrimaryClassification).HasMaxLength(20);
        b.Property(x => x.SubClassification).HasMaxLength(20);
        b.Property(x => x.Source).HasMaxLength(20).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(260);
        b.Property(x => x.StorageKey).HasMaxLength(500);
        b.Property(x => x.Status).HasMaxLength(40).IsRequired();
        b.Property(x => x.RejectionReason).HasMaxLength(1000);
        b.HasIndex(x => x.BatchNumber).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.UploadedByUserId, x.CreatedAt });
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Portfolio).WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DataEntryRowConfiguration : IEntityTypeConfiguration<DataEntryRow>
{
    public void Configure(EntityTypeBuilder<DataEntryRow> b)
    {
        b.ToTable("DataEntryRows");
        b.HasKey(x => x.Id);
        b.Property(x => x.CustomerCode).HasMaxLength(100);
        b.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
        b.Property(x => x.NationalId).HasMaxLength(32);
        b.Property(x => x.MobileNumber).HasMaxLength(32);
        b.Property(x => x.Address).HasMaxLength(600);
        b.Property(x => x.Feedback).HasMaxLength(2000);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.AccountNumber).HasMaxLength(100);
        b.Property(x => x.ContractNumber).HasMaxLength(100);
        b.Property(x => x.OutstandingBalance).HasPrecision(18, 2);
        b.Property(x => x.OverdueBalance).HasPrecision(18, 2);
        b.Property(x => x.Status).HasMaxLength(40).IsRequired();
        b.Property(x => x.ErrorMessage).HasMaxLength(1000);
        b.HasIndex(x => new { x.BatchId, x.RowNumber }).IsUnique();
        b.HasIndex(x => x.CollectionCustomerId);
        b.HasOne(x => x.Batch).WithMany(x => x.Rows).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.CollectionCustomer).WithMany().HasForeignKey(x => x.CollectionCustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CollectionCase).WithMany().HasForeignKey(x => x.CollectionCaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DataEntryNotificationConfiguration : IEntityTypeConfiguration<DataEntryNotification>
{
    public void Configure(EntityTypeBuilder<DataEntryNotification> b)
    {
        b.ToTable("DataEntryNotifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(40).IsRequired();
        b.Property(x => x.MessageArabic).HasMaxLength(1000).IsRequired();
        b.Property(x => x.MessageEnglish).HasMaxLength(1000).IsRequired();
        b.HasIndex(x => new { x.BatchId, x.RecipientUserId, x.Kind }).IsUnique();
        b.HasIndex(x => new { x.RecipientUserId, x.IsRead, x.CreatedAt });
        b.HasOne(x => x.Batch).WithMany(x => x.Notifications).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.RecipientUser).WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
