using Axon.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        builder.Property(r => r.IsSystem)
            .HasDefaultValue(false);

        // Role.Permissions is a domain-side convenience list, not a directly
        // persistable navigation: the real relationship is the RolePermission
        // join row. Without this, EF's convention would mistake it for a
        // one-to-many and try to add a shadow RoleId FK on Permission.
        builder.Ignore(r => r.Permissions);
    }
}
