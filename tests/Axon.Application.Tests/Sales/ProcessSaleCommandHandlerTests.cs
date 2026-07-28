using Axon.Application.Invoicing.Commands;
using Axon.Application.Sales.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;
using CashRegisterEntity = Axon.Domain.Entities.CashRegister.CashRegister;
using CashSessionEntity = Axon.Domain.Entities.CashRegister.CashSession;
using TenantConfigEntity = Axon.Domain.Entities.TenantConfig;

namespace Axon.Application.Tests.Sales;

public class ProcessSaleCommandHandlerTests
{
    // Bug 3 (validación de stock al vender): si el mismo producto aparece en más de
    // una línea de la venta (p. ej. se escaneó el mismo código de barras dos veces en
    // vez de digitar la cantidad), el código anterior comparaba cada línea contra el
    // stock ORIGINAL del producto en vez de contra la cantidad acumulada solicitada.
    // Con Stock=5 e items [3, 3] (total 6 > 5 disponibles), la validación previa dejaba
    // pasar ambas líneas y el error solo aparecía más tarde, dentro de Product.AdjustStock,
    // con un mensaje genérico que ni siquiera nombra el producto. Este test falla en rojo
    // contra el código anterior (el mensaje no contiene el nombre del producto) y pasa en
    // verde con la validación acumulada por producto.
    [Fact]
    public async Task Handle_WhenSameProductRequestedAcrossMultipleLinesExceedsStock_ThrowsWithClearMessage()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create(
            sku: "SKU-001",
            name: "Producto de prueba",
            price: 1000m,
            cost: 500m,
            minStock: 0,
            categoryId: category.Id,
            unitId: unit.Id);
        product.AdjustStock(5);

        var userId = Guid.NewGuid();
        var cashSession = CashSessionEntity.Create(cashRegister.Id, userId, initialAmount: 0m);
        var config = TenantConfigEntity.Create("Negocio de prueba");

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Warehouses.Add(warehouse);
        dbContext.CashRegisters.Add(cashRegister);
        dbContext.Products.Add(product);
        dbContext.CashSessions.Add(cashSession);
        await dbContext.SaveChangesAsync();

        var issueInvoiceHandler = new IssueInvoiceCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakePdfService(),
            new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest>
            {
                new(product.Id, Quantity: 3),
                new(product.Id, Quantity: 3)
            },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 6000m, 10000m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(command, CancellationToken.None));

        Assert.Contains(product.Name, exception.Message);
        Assert.Contains("insuficiente", exception.Message, StringComparison.OrdinalIgnoreCase);

        // Nada debió quedar persistido: la validación debe ocurrir antes de mutar stock.
        var reloadedProduct = await dbContext.Products.FindAsync(product.Id);
        Assert.Equal(5, reloadedProduct!.Stock);
        Assert.Empty(dbContext.Sales);
    }

    [Fact]
    public async Task Handle_WhenStockIsSufficientAcrossMultipleLinesForSameProduct_Succeeds()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create(
            sku: "SKU-002",
            name: "Producto con stock suficiente",
            price: 1000m,
            cost: 500m,
            minStock: 0,
            categoryId: category.Id,
            unitId: unit.Id);
        product.AdjustStock(6);

        var userId = Guid.NewGuid();
        var cashSession = CashSessionEntity.Create(cashRegister.Id, userId, initialAmount: 0m);
        var config = TenantConfigEntity.Create("Negocio de prueba");

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Warehouses.Add(warehouse);
        dbContext.CashRegisters.Add(cashRegister);
        dbContext.Products.Add(product);
        dbContext.CashSessions.Add(cashSession);
        await dbContext.SaveChangesAsync();

        var issueInvoiceHandler = new IssueInvoiceCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakePdfService(),
            new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest>
            {
                new(product.Id, Quantity: 3),
                new(product.Id, Quantity: 3)
            },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 6000m, 10000m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(6000m, result.Total);

        var reloadedProduct = await dbContext.Products.FindAsync(product.Id);
        Assert.Equal(0, reloadedProduct!.Stock);
    }

    // Tope de descuento por rol (Matriz de Roles y Permisos v2, regla transversal
    // C): un Cajero con tope de 10% no puede aplicar un descuento del 20%.
    [Fact]
    public async Task Handle_WhenDiscountExceedsUsersRoleCap_ThrowsDomainException()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create("SKU-DISC-1", "Producto con descuento", 100000m, 50000m, 0, category.Id, unit.Id);
        product.AdjustStock(10);

        var userId = Guid.NewGuid();
        var cashSession = CashSessionEntity.Create(cashRegister.Id, userId, initialAmount: 0m);
        var config = TenantConfigEntity.Create("Negocio de prueba");

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Warehouses.Add(warehouse);
        dbContext.CashRegisters.Add(cashRegister);
        dbContext.Products.Add(product);
        dbContext.CashSessions.Add(cashSession);
        await dbContext.SaveChangesAsync();

        var issueInvoiceHandler = new IssueInvoiceCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId, MaxDiscountPercentage = 10m },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        // Descuento del 20% (20.000 sobre un subtotal bruto de 100.000) — supera el tope de 10%.
        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 1, Discount: 20000m) },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 80000m, 80000m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null);

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("tope permitido", exception.Message);

        Assert.Empty(dbContext.Sales);
        var reloadedProduct = await dbContext.Products.FindAsync(product.Id);
        Assert.Equal(10, reloadedProduct!.Stock);
    }

    [Fact]
    public async Task Handle_WhenDiscountIsWithinUsersRoleCap_Succeeds()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create("SKU-DISC-2", "Producto con descuento permitido", 100000m, 50000m, 0, category.Id, unit.Id);
        product.AdjustStock(10);

        var userId = Guid.NewGuid();
        var cashSession = CashSessionEntity.Create(cashRegister.Id, userId, initialAmount: 0m);
        var config = TenantConfigEntity.Create("Negocio de prueba");

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Warehouses.Add(warehouse);
        dbContext.CashRegisters.Add(cashRegister);
        dbContext.Products.Add(product);
        dbContext.CashSessions.Add(cashSession);
        await dbContext.SaveChangesAsync();

        var issueInvoiceHandler = new IssueInvoiceCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId, MaxDiscountPercentage = 10m },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        // Descuento del 5% (5.000 sobre 100.000) — dentro del tope de 10%.
        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 1, Discount: 5000m) },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 95000m, 95000m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(95000m, result.Total);
    }
}
