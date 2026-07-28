using Axon.Application.CashRegister.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Entities.CashRegister;
using Axon.Domain.Exceptions;
using CashRegisterEntity = Axon.Domain.Entities.CashRegister.CashRegister;

namespace Axon.Application.Tests.CashRegister;

public class GetCashSessionSummaryQueryHandlerTests
{
    [Fact]
    public async Task Handle_ResolvesCashierAndClosedByNamesFromUsers()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var role = Role.Create("Cajero", "", isSystem: true);
        var cashier = User.Create("Ana Cajera", "ana@test.com", "irrelevante", role.Id);
        var supervisor = User.Create("Beto Supervisor", "beto@test.com", "irrelevante", role.Id);

        var register = CashRegisterEntity.Create("Caja Principal", "", isDefault: true);
        var session = CashSession.Create(register.Id, cashier.Id, 50000m);
        session.Close(supervisor.Id, 50000m);

        dbContext.Roles.Add(role);
        dbContext.Users.AddRange(cashier, supervisor);
        dbContext.CashRegisters.Add(register);
        dbContext.CashSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var handler = new GetCashSessionSummaryQueryHandler(dbContext);

        var result = await handler.Handle(new GetCashSessionSummaryQuery(session.Id), CancellationToken.None);

        Assert.Equal(cashier.Id, result.CashierId);
        Assert.Equal("Ana Cajera", result.CashierName);
        Assert.Equal("Beto Supervisor", result.ClosedByName);
    }

    [Fact]
    public async Task Handle_WithNonExistentSession_ThrowsDomainException()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var handler = new GetCashSessionSummaryQueryHandler(dbContext);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new GetCashSessionSummaryQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
