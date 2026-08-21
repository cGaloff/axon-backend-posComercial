using Axon.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class PendingTenantRegistrationConfiguration : IEntityTypeConfiguration<PendingTenantRegistration>
{
    public void Configure(EntityTypeBuilder<PendingTenantRegistration> builder)
    {
        builder.ToTable("pending_tenant_registrations", "public");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.BusinessName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.OwnerEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.OwnerPasswordHash)
            .IsRequired();

        builder.Property(p => p.Plan)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.CodeHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(p => p.OwnerEmail);
    }
}
