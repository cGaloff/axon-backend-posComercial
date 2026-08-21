using Axon.Application.Auth.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Auth;

public class ResetPasswordCommandHandlerTests
{
    private static async Task<(ResetPasswordCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, User User, FakePasswordHasher PasswordHasher)> ArrangeAsync()
    {
        var dbContext = TestDbContextFactory.Create();
        var passwordHasher = new FakePasswordHasher();

        var role = Role.Create("Cajero", "", isSystem: true);
        var user = User.Create("Ana Cajera", "ana@test.com", passwordHasher.Hash("vieja123"), role.Id);

        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            user.RegisterFailedLoginAttempt();
        }

        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        dbContext.RefreshTokens.Add(RefreshToken.Create(user.Id, RefreshToken.HashToken("some-refresh-token"), DateTime.UtcNow.AddDays(7)));
        await dbContext.SaveChangesAsync();

        var handler = new ResetPasswordCommandHandler(dbContext, new FakeUnitOfWork(dbContext), passwordHasher);

        return (handler, dbContext, user, passwordHasher);
    }

    [Fact]
    public async Task Handle_WithValidToken_UpdatesPasswordUnlocksAccountAndRevokesRefreshTokens()
    {
        var (handler, dbContext, user, passwordHasher) = await ArrangeAsync();
        var rawToken = "raw-reset-token";
        dbContext.PasswordResetTokens.Add(
            PasswordResetToken.Create(user.Id, PasswordResetToken.HashToken(rawToken), DateTime.UtcNow.AddMinutes(30)));
        await dbContext.SaveChangesAsync();

        await handler.Handle(new ResetPasswordCommand(rawToken, "nueva12345", "test-tenant"), CancellationToken.None);

        var updatedUser = await dbContext.Users.FindAsync(user.Id);
        Assert.Equal(passwordHasher.Hash("nueva12345"), updatedUser!.PasswordHash);
        Assert.False(updatedUser.IsLockedOut);
        Assert.Equal(0, updatedUser.FailedLoginAttempts);

        var usedToken = Assert.Single(dbContext.PasswordResetTokens);
        Assert.False(usedToken.IsActive);

        var refreshToken = Assert.Single(dbContext.RefreshTokens);
        Assert.False(refreshToken.IsActive);
    }

    [Fact]
    public async Task Handle_WithUnknownToken_Throws()
    {
        var (handler, _, _, _) = await ArrangeAsync();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ResetPasswordCommand("token-inexistente", "nueva12345", "test-tenant"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithExpiredToken_Throws()
    {
        var (handler, dbContext, user, _) = await ArrangeAsync();
        var rawToken = "raw-reset-token";
        dbContext.PasswordResetTokens.Add(
            PasswordResetToken.Create(user.Id, PasswordResetToken.HashToken(rawToken), DateTime.UtcNow.AddMinutes(-1)));
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ResetPasswordCommand(rawToken, "nueva12345", "test-tenant"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithAlreadyUsedToken_Throws()
    {
        var (handler, dbContext, user, _) = await ArrangeAsync();
        var rawToken = "raw-reset-token";
        var resetToken = PasswordResetToken.Create(user.Id, PasswordResetToken.HashToken(rawToken), DateTime.UtcNow.AddMinutes(30));
        resetToken.MarkUsed();
        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ResetPasswordCommand(rawToken, "nueva12345", "test-tenant"), CancellationToken.None));
    }
}
