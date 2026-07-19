using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class ProductSupplierConfiguration : IEntityTypeConfiguration<ProductSupplier>
{
    public void Configure(EntityTypeBuilder<ProductSupplier> builder)
    {
        builder.ToTable("product_suppliers");

        builder.HasKey(ps => new { ps.ProductId, ps.SupplierId });

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(ps => ps.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(ps => ps.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(ps => ps.LastPurchasePrice)
            .HasColumnType("decimal(12,2)");

        builder.Property(ps => ps.IsPreferred)
            .HasDefaultValue(false);
    }
}
