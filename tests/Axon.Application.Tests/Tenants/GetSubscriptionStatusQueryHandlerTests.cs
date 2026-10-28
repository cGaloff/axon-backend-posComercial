using Axon.Application.Tenants.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Tests.Tenants;

public class GetSubscriptionStatusQueryHandlerTests
{
    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Subscription:TrialDays"] = "5" })
            .Build();

    [Fact]
    public async Task Handle_WithTrialTenant_ReturnsTrialingAndDaysRemaining()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var tenant = Tenant.Create("test-tenant", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(5), isTrial: true);
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var handler = new GetSubscriptionStatusQueryHandler(dbContext, new FakeTenantContext(), CreateConfiguration());

        var result = await handler.Handle(new GetSubscriptionStatusQuery(), CancellationToken.None);

        Assert.True(result.IsActive);
        Assert.Equal("trialing", result.Status);
        Assert.True(result.IsTrial);
        Assert.Equal(5, result.TrialDaysTotal);
        Assert.Equal("basic", result.Plan);
        Assert.Equal(5, result.DaysRemaining);
        Assert.Equal(0m, result.AmountPaid);
        Assert.Null(result.LastPaymentDate);
        Assert.Equal(tenant.SubscriptionExpiresAt, result.NextRenewalDate);
    }

    [Fact]
    public async Task Handle_WithPaidTenant_ReturnsActiveWithLastPayment()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var tenant = Tenant.Create("test-tenant", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(1), isTrial: true);
        var previous = tenant.SubscriptionExpiresAt;
        var newExpiry = tenant.ExtendSubscriptionByDays(365, BillingCycles.Anual, DateTime.UtcNow);
        dbContext.Tenants.Add(tenant);
        dbContext.SubscriptionExtensions.Add(
            SubscriptionExtension.Create(tenant.Id, BillingCycles.Anual, 365, 486000m, "ref-1", previous, newExpiry));
        await dbContext.SaveChangesAsync();

        var handler = new GetSubscriptionStatusQueryHandler(dbContext, new FakeTenantContext(), CreateConfiguration());

        var result = await handler.Handle(new GetSubscriptionStatusQuery(), CancellationToken.None);

        Assert.Equal("active", result.Status);
        Assert.False(result.IsTrial);
        Assert.Equal("anual", result.CurrentBillingCycle);
        Assert.Equal(486000m, result.AmountPaid);
        Assert.NotNull(result.LastPaymentDate);
        Assert.Equal(366, result.DaysRemaining);
    }

    [Fact]
    public async Task Handle_WithExpiredTenant_ReturnsZeroDaysAndExpired()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        dbContext.Tenants.Add(Tenant.Create("test-tenant", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(-2)));
        await dbContext.SaveChangesAsync();

        var handler = new GetSubscriptionStatusQueryHandler(dbContext, new FakeTenantContext(), CreateConfiguration());

        var result = await handler.Handle(new GetSubscriptionStatusQuery(), CancellationToken.None);

        Assert.False(result.IsActive);
        Assert.Equal("expired", result.Status);
        Assert.Equal(0, result.DaysRemaining);
    }

    [Fact]
    public async Task Handle_WithGrandfatheredTenant_ReturnsNullDaysRemaining()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        dbContext.Tenants.Add(Tenant.Create("test-tenant", "Mi Negocio", "basic", "owner@test.com"));
        await dbContext.SaveChangesAsync();

        var handler = new GetSubscriptionStatusQueryHandler(dbContext, new FakeTenantContext(), CreateConfiguration());

        var result = await handler.Handle(new GetSubscriptionStatusQuery(), CancellationToken.None);

        Assert.Equal("active", result.Status);
        Assert.Null(result.DaysRemaining);
        Assert.Null(result.SubscriptionExpiresAt);
        Assert.Null(result.NextRenewalDate);
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_Throws()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var handler = new GetSubscriptionStatusQueryHandler(dbContext, new FakeTenantContext(), CreateConfiguration());

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new GetSubscriptionStatusQuery(), CancellationToken.None));
    }
}
