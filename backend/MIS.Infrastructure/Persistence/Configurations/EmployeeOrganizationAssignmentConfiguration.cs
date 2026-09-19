using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class EmployeeOrganizationAssignmentConfiguration : IEntityTypeConfiguration<EmployeeOrganizationAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeOrganizationAssignment> builder)
    {
        builder.ToTable("EmployeeOrganizationAssignments");
        builder.HasKey(item => new { item.EmployeeId, item.OrganizationId });
        builder.Property(item => item.AssignedAt).IsRequired();
        builder.HasIndex(item => new { item.OrganizationId, item.EmployeeId });
        builder.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Organization).WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
