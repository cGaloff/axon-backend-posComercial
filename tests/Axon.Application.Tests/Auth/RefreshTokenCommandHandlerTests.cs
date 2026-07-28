using System.Reflection;
using Axon.Application.Auth.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Tests.Auth;

public class RefreshTokenCommandHandlerTests
{
    private const string RawRefreshToken = "raw-refresh-token-value";

    private static void SetLastActivityAt(User user, DateTime lastActivityAtUtc)
    {
        typeof(User).GetProperty(nameof(User.LastActivityAt), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(user, lastActivityAtUtc);
    }

    private static IConfiguration BuildConfiguration(int inactivityTimeoutMinutes) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenExpiresInDays"] = "7",
                ["Jwt:InactivityTimeoutMinutes"] = inactivityTimeoutMinutes.ToString()
            })
            .Build();

    private static async Task<(RefreshTokenCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, RefreshToken Token)> ArrangeAsync(
        DateTime lastActivityAtUtc, int inactivityTimeoutMinutes)
    {
        var dbContext = TestDbContextFactory.Create();

        var role = Role.Create("Cajero", "", isSystem: true);
        var user = User.Create("Ana Cajera", "ana@test.com", "irrelevante", role.Id);
        SetLastActivityAt(user, lastActivityAtUtc);

        var token = RefreshToken.Create(user.Id, RefreshToken.HashToken(RawRefreshToken), DateTime.UtcNow.AddDays(7));

        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync();

        var handler = new RefreshTokenCommandHandler(
            dbContext,
            new FakeJwtTokenService(),
            new FakeTenantContext(),
            new FakeUnitOfWork(dbContext),
            BuildConfiguration(inactivityTimeoutMinutes));

        return (handler, dbContext, token);
    }

    [Fact]
    public async Task Handle_WithRecentActivity_SucceedsAndExtendsActivity()
    {
        var (handler, dbContext, token) = await ArrangeAsync(DateTime.UtcNow.AddMinutes(-5), inactivityTimeoutMinutes: 60);

        await handler.Handle(new RefreshTokenCommand(RawRefreshToken), CancellationToken.None);

        var oldToken = await dbContext.RefreshTokens.FindAsync(token.Id);
        Assert.NotNull(oldToken!.RevokedAt);

        var user = await dbContext.Users.SingleAsync();
        Assert.True(DateTime.UtcNow - user.LastActivityAt!.Value < TimeSpan.FromSeconds(5));
    }

    // Cierre de sesión por inactividad (Matriz de Roles y Permisos v2, "seguridad
    // técnica"): aunque el refresh token en sí sigue vigente, si pasó más tiempo
    // del umbral desde la última actividad, se exige login completo de nuevo.
    [Fact]
    public async Task Handle_AfterInactivityTimeout_ThrowsAndRevokesTheToken()
    {
        var (handler, dbContext, token) = await ArrangeAsync(DateTime.UtcNow.AddMinutes(-90), inactivityTimeoutMinutes: 60);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new RefreshTokenCommand(RawRefreshToken), CancellationToken.None));
        Assert.Contains("inactividad", ex.Message);

        var revokedToken = await dbContext.RefreshTokens.FindAsync(token.Id);
        Assert.NotNull(revokedToken!.RevokedAt);
    }
}
