using Axon.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", "public");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.Slug)
            .IsUnique();

        builder.Property(t => t.SchemaName)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.SchemaName)
            .IsUnique();

        builder.Property(t => t.BusinessName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Plan)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("basic");

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true);
    }
}
