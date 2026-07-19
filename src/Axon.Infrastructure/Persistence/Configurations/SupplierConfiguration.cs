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

        builder.Property(s => s.Nit)
            .HasMaxLength(20);

        builder.Property(s => s.ContactName)
            .HasMaxLength(200);

        builder.Property(s => s.Phone)
            .HasMaxLength(50);

        builder.Property(s => s.Email)
            .HasMaxLength(200);

        builder.Property(s => s.Address)
            .HasMaxLength(500);

        builder.Property(s => s.City)
            .HasMaxLength(100);

        builder.Property(s => s.PaymentTermDays)
            .HasDefaultValue(30);

        builder.Property(s => s.IsActive)
            .HasDefaultValue(true);
    }
}
