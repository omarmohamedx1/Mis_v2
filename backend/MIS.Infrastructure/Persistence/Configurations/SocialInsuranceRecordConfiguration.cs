using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class SocialInsuranceRecordConfiguration : IEntityTypeConfiguration<SocialInsuranceRecord>
{
    public void Configure(EntityTypeBuilder<SocialInsuranceRecord> b)
    {
        b.ToTable("SocialInsuranceRecords", t => {
            t.HasCheckConstraint("CK_SocialInsurance_Salary", "\"InsurableSalary\" > 0");
            t.HasCheckConstraint("CK_SocialInsurance_Status", "\"InsuranceStatus\" IN ('Insured','NotInsured','Suspended','Ended')");
            t.HasCheckConstraint("CK_SocialInsurance_Start", "\"InsuranceStatus\" NOT IN ('Insured','Suspended') OR \"InsuranceStartDate\" IS NOT NULL");
            t.HasCheckConstraint("CK_SocialInsurance_End", "(\"InsuranceStatus\" = 'Ended') = (\"InsuranceEndDate\" IS NOT NULL)");
            t.HasCheckConstraint("CK_SocialInsurance_Dates", "\"InsuranceEndDate\" IS NULL OR \"InsuranceStartDate\" IS NULL OR \"InsuranceEndDate\" >= \"InsuranceStartDate\"");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.SocialInsuranceNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.InsuranceStatus).HasMaxLength(20).IsRequired();
        b.Property(x => x.InsurableSalary).HasPrecision(18, 2);
        b.Property(x => x.InsuranceOffice).HasMaxLength(160);
        b.Property(x => x.ReferenceNumber).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("\"InsuranceStatus\" <> 'Ended'");
        b.HasIndex(x => x.SocialInsuranceNumber).IsUnique().HasFilter("\"InsuranceStatus\" <> 'Ended'");
        b.Property<uint>("Version").IsRowVersion();
    }
}
