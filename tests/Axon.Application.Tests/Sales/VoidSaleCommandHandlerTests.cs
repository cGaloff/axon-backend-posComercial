using Axon.Application.Sales.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Sales;

public class VoidSaleCommandHandlerTests
{
    private static async Task<(VoidSaleCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, Sale Sale, FakeCurrentUserContext CurrentUser)> ArrangeAsync()
    {
        var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUserContext { Permissions = new List<string> { "sales:void" } };

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 1000m, quantity: 1);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total));

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new VoidSaleCommandHandler(dbContext, new FakeUnitOfWork(dbContext), currentUser, new FakePasswordHasher());

        return (handler, dbContext, sale, currentUser);
    }

    [Fact]
    public async Task Handle_WithCompletedSale_MarksItAsVoidedWithReasonAndUser()
    {
        var (handler, dbContext, sale, currentUser) = await ArrangeAsync();

        await handler.Handle(new VoidSaleCommand(sale.Id, "Venta duplicada por error"), CancellationToken.None);

        var updated = await dbContext.Sales.FindAsync(sale.Id);
        Assert.Equal(SaleStatus.Voided, updated!.Status);
        Assert.Equal(currentUser.UserId, updated.VoidedBy);
        Assert.Equal("Venta duplicada por error", updated.VoidReason);
        Assert.NotNull(updated.VoidedAt);
        Assert.Null(updated.AuthorizedBy);
    }

    [Fact]
    public async Task Handle_OnAlreadyVoidedSale_ThrowsDomainException()
    {
        var (handler, _, sale, _) = await ArrangeAsync();

        await handler.Handle(new VoidSaleCommand(sale.Id, "Primera anulación"), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new VoidSaleCommand(sale.Id, "Segunda anulación"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithNonExistentSale_ThrowsDomainException()
    {
        var (handler, _, _, _) = await ArrangeAsync();

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new VoidSaleCommand(Guid.NewGuid(), "Motivo"), CancellationToken.None));
    }

    // Autorización de supervisor (Matriz de Roles y Permisos v2, rol Cajero): un
    // usuario SIN sales:void (p. ej. un Cajero) no puede anular sin el PIN de un
    // Administrador/Propietario.
    [Fact]
    public async Task Handle_WithoutSalesVoidPermissionAndNoSupervisorPin_ThrowsDomainException()
    {
        var dbContext = TestDbContextFactory.Create();
        var cashier = new FakeCurrentUserContext { Permissions = new List<string>() };

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 1000m, quantity: 1);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total));
        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new VoidSaleCommandHandler(dbContext, new FakeUnitOfWork(dbContext), cashier, new FakePasswordHasher());

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new VoidSaleCommand(sale.Id, "Motivo"), CancellationToken.None));
        Assert.Contains("PIN", ex.Message);
    }

    [Fact]
    public async Task Handle_WithoutSalesVoidPermissionButValidSupervisorPin_VoidsAndRecordsAuthorizer()
    {
        var dbContext = TestDbContextFactory.Create();
        var passwordHasher = new FakePasswordHasher();

        var role = Role.Create("Propietario", "", isSystem: true);
        var permission = Permission.Create("sales", "void");
        role.AddPermission(permission);
        var supervisor = User.Create("Ana Admin", "ana@test.com", "irrelevante", role.Id);
        supervisor.SetPin(passwordHasher.Hash("1234"));

        dbContext.Roles.Add(role);
        dbContext.Users.Add(supervisor);

        var cashier = new FakeCurrentUserContext { Permissions = new List<string>() };

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 1000m, quantity: 1);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total));
        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new VoidSaleCommandHandler(dbContext, new FakeUnitOfWork(dbContext), cashier, passwordHasher);

        await handler.Handle(new VoidSaleCommand(sale.Id, "Motivo", SupervisorPin: "1234"), CancellationToken.None);

        var updated = await dbContext.Sales.FindAsync(sale.Id);
        Assert.Equal(SaleStatus.Voided, updated!.Status);
        Assert.Equal(cashier.UserId, updated.VoidedBy);
        Assert.Equal(supervisor.Id, updated.AuthorizedBy);
    }

    [Fact]
    public async Task Handle_WithoutSalesVoidPermissionAndWrongSupervisorPin_ThrowsDomainException()
    {
        var dbContext = TestDbContextFactory.Create();
        var passwordHasher = new FakePasswordHasher();

        var role = Role.Create("Propietario", "", isSystem: true);
        role.AddPermission(Permission.Create("sales", "void"));
        var supervisor = User.Create("Ana Admin", "ana@test.com", "irrelevante", role.Id);
        supervisor.SetPin(passwordHasher.Hash("1234"));

        dbContext.Roles.Add(role);
        dbContext.Users.Add(supervisor);

        var cashier = new FakeCurrentUserContext { Permissions = new List<string>() };

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 1000m, quantity: 1);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total));
        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new VoidSaleCommandHandler(dbContext, new FakeUnitOfWork(dbContext), cashier, passwordHasher);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new VoidSaleCommand(sale.Id, "Motivo", SupervisorPin: "9999"), CancellationToken.None));
    }
}
