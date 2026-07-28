using Axon.Application.Auth.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Tests.Auth;

public class LoginCommandHandlerTests
{
    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:RefreshTokenExpiresInDays"] = "7" })
            .Build();

    private static async Task<(LoginCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, User User, FakePasswordHasher PasswordHasher)> ArrangeAsync()
    {
        var dbContext = TestDbContextFactory.Create();
        var passwordHasher = new FakePasswordHasher();

        var role = Role.Create("Cajero", "", isSystem: true);
        var user = User.Create("Ana Cajera", "ana@test.com", passwordHasher.Hash("correcta123"), role.Id);

        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = new LoginCommandHandler(
            dbContext,
            passwordHasher,
            new FakeJwtTokenService(),
            new FakeTenantContext(),
            new FakeUnitOfWork(dbContext),
            BuildConfiguration());

        return (handler, dbContext, user, passwordHasher);
    }

    [Fact]
    public async Task Handle_WithCorrectCredentials_SucceedsAndResetsFailedAttempts()
    {
        var (handler, dbContext, user, _) = await ArrangeAsync();

        await handler.Handle(new LoginCommand(user.Email, "correcta123", "test-tenant"), CancellationToken.None);

        var updated = await dbContext.Users.FindAsync(user.Id);
        Assert.Equal(0, updated!.FailedLoginAttempts);
        Assert.Null(updated.LockedUntil);
        Assert.NotNull(updated.LastActivityAt);

        var attempt = Assert.Single(dbContext.LoginAttempts);
        Assert.True(attempt.Success);
        Assert.Equal(user.Id, attempt.UserId);
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ThrowsAndIncrementsFailedAttempts()
    {
        var (handler, dbContext, user, _) = await ArrangeAsync();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new LoginCommand(user.Email, "incorrecta", "test-tenant"), CancellationToken.None));

        var updated = await dbContext.Users.FindAsync(user.Id);
        Assert.Equal(1, updated!.FailedLoginAttempts);
        Assert.Null(updated.LockedUntil);

        var attempt = Assert.Single(dbContext.LoginAttempts);
        Assert.False(attempt.Success);
    }

    [Fact]
    public async Task Handle_WithNonExistentEmail_ThrowsAndRecordsAttemptWithoutUserId()
    {
        var (handler, dbContext, _, _) = await ArrangeAsync();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new LoginCommand("no-existe@test.com", "cualquiera", "test-tenant"), CancellationToken.None));

        var attempt = Assert.Single(dbContext.LoginAttempts);
        Assert.Null(attempt.UserId);
        Assert.False(attempt.Success);
    }

    // Seguridad técnica (Matriz de Roles y Permisos v2): tras 5 intentos fallidos
    // seguidos, la cuenta se bloquea — ni siquiera la contraseña correcta funciona
    // hasta que pase el plazo de bloqueo.
    [Fact]
    public async Task Handle_AfterMaxFailedAttempts_LocksAccountEvenWithCorrectPasswordAfterward()
    {
        var (handler, dbContext, user, _) = await ArrangeAsync();

        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            await Assert.ThrowsAsync<DomainException>(
                () => handler.Handle(new LoginCommand(user.Email, "incorrecta", "test-tenant"), CancellationToken.None));
        }

        var lockedUser = await dbContext.Users.FindAsync(user.Id);
        Assert.True(lockedUser!.IsLockedOut);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new LoginCommand(user.Email, "correcta123", "test-tenant"), CancellationToken.None));
        Assert.Contains("bloqueada", ex.Message);
    }
}
