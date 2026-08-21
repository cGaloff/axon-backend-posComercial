using Axon.Application.Tenants.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Tenants;

public class GetSubscriptionStatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithActiveTenant_ReturnsStatusAndDaysRemaining()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var tenant = Tenant.Create("test-tenant", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(5));
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var handler = new GetSubscriptionStatusQueryHandler(dbContext, new FakeTenantContext());

        var result = await handler.Handle(new GetSubscriptionStatusQuery(), CancellationToken.None);

        Assert.True(result.IsActive);
        Assert.Equal("basic", result.Plan);
        Assert.Equal(5, result.DaysRemaining);
    }

    [Fact]
    public async Task Handle_WithGrandfatheredTenant_ReturnsNullDaysRemaining()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var tenant = Tenant.Create("test-tenant", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(5));
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        dbContext.Entry(tenant).Property("SubscriptionExpiresAt").CurrentValue = null;
        await dbContext.SaveChangesAsync();

        var handler = new GetSubscriptionStatusQueryHandler(dbContext, new FakeTenantContext());

        var result = await handler.Handle(new GetSubscriptionStatusQuery(), CancellationToken.None);

        Assert.Null(result.DaysRemaining);
        Assert.Null(result.SubscriptionExpiresAt);
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_Throws()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var handler = new GetSubscriptionStatusQueryHandler(dbContext, new FakeTenantContext());

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new GetSubscriptionStatusQuery(), CancellationToken.None));
    }
}
