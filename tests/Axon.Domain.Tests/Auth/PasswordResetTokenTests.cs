using Axon.Domain.Entities;

namespace Axon.Domain.Tests.Auth;

public class PasswordResetTokenTests
{
    [Fact]
    public void Create_IsActiveByDefault()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", DateTime.UtcNow.AddMinutes(30));

        Assert.True(token.IsActive);
        Assert.Null(token.UsedAt);
    }

    [Fact]
    public void MarkUsed_MakesTokenInactive()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", DateTime.UtcNow.AddMinutes(30));

        token.MarkUsed();

        Assert.False(token.IsActive);
        Assert.NotNull(token.UsedAt);
    }

    [Fact]
    public void IsActive_FalseWhenExpired()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", DateTime.UtcNow.AddMinutes(-1));

        Assert.False(token.IsActive);
    }

    [Fact]
    public void HashToken_SameInputProducesSameHash()
    {
        var hash1 = PasswordResetToken.HashToken("raw-token-value");
        var hash2 = PasswordResetToken.HashToken("raw-token-value");

        Assert.Equal(hash1, hash2);
        Assert.NotEqual("raw-token-value", hash1);
    }
}
