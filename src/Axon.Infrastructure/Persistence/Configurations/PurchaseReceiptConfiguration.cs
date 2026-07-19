using Axon.Domain.Entities.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("purchase_receipts");

        builder.HasKey(r => r.Id);

        builder.HasOne<PurchaseOrder>()
            .WithMany()
            .HasForeignKey(r => r.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.TotalReceived)
            .HasColumnType("decimal(12,2)");

        builder.Navigation(r => r.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(pr => pr.Items, items =>
        {
            items.ToTable("purchase_receipt_items");

            items.WithOwner().HasForeignKey(i => i.PurchaseReceiptId);

            items.HasKey(i => i.Id);

            items.Property(i => i.ProductName)
                .IsRequired()
                .HasMaxLength(200);

            items.Property(i => i.UnitCost)
                .HasColumnType("decimal(12,2)");

            items.Property(i => i.Subtotal)
                .HasColumnType("decimal(12,2)");
        });

        builder.HasIndex(r => new { r.PurchaseOrderId, r.ReceivedAt });
    }
}
