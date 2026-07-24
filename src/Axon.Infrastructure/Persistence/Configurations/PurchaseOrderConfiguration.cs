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

        builder.Property(o => o.SupplierInvoiceNumber)
            .HasMaxLength(100);

        builder.Property(o => o.SupplierDocumentTypeAtPurchase)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(o => o.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

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

            items.Property(i => i.TaxAmount)
                .HasColumnType("decimal(12,2)")
                .HasDefaultValue(0m);

            items.OwnsMany(i => i.Taxes, taxes =>
            {
                taxes.ToTable("purchase_order_item_taxes");

                taxes.WithOwner().HasForeignKey(t => t.PurchaseOrderItemId);

                taxes.HasKey(t => t.Id);

                taxes.Property(t => t.TaxTypeName)
                    .IsRequired()
                    .HasMaxLength(100);

                taxes.Property(t => t.Percentage)
                    .HasColumnType("decimal(9,4)");

                taxes.Property(t => t.Amount)
                    .HasColumnType("decimal(12,2)");
            });

            items.Navigation(i => i.Taxes)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Navigation(o => o.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(o => new { o.SupplierId, o.Status });

        builder.HasIndex(o => o.CreatedBy);
    }
}
