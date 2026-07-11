using Axon.Domain.Entities;
using Axon.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Axon.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
    }
}
