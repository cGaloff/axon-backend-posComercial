using Axon.Application.Suppliers.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Suppliers;
using Axon.Domain.Entities.Taxes;

namespace Axon.Application.Tests.Suppliers;

public class CreatePurchaseOrderCommandHandlerTests
{
    // Compra con impuestos múltiples (IVA 19% + ICA 0.7%) aplicados a un
    // producto vía su ProductTax vigente, con cantidades/costos de 3+ dígitos.
    // Revalida el fix de suma del Prompt 2 ahora que TotalOrdered = Subtotal +
    // TaxAmount por línea (antes solo era Subtotal): el escenario cambió lo
    // suficiente como para necesitar este test nuevo.
    [Fact]
    public async Task Handle_WithMultipleTaxesAndLargeAmounts_ComputesSubtotalTaxAndTotalCorrectly()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var supplier = Supplier.Create(
            "Distribuidora ACME", SupplierDocumentType.NIT, "900123456-7",
            "Juan Perez", "3001234567", "juan@acme.com");

        var iva = TaxType.Create("IVA", "IVA");
        var ica = TaxType.Create("ICA", "ICA");

        // 120 unidades a $999.99 (más de 2 dígitos en cantidad y costo).
        var product = Product.Create("SKU-001", "Producto con impuestos", price: 2000m, cost: 500m, minStock: 0, categoryId: category.Id, unitId: unit.Id);
        product.SetTaxes(new[] { (iva.Id, 19m), (ica.Id, 0.7m) });

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Suppliers.Add(supplier);
        dbContext.TaxTypes.AddRange(iva, ica);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var handler = new CreatePurchaseOrderCommandHandler(dbContext, new FakeUnitOfWork(dbContext), new FakeCurrentUserContext());

        var command = new CreatePurchaseOrderCommand(
            supplier.Id,
            new List<PurchaseOrderItemRequest> { new(product.Id, QuantityOrdered: 120, UnitCost: 999.99m) },
            SupplierInvoiceNumber: "FAC-001",
            SupplierInvoiceDate: DateTime.UtcNow,
            ExpectedDate: null,
            Notes: null);

        var orderId = await handler.Handle(command, CancellationToken.None);

        var order = await dbContext.PurchaseOrders.FindAsync(orderId);
        var item = Assert.Single(order!.Items);

        var expectedSubtotal = 120 * 999.99m; // 119998.80
        var expectedIvaAmount = expectedSubtotal * 19m / 100;
        var expectedIcaAmount = expectedSubtotal * 0.7m / 100;
        var expectedTaxAmount = expectedIvaAmount + expectedIcaAmount;
        var expectedTotal = expectedSubtotal + expectedTaxAmount;

        Assert.Equal(119998.80m, expectedSubtotal);
        Assert.Equal(expectedSubtotal, item.Subtotal);
        Assert.Equal(expectedTaxAmount, item.TaxAmount);
        Assert.Equal(expectedTotal, item.Total);
        Assert.Equal(expectedTotal, order.TotalOrdered);

        Assert.Equal(2, item.Taxes.Count);
        Assert.Contains(item.Taxes, t => t.TaxTypeName == "IVA" && t.Percentage == 19m);
        Assert.Contains(item.Taxes, t => t.TaxTypeName == "ICA" && t.Percentage == 0.7m);
    }

    [Fact]
    public async Task Handle_WithProductWithoutTaxes_TotalOrderedEqualsSubtotal()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var supplier = Supplier.Create(
            "Distribuidora ACME", SupplierDocumentType.NIT, "900123456-7",
            "Juan Perez", "3001234567", "juan@acme.com");

        var product = Product.Create("SKU-002", "Producto sin impuestos", price: 1000m, cost: 500m, minStock: 0, categoryId: category.Id, unitId: unit.Id);

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Suppliers.Add(supplier);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var handler = new CreatePurchaseOrderCommandHandler(dbContext, new FakeUnitOfWork(dbContext), new FakeCurrentUserContext());

        var command = new CreatePurchaseOrderCommand(
            supplier.Id,
            new List<PurchaseOrderItemRequest> { new(product.Id, QuantityOrdered: 10, UnitCost: 100m) },
            SupplierInvoiceNumber: null,
            SupplierInvoiceDate: null,
            ExpectedDate: null,
            Notes: null);

        var orderId = await handler.Handle(command, CancellationToken.None);

        var order = await dbContext.PurchaseOrders.FindAsync(orderId);

        Assert.Equal(1000m, order!.TotalOrdered);
        Assert.Equal(SupplierDocumentType.NIT, order.SupplierDocumentTypeAtPurchase);
    }
}
