using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class AccountingPayrollPeriodConfiguration : IEntityTypeConfiguration<AccountingPayrollPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPayrollPeriod> b)
    {
        b.ToTable("AccountingPayrollPeriods");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(40).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.Year, x.Month }).IsUnique();
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountingEmployeePayrollConfiguration : IEntityTypeConfiguration<AccountingEmployeePayroll>
{
    public void Configure(EntityTypeBuilder<AccountingEmployeePayroll> b)
    {
        b.ToTable("AccountingEmployeePayrolls");
        b.HasKey(x => x.Id);
        b.Property(x => x.EmployeeNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.EmployeeName).HasMaxLength(200).IsRequired();
        b.Property(x => x.DepartmentName).HasMaxLength(200);
        b.Property(x => x.PositionName).HasMaxLength(200);
        b.Property(x => x.BasicSalary).HasPrecision(18, 2);
        b.Property(x => x.Allowances).HasPrecision(18, 2);
        b.Property(x => x.Transportation).HasPrecision(18, 2);
        b.Property(x => x.Commissions).HasPrecision(18, 2);
        b.Property(x => x.Bonuses).HasPrecision(18, 2);
        b.Property(x => x.Deductions).HasPrecision(18, 2);
        b.Property(x => x.OtherAdjustments).HasPrecision(18, 2);
        b.Property(x => x.NetSalary).HasPrecision(18, 2);
        b.Property(x => x.Status).HasMaxLength(40).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.PeriodId, x.EmployeeId }).IsUnique();
        b.HasOne(x => x.Period).WithMany(x => x.Payrolls).HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CompensationSnapshot).WithMany().HasForeignKey(x => x.CompensationSnapshotId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountingTransportationClaimConfiguration : IEntityTypeConfiguration<AccountingTransportationClaim>
{
    public void Configure(EntityTypeBuilder<AccountingTransportationClaim> b)
    {
        b.ToTable("AccountingTransportationClaims");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Purpose).HasMaxLength(500).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.Status).HasMaxLength(40).IsRequired();
        b.Property(x => x.AttachmentFileName).HasMaxLength(260);
        b.Property(x => x.AttachmentContentType).HasMaxLength(120);
        b.Property(x => x.AttachmentStorageKey).HasMaxLength(1024);
        b.Property(x => x.AttachmentSha256).HasMaxLength(128);
        b.HasIndex(x => new { x.EmployeeId, x.ClaimDate });
        b.HasIndex(x => x.Status);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FieldVisit).WithMany().HasForeignKey(x => x.FieldVisitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SubmittedByUser).WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountingCommissionRuleConfiguration : IEntityTypeConfiguration<AccountingCommissionRule>
{
    public void Configure(EntityTypeBuilder<AccountingCommissionRule> b)
    {
        b.ToTable("AccountingCommissionRules");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.NameArabic).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEnglish).HasMaxLength(200).IsRequired();
        b.Property(x => x.Scope).HasMaxLength(40).IsRequired();
        b.Property(x => x.Basis).HasMaxLength(40).IsRequired();
        b.Property(x => x.Percentage).HasPrecision(9, 4);
        b.Property(x => x.FixedAmount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.Code, x.Version }).IsUnique();
        b.HasIndex(x => new { x.Scope, x.IsActive, x.EffectiveFrom });
    }
}

public sealed class AccountingCollectorCommissionConfiguration : IEntityTypeConfiguration<AccountingCollectorCommission>
{
    public void Configure(EntityTypeBuilder<AccountingCollectorCommission> b)
    {
        b.ToTable("AccountingCollectorCommissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.CollectedAmount).HasPrecision(18, 2);
        b.Property(x => x.EligibleAmount).HasPrecision(18, 2);
        b.Property(x => x.RateApplied).HasPrecision(9, 4);
        b.Property(x => x.CommissionAmount).HasPrecision(18, 2);
        b.Property(x => x.Adjustments).HasPrecision(18, 2);
        b.Property(x => x.FinalCommission).HasPrecision(18, 2);
        b.Property(x => x.CalculationSnapshotJson).HasColumnType("jsonb").HasDefaultValue("[]");
        b.Property(x => x.Status).HasMaxLength(40).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.CollectorUserId, x.PeriodYear, x.PeriodMonth })
            .IsUnique()
            .HasFilter("\"Status\" <> 'CANCELLED'");
        b.HasOne(x => x.CollectorUser).WithMany().HasForeignKey(x => x.CollectorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CommissionRule).WithMany().HasForeignKey(x => x.CommissionRuleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountingSupervisorCommissionConfiguration : IEntityTypeConfiguration<AccountingSupervisorCommission>
{
    public void Configure(EntityTypeBuilder<AccountingSupervisorCommission> b)
    {
        b.ToTable("AccountingSupervisorCommissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.TeamSummary).HasMaxLength(1000);
        b.Property(x => x.TeamCollectedAmount).HasPrecision(18, 2);
        b.Property(x => x.EligibleAmount).HasPrecision(18, 2);
        b.Property(x => x.RateApplied).HasPrecision(9, 4);
        b.Property(x => x.CommissionAmount).HasPrecision(18, 2);
        b.Property(x => x.Adjustments).HasPrecision(18, 2);
        b.Property(x => x.FinalCommission).HasPrecision(18, 2);
        b.Property(x => x.CalculationSnapshotJson).HasColumnType("jsonb").HasDefaultValue("[]");
        b.Property(x => x.Status).HasMaxLength(40).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.SupervisorUserId, x.PeriodYear, x.PeriodMonth })
            .IsUnique()
            .HasFilter("\"Status\" <> 'CANCELLED'");
        b.HasOne(x => x.SupervisorUser).WithMany().HasForeignKey(x => x.SupervisorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CommissionRule).WithMany().HasForeignKey(x => x.CommissionRuleId).OnDelete(DeleteBehavior.Restrict);
    }
}
