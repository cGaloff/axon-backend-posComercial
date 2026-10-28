using Axon.Application.Tenants.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Tenants;

public class ExtendTenantSubscriptionCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithExpiredTenant_ExtendsFromNowAndReactivates()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var tenant = Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(-1));
        tenant.Suspend();
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var invalidator = new FakeTenantCacheInvalidator();
        var handler = new ExtendTenantSubscriptionCommandHandler(dbContext, invalidator);

        var result = await handler.Handle(new ExtendTenantSubscriptionCommand("mi-negocio", 30), CancellationToken.None);

        var updated = dbContext.Tenants.Single(t => t.Id == tenant.Id);
        Assert.True(updated.IsActive);
        Assert.InRange(updated.SubscriptionExpiresAt!.Value, DateTime.UtcNow.AddDays(29.9), DateTime.UtcNow.AddDays(30.1));
        Assert.Equal(30, result.DaysAdded);
        Assert.False(result.AlreadyProcessed);
        Assert.Contains("mi-negocio", invalidator.InvalidatedSlugs);
    }

    [Fact]
    public async Task Handle_WithTenantStillActive_AddsDaysOnTopOfCurrentExpiry()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var currentExpiry = DateTime.UtcNow.AddDays(10);
        var tenant = Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", currentExpiry, isTrial: true);
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var handler = new ExtendTenantSubscriptionCommandHandler(dbContext, new FakeTenantCacheInvalidator());

        await handler.Handle(
            new ExtendTenantSubscriptionCommand("mi-negocio", 365, "anual", 486000m, "epayco-ref-1"),
            CancellationToken.None);

        var updated = dbContext.Tenants.Single(t => t.Id == tenant.Id);
        Assert.Equal(currentExpiry.AddDays(365), updated.SubscriptionExpiresAt);
        Assert.False(updated.IsTrial);
        Assert.Equal("anual", updated.BillingCycle);

        var extension = dbContext.SubscriptionExtensions.Single();
        Assert.Equal(486000m, extension.Amount);
        Assert.Equal("epayco-ref-1", extension.TransactionReference);
        Assert.Equal(currentExpiry, extension.PreviousExpiresAt);
    }

    [Fact]
    public async Task Handle_WithoutExtensionDays_DerivesDaysFromBillingCycle()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        dbContext.Tenants.Add(Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(-3)));
        await dbContext.SaveChangesAsync();

        var handler = new ExtendTenantSubscriptionCommandHandler(dbContext, new FakeTenantCacheInvalidator());

        var result = await handler.Handle(
            new ExtendTenantSubscriptionCommand("mi-negocio", null, "Semestral", 285000m, "epayco-ref-2"),
            CancellationToken.None);

        Assert.Equal(180, result.DaysAdded);
        Assert.Equal("semestral", dbContext.Tenants.Single().BillingCycle);
    }

    [Fact]
    public async Task Handle_WithRepeatedTransactionReference_DoesNotExtendTwice()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        dbContext.Tenants.Add(Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(2)));
        await dbContext.SaveChangesAsync();

        var handler = new ExtendTenantSubscriptionCommandHandler(dbContext, new FakeTenantCacheInvalidator());
        var command = new ExtendTenantSubscriptionCommand("mi-negocio", 30, "mensual", 50000m, "epayco-ref-3");

        var first = await handler.Handle(command, CancellationToken.None);
        var retry = await handler.Handle(command, CancellationToken.None);

        Assert.False(first.AlreadyProcessed);
        Assert.True(retry.AlreadyProcessed);
        Assert.Equal(0, retry.DaysAdded);
        Assert.Equal(first.SubscriptionExpiresAt, retry.SubscriptionExpiresAt);
        Assert.Single(dbContext.SubscriptionExtensions);
    }

    [Fact]
    public async Task Handle_WithUnknownSlug_Throws()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var handler = new ExtendTenantSubscriptionCommandHandler(dbContext, new FakeTenantCacheInvalidator());

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ExtendTenantSubscriptionCommand("no-existe", 30), CancellationToken.None));
    }

    [Fact]
    public void Validator_WithoutDaysNorCycle_Fails()
    {
        var result = new ExtendTenantSubscriptionCommandValidator()
            .Validate(new ExtendTenantSubscriptionCommand("mi-negocio", null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_WithUnknownCycle_Fails()
    {
        var result = new ExtendTenantSubscriptionCommandValidator()
            .Validate(new ExtendTenantSubscriptionCommand("mi-negocio", null, "bimestral"));

        Assert.False(result.IsValid);
    }
}
