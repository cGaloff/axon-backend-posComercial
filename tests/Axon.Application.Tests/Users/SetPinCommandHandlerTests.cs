using Axon.Application.Tests.TestSupport;
using Axon.Application.Users.Commands;
using Axon.Domain.Entities;

namespace Axon.Application.Tests.Users;

public class SetPinCommandHandlerTests
{
    [Fact]
    public async Task Handle_SetsPinForTheCurrentUser()
    {
        var dbContext = TestDbContextFactory.Create();
        var passwordHasher = new FakePasswordHasher();

        var role = Role.Create("Administrador", "", isSystem: true);
        var user = User.Create("Ana Admin", "ana@test.com", "irrelevante", role.Id);
        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var currentUser = new FakeCurrentUserContext { UserId = user.Id };
        var handler = new SetPinCommandHandler(dbContext, new FakeUnitOfWork(dbContext), currentUser, passwordHasher);

        await handler.Handle(new SetPinCommand("4321"), CancellationToken.None);

        var updated = await dbContext.Users.FindAsync(user.Id);
        Assert.True(passwordHasher.Verify("4321", updated!.PinHash!));
    }
}
