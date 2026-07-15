using Axon.Domain.Entities.CashRegister;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CashRegisterEntity = Axon.Domain.Entities.CashRegister.CashRegister;

namespace Axon.Infrastructure.Persistence.Configurations;

public class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> builder)
    {
        builder.ToTable("cash_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.InitialAmount)
            .HasColumnType("decimal(12,2)");

        builder.Property(s => s.ExpectedAmount)
            .HasColumnType("decimal(12,2)");

        builder.Property(s => s.CountedAmount)
            .HasColumnType("decimal(12,2)");

        builder.Property(s => s.Difference)
            .HasColumnType("decimal(12,2)");

        builder.HasOne<CashRegisterEntity>()
            .WithMany()
            .HasForeignKey(s => s.CashRegisterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.CashRegisterId, s.Status });

        builder.HasIndex(s => s.OpenedBy);
    }
}
