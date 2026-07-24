using Axon.Domain.Entities.Suppliers;

namespace Axon.Domain.Tests.Suppliers;

public class PurchaseReceiptTests
{
    // Ver PurchaseOrderTests para el contexto completo del bug 2 reportado.
    // PurchaseReceipt.TotalReceived usa el mismo patrón (acumulación exacta con
    // `decimal`) y se verifica aquí con las mismas cantidades/costos de 3+ dígitos.
    [Fact]
    public void AddItem_WithQuantitiesAndCostsOfThreeOrMoreDigits_TotalReceivedSumsExactly()
    {
        var receipt = PurchaseReceipt.Create(Guid.NewGuid(), Guid.NewGuid());

        var item1 = PurchaseReceiptItem.Create(receipt.Id, Guid.NewGuid(), Guid.NewGuid(), "Producto A", quantityReceived: 120, unitCost: 999.99m);
        var item2 = PurchaseReceiptItem.Create(receipt.Id, Guid.NewGuid(), Guid.NewGuid(), "Producto B", quantityReceived: 350, unitCost: 45.75m);

        receipt.AddItem(item1);
        receipt.AddItem(item2);

        Assert.Equal(136011.30m, receipt.TotalReceived);
    }
}
