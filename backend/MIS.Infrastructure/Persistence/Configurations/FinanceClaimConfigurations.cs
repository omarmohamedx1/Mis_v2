using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class FinanceClaimConfiguration : IEntityTypeConfiguration<FinanceClaim>
{
    public void Configure(EntityTypeBuilder<FinanceClaim> b)
    {
        b.ToTable("claims", "finance", t => t.HasCheckConstraint("CK_FinanceClaims_Status", "\"Status\" IN ('PENDING','RECEIVED','CANCELLED')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Serial).HasMaxLength(40);
        b.Property(x => x.BankName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ClaimType).HasMaxLength(200).IsRequired();
        b.Property(x => x.Month).HasMaxLength(80).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.AmountWithoutVat).HasPrecision(18, 2);
        b.Property(x => x.VatPercent).HasPrecision(9, 4);
        b.Property(x => x.Vat).HasPrecision(18, 2);
        b.Property(x => x.TotalWithVat).HasPrecision(18, 2);
        b.Property(x => x.TaxAtSourcePercent).HasPrecision(9, 4);
        b.Property(x => x.TaxAtSource).HasPrecision(18, 2);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.Property(x => x.TotalInBank).HasPrecision(18, 2);
        b.Property(x => x.ClaimDate).HasColumnType("date");
        b.Property(x => x.DateInBank).HasColumnType("date");
        b.HasIndex(x => new { x.ClaimDate, x.Status });
        b.HasIndex(x => new { x.BankOrganizationId, x.ClaimType, x.Month });
        b.HasIndex(x => x.ImportBatchId);
        b.HasOne(x => x.BankOrganization).WithMany().HasForeignKey(x => x.BankOrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ImportBatch).WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UpdatedBy).WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReceiptConfirmedBy).WithMany().HasForeignKey(x => x.ReceiptConfirmedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DeletedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FinanceClaimReceiptPostingConfiguration : IEntityTypeConfiguration<FinanceClaimReceiptPosting>
{
    public void Configure(EntityTypeBuilder<FinanceClaimReceiptPosting> b)
    {
        b.ToTable("claim_receipt_postings", "finance", t => t.HasCheckConstraint("CK_FinanceClaimReceiptPostings_Amount", "\"Amount\" > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.ReceiptDate).HasColumnType("date");
        b.HasIndex(x => x.AccountingBookEntryId).IsUnique();
        b.HasIndex(x => x.ClaimId).IsUnique().HasFilter("\"ReversedAt\" IS NULL");
        b.HasOne(x => x.Claim).WithMany(x => x.ReceiptPostings).HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AccountingBookEntry).WithMany().HasForeignKey(x => x.AccountingBookEntryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReversedBy).WithMany().HasForeignKey(x => x.ReversedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FinanceClaimImportBatchConfiguration : IEntityTypeConfiguration<FinanceClaimImportBatch>
{
    public void Configure(EntityTypeBuilder<FinanceClaimImportBatch> b)
    {
        b.ToTable("claim_import_batches", "finance");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.FileHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.PreviewJson).HasColumnType("jsonb").IsRequired();
        b.HasIndex(x => x.FileHash).IsUnique();
        b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ConfirmedBy).WithMany().HasForeignKey(x => x.ConfirmedById).OnDelete(DeleteBehavior.Restrict);
    }
}
