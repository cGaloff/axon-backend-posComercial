using Axon.Application.Inventory.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Exceptions;
using TenantConfigEntity = Axon.Domain.Entities.TenantConfig;

namespace Axon.Application.Tests.Inventory;

// Diagnóstico del reporte "agregar stock lanza una excepción": AdjustStockCommandHandler
// exige que exista una bodega marcada IsDefault=true en el tenant. Si el tenant no tiene
// ninguna (p. ej. provisionado antes de que el seed incluyera la bodega por defecto, o
// provisionado por fuera del flujo normal), CUALQUIER intento de ajustar stock falla con
// DomainException, sin importar qué tan bien esté configurado el producto.
public class AdjustStockCommandHandlerTests
{
    private static async Task<(AdjustStockCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, Product Product)> ArrangeAsync(
        bool withDefaultWarehouse, decimal mermaApprovalThreshold = 0m)
    {
        var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var product = Product.Create("SKU-STOCK-TEST", "Producto de prueba", 1000m, 500m, minStock: 5, category.Id, unit.Id);

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Products.Add(product);

        if (withDefaultWarehouse)
        {
            dbContext.Warehouses.Add(Warehouse.Create("Tienda Principal", "Bodega principal", isDefault: true));
        }

        await dbContext.SaveChangesAsync();

        var config = TenantConfigEntity.Create("Negocio de prueba");
        config.SetMermaApprovalThreshold(mermaApprovalThreshold);

        var handler = new AdjustStockCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakeCurrentUserContext(), new FakeTenantConfigRepository(config));

        return (handler, dbContext, product);
    }

    [Fact]
    public async Task Handle_WithoutADefaultWarehouse_ThrowsDomainException()
    {
        var (handler, _, product) = await ArrangeAsync(withDefaultWarehouse: false);

        var command = new AdjustStockCommand(product.Id, 50, InventoryMovementType.InitialStock, "Carga inicial");

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("bodega por defecto", ex.Message);
    }

    [Fact]
    public async Task Handle_WithADefaultWarehouse_IncreasesProductStock()
    {
        var (handler, dbContext, product) = await ArrangeAsync(withDefaultWarehouse: true);

        var command = new AdjustStockCommand(product.Id, 50, InventoryMovementType.InitialStock, "Carga inicial");

        await handler.Handle(command, CancellationToken.None);

        var updated = await dbContext.Products.FindAsync(product.Id);
        Assert.Equal(50, updated!.Stock);
    }

    // Regla transversal B (Matriz de Roles y Permisos v2): una merma cuyo valor
    // (cantidad x costo) supera el umbral configurado NO se aplica de inmediato —
    // queda pendiente de aprobación, y el stock no se toca todavía.
    [Fact]
    public async Task Handle_LossAboveThreshold_LeavesStockUntouchedAndCreatesPendingMovement()
    {
        // Costo 500 x cantidad 200 = 100.000, por encima del umbral de 50.000.
        var (handler, dbContext, product) = await ArrangeAsync(withDefaultWarehouse: true, mermaApprovalThreshold: 50000m);
        product.AdjustStock(200);
        await dbContext.SaveChangesAsync();

        var command = new AdjustStockCommand(product.Id, -200, InventoryMovementType.Loss, "Producto dañado en bodega");

        await handler.Handle(command, CancellationToken.None);

        var updatedProduct = await dbContext.Products.FindAsync(product.Id);
        Assert.Equal(200, updatedProduct!.Stock);

        var movement = Assert.Single(dbContext.InventoryMovements);
        Assert.Equal(InventoryMovementStatus.PendingApproval, movement.Status);
        Assert.Equal(200, movement.StockAfter);
    }

    [Fact]
    public async Task Handle_LossBelowThreshold_AppliesImmediately()
    {
        // Costo 500 x cantidad 10 = 5.000, por debajo del umbral de 50.000.
        var (handler, dbContext, product) = await ArrangeAsync(withDefaultWarehouse: true, mermaApprovalThreshold: 50000m);
        product.AdjustStock(200);
        await dbContext.SaveChangesAsync();

        var command = new AdjustStockCommand(product.Id, -10, InventoryMovementType.Loss, "Merma menor");

        await handler.Handle(command, CancellationToken.None);

        var updatedProduct = await dbContext.Products.FindAsync(product.Id);
        Assert.Equal(190, updatedProduct!.Stock);

        var movement = Assert.Single(dbContext.InventoryMovements);
        Assert.Equal(InventoryMovementStatus.Applied, movement.Status);
    }

    // Default seguro: sin configurar el umbral explícitamente (queda en 0), TODA
    // merma requiere aprobación — nunca se interpreta como "sin límite".
    [Fact]
    public async Task Handle_LossWithDefaultZeroThreshold_AlwaysRequiresApproval()
    {
        var (handler, dbContext, product) = await ArrangeAsync(withDefaultWarehouse: true, mermaApprovalThreshold: 0m);
        product.AdjustStock(200);
        await dbContext.SaveChangesAsync();

        var command = new AdjustStockCommand(product.Id, -1, InventoryMovementType.Loss, "Merma mínima");

        await handler.Handle(command, CancellationToken.None);

        var updatedProduct = await dbContext.Products.FindAsync(product.Id);
        Assert.Equal(200, updatedProduct!.Stock);

        var movement = Assert.Single(dbContext.InventoryMovements);
        Assert.Equal(InventoryMovementStatus.PendingApproval, movement.Status);
    }
}
