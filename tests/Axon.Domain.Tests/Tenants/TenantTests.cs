using Axon.Domain.Entities;

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
    public void MarkTrialReminderSent_SetsTimestamp()
    {
        var tenant = Tenant.Create("mi-negocio", "Mi Negocio", "basic", "owner@test.com", DateTime.UtcNow.AddDays(7));

        tenant.MarkTrialReminderSent();

        Assert.NotNull(tenant.LastTrialReminderSentAt);
    }
}
