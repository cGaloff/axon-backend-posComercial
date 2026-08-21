using Axon.Application.Tenants.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Tenants;

public class ExtendTenantSubscriptionCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithExistingTenant_ExtendsAndReactivates()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var tenant = Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(-1));
        tenant.Suspend();
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var handler = new ExtendTenantSubscriptionCommandHandler(dbContext);

        await handler.Handle(new ExtendTenantSubscriptionCommand("mi-negocio", 30), CancellationToken.None);

        var updated = dbContext.Tenants.Single(t => t.Id == tenant.Id);
        Assert.True(updated.IsActive);
        Assert.True(updated.SubscriptionExpiresAt > DateTime.UtcNow.AddDays(29));
    }

    [Fact]
    public async Task Handle_WithUnknownSlug_Throws()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var handler = new ExtendTenantSubscriptionCommandHandler(dbContext);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ExtendTenantSubscriptionCommand("no-existe", 30), CancellationToken.None));
    }
}
