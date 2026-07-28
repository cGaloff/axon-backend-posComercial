using Axon.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("inventory_movements");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.Reason)
            .HasMaxLength(500);

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30)
            .HasDefaultValue(InventoryMovementStatus.Applied);

        builder.Property(m => m.RejectionReason)
            .HasMaxLength(500);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(m => m.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.ProductId, m.CreatedAt });
    }
}
