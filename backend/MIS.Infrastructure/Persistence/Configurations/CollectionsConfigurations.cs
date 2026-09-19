using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

internal static class CollectionsConfigurationExtensions
{
    public static PropertyBuilder<decimal> Money(this PropertyBuilder<decimal> property) => property.HasPrecision(18, 2);
    public static PropertyBuilder<decimal?> Money(this PropertyBuilder<decimal?> property) => property.HasPrecision(18, 2);
}

public sealed class ClientOrganizationConfiguration : IEntityTypeConfiguration<ClientOrganization>
{
    public void Configure(EntityTypeBuilder<ClientOrganization> b)
    {
        b.ToTable("CollectionClientOrganizations"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(40).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.NameArabic).HasMaxLength(200).IsRequired(); b.Property(x => x.NameEnglish).HasMaxLength(200).IsRequired();
        b.Property(x => x.OrganizationType).HasMaxLength(40).IsRequired(); b.Property(x => x.LogoStorageKey).HasMaxLength(1024);
        b.Property(x => x.ContactEmail).HasMaxLength(256); b.Property(x => x.ContactPhone).HasMaxLength(32); b.Property(x => x.SettingsJson).HasColumnType("jsonb").HasDefaultValue("{}");
        b.HasIndex(x => new { x.IsActive, x.OrganizationType });
    }
}

public sealed class CollectionPortfolioConfiguration : IEntityTypeConfiguration<CollectionPortfolio>
{
    public void Configure(EntityTypeBuilder<CollectionPortfolio> b)
    {
        b.ToTable("CollectionPortfolios"); b.HasKey(x => x.Id); b.Property(x => x.Code).HasMaxLength(60).IsRequired();
        b.Property(x => x.NameArabic).HasMaxLength(200).IsRequired(); b.Property(x => x.NameEnglish).HasMaxLength(200).IsRequired(); b.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        b.Property(x => x.TargetAmount).Money(); b.Property(x => x.SettingsJson).HasColumnType("jsonb").HasDefaultValue("{}"); b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
        b.Property(x => x.PrimaryClassification).HasMaxLength(20); b.Property(x => x.SubClassification).HasMaxLength(20);
        b.HasIndex(x => new { x.OrganizationId, x.PrimaryClassification, x.SubClassification }).IsUnique()
            .HasFilter("\"PrimaryClassification\" IS NOT NULL AND \"SubClassification\" IS NOT NULL");
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CollectionCustomerConfiguration : IEntityTypeConfiguration<CollectionCustomer>
{
    public void Configure(EntityTypeBuilder<CollectionCustomer> b)
    {
        b.ToTable("CollectionCustomers"); b.HasKey(x => x.Id); b.Property(x => x.CustomerCode).HasMaxLength(100).IsRequired();
        b.Property(x => x.FullNameArabic).HasMaxLength(200); b.Property(x => x.FullNameEnglish).HasMaxLength(200); b.Property(x => x.NationalId).HasMaxLength(32);
        b.Property(x => x.PrimaryPhone).HasMaxLength(32); b.Property(x => x.AlternatePhone).HasMaxLength(32); b.Property(x => x.AddressArabic).HasMaxLength(600); b.Property(x => x.AddressEnglish).HasMaxLength(600);
        b.Property(x => x.Governorate).HasMaxLength(100); b.Property(x => x.Area).HasMaxLength(100); b.Property(x => x.Employer).HasMaxLength(200);
        b.Property(x => x.TertiaryPhone).HasMaxLength(32); b.Property(x => x.City).HasMaxLength(100); b.Property(x => x.JobTitle).HasMaxLength(200);
        b.Property(x => x.SecondaryAddress).HasMaxLength(600);
        b.Property(x => x.Feedback).HasMaxLength(2000); b.Property(x => x.Notes).HasMaxLength(2000); b.Property(x => x.DataEntrySource).HasMaxLength(20);
        b.HasIndex(x => new { x.OrganizationId, x.CustomerCode }).IsUnique(); b.HasIndex(x => new { x.OrganizationId, x.NationalId }).IsUnique().HasFilter("\"NationalId\" IS NOT NULL"); b.HasIndex(x => x.PrimaryPhone); b.HasIndex(x => x.NationalId);
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DelinquencyBucketDefinitionConfiguration : IEntityTypeConfiguration<DelinquencyBucketDefinition>
{
    public void Configure(EntityTypeBuilder<DelinquencyBucketDefinition> b)
    {
        b.ToTable("CollectionBucketDefinitions"); b.HasKey(x => x.Id); b.Property(x => x.Code).HasMaxLength(40).IsRequired(); b.Property(x => x.NameArabic).HasMaxLength(100).IsRequired(); b.Property(x => x.NameEnglish).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.OrganizationId, x.PortfolioId, x.Code }).IsUnique(); b.HasIndex(x => new { x.OrganizationId, x.PortfolioId, x.SortOrder });
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Portfolio).WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CollectionTeamConfiguration : IEntityTypeConfiguration<CollectionTeam>
{
    public void Configure(EntityTypeBuilder<CollectionTeam> b)
    { b.ToTable("CollectionTeams"); b.HasKey(x => x.Id); b.Property(x => x.Code).HasMaxLength(50).IsRequired(); b.Property(x => x.NameArabic).HasMaxLength(160).IsRequired(); b.Property(x => x.NameEnglish).HasMaxLength(160).IsRequired(); b.HasIndex(x => x.Code).IsUnique(); b.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CollectionTeamMemberConfiguration : IEntityTypeConfiguration<CollectionTeamMember>
{
    public void Configure(EntityTypeBuilder<CollectionTeamMember> b)
    { b.ToTable("CollectionTeamMembers"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.TeamId, x.UserId }).IsUnique(); b.HasIndex(x => new { x.UserId, x.IsActive }); b.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CollectionUserAccessConfiguration : IEntityTypeConfiguration<CollectionUserAccess>
{
    public void Configure(EntityTypeBuilder<CollectionUserAccess> b)
    { b.ToTable("CollectionUserAccess"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.UserId, x.OrganizationId, x.PortfolioId }).IsUnique(); b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Portfolio).WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CollectionCaseConfiguration : IEntityTypeConfiguration<CollectionCase>
{
    public void Configure(EntityTypeBuilder<CollectionCase> b)
    {
        b.ToTable("CollectionCases", table => { table.HasCheckConstraint("CK_CollectionCases_Amounts", "\"OriginalAmount\" >= 0 AND \"OutstandingBalance\" >= 0 AND \"OverdueBalance\" >= 0"); table.HasCheckConstraint("CK_CollectionCases_Dpd", "\"DaysPastDue\" >= 0"); }); b.HasKey(x => x.Id);
        b.Property(x => x.CaseNumber).HasMaxLength(80).IsRequired(); b.HasIndex(x => x.CaseNumber).IsUnique(); b.Property(x => x.AccountReference).HasMaxLength(120).IsRequired(); b.Property(x => x.ContractReference).HasMaxLength(120); b.Property(x => x.ProductType).HasMaxLength(100);
        b.Property(x => x.CardNumber).HasMaxLength(64); b.Property(x => x.ImportStatusText).HasMaxLength(100); b.Property(x => x.Stage).HasMaxLength(100); b.Property(x => x.CreditLimit).Money(); b.Property(x => x.PurchaseAvailableLimit).Money(); b.Property(x => x.LastPaymentAmount).Money(); b.Property(x => x.LastTransactionAmount).Money(); b.Property(x => x.ImportRawJson).HasColumnType("jsonb"); b.HasIndex(x => x.CardNumber);
        b.Property(x => x.PreviousCollectorName).HasMaxLength(200); b.Property(x => x.FileCollectorName).HasMaxLength(200); b.Property(x => x.ImportBucketLabel).HasMaxLength(100);
        b.Property(x => x.OriginalAmount).Money(); b.Property(x => x.PrincipalAmount).Money(); b.Property(x => x.OutstandingBalance).Money(); b.Property(x => x.OverdueBalance).Money(); b.Property(x => x.Penalties).Money(); b.Property(x => x.Fees).Money(); b.Property(x => x.TotalDue).Money();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired(); b.Property(x => x.Priority).HasMaxLength(20).IsRequired(); b.Property(x => x.PriorityExplanation).HasMaxLength(1000);
        b.HasIndex(x => new { x.PortfolioId, x.AccountReference }).IsUnique(); b.HasIndex(x => new { x.Status, x.AssignedCollectorId, x.PriorityScore }); b.HasIndex(x => new { x.CurrentBucketId, x.DaysPastDue }); b.HasIndex(x => x.NextFollowUpAt);
        b.HasOne(x => x.Portfolio).WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CurrentBucket).WithMany().HasForeignKey(x => x.CurrentBucketId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.AssignedCollector).WithMany().HasForeignKey(x => x.AssignedCollectorId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.FileCollectorUser).WithMany().HasForeignKey(x => x.FileCollectorUserId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.PreviousCollectorUser).WithMany().HasForeignKey(x => x.PreviousCollectorUserId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => x.FileCollectorUserId); b.HasIndex(x => x.PreviousCollectorUserId); b.HasOne(x => x.AssignedTeam).WithMany().HasForeignKey(x => x.AssignedTeamId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.SourceImport).WithMany().HasForeignKey(x => x.SourceImportId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => x.SourceImportId);
        b.HasOne(x => x.ArchivedBy).WithMany().HasForeignKey(x => x.ArchivedById).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.RestoredBy).WithMany().HasForeignKey(x => x.RestoredById).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.ArchiveReason).HasMaxLength(80); b.Property(x => x.ArchiveNotes).HasMaxLength(1000); b.Property(x => x.RestoreReason).HasMaxLength(500); b.HasIndex(x => new { x.PortfolioId, x.IsArchived }); b.HasIndex(x => x.ArchivedAt);
    }
}

public sealed class CaseBucketHistoryConfiguration : IEntityTypeConfiguration<CaseBucketHistory>
{
    public void Configure(EntityTypeBuilder<CaseBucketHistory> b) { b.ToTable("CollectionCaseBucketHistory"); b.HasKey(x => x.Id); b.Property(x => x.Reason).HasMaxLength(500).IsRequired(); b.Property(x => x.Source).HasMaxLength(32).IsRequired(); b.HasIndex(x => new { x.CaseId, x.ChangedAt }); b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict); b.HasOne<DelinquencyBucketDefinition>().WithMany().HasForeignKey(x => x.PreviousBucketId).OnDelete(DeleteBehavior.Restrict); b.HasOne<DelinquencyBucketDefinition>().WithMany().HasForeignKey(x => x.NewBucketId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.ChangedBy).WithMany().HasForeignKey(x => x.ChangedById).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CollectionAssignmentHistoryConfiguration : IEntityTypeConfiguration<CollectionAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<CollectionAssignmentHistory> b) { b.ToTable("CollectionAssignmentHistory"); b.HasKey(x => x.Id); b.Property(x => x.Reason).HasMaxLength(500).IsRequired(); b.Property(x => x.Source).HasMaxLength(32).IsRequired(); b.Property(x => x.RuleCode).HasMaxLength(80); b.HasIndex(x => new { x.CaseId, x.AssignedAt }); b.HasIndex(x => new { x.AssignedToId, x.AssignedAt }); b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.PreviousAssignee).WithMany().HasForeignKey(x => x.PreviousAssigneeId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.AssignedTo).WithMany().HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.Restrict).IsRequired(false); b.HasOne(x => x.AssignedBy).WithMany().HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CollectionActivityConfiguration : IEntityTypeConfiguration<CollectionActivity>
{
    public void Configure(EntityTypeBuilder<CollectionActivity> b) { b.ToTable("CollectionActivities"); b.HasKey(x => x.Id); b.Property(x => x.ActivityType).HasMaxLength(40).IsRequired(); b.Property(x => x.Result).HasMaxLength(100); b.Property(x => x.Notes).HasMaxLength(4000); b.Property(x => x.Channel).HasMaxLength(40); b.HasIndex(x => new { x.CaseId, x.CreatedAt }); b.HasIndex(x => new { x.CreatedById, x.NextFollowUpAt }); b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class PromiseToPayConfiguration : IEntityTypeConfiguration<PromiseToPay>
{
    public void Configure(EntityTypeBuilder<PromiseToPay> b) { b.ToTable("CollectionPromisesToPay", table => table.HasCheckConstraint("CK_CollectionPromises_Amount", "\"PromisedAmount\" > 0 AND \"ActualPaidAmount\" >= 0")); b.HasKey(x => x.Id); b.Property(x => x.PromisedAmount).Money(); b.Property(x => x.ActualPaidAmount).Money(); b.Property(x => x.PromiseDate).HasColumnType("date"); b.Property(x => x.Channel).HasMaxLength(40).IsRequired(); b.Property(x => x.Notes).HasMaxLength(2000); b.Property(x => x.Status).HasMaxLength(32).IsRequired(); b.HasIndex(x => new { x.Status, x.PromiseDate }); b.HasIndex(x => new { x.CaseId, x.PromiseDate }); b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Collector).WithMany().HasForeignKey(x => x.CollectorId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CollectionPaymentConfiguration : IEntityTypeConfiguration<CollectionPayment>
{
    public void Configure(EntityTypeBuilder<CollectionPayment> b) { b.ToTable("CollectionPayments", table => table.HasCheckConstraint("CK_CollectionPayments_Amount", "\"Amount\" > 0")); b.HasKey(x => x.Id); b.Property(x => x.Amount).Money(); b.Property(x => x.PaymentDate).HasColumnType("date"); b.Property(x => x.Method).HasMaxLength(40).IsRequired(); b.Property(x => x.ReferenceNumber).HasMaxLength(160).IsRequired(); b.Property(x => x.ProofStorageKey).HasMaxLength(1024); b.Property(x => x.Status).HasMaxLength(32).IsRequired(); b.Property(x => x.RejectionReason).HasMaxLength(1000); b.HasIndex(x => x.ReferenceNumber).IsUnique(); b.HasIndex(x => new { x.Status, x.SubmittedAt }); b.HasIndex(x => new { x.CaseId, x.PaymentDate }); b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => x.SubmittedById).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.VerifiedBy).WithMany().HasForeignKey(x => x.VerifiedById).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class FieldVisitConfiguration : IEntityTypeConfiguration<FieldVisit>
{
    public void Configure(EntityTypeBuilder<FieldVisit> b) { b.ToTable("CollectionFieldVisits"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasMaxLength(32).IsRequired(); b.Property(x => x.Address).HasMaxLength(600).IsRequired(); b.Property(x => x.Governorate).HasMaxLength(100); b.Property(x => x.Area).HasMaxLength(100); b.Property(x => x.Purpose).HasMaxLength(500); b.Property(x => x.CheckInLatitude).HasPrecision(9, 6); b.Property(x => x.CheckInLongitude).HasPrecision(9, 6); b.Property(x => x.Result).HasMaxLength(100); b.Property(x => x.Notes).HasMaxLength(3000); b.HasIndex(x => x.CaseId); b.HasIndex(x => new { x.CollectorId, x.ScheduledAt, x.Status }); b.HasIndex(x => new { x.Status, x.ScheduledAt }); b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Collector).WithMany().HasForeignKey(x => x.CollectorId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CollectionDcrConfiguration : IEntityTypeConfiguration<CollectionDcr>
{
    public void Configure(EntityTypeBuilder<CollectionDcr> b)
    {
        b.ToTable("CollectionDcrs", table => table.HasCheckConstraint("CK_CollectionDcrs_Amounts", "(\"PtpAmount\" IS NULL OR \"PtpAmount\" > 0) AND (\"PaidAmount\" IS NULL OR \"PaidAmount\" > 0)"));
        b.HasKey(x => x.Id); b.Property(x => x.DcrDate).HasColumnType("date"); b.Property(x => x.ActionCover).HasMaxLength(24).IsRequired();
        b.Property(x => x.Action).HasMaxLength(32).IsRequired(); b.Property(x => x.Feedback).HasMaxLength(2000).IsRequired(); b.Property(x => x.Comment).HasMaxLength(2000);
        b.Property(x => x.PtpDate).HasColumnType("date"); b.Property(x => x.PtpAmount).Money(); b.Property(x => x.PaidDate).HasColumnType("date"); b.Property(x => x.PaidAmount).Money(); b.Property(x => x.VisitDate).HasColumnType("date");
        b.HasIndex(x => new { x.BankId, x.DcrDate }); b.HasIndex(x => new { x.CaseId, x.CreatedAt }); b.HasIndex(x => new { x.CreatedByUserId, x.DcrDate });
        b.HasOne(x => x.Bank).WithMany().HasForeignKey(x => x.BankId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.LinkedPtp).WithMany().HasForeignKey(x => x.LinkedPtpId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LinkedVisit).WithMany().HasForeignKey(x => x.LinkedVisitId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CollectionComplaintConfiguration : IEntityTypeConfiguration<CollectionComplaint>
{
    public void Configure(EntityTypeBuilder<CollectionComplaint> b) { b.ToTable("CollectionComplaints"); b.HasKey(x => x.Id); b.Property(x => x.Reference).HasMaxLength(80).IsRequired(); b.HasIndex(x => x.Reference).IsUnique(); b.Property(x => x.Source).HasMaxLength(60).IsRequired(); b.Property(x => x.Category).HasMaxLength(100).IsRequired(); b.Property(x => x.Severity).HasMaxLength(30).IsRequired(); b.Property(x => x.Description).HasMaxLength(4000).IsRequired(); b.Property(x => x.Status).HasMaxLength(32).IsRequired(); b.Property(x => x.Resolution).HasMaxLength(4000); b.Property(x => x.RejectionReason).HasMaxLength(2000); b.HasIndex(x => x.CaseId); b.HasIndex(x => new { x.Status, x.SlaDueAt }); b.HasIndex(x => new { x.OwnerId, x.Status }); b.HasIndex(x => new { x.CaseId, x.ReceivedAt }); b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CollectionComplaintNoteConfiguration : IEntityTypeConfiguration<CollectionComplaintNote>
{
    public void Configure(EntityTypeBuilder<CollectionComplaintNote> b) { b.ToTable("CollectionComplaintNotes"); b.HasKey(x => x.Id); b.Property(x => x.Text).HasMaxLength(3000).IsRequired(); b.HasIndex(x => new { x.ComplaintId, x.CreatedAt }); b.HasOne(x => x.Complaint).WithMany().HasForeignKey(x => x.ComplaintId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CollectionAuditLogConfiguration : IEntityTypeConfiguration<CollectionAuditLog>
{
    public void Configure(EntityTypeBuilder<CollectionAuditLog> b) { b.ToTable("CollectionAuditLogs"); b.HasKey(x => x.Id); b.Property(x => x.Action).HasMaxLength(100).IsRequired(); b.Property(x => x.EntityType).HasMaxLength(100).IsRequired(); b.Property(x => x.BeforeJson).HasColumnType("jsonb"); b.Property(x => x.AfterJson).HasColumnType("jsonb"); b.Property(x => x.Source).HasMaxLength(60); b.HasIndex(x => new { x.CaseId, x.OccurredAt }); b.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt }); b.HasIndex(x => new { x.UserId, x.OccurredAt }); b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CollectionImportBatchConfiguration : IEntityTypeConfiguration<CollectionImportBatch>
{
    public void Configure(EntityTypeBuilder<CollectionImportBatch> b) { b.ToTable("CollectionImportBatches"); b.HasKey(x => x.Id); b.Property(x => x.FileName).HasMaxLength(260).IsRequired(); b.Property(x => x.ContentType).HasMaxLength(120).IsRequired(); b.Property(x => x.FileHash).HasMaxLength(64).IsRequired(); b.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired(); b.Property(x => x.Status).HasMaxLength(32).IsRequired(); b.Property(x => x.FailureReason).HasMaxLength(2000); b.HasIndex(x => new { x.OrganizationId, x.UploadedAt }); b.HasIndex(x => new { x.FileHash, x.PortfolioId }); b.HasIndex(x => new { x.Status, x.UploadedAt }); b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Portfolio).WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CollectionImportRowConfiguration : IEntityTypeConfiguration<CollectionImportRow>
{
    public void Configure(EntityTypeBuilder<CollectionImportRow> b) { b.ToTable("CollectionImportRows"); b.HasKey(x => x.Id); b.Property(x => x.AccountReference).HasMaxLength(120); b.Property(x => x.CustomerCode).HasMaxLength(100); b.Property(x => x.NameArabic).HasMaxLength(200); b.Property(x => x.NameEnglish).HasMaxLength(200); b.Property(x => x.NationalId).HasMaxLength(32); b.Property(x => x.Phone).HasMaxLength(32); b.Property(x => x.ContractReference).HasMaxLength(120); b.Property(x => x.ProductType).HasMaxLength(100); b.Property(x => x.OutstandingBalance).Money(); b.Property(x => x.OverdueBalance).Money(); b.Property(x => x.RawJson).HasColumnType("jsonb"); b.Property(x => x.ErrorsJson).HasColumnType("jsonb"); b.HasIndex(x => new { x.BatchId, x.RowNumber }).IsUnique(); b.HasIndex(x => new { x.BatchId, x.IsValid }); b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class CollectionAttachmentConfiguration : IEntityTypeConfiguration<CollectionAttachment>
{
    public void Configure(EntityTypeBuilder<CollectionAttachment> b) { b.ToTable("CollectionAttachments"); b.HasKey(x => x.Id); b.Property(x => x.Category).HasMaxLength(40).IsRequired(); b.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired(); b.Property(x => x.ContentType).HasMaxLength(120).IsRequired(); b.Property(x => x.FileHash).HasMaxLength(64).IsRequired(); b.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired(); b.HasIndex(x => new { x.CaseId, x.UploadedAt }); b.HasIndex(x => x.PaymentId); b.HasIndex(x => new { x.ComplaintId, x.UploadedAt }); b.HasIndex(x => new { x.FileHash, x.CaseId }); b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Complaint).WithMany().HasForeignKey(x => x.ComplaintId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict); }
}
