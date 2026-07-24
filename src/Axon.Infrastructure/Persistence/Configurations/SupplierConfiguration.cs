using Axon.Domain.Entities.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.DocumentType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.DocumentNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(s => new { s.DocumentType, s.DocumentNumber })
            .IsUnique();

        builder.Property(s => s.ContactName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Phone)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Address)
            .HasMaxLength(500);

        builder.Property(s => s.City)
            .HasMaxLength(100);

        builder.Property(s => s.IsActive)
            .HasDefaultValue(true);
    }
}
