using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class RuntimeSettingConfiguration : IEntityTypeConfiguration<RuntimeSetting>
{
    public void Configure(EntityTypeBuilder<RuntimeSetting> builder)
    {
        builder.ToTable("RuntimeSettings");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(2000).IsRequired();
    }
}
