using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;

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

    // Reportado por negocio: una venta sin nombre de cliente debe quedar como
    // "Consumidor Final" (en vez de un campo en blanco), pero el documento
    // (CC/NIT) no tiene default — si no se da, queda vacío a propósito.
    [Fact]
    public void Create_WithoutCustomerNameOrDocument_DefaultsNameButLeavesDocumentEmpty()
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal("Consumidor Final", sale.CustomerName);
        Assert.Equal(string.Empty, sale.CustomerDocumentNumber);
        Assert.Null(sale.CustomerDocumentType);
    }

    [Fact]
    public void Create_WithBlankCustomerNameAndDocument_DefaultsNameButLeavesDocumentEmpty()
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid(), customerName: "   ");

        Assert.Equal("Consumidor Final", sale.CustomerName);
        Assert.Equal(string.Empty, sale.CustomerDocumentNumber);
    }

    [Fact]
    public void Create_WithRealCustomerNameAndDocument_KeepsThemAsGiven()
    {
        var sale = Sale.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            customerName: "Juan Pérez",
            customerDocumentType: CustomerDocumentType.Cc,
            customerDocumentNumber: "1002003004");

        Assert.Equal("Juan Pérez", sale.CustomerName);
        Assert.Equal(CustomerDocumentType.Cc, sale.CustomerDocumentType);
        Assert.Equal("1002003004", sale.CustomerDocumentNumber);
    }

    // Un cliente puede dar su nombre pero no querer dar el documento (o
    // viceversa) — cada uno se resuelve de forma independiente.
    [Fact]
    public void Create_WithCustomerNameButNoDocument_KeepsNameAndLeavesDocumentEmpty()
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid(), customerName: "Juan Pérez");

        Assert.Equal("Juan Pérez", sale.CustomerName);
        Assert.Equal(string.Empty, sale.CustomerDocumentNumber);
    }

    // Descuento general de la venta: por defecto en 0 (venta sin descuento
    // general, solo con descuentos manuales por producto si los hubiera).
    [Fact]
    public void Create_WithoutGeneralDiscount_DefaultsToZero()
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(0m, sale.GeneralDiscountAmount);
    }

    [Fact]
    public void SetGeneralDiscountAmount_WithPositiveAmount_SetsIt()
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());

        sale.SetGeneralDiscountAmount(5000m);

        Assert.Equal(5000m, sale.GeneralDiscountAmount);
        Assert.Null(sale.GeneralDiscountPercentage);
    }

    // Cuando el descuento general se dio como % (no como monto fijo), se
    // guarda el % original además del monto ya convertido — para que la
    // factura pueda mostrar ambos ("Descuento general (10%): -$X").
    [Fact]
    public void SetGeneralDiscountAmount_WithPercentage_SetsBothAmountAndPercentage()
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());

        sale.SetGeneralDiscountAmount(5000m, percentage: 10m);

        Assert.Equal(5000m, sale.GeneralDiscountAmount);
        Assert.Equal(10m, sale.GeneralDiscountPercentage);
    }

    [Fact]
    public void SetGeneralDiscountAmount_WithPercentageOver100_Throws()
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<DomainException>(() => sale.SetGeneralDiscountAmount(5000m, percentage: 101m));
    }

    [Fact]
    public void SetGeneralDiscountAmount_WithNegativeAmount_Throws()
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<DomainException>(() => sale.SetGeneralDiscountAmount(-1m));
    }
}
