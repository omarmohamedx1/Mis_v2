using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmployeeNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.FullNameArabic).HasMaxLength(160);
        builder.Property(x => x.FullNameEnglish).HasMaxLength(160);
        builder.Property(x => x.NationalId).HasMaxLength(32);
        builder.Property(x => x.DateOfBirth).HasColumnType("date");
        builder.Property(x => x.Gender).HasMaxLength(32);
        builder.Property(x => x.MaritalStatus).HasMaxLength(32);
        builder.Property(x => x.ProfilePhotoStorageKey).HasMaxLength(1024);
        builder.Property(x => x.MobileNumber).HasMaxLength(32);
        builder.Property(x => x.AlternativeMobileNumber).HasMaxLength(32);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.HireDate).HasColumnType("date");
        builder.Property(x => x.OperationalRole).HasMaxLength(24);
        builder.Property(x => x.FingerprintEnrollmentDate).HasColumnType("date");
        builder.Property(x => x.WorkNumber).HasMaxLength(50);
        builder.Property(x => x.PackageType).HasMaxLength(80);
        builder.Property(x => x.Status).HasMaxLength(32).HasDefaultValue(Employee.ActiveStatus).IsRequired();
        builder.Property(x => x.TerminationDate).HasColumnType("date");
        builder.Property(x => x.TerminationReason).HasMaxLength(500);
        builder.Property(x => x.ArchiveReason).HasMaxLength(500);
        builder.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.HasIndex(x => x.EmployeeNumber).IsUnique();
        builder.HasIndex(x => x.NationalId).IsUnique().HasFilter("\"NationalId\" IS NOT NULL");
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.PositionId);
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.EmploymentTypeId);
        builder.HasIndex(x => x.DirectManagerId);
        builder.HasIndex(x => x.HireDate);
        builder.HasIndex(x => new { x.IsArchived, x.Status });
        builder.HasIndex(x => x.OperationalRole);
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Position).WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EmploymentType).WithMany().HasForeignKey(x => x.EmploymentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DirectManager).WithMany().HasForeignKey(x => x.DirectManagerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ArchivedByUser).WithMany().HasForeignKey(x => x.ArchivedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
