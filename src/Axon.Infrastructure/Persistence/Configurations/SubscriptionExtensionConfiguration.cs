using Axon.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class SubscriptionExtensionConfiguration : IEntityTypeConfiguration<SubscriptionExtension>
{
    public void Configure(EntityTypeBuilder<SubscriptionExtension> builder)
    {
        builder.ToTable("subscription_extensions", "public");

        builder.HasKey(e => e.Id);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.CreatedAt });

        builder.Property(e => e.BillingCycle)
            .HasMaxLength(20);

        builder.Property(e => e.Amount)
            .HasColumnType("decimal(18,2)");

        builder.Property(e => e.TransactionReference)
            .HasMaxLength(200);

        // Postgres admite varios NULL en un índice único: las extensiones
        // manuales (sin referencia) no chocan entre sí.
        builder.HasIndex(e => e.TransactionReference)
            .IsUnique();
    }
}
