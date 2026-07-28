using Axon.Domain.Entities.Taxes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class TaxTypeConfiguration : IEntityTypeConfiguration<TaxType>
{
    public void Configure(EntityTypeBuilder<TaxType> builder)
    {
        builder.ToTable("tax_types");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Catálogo fijo de 8 valores (ver TaxCode): se guarda como texto
        // (nombre del enum) para que la columna siga siendo legible en la BD.
        builder.Property(t => t.Code)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true);
    }
}
