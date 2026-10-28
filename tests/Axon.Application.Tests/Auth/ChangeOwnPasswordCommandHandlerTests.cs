using Axon.Application.Auth.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Auth;

public class ChangeOwnPasswordCommandHandlerTests
{
    private static async Task<(ChangeOwnPasswordCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, User User, FakePasswordHasher PasswordHasher)> ArrangeAsync()
    {
        var dbContext = TestDbContextFactory.Create();
        var passwordHasher = new FakePasswordHasher();

        var role = Role.Create("Cajero", "", isSystem: true);
        var user = User.Create("Ana Cajera", "ana@test.com", passwordHasher.Hash("actual1234"), role.Id);

        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = new ChangeOwnPasswordCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            passwordHasher,
            new FakeCurrentUserContext { UserId = user.Id });

        return (handler, dbContext, user, passwordHasher);
    }

    [Fact]
    public async Task Handle_WithCorrectCurrentPassword_UpdatesHash()
    {
        var (handler, dbContext, user, passwordHasher) = await ArrangeAsync();

        await handler.Handle(new ChangeOwnPasswordCommand("actual1234", "nueva12345"), CancellationToken.None);

        var updated = await dbContext.Users.FindAsync(user.Id);
        Assert.Equal(passwordHasher.Hash("nueva12345"), updated!.PasswordHash);
    }

    [Fact]
    public async Task Handle_WithWrongCurrentPassword_ThrowsAndKeepsHash()
    {
        var (handler, dbContext, user, passwordHasher) = await ArrangeAsync();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ChangeOwnPasswordCommand("otra-cosa", "nueva12345"), CancellationToken.None));

        var unchanged = await dbContext.Users.FindAsync(user.Id);
        Assert.Equal(passwordHasher.Hash("actual1234"), unchanged!.PasswordHash);
    }

    [Fact]
    public void Validator_WithSameOrShortPassword_Fails()
    {
        var validator = new ChangeOwnPasswordCommandValidator();

        Assert.False(validator.Validate(new ChangeOwnPasswordCommand("actual1234", "actual1234")).IsValid);
        Assert.False(validator.Validate(new ChangeOwnPasswordCommand("actual1234", "corta")).IsValid);
        Assert.True(validator.Validate(new ChangeOwnPasswordCommand("actual1234", "nueva12345")).IsValid);
    }
}
