using Axon.Domain.Entities;

namespace Axon.Domain.Tests;

public class TenantTests
{
    [Fact]
    public void Create_SinFechaDeVencimiento_DejaElTenantSinVencer()
    {
        var tenant = Tenant.Create("panaderia", "Panadería", "basic");

        Assert.Null(tenant.SubscriptionExpiresAt);
        Assert.False(tenant.IsSubscriptionExpired(DateTime.UtcNow));
        Assert.False(tenant.IsSubscriptionExpired(DateTime.UtcNow.AddYears(50)));
    }

    [Fact]
    public void Create_ConFechaDeVencimiento_LaGuardaEnUtc()
    {
        var vence = new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Unspecified);

        var tenant = Tenant.Create("panaderia", "Panadería", "basic", subscriptionExpiresAt: vence);

        Assert.Equal(DateTimeKind.Utc, tenant.SubscriptionExpiresAt!.Value.Kind);
        Assert.Equal(vence, tenant.SubscriptionExpiresAt.Value);
    }

    [Fact]
    public void IsSubscriptionExpired_ConFechaPasada_EsVerdadero()
    {
        var tenant = Tenant.Create("panaderia", "Panadería", "basic", subscriptionExpiresAt: DateTime.UtcNow.AddDays(-1));

        Assert.True(tenant.IsSubscriptionExpired(DateTime.UtcNow));
    }

    [Fact]
    public void IsSubscriptionExpired_ConFechaFutura_EsFalso()
    {
        var tenant = Tenant.Create("panaderia", "Panadería", "basic", subscriptionExpiresAt: DateTime.UtcNow.AddDays(5));

        Assert.False(tenant.IsSubscriptionExpired(DateTime.UtcNow));
    }

    [Fact]
    public void UpdateSubscription_ExtiendeLaFechaYRespetaElPlanSiNoSeEnvia()
    {
        var tenant = Tenant.Create("panaderia", "Panadería", "basic", subscriptionExpiresAt: DateTime.UtcNow.AddDays(-1));
        var nuevaFecha = DateTime.UtcNow.AddDays(30);

        tenant.UpdateSubscription(nuevaFecha);

        Assert.Equal(nuevaFecha, tenant.SubscriptionExpiresAt);
        Assert.Equal("basic", tenant.Plan);
        Assert.False(tenant.IsSubscriptionExpired(DateTime.UtcNow));
    }

    [Fact]
    public void UpdateSubscription_ConPlan_TambienCambiaElPlan()
    {
        var tenant = Tenant.Create("panaderia", "Panadería", "basic");

        tenant.UpdateSubscription(DateTime.UtcNow.AddDays(30), "pro");

        Assert.Equal("pro", tenant.Plan);
    }

    [Fact]
    public void UpdateSubscription_ConNull_DejaAlTenantSinVencimiento()
    {
        var tenant = Tenant.Create("panaderia", "Panadería", "basic", subscriptionExpiresAt: DateTime.UtcNow.AddDays(5));

        tenant.UpdateSubscription(null);

        Assert.Null(tenant.SubscriptionExpiresAt);
        Assert.False(tenant.IsSubscriptionExpired(DateTime.UtcNow.AddYears(50)));
    }

    [Fact]
    public void UpdateSubscription_NoTocaIsActive()
    {
        var tenant = Tenant.Create("panaderia", "Panadería", "basic");

        tenant.UpdateSubscription(DateTime.UtcNow.AddDays(-10));

        Assert.True(tenant.IsActive);
    }

    [Fact]
    public void UpdateSubscription_ConFechaFutura_ReactivaTenantSuspendidoPorElBarrido()
    {
        var tenant = Tenant.Create("panaderia", "Panadería", "basic", subscriptionExpiresAt: DateTime.UtcNow.AddDays(-1));
        tenant.Suspend();

        tenant.UpdateSubscription(DateTime.UtcNow.AddDays(30));

        Assert.True(tenant.IsActive);
        Assert.Null(tenant.LastTrialReminderSentAt);
    }
}
