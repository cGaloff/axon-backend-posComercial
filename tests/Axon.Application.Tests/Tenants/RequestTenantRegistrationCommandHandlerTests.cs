using Axon.Application.Tenants.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Tests.Tenants;

public class RequestTenantRegistrationCommandHandlerTests
{
    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:RegistrationCodeExpiresInMinutes"] = "15"
            })
            .Build();

    private static (RequestTenantRegistrationCommandHandler Handler, Axon.Infrastructure.Persistence.AppDbContext DbContext, FakeEmailService EmailService)
        Arrange()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var emailService = new FakeEmailService();

        var handler = new RequestTenantRegistrationCommandHandler(
            dbContext,
            new FakePasswordHasher(),
            emailService,
            BuildConfiguration());

        return (handler, dbContext, emailService);
    }

    private static RequestTenantRegistrationCommand ValidCommand(string email = "owner@test.com") =>
        new("Mi Negocio", "mi-negocio", email, "password123", "basic");

    [Fact]
    public async Task Handle_WithNewSlug_CreatesPendingRegistrationAndSendsCode()
    {
        var (handler, dbContext, emailService) = Arrange();

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        var pending = Assert.Single(dbContext.PendingTenantRegistrations);
        Assert.Equal(result.PendingRegistrationId, pending.Id);
        Assert.True(pending.IsActive);
        Assert.Equal("owner@test.com", emailService.LastVerificationEmail);
        Assert.NotNull(emailService.LastVerificationCode);
        Assert.Equal(6, emailService.LastVerificationCode!.Length);
    }

    [Fact]
    public async Task Handle_WithSlugAlreadyTaken_Throws()
    {
        var (handler, dbContext, _) = Arrange();
        dbContext.Tenants.Add(Tenant.Create("mi-negocio", "Otro Negocio", "basic", "otro@test.com", DateTime.UtcNow.AddDays(7)));
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(ValidCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CalledTwiceWithSameEmail_InvalidatesPreviousPendingRegistration()
    {
        var (handler, dbContext, _) = Arrange();

        var first = await handler.Handle(ValidCommand(), CancellationToken.None);
        await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(2, dbContext.PendingTenantRegistrations.Count());
        var firstPending = dbContext.PendingTenantRegistrations.Single(p => p.Id == first.PendingRegistrationId);
        Assert.False(firstPending.IsActive);
    }
}
