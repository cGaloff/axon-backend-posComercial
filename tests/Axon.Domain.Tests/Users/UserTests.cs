using Axon.Domain.Entities;

namespace Axon.Domain.Tests.Users;

public class UserTests
{
    // ResetPassword (recuperación por email) desbloquea la cuenta además de
    // cambiar el hash — a diferencia de ChangePassword (cambio de admin/propio,
    // ya autenticado), que no debe tocar el estado de bloqueo.
    [Fact]
    public void ResetPassword_ClearsLockoutStateAndUpdatesHash()
    {
        var role = Role.Create("Cajero", "", isSystem: true);
        var user = User.Create("Ana Cajera", "ana@test.com", "hash-viejo", role.Id);

        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            user.RegisterFailedLoginAttempt();
        }

        Assert.True(user.IsLockedOut);

        user.ResetPassword("hash-nuevo");

        Assert.Equal("hash-nuevo", user.PasswordHash);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
        Assert.False(user.IsLockedOut);
    }

    [Fact]
    public void ChangePassword_DoesNotAffectLockoutState()
    {
        var role = Role.Create("Cajero", "", isSystem: true);
        var user = User.Create("Ana Cajera", "ana@test.com", "hash-viejo", role.Id);

        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            user.RegisterFailedLoginAttempt();
        }

        user.ChangePassword("hash-nuevo");

        Assert.Equal("hash-nuevo", user.PasswordHash);
        Assert.True(user.IsLockedOut);
    }
}
