using Axon.Application.Tenants.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Tenants;

public class ConfirmTenantRegistrationCommandHandlerTests
{
    private const string RawCode = "123456";

    private static (ConfirmTenantRegistrationCommandHandler Handler, Axon.Infrastructure.Persistence.AppDbContext DbContext, FakeRegisterTenantMediator Mediator, PendingTenantRegistration Pending)
        Arrange(DateTime? expiresAt = null)
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var mediator = new FakeRegisterTenantMediator();

        var pending = PendingTenantRegistration.Create(
            "Mi Negocio", "mi-negocio", "owner@test.com", "hashed-password", "basic",
            PendingTenantRegistration.HashVerificationCode(RawCode),
            expiresAt ?? DateTime.UtcNow.AddMinutes(15));

        dbContext.PendingTenantRegistrations.Add(pending);
        dbContext.SaveChanges();

        var handler = new ConfirmTenantRegistrationCommandHandler(dbContext, mediator);

        return (handler, dbContext, mediator, pending);
    }

    [Fact]
    public async Task Handle_WithCorrectCode_CreatesTenantAndConsumesRequest()
    {
        var (handler, dbContext, mediator, pending) = Arrange();

        var result = await handler.Handle(new ConfirmTenantRegistrationCommand(pending.Id, RawCode), CancellationToken.None);

        Assert.Equal(mediator.Result, result);
        Assert.NotNull(mediator.LastCommand);
        Assert.Equal(pending.BusinessName, mediator.LastCommand!.BusinessName);
        Assert.Equal(pending.Slug, mediator.LastCommand.Slug);
        Assert.Equal(pending.OwnerEmail, mediator.LastCommand.OwnerEmail);
        Assert.Equal(pending.OwnerPasswordHash, mediator.LastCommand.OwnerPasswordHash);
        Assert.Equal(pending.Plan, mediator.LastCommand.Plan);

        var updatedPending = dbContext.PendingTenantRegistrations.Single(p => p.Id == pending.Id);
        Assert.False(updatedPending.IsActive);
    }

    [Fact]
    public async Task Handle_WithWrongCode_IncrementsAttemptsAndThrows()
    {
        var (handler, dbContext, _, pending) = Arrange();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ConfirmTenantRegistrationCommand(pending.Id, "000000"), CancellationToken.None));

        var updatedPending = dbContext.PendingTenantRegistrations.Single(p => p.Id == pending.Id);
        Assert.Equal(1, updatedPending.FailedAttempts);
        Assert.True(updatedPending.IsActive);
    }

    [Fact]
    public async Task Handle_AfterMaxFailedAttempts_LocksOutEvenWithCorrectCodeAfterward()
    {
        var (handler, _, _, pending) = Arrange();

        for (var i = 0; i < PendingTenantRegistration.MaxCodeAttempts; i++)
        {
            await Assert.ThrowsAsync<DomainException>(
                () => handler.Handle(new ConfirmTenantRegistrationCommand(pending.Id, "000000"), CancellationToken.None));
        }

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ConfirmTenantRegistrationCommand(pending.Id, RawCode), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithExpiredRequest_Throws()
    {
        var (handler, _, _, pending) = Arrange(DateTime.UtcNow.AddMinutes(-1));

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ConfirmTenantRegistrationCommand(pending.Id, RawCode), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithUnknownId_Throws()
    {
        var (handler, _, _, _) = Arrange();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ConfirmTenantRegistrationCommand(Guid.NewGuid(), RawCode), CancellationToken.None));
    }
}
