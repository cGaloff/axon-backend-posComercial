using Axon.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("sales");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SaleNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(s => s.SaleNumber)
            .IsUnique();

        builder.Property(s => s.CustomerName)
            .HasMaxLength(200);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Total)
            .HasColumnType("decimal(12,2)");

        builder.OwnsMany(s => s.Payments, payments =>
        {
            payments.ToTable("sale_payments");

            payments.WithOwner().HasForeignKey(p => p.SaleId);

            payments.HasKey(p => p.Id);

            payments.Property(p => p.Method)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            payments.Property(p => p.Amount)
                .HasColumnType("decimal(12,2)");

            payments.Property(p => p.AmountTendered)
                .HasColumnType("decimal(12,2)");

            payments.Property(p => p.Change)
                .HasColumnType("decimal(12,2)");
        });

        builder.Navigation(s => s.Payments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(s => s.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(s => s.Items, items =>
        {
            items.ToTable("sale_items");

            items.WithOwner().HasForeignKey(i => i.SaleId);

            items.HasKey(i => i.Id);

            items.Property(i => i.ProductName)
                .IsRequired()
                .HasMaxLength(200);

            items.Property(i => i.ProductSku)
                .IsRequired()
                .HasMaxLength(100);

            items.Property(i => i.UnitPrice)
                .HasColumnType("decimal(12,2)");

            items.Property(i => i.Discount)
                .HasColumnType("decimal(12,2)");

            items.Property(i => i.Subtotal)
                .HasColumnType("decimal(12,2)");

            items.Property(i => i.SubtotalBase)
                .HasColumnType("decimal(12,2)")
                .HasDefaultValue(0m);

            items.OwnsMany(i => i.Taxes, taxes =>
            {
                taxes.ToTable("sale_item_taxes");

                taxes.WithOwner().HasForeignKey(t => t.SaleItemId);

                taxes.HasKey(t => t.Id);

                // Snapshot histórico: sin FK a tax_types a propósito (el catálogo
                // puede cambiar o el TaxType puede desactivarse después de la venta;
                // el nombre y el porcentaje ya quedaron copiados en esta fila).
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

        builder.HasIndex(s => new { s.CreatedAt, s.Status });

        builder.HasIndex(s => s.CustomerId);
    }
}
