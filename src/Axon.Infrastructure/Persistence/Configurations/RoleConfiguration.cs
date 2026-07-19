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

        // Role.Permissions is IReadOnlyList<Permission> with no public setter,
        // backed by the private field _permissions -> EF needs field access to
        // populate it during Include/ThenInclude.
        builder.Navigation(r => r.Permissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Explicit many-to-many through RolePermission (no extra payload columns,
        // just the composite key), which also fully configures RolePermission's
        // own table/keys/FKs — no separate IEntityTypeConfiguration<RolePermission>
        // needed or applied elsewhere.
        builder.HasMany(r => r.Permissions)
            .WithMany()
            .UsingEntity<RolePermission>(
                j => j.HasOne<Permission>()
                    .WithMany()
                    .HasForeignKey(rp => rp.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<Role>()
                    .WithMany()
                    .HasForeignKey(rp => rp.RoleId)
                    .OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.HasKey(rp => new { rp.RoleId, rp.PermissionId });
                    j.ToTable("role_permissions");
                });
    }
}
