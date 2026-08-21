using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Axon.Infrastructure.Persistence;

public class AppDbContext : DbContext, IMasterDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<PendingTenantRegistration> PendingTenantRegistrations => Set<PendingTenantRegistration>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new PendingTenantRegistrationConfiguration());
    }
}
