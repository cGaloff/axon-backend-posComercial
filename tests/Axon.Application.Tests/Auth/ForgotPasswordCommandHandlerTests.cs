using Axon.Application.Auth.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Tests.Auth;

public class ForgotPasswordCommandHandlerTests
{
    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:PasswordResetTokenExpiresInMinutes"] = "30",
                ["Frontend:ResetPasswordUrl"] = "https://app.test.com/reset-password"
            })
            .Build();

    private static async Task<(ForgotPasswordCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, User User, FakeEmailService EmailService)> ArrangeAsync()
    {
        var dbContext = TestDbContextFactory.Create();
        var emailService = new FakeEmailService();

        var role = Role.Create("Cajero", "", isSystem: true);
        var user = User.Create("Ana Cajera", "ana@test.com", "hash", role.Id);

        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = new ForgotPasswordCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            emailService,
            BuildConfiguration());

        return (handler, dbContext, user, emailService);
    }

    [Fact]
    public async Task Handle_WithExistingActiveUser_CreatesTokenAndSendsEmail()
    {
        var (handler, dbContext, user, emailService) = await ArrangeAsync();

        await handler.Handle(new ForgotPasswordCommand(user.Email, "test-tenant"), CancellationToken.None);

        var token = Assert.Single(dbContext.PasswordResetTokens);
        Assert.Equal(user.Id, token.UserId);
        Assert.True(token.IsActive);

        Assert.Equal(user.Email, emailService.LastPasswordResetEmail);
        Assert.Contains("tenant=test-tenant", emailService.LastPasswordResetLink);
    }

    [Fact]
    public async Task Handle_WithNonExistentEmail_DoesNotThrowAndCreatesNoToken()
    {
        var (handler, dbContext, _, emailService) = await ArrangeAsync();

        await handler.Handle(new ForgotPasswordCommand("no-existe@test.com", "test-tenant"), CancellationToken.None);

        Assert.Empty(dbContext.PasswordResetTokens);
        Assert.Null(emailService.LastPasswordResetEmail);
    }

    [Fact]
    public async Task Handle_CalledTwice_InvalidatesPreviousToken()
    {
        var (handler, dbContext, user, _) = await ArrangeAsync();

        await handler.Handle(new ForgotPasswordCommand(user.Email, "test-tenant"), CancellationToken.None);
        var firstToken = Assert.Single(dbContext.PasswordResetTokens);

        await handler.Handle(new ForgotPasswordCommand(user.Email, "test-tenant"), CancellationToken.None);

        Assert.Equal(2, dbContext.PasswordResetTokens.Count());
        var refreshedFirstToken = dbContext.PasswordResetTokens.Single(t => t.Id == firstToken.Id);
        Assert.False(refreshedFirstToken.IsActive);
    }
}
