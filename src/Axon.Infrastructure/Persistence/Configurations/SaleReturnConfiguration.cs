using Axon.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> builder)
    {
        builder.ToTable("sale_returns");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reason)
            .IsRequired();

        builder.Property(r => r.Total)
            .HasColumnType("decimal(12,2)");

        builder.HasOne<Sale>()
            .WithMany()
            .HasForeignKey(r => r.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
