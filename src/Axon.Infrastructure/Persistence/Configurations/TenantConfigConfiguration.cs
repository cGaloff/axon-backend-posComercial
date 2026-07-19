using Axon.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class TenantConfigConfiguration : IEntityTypeConfiguration<TenantConfig>
{
    public void Configure(EntityTypeBuilder<TenantConfig> builder)
    {
        builder.ToTable("tenant_config");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.BusinessName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Nit)
            .HasMaxLength(20);

        builder.Property(c => c.Address)
            .HasMaxLength(500);

        builder.Property(c => c.Phone)
            .HasMaxLength(50);

        builder.Property(c => c.Email)
            .HasMaxLength(200);

        builder.Property(c => c.Website)
            .HasMaxLength(200);

        builder.Property(c => c.LogoUrl)
            .HasMaxLength(500);

        builder.Property(c => c.IsResponsableIva)
            .HasDefaultValue(false);
    }
}
