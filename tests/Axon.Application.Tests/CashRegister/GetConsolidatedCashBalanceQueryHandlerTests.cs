using Axon.Application.CashRegister.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.CashRegister;
using CashRegisterEntity = Axon.Domain.Entities.CashRegister.CashRegister;

namespace Axon.Application.Tests.CashRegister;

public class GetConsolidatedCashBalanceQueryHandlerTests
{
    private static (CashRegisterEntity Register, CashSession Session) OpenRegisterWithSession(
        Axon.Infrastructure.Persistence.TenantDbContext dbContext, string name, decimal initialAmount, Guid userId)
    {
        var register = CashRegisterEntity.Create(name, "", isDefault: false);
        var session = CashSession.Create(register.Id, userId, initialAmount);

        dbContext.CashRegisters.Add(register);
        dbContext.CashSessions.Add(session);

        return (register, session);
    }

    private static void AddMovement(
        Axon.Infrastructure.Persistence.TenantDbContext dbContext, CashSession session, CashMovementType type, decimal amount, Guid userId)
    {
        var movement = CashMovement.Create(session.Id, type, amount, "Movimiento de prueba", userId);
        session.AddCashMovement(amount, type);
        dbContext.CashMovements.Add(movement);
    }

    [Fact]
    public async Task Handle_WithSingleActiveRegister_ConsolidatedTotalEqualsIndividualBalance()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var (register, session) = OpenRegisterWithSession(dbContext, "Caja Principal", 50000m, userId);
        AddMovement(dbContext, session, CashMovementType.CashSale, 20000m, userId);
        await dbContext.SaveChangesAsync();

        var handler = new GetConsolidatedCashBalanceQueryHandler(dbContext);

        var result = await handler.Handle(new GetConsolidatedCashBalanceQuery(), CancellationToken.None);

        var single = Assert.Single(result.Registers);
        Assert.Equal(register.Id, single.CashRegisterId);
        Assert.Equal(70000m, single.ExpectedAmount);
        Assert.Equal(70000m, result.TotalExpectedAmount);
    }

    [Fact]
    public async Task Handle_WithMultipleActiveRegisters_ReturnsCorrectIndividualBalancesAndConsolidatedTotal()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var (registerA, sessionA) = OpenRegisterWithSession(dbContext, "Caja A", 50000m, userId);
        AddMovement(dbContext, sessionA, CashMovementType.CashSale, 20000m, userId);

        var (registerB, sessionB) = OpenRegisterWithSession(dbContext, "Caja B", 30000m, userId);
        AddMovement(dbContext, sessionB, CashMovementType.CashSale, 10000m, userId);
        AddMovement(dbContext, sessionB, CashMovementType.Expense, 5000m, userId);

        await dbContext.SaveChangesAsync();

        var handler = new GetConsolidatedCashBalanceQueryHandler(dbContext);

        var result = await handler.Handle(new GetConsolidatedCashBalanceQuery(), CancellationToken.None);

        Assert.Equal(2, result.Registers.Count);

        var balanceA = result.Registers.Single(r => r.CashRegisterId == registerA.Id);
        var balanceB = result.Registers.Single(r => r.CashRegisterId == registerB.Id);

        Assert.Equal(70000m, balanceA.ExpectedAmount); // 50000 + 20000
        Assert.Equal(35000m, balanceB.ExpectedAmount); // 30000 + 10000 - 5000

        Assert.Equal(105000m, result.TotalExpectedAmount); // 70000 + 35000
    }

    // Cajas cerradas mezcladas con activas: solo las activas entran en la
    // consolidación (decisión documentada: una caja cerrada ya contó y liquidó
    // su dinero, no tiene un "balance en curso" que consolidar).
    [Fact]
    public async Task Handle_WithClosedAndActiveRegisters_OnlyIncludesActiveOnesInConsolidation()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var (activeRegister, activeSession) = OpenRegisterWithSession(dbContext, "Caja Activa", 50000m, userId);
        AddMovement(dbContext, activeSession, CashMovementType.CashSale, 20000m, userId);

        var (_, closedSession) = OpenRegisterWithSession(dbContext, "Caja Cerrada", 100000m, userId);
        AddMovement(dbContext, closedSession, CashMovementType.CashSale, 50000m, userId);
        closedSession.Close(userId, countedAmount: 150000m);

        await dbContext.SaveChangesAsync();

        var handler = new GetConsolidatedCashBalanceQueryHandler(dbContext);

        var result = await handler.Handle(new GetConsolidatedCashBalanceQuery(), CancellationToken.None);

        var single = Assert.Single(result.Registers);
        Assert.Equal(activeRegister.Id, single.CashRegisterId);
        Assert.Equal(70000m, result.TotalExpectedAmount);
    }

    [Fact]
    public async Task Handle_WithNoActiveRegisters_ReturnsEmptyConsolidationWithZeroTotal()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var (_, session) = OpenRegisterWithSession(dbContext, "Caja", 10000m, userId);
        session.Close(userId, countedAmount: 10000m);
        await dbContext.SaveChangesAsync();

        var handler = new GetConsolidatedCashBalanceQueryHandler(dbContext);

        var result = await handler.Handle(new GetConsolidatedCashBalanceQuery(), CancellationToken.None);

        Assert.Empty(result.Registers);
        Assert.Equal(0m, result.TotalExpectedAmount);
    }

    // El aislamiento entre tenants es automático (schema-per-tenant), pero se
    // verifica aquí con dos TenantDbContext independientes (cada uno una base
    // InMemory separada, equivalente para este propósito a un schema de tenant
    // distinto): las cajas de un "tenant" nunca deben aparecer en la consolidación
    // del otro.
    [Fact]
    public async Task Handle_TwoIndependentTenantContexts_DoNotMixCashRegisterBalances()
    {
        await using var dbContextTenantA = TestDbContextFactory.Create();
        await using var dbContextTenantB = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var (registerA, sessionA) = OpenRegisterWithSession(dbContextTenantA, "Caja Tenant A", 10000m, userId);
        AddMovement(dbContextTenantA, sessionA, CashMovementType.CashSale, 5000m, userId);
        await dbContextTenantA.SaveChangesAsync();

        var (registerB, sessionB) = OpenRegisterWithSession(dbContextTenantB, "Caja Tenant B", 90000m, userId);
        AddMovement(dbContextTenantB, sessionB, CashMovementType.CashSale, 1000m, userId);
        await dbContextTenantB.SaveChangesAsync();

        var handlerA = new GetConsolidatedCashBalanceQueryHandler(dbContextTenantA);
        var handlerB = new GetConsolidatedCashBalanceQueryHandler(dbContextTenantB);

        var resultA = await handlerA.Handle(new GetConsolidatedCashBalanceQuery(), CancellationToken.None);
        var resultB = await handlerB.Handle(new GetConsolidatedCashBalanceQuery(), CancellationToken.None);

        var onlyA = Assert.Single(resultA.Registers);
        Assert.Equal(registerA.Id, onlyA.CashRegisterId);
        Assert.Equal(15000m, resultA.TotalExpectedAmount);

        var onlyB = Assert.Single(resultB.Registers);
        Assert.Equal(registerB.Id, onlyB.CashRegisterId);
        Assert.Equal(91000m, resultB.TotalExpectedAmount);
    }
}
