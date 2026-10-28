using Axon.Domain.Entities;
using Axon.Domain.Exceptions;

namespace Axon.Domain.Tests.Tenants;

public class TenantTests
{
    [Fact]
    public void Create_SetsOwnerEmailAndSubscriptionExpiresAt()
    {
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var tenant = Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", expiresAt);

        Assert.Equal("owner@test.com", tenant.OwnerEmail);
        Assert.Equal(expiresAt, tenant.SubscriptionExpiresAt);
        Assert.True(tenant.IsActive);
        Assert.Null(tenant.LastTrialReminderSentAt);
    }

    [Fact]
    public void Suspend_DeactivatesTenant()
    {
        var tenant = Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(7));

        tenant.Suspend();

        Assert.False(tenant.IsActive);
    }

    [Fact]
    public void ExtendSubscription_ReactivatesAndPushesExpiryAndClearsReminder()
    {
        var tenant = Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(-1));
        tenant.Suspend();
        tenant.MarkTrialReminderSent();

        var newExpiry = DateTime.UtcNow.AddDays(30);
        tenant.ExtendSubscription(newExpiry);

        Assert.True(tenant.IsActive);
        Assert.Equal(newExpiry, tenant.SubscriptionExpiresAt);
        Assert.Null(tenant.LastTrialReminderSentAt);
    }

    [Fact]
    public void ExtendSubscriptionByDays_BeforeExpiry_AddsOnTopOfRemainingDays()
    {
        var now = DateTime.UtcNow;
        var tenant = Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", now.AddDays(3), isTrial: true);

        var newExpiry = tenant.ExtendSubscriptionByDays(30, BillingCycles.Mensual, now);

        Assert.Equal(now.AddDays(33), newExpiry);
        Assert.False(tenant.IsTrial);
        Assert.Equal(BillingCycles.Mensual, tenant.BillingCycle);
    }

    [Fact]
    public void ExtendSubscriptionByDays_AfterExpiry_CountsFromNow()
    {
        var now = DateTime.UtcNow;
        var tenant = Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", now.AddDays(-10));
        tenant.Suspend();

        var newExpiry = tenant.ExtendSubscriptionByDays(90, BillingCycles.Trimestral, now);

        Assert.Equal(now.AddDays(90), newExpiry);
        Assert.True(tenant.IsActive);
    }

    [Fact]
    public void ExtendSubscriptionByDays_WithNonPositiveDays_Throws()
    {
        var tenant = Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(3));

        Assert.Throws<DomainException>(() => tenant.ExtendSubscriptionByDays(0, null, DateTime.UtcNow));
    }

    [Fact]
    public void UpdateSubscription_EndsTrial()
    {
        var tenant = Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(3), isTrial: true);

        tenant.UpdateSubscription(DateTime.UtcNow.AddDays(30));

        Assert.False(tenant.IsTrial);
    }

    [Fact]
    public void MarkTrialReminderSent_SetsTimestamp()
    {
        var tenant = Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(7));

        tenant.MarkTrialReminderSent();

        Assert.NotNull(tenant.LastTrialReminderSentAt);
    }
}
