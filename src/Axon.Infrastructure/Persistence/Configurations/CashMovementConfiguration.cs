using Axon.Domain.Entities.CashRegister;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ToTable("cash_movements");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.Amount)
            .HasColumnType("decimal(12,2)");

        builder.Property(m => m.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne<CashSession>()
            .WithMany()
            .HasForeignKey(m => m.CashSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.CashSessionId, m.CreatedAt });
    }
}
