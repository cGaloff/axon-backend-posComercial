using Axon.Domain.Entities;

namespace Axon.Domain.Tests.Tenants;

public class PendingTenantRegistrationTests
{
    private static PendingTenantRegistration CreatePending(DateTime? expiresAt = null) =>
        PendingTenantRegistration.Create(
            "Mi Negocio", "mi-negocio", "owner@test.com", "hashed-password", "basic",
            "code-hash", expiresAt ?? DateTime.UtcNow.AddMinutes(15));

    [Fact]
    public void Create_IsActiveByDefault()
    {
        var pending = CreatePending();

        Assert.True(pending.IsActive);
        Assert.Equal(0, pending.FailedAttempts);
        Assert.Null(pending.ConsumedAt);
    }

    [Fact]
    public void IsActive_FalseWhenExpired()
    {
        var pending = CreatePending(DateTime.UtcNow.AddMinutes(-1));

        Assert.False(pending.IsActive);
    }

    [Fact]
    public void MarkConsumed_MakesItInactive()
    {
        var pending = CreatePending();

        pending.MarkConsumed();

        Assert.False(pending.IsActive);
        Assert.NotNull(pending.ConsumedAt);
    }

    [Fact]
    public void RegisterFailedAttempt_IncrementsButStaysActiveBelowMax()
    {
        var pending = CreatePending();

        for (var i = 0; i < PendingTenantRegistration.MaxCodeAttempts - 1; i++)
        {
            pending.RegisterFailedAttempt();
        }

        Assert.Equal(PendingTenantRegistration.MaxCodeAttempts - 1, pending.FailedAttempts);
        Assert.True(pending.IsActive);
    }

    [Fact]
    public void RegisterFailedAttempt_AtMax_ConsumesTheRequest()
    {
        var pending = CreatePending();

        for (var i = 0; i < PendingTenantRegistration.MaxCodeAttempts; i++)
        {
            pending.RegisterFailedAttempt();
        }

        Assert.False(pending.IsActive);
        Assert.NotNull(pending.ConsumedAt);
    }

    [Fact]
    public void HashVerificationCode_SameInputProducesSameHash()
    {
        var hash1 = PendingTenantRegistration.HashVerificationCode("123456");
        var hash2 = PendingTenantRegistration.HashVerificationCode("123456");

        Assert.Equal(hash1, hash2);
        Assert.NotEqual("123456", hash1);
    }
}
