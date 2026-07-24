using System.Reflection;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Exceptions;

namespace Axon.Domain.Tests.Inventory;

public class ProductTaxTests
{
    private static Product CreateTestProduct()
    {
        return Product.Create(
            sku: "SKU-001",
            name: "Producto de prueba",
            price: 1000m,
            cost: 500m,
            minStock: 0,
            categoryId: Guid.NewGuid(),
            unitId: Guid.NewGuid());
    }

    // Prueba estructural de que el whitelist {0,5,19} fue eliminado por completo:
    // Product ya ni siquiera tiene una propiedad TaxPercentage. Con el código
    // viejo, GetProperty devolvía una PropertyInfo no nula; con el modelo nuevo
    // no existe en absoluto.
    [Fact]
    public void Product_NoLongerHasTaxPercentageProperty()
    {
        var property = typeof(Product).GetProperty("TaxPercentage");

        Assert.Null(property);
    }

    // Con el whitelist viejo, Product.Create/UpdateTaxPercentage lanzaba
    // DomainException para cualquier porcentaje distinto de 0, 5 o 19. Este test
    // fallaría en rojo contra ese código (7.35% habría sido rechazado) y pasa en
    // verde con el modelo flexible: el porcentaje es libre, lo define el usuario.
    [Fact]
    public void SetTaxes_AllowsArbitraryPercentageNotInOldWhitelist()
    {
        var product = CreateTestProduct();
        var taxTypeId = Guid.NewGuid();

        product.SetTaxes(new[] { (taxTypeId, 7.35m) });

        var tax = Assert.Single(product.Taxes);
        Assert.Equal(7.35m, tax.Percentage);
    }

    [Fact]
    public void SetTaxes_WithZeroTaxes_ResultsInEmptyTaxesList()
    {
        var product = CreateTestProduct();

        product.SetTaxes(Array.Empty<(Guid, decimal)>());

        Assert.Empty(product.Taxes);
    }

    [Fact]
    public void SetTaxes_WithOneTax_Succeeds()
    {
        var product = CreateTestProduct();
        var ivaId = Guid.NewGuid();

        product.SetTaxes(new[] { (ivaId, 19m) });

        var tax = Assert.Single(product.Taxes);
        Assert.Equal(ivaId, tax.TaxTypeId);
        Assert.Equal(19m, tax.Percentage);
        Assert.Equal(product.Id, tax.ProductId);
    }

    // Caso de negocio explícito del prompt: IVA 19% + ICA 0.7% simultáneos.
    [Fact]
    public void SetTaxes_WithMultipleSimultaneousTaxes_Succeeds()
    {
        var product = CreateTestProduct();
        var ivaId = Guid.NewGuid();
        var icaId = Guid.NewGuid();

        product.SetTaxes(new[] { (ivaId, 19m), (icaId, 0.7m) });

        Assert.Equal(2, product.Taxes.Count);
        Assert.Contains(product.Taxes, t => t.TaxTypeId == ivaId && t.Percentage == 19m);
        Assert.Contains(product.Taxes, t => t.TaxTypeId == icaId && t.Percentage == 0.7m);
    }

    [Fact]
    public void SetTaxes_ReplacesPreviousTaxes()
    {
        var product = CreateTestProduct();
        var ivaId = Guid.NewGuid();
        var icaId = Guid.NewGuid();

        product.SetTaxes(new[] { (ivaId, 19m) });
        product.SetTaxes(new[] { (icaId, 0.7m) });

        var tax = Assert.Single(product.Taxes);
        Assert.Equal(icaId, tax.TaxTypeId);
    }

    // Decisión de diseño documentada: no se permite asignar el mismo TaxType dos
    // veces al mismo producto (sería ambiguo: ¿cuál de los dos porcentajes aplica?).
    [Fact]
    public void SetTaxes_WithDuplicateTaxTypeId_ThrowsDomainException()
    {
        var product = CreateTestProduct();
        var ivaId = Guid.NewGuid();

        Assert.Throws<DomainException>(() =>
            product.SetTaxes(new[] { (ivaId, 19m), (ivaId, 5m) }));
    }

    [Fact]
    public void SetTaxes_WithNegativePercentage_ThrowsDomainException()
    {
        var product = CreateTestProduct();

        Assert.Throws<DomainException>(() =>
            product.SetTaxes(new[] { (Guid.NewGuid(), -1m) }));
    }
}
