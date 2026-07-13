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

        builder.Property(s => s.PaymentMethod)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Total)
            .HasColumnType("decimal(12,2)");

        builder.Property(s => s.AmountPaid)
            .HasColumnType("decimal(12,2)");

        builder.Property(s => s.Change)
            .HasColumnType("decimal(12,2)");

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
        });

        builder.HasIndex(s => new { s.CreatedAt, s.Status });

        builder.HasIndex(s => s.CustomerId);
    }
}
