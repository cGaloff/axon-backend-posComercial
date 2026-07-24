using Axon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Tests.TestSupport;

public static class TestDbContextFactory
{
    // Cada test recibe una base InMemory nueva (nombre aleatorio) para evitar
    // que el estado de un test se filtre a otro.
    public static TenantDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TenantDbContext(options, new FakeTenantContext());
    }
}
