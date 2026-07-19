using Axon.Domain.Entities.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(o => o.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(o => o.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(po => po.Items, items =>
        {
            items.ToTable("purchase_order_items");

            items.WithOwner().HasForeignKey(i => i.PurchaseOrderId);

            items.HasKey(i => i.Id);

            items.Property(i => i.ProductName)
                .IsRequired()
                .HasMaxLength(200);

            items.Property(i => i.ProductSku)
                .IsRequired()
                .HasMaxLength(100);

            items.Property(i => i.UnitCost)
                .HasColumnType("decimal(12,2)");

            items.Property(i => i.Subtotal)
                .HasColumnType("decimal(12,2)");
        });

        builder.HasIndex(o => new { o.SupplierId, o.Status });

        builder.HasIndex(o => o.CreatedBy);
    }
}
