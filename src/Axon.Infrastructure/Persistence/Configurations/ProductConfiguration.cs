using System.Text.Json;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Taxes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Axon.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Sku)
            .IsRequired()
            .HasMaxLength(100);

        // Unicidad de SKU solo entre productos activos: Deactivate() es un
        // soft-delete, así que el SKU de un producto desactivado debe quedar
        // libre para reutilizarse (ver tenant_schema_template.sql / migración 006).
        builder.HasIndex(p => p.Sku)
            .IsUnique()
            .HasFilter("is_active");

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Price)
            .HasColumnType("decimal(12,2)");

        builder.Property(p => p.Cost)
            .HasColumnType("decimal(12,2)");

        builder.Property(p => p.Stock)
            .HasDefaultValue(0);

        builder.Property(p => p.MinStock)
            .HasDefaultValue(0);

        var attributesConverter = new ValueConverter<Dictionary<string, JsonElement>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(v, (JsonSerializerOptions?)null)
                ?? new Dictionary<string, JsonElement>());

        var attributesComparer = new ValueComparer<Dictionary<string, JsonElement>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

        builder.Property(p => p.Attributes)
            .HasConversion(attributesConverter, attributesComparer)
            .HasColumnType("jsonb")
            .HasColumnName("attributes");

        builder.HasIndex(p => p.Attributes)
            .HasMethod("gin");

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Unit>()
            .WithMany()
            .HasForeignKey(p => p.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.IsActive)
            .HasDefaultValue(true);

        builder.Navigation(p => p.Taxes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.Taxes, taxes =>
        {
            taxes.ToTable("product_taxes");

            taxes.WithOwner().HasForeignKey(t => t.ProductId);

            taxes.HasKey(t => t.Id);

            taxes.Property(t => t.Percentage)
                .HasColumnType("decimal(9,4)");

            // Configuración vigente del producto: a diferencia del snapshot de
            // SaleItemTax, sí se valida con FK contra el catálogo tax_types.
            taxes.HasOne<TaxType>()
                .WithMany()
                .HasForeignKey(t => t.TaxTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            taxes.HasIndex(t => new { t.ProductId, t.TaxTypeId })
                .IsUnique();
        });
    }
}
