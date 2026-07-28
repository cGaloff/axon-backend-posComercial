using Axon.Application.Inventory.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Inventory;

// Deuda técnica reportada: el frontend permitía capturar una cantidad inicial
// al crear el producto, pero el backend la ignoraba (Product.Create siempre
// arrancaba en Stock=0), obligando a crear el producto y luego hacer un
// ajuste de stock aparte.
public class CreateProductCommandHandlerInitialStockTests
{
    private static async Task<(CreateProductCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, Category Category, Unit Unit)> ArrangeAsync(bool withDefaultWarehouse)
    {
        var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);

        if (withDefaultWarehouse)
        {
            dbContext.Warehouses.Add(Warehouse.Create("Tienda Principal", "Bodega principal", isDefault: true));
        }

        await dbContext.SaveChangesAsync();

        var handler = new CreateProductCommandHandler(dbContext, new FakeUnitOfWork(dbContext), new FakeCurrentUserContext());

        return (handler, dbContext, category, unit);
    }

    [Fact]
    public async Task Handle_WithInitialStock_SetsProductStockAndRecordsMovement()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync(withDefaultWarehouse: true);

        var command = new CreateProductCommand(
            "SKU-INITSTOCK", "Producto con stock inicial", "", 1000m, 500m, MinStock: 5,
            category.Id, unit.Id, Attributes: null, Taxes: null, InitialStock: 50);

        var productId = await handler.Handle(command, CancellationToken.None);

        var product = await dbContext.Products.FindAsync(productId);
        Assert.Equal(50, product!.Stock);

        var movement = Assert.Single(dbContext.InventoryMovements);
        Assert.Equal(InventoryMovementType.InitialStock, movement.Type);
        Assert.Equal(50, movement.Quantity);
        Assert.Equal(0, movement.StockBefore);
        Assert.Equal(50, movement.StockAfter);
    }

    [Fact]
    public async Task Handle_WithoutInitialStock_LeavesProductAtZeroWithoutRequiringWarehouse()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync(withDefaultWarehouse: false);

        var command = new CreateProductCommand(
            "SKU-NOSTOCK", "Producto sin stock inicial", "", 1000m, 500m, 0,
            category.Id, unit.Id, Attributes: null, Taxes: null);

        var productId = await handler.Handle(command, CancellationToken.None);

        var product = await dbContext.Products.FindAsync(productId);
        Assert.Equal(0, product!.Stock);
        Assert.Empty(dbContext.InventoryMovements);
    }

    [Fact]
    public async Task Handle_WithInitialStockButNoDefaultWarehouse_ThrowsDomainException()
    {
        var (handler, _, category, unit) = await ArrangeAsync(withDefaultWarehouse: false);

        var command = new CreateProductCommand(
            "SKU-INITSTOCK-NOWH", "Producto con stock inicial", "", 1000m, 500m, 0,
            category.Id, unit.Id, Attributes: null, Taxes: null, InitialStock: 10);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("bodega por defecto", ex.Message);
    }

    [Fact]
    public async Task Handle_WithInitialStockAtOrBelowMinStock_CreatesStockAlert()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync(withDefaultWarehouse: true);

        var command = new CreateProductCommand(
            "SKU-LOWSTOCK", "Producto con stock bajo", "", 1000m, 500m, MinStock: 20,
            category.Id, unit.Id, Attributes: null, Taxes: null, InitialStock: 5);

        await handler.Handle(command, CancellationToken.None);

        Assert.Single(dbContext.StockAlerts);
    }
}
