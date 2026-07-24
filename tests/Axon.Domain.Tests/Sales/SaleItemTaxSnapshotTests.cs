using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Sales;

namespace Axon.Domain.Tests.Sales;

public class SaleItemTaxSnapshotTests
{
    [Fact]
    public void Create_WithZeroTaxes_SubtotalBaseEqualsSubtotalAndNoTaxAmount()
    {
        var item = SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Producto", "SKU-001",
            unitPrice: 100m, quantity: 2);

        Assert.Empty(item.Taxes);
        Assert.Equal(200m, item.Subtotal);
        Assert.Equal(200m, item.SubtotalBase);
        Assert.Equal(0m, item.TotalTaxAmount);
    }

    [Fact]
    public void Create_WithOneTax_ComputesBaseAndAmountCorrectly()
    {
        var ivaId = Guid.NewGuid();

        var item = SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Producto", "SKU-001",
            unitPrice: 119m, quantity: 1,
            appliedTaxes: new[] { (ivaId, "IVA", 19m) });

        Assert.Equal(119m, item.Subtotal);
        Assert.Equal(100m, item.SubtotalBase);

        var tax = Assert.Single(item.Taxes);
        Assert.Equal(ivaId, tax.TaxTypeId);
        Assert.Equal("IVA", tax.TaxTypeName);
        Assert.Equal(19m, tax.Percentage);
        Assert.Equal(19m, tax.Amount);
        Assert.Equal(19m, item.TotalTaxAmount);
    }

    // Caso de negocio explícito del prompt: IVA 19% + ICA simultáneos sobre la
    // misma línea, calculados sobre la misma base gravable (no se componen).
    [Fact]
    public void Create_WithMultipleSimultaneousTaxes_EachAmountSumsToTotalTax()
    {
        var ivaId = Guid.NewGuid();
        var icaId = Guid.NewGuid();

        var item = SaleItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Producto", "SKU-001",
            unitPrice: 120m, quantity: 1,
            appliedTaxes: new[] { (ivaId, "IVA", 19m), (icaId, "ICA", 1m) });

        Assert.Equal(120m, item.Subtotal);
        Assert.Equal(100m, item.SubtotalBase);
        Assert.Equal(2, item.Taxes.Count);

        var iva = item.Taxes.Single(t => t.TaxTypeId == ivaId);
        var ica = item.Taxes.Single(t => t.TaxTypeId == icaId);

        Assert.Equal(19m, iva.Amount);
        Assert.Equal(1m, ica.Amount);
        Assert.Equal(20m, item.TotalTaxAmount);
    }

    // Requisito de auditoría explícito del prompt: la venta debe snapshotear los
    // impuestos aplicados al momento de la venta. Si el producto cambia después
    // (nuevo porcentaje, impuesto distinto, o queda sin impuestos), el SaleItem ya
    // creado no debe alterarse — porque nunca mantuvo una referencia viva al
    // producto, solo copió los valores en el momento de Create().
    [Fact]
    public void Create_SnapshotIsUnaffectedByLaterProductTaxChanges()
    {
        var product = Product.Create("SKU-001", "Producto", 119m, 50m, 0, Guid.NewGuid(), Guid.NewGuid());
        var ivaId = Guid.NewGuid();
        product.SetTaxes(new[] { (ivaId, 19m) });

        var appliedTaxesAtSaleTime = product.Taxes
            .Select(t => (t.TaxTypeId, "IVA", t.Percentage))
            .ToList();

        var item = SaleItem.Create(
            Guid.NewGuid(), product.Id, product.Name, product.Sku,
            unitPrice: product.Price, quantity: 1,
            appliedTaxes: appliedTaxesAtSaleTime);

        // El producto cambia después de la venta: ahora tiene un impuesto distinto
        // con un porcentaje distinto.
        var icaId = Guid.NewGuid();
        product.SetTaxes(new[] { (icaId, 3m) });

        var snapshotTax = Assert.Single(item.Taxes);
        Assert.Equal(ivaId, snapshotTax.TaxTypeId);
        Assert.Equal("IVA", snapshotTax.TaxTypeName);
        Assert.Equal(19m, snapshotTax.Percentage);
        Assert.Equal(19m, snapshotTax.Amount);

        // El producto sí refleja el cambio (no es snapshot, es configuración vigente).
        var currentProductTax = Assert.Single(product.Taxes);
        Assert.Equal(icaId, currentProductTax.TaxTypeId);
        Assert.Equal(3m, currentProductTax.Percentage);
    }
}
