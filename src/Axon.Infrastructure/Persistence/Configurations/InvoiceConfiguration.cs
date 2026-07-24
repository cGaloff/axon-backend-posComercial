using Axon.Domain.Entities.Invoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(i => i.Id);

        builder.HasIndex(i => i.SaleId)
            .IsUnique();

        builder.HasIndex(i => i.Number)
            .IsUnique();

        builder.Property(i => i.SaleNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.CustomerName)
            .HasMaxLength(200);

        builder.Property(i => i.Total)
            .HasColumnType("decimal(12,2)");

        builder.OwnsMany(i => i.Payments, payments =>
        {
            payments.ToTable("invoice_payments");

            payments.WithOwner().HasForeignKey(p => p.InvoiceId);

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

        builder.Navigation(i => i.Payments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(i => i.Items, items =>
        {
            items.ToTable("invoice_items");

            items.WithOwner().HasForeignKey(x => x.InvoiceId);

            items.HasKey(x => x.Id);

            items.Property(x => x.ProductName)
                .IsRequired()
                .HasMaxLength(200);

            items.Property(x => x.ProductSku)
                .IsRequired()
                .HasMaxLength(100);

            items.Property(x => x.UnitPrice)
                .HasColumnType("decimal(12,2)");

            items.Property(x => x.Discount)
                .HasColumnType("decimal(12,2)");

            items.Property(x => x.Subtotal)
                .HasColumnType("decimal(12,2)");

            items.Property(x => x.SubtotalBase)
                .HasColumnType("decimal(12,2)");

            items.OwnsMany(x => x.Taxes, taxes =>
            {
                taxes.ToTable("invoice_item_taxes");

                taxes.WithOwner().HasForeignKey(t => t.InvoiceItemId);

                taxes.HasKey(t => t.Id);

                taxes.Property(t => t.TaxTypeName)
                    .IsRequired()
                    .HasMaxLength(100);

                taxes.Property(t => t.Percentage)
                    .HasColumnType("decimal(9,4)");

                taxes.Property(t => t.Amount)
                    .HasColumnType("decimal(12,2)");
            });

            items.Navigation(x => x.Taxes)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Navigation(i => i.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(i => i.IssuedAt);
    }
}
