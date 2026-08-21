using Axon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Tests.TestSupport;

public static class TestMasterDbContextFactory
{
    // Cada test recibe una base InMemory nueva (nombre aleatorio) para evitar
    // que el estado de un test se filtre a otro. A diferencia de TenantDbContext,
    // AppDbContext no depende de ITenantContext (vive en la base master, no en
    // el schema de un tenant).
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
