using Axon.Application.Tests.TestSupport;
using Axon.Application.Users.Commands;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Users;

public class SetRoleDiscountCapCommandHandlerTests
{
    [Fact]
    public async Task Handle_SetsTheDiscountCapForTheRole()
    {
        var dbContext = TestDbContextFactory.Create();
        var role = Role.Create("Cajero", "", isSystem: true);
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        var handler = new SetRoleDiscountCapCommandHandler(dbContext, new FakeUnitOfWork(dbContext));

        await handler.Handle(new SetRoleDiscountCapCommand(role.Id, 15m), CancellationToken.None);

        var updated = await dbContext.Roles.FindAsync(role.Id);
        Assert.Equal(15m, updated!.MaxDiscountPercentage);
    }

    [Fact]
    public async Task Handle_WithNull_RemovesTheCap()
    {
        var dbContext = TestDbContextFactory.Create();
        var role = Role.Create("Cajero", "", isSystem: true, maxDiscountPercentage: 10m);
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        var handler = new SetRoleDiscountCapCommandHandler(dbContext, new FakeUnitOfWork(dbContext));

        await handler.Handle(new SetRoleDiscountCapCommand(role.Id, null), CancellationToken.None);

        var updated = await dbContext.Roles.FindAsync(role.Id);
        Assert.Null(updated!.MaxDiscountPercentage);
    }

    [Fact]
    public async Task Handle_WithNonExistentRole_ThrowsDomainException()
    {
        var dbContext = TestDbContextFactory.Create();
        var handler = new SetRoleDiscountCapCommandHandler(dbContext, new FakeUnitOfWork(dbContext));

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new SetRoleDiscountCapCommand(Guid.NewGuid(), 10m), CancellationToken.None));
    }
}
