using Axon.Application.Invoicing.Commands;
using Axon.Application.Sales.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
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

    // Reportado por negocio: el documento del cliente (CC/NIT) dado al vender
    // debe quedar guardado en la venta.
    [Fact]
    public async Task Handle_WithCustomerDocument_PersistsItOnTheSale()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create("SKU-DOC", "Producto de prueba", 1000m, 500m, 0, category.Id, unit.Id);
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
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 1) },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 1000m, 1000m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: "Juan Pérez",
            CustomerEmail: null,
            Notes: null,
            CustomerDocumentType: CustomerDocumentType.Cc,
            CustomerDocumentNumber: "1002003004");

        var result = await handler.Handle(command, CancellationToken.None);

        var persistedSale = await dbContext.Sales.FindAsync(result.SaleId);
        Assert.Equal(CustomerDocumentType.Cc, persistedSale!.CustomerDocumentType);
        Assert.Equal("1002003004", persistedSale.CustomerDocumentNumber);
    }

    [Fact]
    public async Task Handle_WithoutCustomerDocument_DefaultsNameButLeavesDocumentEmpty()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create("SKU-NODOC", "Producto de prueba", 1000m, 500m, 0, category.Id, unit.Id);
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
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 1) },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 1000m, 1000m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null);

        var result = await handler.Handle(command, CancellationToken.None);

        var persistedSale = await dbContext.Sales.FindAsync(result.SaleId);
        Assert.Equal("Consumidor Final", persistedSale!.CustomerName);
        Assert.Equal(string.Empty, persistedSale.CustomerDocumentNumber);
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

    // Descuento general de la venta: se reparte proporcionalmente al subtotal
    // de cada producto (1000 vs 3000, total 4000) y la última línea recibe el
    // residuo, no cada una un cálculo independiente redondeado.
    [Fact]
    public async Task Handle_WithSaleDiscountAmount_DistributesProportionallyAcrossItems()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var productA = Product.Create("SKU-GEN-A", "Producto A", 1000m, 500m, 0, category.Id, unit.Id);
        productA.AdjustStock(10);
        var productB = Product.Create("SKU-GEN-B", "Producto B", 3000m, 1500m, 0, category.Id, unit.Id);
        productB.AdjustStock(10);

        var userId = Guid.NewGuid();
        var cashSession = CashSessionEntity.Create(cashRegister.Id, userId, initialAmount: 0m);
        var config = TenantConfigEntity.Create("Negocio de prueba");

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Warehouses.Add(warehouse);
        dbContext.CashRegisters.Add(cashRegister);
        dbContext.Products.AddRange(productA, productB);
        dbContext.CashSessions.Add(cashSession);
        await dbContext.SaveChangesAsync();

        var issueInvoiceHandler = new IssueInvoiceCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        // Subtotal total 4.000; descuento general de 400 (10%) -> A recibe
        // 100 (1000/4000 de 400) y B recibe el residuo, 300.
        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest>
            {
                new(productA.Id, Quantity: 1),
                new(productB.Id, Quantity: 1)
            },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 3600m, 3600m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null,
            SaleDiscountAmount: 400m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(3600m, result.Total);

        var persistedSale = await dbContext.Sales.Include(s => s.Items).SingleAsync(s => s.Id == result.SaleId);
        Assert.Equal(400m, persistedSale.GeneralDiscountAmount);

        // Se dio como monto fijo, no como % -> no hay % que mostrar en la factura.
        Assert.Null(persistedSale.GeneralDiscountPercentage);

        var itemA = persistedSale.Items.Single(i => i.ProductId == productA.Id);
        var itemB = persistedSale.Items.Single(i => i.ProductId == productB.Id);

        Assert.Equal(100m, itemA.GeneralDiscountShare);
        Assert.Equal(300m, itemB.GeneralDiscountShare);
        Assert.Equal(900m, itemA.Subtotal);
        Assert.Equal(2700m, itemB.Subtotal);
    }

    [Fact]
    public async Task Handle_WithSaleDiscountPercentage_ComputesAmountFromSaleSubtotal()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create("SKU-GEN-PCT", "Producto con % general", 100000m, 50000m, 0, category.Id, unit.Id);
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
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 1) },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 90000m, 90000m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null,
            SaleDiscountPercentage: 10m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(90000m, result.Total);

        var persistedSale = await dbContext.Sales.FindAsync(result.SaleId);
        Assert.Equal(10000m, persistedSale!.GeneralDiscountAmount);

        // Se dio como % -> la factura debe poder mostrar ese 10% junto al monto.
        Assert.Equal(10m, persistedSale.GeneralDiscountPercentage);
    }

    // El descuento general se reparte sobre lo que queda DESPUÉS del descuento
    // manual por producto, no sobre el precio de lista completo.
    [Fact]
    public async Task Handle_WithSaleDiscountAndManualItemDiscount_GeneralShareAppliesAfterManualDiscount()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create("SKU-GEN-COMBO", "Producto combinado", 1000m, 500m, 0, category.Id, unit.Id);
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
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        // Precio 1000, descuento manual 100 -> queda 900; descuento general de
        // 10% se calcula sobre esos 900 (=90), no sobre los 1000 originales.
        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 1, Discount: 100m) },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 810m, 810m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null,
            SaleDiscountPercentage: 10m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(810m, result.Total);

        var persistedSale = await dbContext.Sales.Include(s => s.Items).SingleAsync(s => s.Id == result.SaleId);
        var item = persistedSale.Items.Single();

        Assert.Equal(100m, item.Discount);
        Assert.Equal(90m, item.GeneralDiscountShare);
        Assert.Equal(810m, item.Subtotal);
    }

    [Fact]
    public async Task Handle_WhenSaleDiscountAmountExceedsSaleSubtotal_ThrowsDomainException()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create("SKU-GEN-EXCESS", "Producto", 1000m, 500m, 0, category.Id, unit.Id);
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
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 1) },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 1000m, 1000m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null,
            SaleDiscountAmount: 1500m);

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Empty(dbContext.Sales);
    }

    // Mismo tope de descuento por rol que ya aplica a los descuentos por
    // producto, ahora evaluado sobre el % efectivo del descuento general.
    [Fact]
    public async Task Handle_WhenSaleDiscountExceedsUsersRoleCap_ThrowsDomainException()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create("SKU-GEN-CAP", "Producto con tope", 100000m, 50000m, 0, category.Id, unit.Id);
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
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId, MaxDiscountPercentage = 10m },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        // Descuento general del 20% sobre 100.000 -> supera el tope del 10%.
        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 1) },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 80000m, 80000m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null,
            SaleDiscountPercentage: 20m);

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("tope permitido", exception.Message);
        Assert.Empty(dbContext.Sales);
    }

    // El descuento manual por producto ahora también se puede dar como % (no
    // solo monto fijo): el handler lo convierte al monto equivalente sobre el
    // precio de ESE producto y guarda el % original para mostrarlo en la factura.
    [Fact]
    public async Task Handle_WithItemDiscountPercentage_ConvertsToAmountAndPersistsPercentage()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create("SKU-ITEM-PCT", "iPhone", 1000000m, 700000m, 0, category.Id, unit.Id);
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
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        // 5% de 1.000.000 = 50.000 de descuento.
        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 1, DiscountPercentage: 5m) },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 950000m, 950000m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(950000m, result.Total);

        var persistedSale = await dbContext.Sales.Include(s => s.Items).SingleAsync(s => s.Id == result.SaleId);
        var item = persistedSale.Items.Single();

        Assert.Equal(50000m, item.Discount);
        Assert.Equal(5m, item.DiscountPercentage);
    }

    [Fact]
    public async Task Handle_WhenItemDiscountPercentageExceedsUsersRoleCap_ThrowsDomainException()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create("SKU-ITEM-PCT-CAP", "iPhone", 1000000m, 700000m, 0, category.Id, unit.Id);
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
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId, MaxDiscountPercentage = 10m },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        // 20% en un producto puntual -> supera el tope del 10% igual que si se
        // hubiera dado como monto fijo equivalente.
        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 1, DiscountPercentage: 20m) },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 800000m, 800000m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null);

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("tope permitido", exception.Message);
        Assert.Empty(dbContext.Sales);
    }

    // Costo snapshoteado al momento de la venta (Product.Cost tal como estaba
    // entonces), necesario para el reporte de ganancia — no debe cambiar
    // retroactivamente si el costo del producto se actualiza después.
    [Fact]
    public async Task Handle_PersistsProductCostAsUnitCostSnapshotOnSaleItem()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create("SKU-COST", "iPhone", 1000000m, 700000m, 0, category.Id, unit.Id);
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
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 2) },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 2000000m, 2000000m) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null);

        var result = await handler.Handle(command, CancellationToken.None);

        var persistedSale = await dbContext.Sales.Include(s => s.Items).SingleAsync(s => s.Id == result.SaleId);
        Assert.Equal(700000m, persistedSale.Items.Single().UnitCost);
    }
}
