using Axon.Application.CashRegister.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Entities.CashRegister;
using Axon.Domain.Exceptions;
using CashRegisterEntity = Axon.Domain.Entities.CashRegister.CashRegister;

namespace Axon.Application.Tests.CashRegister;

// Deuda técnica: antes la apertura de caja siempre asumía que quien hace la
// petición ES el cajero que va a operar el turno. Ahora se asigna
// explícitamente un cajero (puede ser distinto de quien abre la caja, p. ej.
// un Administrador configurando el turno de un empleado), y ese cajero queda
// registrado como Sale/CashSession.OpenedBy para aparecer en el historial.
public class OpenCashSessionCommandHandlerTests
{
    private static async Task<(OpenCashSessionCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, CashRegisterEntity Register, User Cashier)> ArrangeAsync()
    {
        var dbContext = TestDbContextFactory.Create();

        var cashierRole = Role.Create("Cajero", "", isSystem: true);
        var permission = Permission.Create("cash_register", "write");
        cashierRole.AddPermission(permission);

        var cashier = User.Create("Ana Cajera", "ana@test.com", "irrelevante", cashierRole.Id);
        var register = CashRegisterEntity.Create("Caja Principal", "", isDefault: true);

        dbContext.Roles.Add(cashierRole);
        dbContext.Users.Add(cashier);
        dbContext.CashRegisters.Add(register);
        await dbContext.SaveChangesAsync();

        var handler = new OpenCashSessionCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext());

        return (handler, dbContext, register, cashier);
    }

    [Fact]
    public async Task Handle_WithValidCashier_OpensSessionAssignedToThatCashier()
    {
        var (handler, dbContext, register, cashier) = await ArrangeAsync();

        var result = await handler.Handle(
            new OpenCashSessionCommand(register.Id, cashier.Id, 50000m), CancellationToken.None);

        Assert.Equal(cashier.Id, result.CashierId);
        Assert.Equal("Ana Cajera", result.CashierName);

        var session = await dbContext.CashSessions.FindAsync(result.SessionId);
        Assert.Equal(cashier.Id, session!.OpenedBy);
    }

    [Fact]
    public async Task Handle_WithNonExistentCashier_ThrowsDomainException()
    {
        var (handler, _, register, _) = await ArrangeAsync();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new OpenCashSessionCommand(register.Id, Guid.NewGuid(), 50000m), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithInactiveCashier_ThrowsDomainException()
    {
        var (handler, dbContext, register, cashier) = await ArrangeAsync();
        cashier.Deactivate();
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new OpenCashSessionCommand(register.Id, cashier.Id, 50000m), CancellationToken.None));
    }

    // Un usuario sin permiso cash_register:write (p. ej. un Bodeguero) no puede
    // ser asignado como cajero de un turno.
    [Fact]
    public async Task Handle_WithUserLackingCashRegisterPermission_ThrowsDomainException()
    {
        var dbContext = TestDbContextFactory.Create();

        var warehouseRole = Role.Create("Bodeguero", "", isSystem: true);
        var warehouseUser = User.Create("Beto Bodeguero", "beto@test.com", "irrelevante", warehouseRole.Id);
        var register = CashRegisterEntity.Create("Caja Principal", "", isDefault: true);

        dbContext.Roles.Add(warehouseRole);
        dbContext.Users.Add(warehouseUser);
        dbContext.CashRegisters.Add(register);
        await dbContext.SaveChangesAsync();

        var handler = new OpenCashSessionCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext());

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new OpenCashSessionCommand(register.Id, warehouseUser.Id, 50000m), CancellationToken.None));
    }
}
