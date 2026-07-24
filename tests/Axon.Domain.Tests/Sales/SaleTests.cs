using Axon.Domain.Entities.Sales;

namespace Axon.Domain.Tests.Sales;

public class SaleTests
{
    // Bug 1 reportado: "el total de venta no suma correctamente". Se revisó
    // exhaustivamente Sale.AddItem, SaleItem.Create, ProcessSaleCommandHandler,
    // GetSalesHistoryQueryHandler y PdfService.ComposeTotals (todo el camino desde
    // que se calcula el total hasta que se persiste, se lista y se imprime en el
    // recibo). En los cuatro puntos el total es siempre `Sum(items.Subtotal)` sobre
    // valores `decimal`, sin redondeos intermedios ni recomputaciones divergentes.
    // No fue posible reproducir el defecto descrito; este test documenta el
    // comportamiento correcto verificado (pasa en verde con el código actual, sin
    // ningún cambio de por medio) y sirve de regresión si algo lo rompe a futuro.
    [Fact]
    public void AddItem_WithMultipleLinesAndDiscounts_TotalEqualsSumOfSubtotals()
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());

        var item1 = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto A", "SKU-A", unitPrice: 15990.50m, quantity: 3, discount: 1000m);
        var item2 = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto B", "SKU-B", unitPrice: 4999.99m, quantity: 7);
        var item3 = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto C", "SKU-C", unitPrice: 120000m, quantity: 1, discount: 15000m);

        sale.AddItem(item1);
        sale.AddItem(item2);
        sale.AddItem(item3);

        var expectedTotal = item1.Subtotal + item2.Subtotal + item3.Subtotal;

        Assert.Equal(186971.43m, expectedTotal);
        Assert.Equal(expectedTotal, sale.Total);
    }

    [Fact]
    public void AddItem_WithDuplicateProductAcrossTwoLines_TotalStillEqualsSumOfSubtotals()
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var productId = Guid.NewGuid();

        var item1 = SaleItem.Create(sale.Id, productId, "Producto A", "SKU-A", unitPrice: 1000m, quantity: 3);
        var item2 = SaleItem.Create(sale.Id, productId, "Producto A", "SKU-A", unitPrice: 1000m, quantity: 3);

        sale.AddItem(item1);
        sale.AddItem(item2);

        Assert.Equal(6000m, sale.Total);
    }
}
