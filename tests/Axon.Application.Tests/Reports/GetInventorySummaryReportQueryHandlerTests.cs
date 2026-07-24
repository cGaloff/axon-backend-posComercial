using Axon.Application.Reports.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;

namespace Axon.Application.Tests.Reports;

public class GetInventorySummaryReportQueryHandlerTests
{
    // Bug 5 (informe de inventario, sección "acciones" vacía): StockAlert se crea en
    // ProcessSaleCommandHandler y AdjustStockCommandHandler cuando el stock cae por
    // debajo del mínimo, pero ningún query lo exponía: InventorySummaryReportDto no
    // tenía ningún campo para alertas/movimientos pendientes. Este test no puede
    // "fallar en rojo" contra el DTO anterior en el sentido estricto (el campo
    // PendingStockAlerts no existía, así que el código previo directamente no
    // compilaría con este test) — la reproducción en rojo real es la ausencia total
    // del campo; ahora se verifica que el handler devuelve las alertas no leídas.
    [Fact]
    public async Task Handle_ReturnsOnlyUnreadStockAlerts()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);

        var lowStockProduct = Product.Create("SKU-LOW", "Producto con stock bajo", 1000m, 500m, minStock: 5, categoryId: category.Id, unitId: unit.Id);
        var okProduct = Product.Create("SKU-OK", "Producto con stock normal", 1000m, 500m, minStock: 5, categoryId: category.Id, unitId: unit.Id);

        var unreadAlert = StockAlert.Create(lowStockProduct.Id, warehouse.Id, currentStock: 2, minStock: 5);
        var readAlert = StockAlert.Create(okProduct.Id, warehouse.Id, currentStock: 1, minStock: 5);
        MarkAsRead(readAlert);

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Warehouses.Add(warehouse);
        dbContext.Products.AddRange(lowStockProduct, okProduct);
        dbContext.StockAlerts.AddRange(unreadAlert, readAlert);
        await dbContext.SaveChangesAsync();

        var handler = new GetInventorySummaryReportQueryHandler(dbContext);

        var result = await handler.Handle(new GetInventorySummaryReportQuery(), CancellationToken.None);

        var alert = Assert.Single(result.PendingStockAlerts);
        Assert.Equal(lowStockProduct.Id, alert.ProductId);
        Assert.Equal(lowStockProduct.Name, alert.ProductName);
        Assert.Equal(2, alert.CurrentStock);
    }

    private static void MarkAsRead(StockAlert alert)
    {
        // StockAlert no expone ningún método de dominio para marcarse como leída
        // (deuda técnica ya identificada en el diagnóstico); se usa reflexión solo
        // para preparar el escenario de prueba, no como solución al bug.
        typeof(StockAlert).GetProperty(nameof(StockAlert.IsRead))!.SetValue(alert, true);
    }
}
