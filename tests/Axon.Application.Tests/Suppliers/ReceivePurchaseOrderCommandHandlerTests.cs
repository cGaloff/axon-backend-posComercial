using Axon.Application.Suppliers.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Suppliers;

namespace Axon.Application.Tests.Suppliers;

public class ReceivePurchaseOrderCommandHandlerTests
{
    // Cobertura directa del handler nombrado en el bug 2 ("el handler de recibir
    // orden/compra"), con cantidades/costos de 3+ dígitos. Verifica tanto
    // PurchaseReceipt.TotalReceived como el costo promedio ponderado (CPP)
    // recalculado sobre el producto. Ver PurchaseOrderTests/PurchaseReceiptTests
    // para la verificación a nivel de dominio; este test confirma que el propio
    // handler no introduce ningún error de redondeo o truncamiento adicional.
    [Fact]
    public async Task Handle_WithThreeDigitQuantityAndCost_ComputesExactTotalAndWeightedAverageCost()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var supplier = Supplier.Create(
            "Proveedor de prueba", SupplierDocumentType.NIT, "900123456",
            "Juan Perez", "3001234567", "juan@proveedor.com");

        // Stock inicial 100 a costo 50.00; se reciben 120 unidades a costo 999.99.
        var product = Product.Create("SKU-001", "Producto de prueba", price: 2000m, cost: 50.00m, minStock: 0, categoryId: category.Id, unitId: unit.Id);
        product.AdjustStock(100);

        var order = PurchaseOrder.Create(supplier.Id, Guid.NewGuid(), supplier.DocumentType);
        var orderItem = PurchaseOrderItem.Create(order.Id, product.Id, product.Name, product.Sku, quantityOrdered: 120, unitCost: 999.99m);
        order.AddItem(orderItem);

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Warehouses.Add(warehouse);
        dbContext.Suppliers.Add(supplier);
        dbContext.Products.Add(product);
        dbContext.PurchaseOrders.Add(order);
        await dbContext.SaveChangesAsync();

        var handler = new ReceivePurchaseOrderCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCurrentUserContext());

        var command = new ReceivePurchaseOrderCommand(
            order.Id,
            new List<ReceiptItemRequest> { new(orderItem.Id, QuantityReceived: 120) },
            Notes: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(119998.80m, result.TotalReceived);

        var reloadedProduct = await dbContext.Products.FindAsync(product.Id);
        Assert.Equal(220, reloadedProduct!.Stock);

        // CPP = (100*50.00 + 120*999.99) / 220 = (5000 + 119998.80) / 220
        var expectedAverageCost = (100 * 50.00m + 120 * 999.99m) / 220;
        Assert.Equal(expectedAverageCost, reloadedProduct.Cost);
    }
}
