using Axon.Domain.Entities.Suppliers;

namespace Axon.Domain.Tests.Suppliers;

public class PurchaseOrderTests
{
    // Bug 2 reportado: "el total de compra no suma correctamente con más de 2
    // dígitos". Se revisó exhaustivamente PurchaseOrder.TotalOrdered,
    // PurchaseOrderItem.Create/Subtotal, PurchaseReceipt.AddItem/TotalReceived,
    // ReceivePurchaseOrderCommandHandler (incluido el cálculo de costo promedio
    // ponderado) y las columnas decimal(12,2) tanto en las Configurations de EF
    // Core como en tenant_schema_template.sql. Todos los montos son `decimal` de
    // extremo a extremo y decimal(12,2) admite hasta 10 dígitos enteros, muy por
    // encima de cualquier cantidad/costo realista. No fue posible reproducir el
    // defecto descrito con cantidades o costos de 3+ dígitos. Este test documenta
    // el comportamiento correcto verificado (pasa en verde con el código actual).
    [Fact]
    public void AddItem_WithQuantitiesAndCostsOfThreeOrMoreDigits_TotalOrderedSumsExactly()
    {
        var order = PurchaseOrder.Create(Guid.NewGuid(), Guid.NewGuid(), SupplierDocumentType.NIT);

        // Cantidades y costos con más de 2 dígitos (120 unidades, $999.99, etc.).
        var item1 = PurchaseOrderItem.Create(order.Id, Guid.NewGuid(), "Producto A", "SKU-A", quantityOrdered: 120, unitCost: 999.99m);
        var item2 = PurchaseOrderItem.Create(order.Id, Guid.NewGuid(), "Producto B", "SKU-B", quantityOrdered: 350, unitCost: 45.75m);

        order.AddItem(item1);
        order.AddItem(item2);

        var expectedTotal = (120 * 999.99m) + (350 * 45.75m);

        Assert.Equal(136011.30m, expectedTotal);
        Assert.Equal(expectedTotal, order.TotalOrdered);
    }
}
