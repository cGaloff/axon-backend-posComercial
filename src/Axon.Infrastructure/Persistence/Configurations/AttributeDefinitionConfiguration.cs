using System.Linq;
using System.Text.Json;
using Axon.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Axon.Infrastructure.Persistence.Configurations;

public class AttributeDefinitionConfiguration : IEntityTypeConfiguration<AttributeDefinition>
{
    public void Configure(EntityTypeBuilder<AttributeDefinition> builder)
    {
        builder.ToTable("attribute_definitions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Label)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Type)
            .IsRequired()
            .HasMaxLength(50);

        var optionsConverter = new ValueConverter<List<string>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null));

        var optionsComparer = new ValueComparer<List<string>?>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            v => v == null ? 0 : v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s)),
            v => v == null ? null : v.ToList());

        builder.Property(a => a.Options)
            .HasConversion(optionsConverter, optionsComparer)
            .HasColumnType("jsonb");

        builder.HasIndex(a => new { a.Key, a.CategoryId })
            .IsUnique();

        builder.Property(a => a.IsActive)
            .HasDefaultValue(true);
    }
}
