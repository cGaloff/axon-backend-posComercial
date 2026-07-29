using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;

namespace Axon.Domain.Tests.Sales;

public class SaleItemGeneralDiscountTests
{
    [Fact]
    public void Create_WithoutGeneralDiscountShare_DefaultsToZeroAndDoesNotAffectSubtotal()
    {
        var item = SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Producto", "SKU-001",
            unitPrice: 100m, quantity: 2);

        Assert.Equal(0m, item.GeneralDiscountShare);
        Assert.Equal(200m, item.Subtotal);
    }

    // La porción del descuento general se resta del subtotal ADEMÁS del
    // descuento manual por producto (Discount) — ambos reducen el mismo
    // subtotal, pero se guardan por separado para que la factura los muestre
    // como conceptos distintos (ver PdfService.ComposeTotals).
    [Fact]
    public void Create_WithManualDiscountAndGeneralDiscountShare_BothReduceSubtotal()
    {
        var item = SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Producto", "SKU-001",
            unitPrice: 1000m, quantity: 1,
            discount: 100m,
            generalDiscountShare: 50m);

        Assert.Equal(100m, item.Discount);
        Assert.Equal(50m, item.GeneralDiscountShare);
        Assert.Equal(850m, item.Subtotal);
    }

    [Fact]
    public void Create_WithGeneralDiscountShareAndTax_RecomputesTaxOverReducedBase()
    {
        var ivaId = Guid.NewGuid();

        var item = SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Producto", "SKU-001",
            unitPrice: 238m, quantity: 1,
            generalDiscountShare: 119m,
            appliedTaxes: new[] { (ivaId, "IVA", 19m) });

        // Subtotal pasa de 238 a 119 (mitad); la base gravable y el impuesto
        // se recalculan sobre ese subtotal reducido, no sobre el original.
        Assert.Equal(119m, item.Subtotal);
        Assert.Equal(100m, item.SubtotalBase);
        Assert.Equal(19m, item.TotalTaxAmount);
    }

    [Fact]
    public void Create_WithNegativeGeneralDiscountShare_Throws()
    {
        Assert.Throws<DomainException>(() => SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Producto", "SKU-001",
            unitPrice: 100m, quantity: 1,
            generalDiscountShare: -1m));
    }

    [Fact]
    public void Create_WithManualAndGeneralDiscountTogetherExceedingSubtotal_Throws()
    {
        Assert.Throws<DomainException>(() => SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Producto", "SKU-001",
            unitPrice: 100m, quantity: 1,
            discount: 60m,
            generalDiscountShare: 40m));
    }
}
