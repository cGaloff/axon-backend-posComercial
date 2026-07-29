using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;

namespace Axon.Domain.Tests.Sales;

public class SaleItemDiscountPercentageAndCostTests
{
    [Fact]
    public void Create_WithoutDiscountPercentageOrCost_DefaultsToNullAndZero()
    {
        var item = SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Producto", "SKU-001",
            unitPrice: 100m, quantity: 2);

        Assert.Null(item.DiscountPercentage);
        Assert.Equal(0m, item.UnitCost);
    }

    // El % se guarda tal cual solo para mostrarlo en la factura ("Desc: (5%)")
    // — el monto ya convertido (discount) es el que realmente resta del
    // subtotal, no un recálculo a partir del %.
    [Fact]
    public void Create_WithDiscountPercentage_StoresItAlongsideTheConvertedAmount()
    {
        var item = SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "iPhone", "SKU-IPHONE",
            unitPrice: 1000000m, quantity: 1,
            discount: 50000m,
            discountPercentage: 5m);

        Assert.Equal(50000m, item.Discount);
        Assert.Equal(5m, item.DiscountPercentage);
        Assert.Equal(950000m, item.Subtotal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_WithDiscountPercentageOutOfRange_Throws(decimal percentage)
    {
        Assert.Throws<DomainException>(() => SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Producto", "SKU-001",
            unitPrice: 100m, quantity: 1,
            discountPercentage: percentage));
    }

    // Costo snapshoteado al momento de la venta (no el costo actual del
    // producto) — necesario para que el reporte de ganancia no cambie
    // retroactivamente si el costo promedio ponderado del producto cambia
    // después por una compra nueva.
    [Fact]
    public void Create_WithUnitCost_StoresItIndependentlyOfPriceAndDiscount()
    {
        var item = SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "iPhone", "SKU-IPHONE",
            unitPrice: 1000000m, quantity: 2,
            discount: 50000m,
            unitCost: 700000m);

        Assert.Equal(700000m, item.UnitCost);
        Assert.Equal(1950000m, item.Subtotal);
    }

    [Fact]
    public void Create_WithNegativeUnitCost_Throws()
    {
        Assert.Throws<DomainException>(() => SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Producto", "SKU-001",
            unitPrice: 100m, quantity: 1,
            unitCost: -1m));
    }
}
