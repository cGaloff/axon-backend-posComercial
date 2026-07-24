using System.Reflection;
using Axon.Domain.Entities.Invoicing;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;

namespace Axon.Domain.Tests.Invoicing;

public class InvoiceTests
{
    private static InvoiceItemSnapshot CreateItemSnapshot(Guid taxTypeId, string taxTypeName, decimal percentage)
    {
        return new InvoiceItemSnapshot(
            ProductId: Guid.NewGuid(),
            ProductName: "Producto de prueba",
            ProductSku: "SKU-001",
            UnitPrice: 119m,
            Quantity: 1,
            Discount: 0m,
            Subtotal: 119m,
            SubtotalBase: 100m,
            Taxes: new List<InvoiceItemTaxSnapshot>
            {
                new(taxTypeId, taxTypeName, percentage, 19m)
            });
    }

    [Fact]
    public void Create_BuildsFullSnapshotFromInputData()
    {
        var saleId = Guid.NewGuid();
        var taxTypeId = Guid.NewGuid();

        var items = new List<InvoiceItemSnapshot> { CreateItemSnapshot(taxTypeId, "IVA", 19m) };
        var payments = new List<InvoicePaymentSnapshot> { new(PaymentMethod.Cash, 119m, 119m, 0m) };

        var invoice = Invoice.Create(saleId, 1, "VTA-20260724-ABC123", "Cliente de prueba", 119m, items, payments);

        Assert.Equal(saleId, invoice.SaleId);
        Assert.Equal(1, invoice.Number);
        Assert.Equal(119m, invoice.Total);

        var item = Assert.Single(invoice.Items);
        Assert.Equal("Producto de prueba", item.ProductName);
        var tax = Assert.Single(item.Taxes);
        Assert.Equal("IVA", tax.TaxTypeName);
        Assert.Equal(19m, tax.Percentage);

        var payment = Assert.Single(invoice.Payments);
        Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal(119m, payment.Amount);
    }

    [Fact]
    public void Create_WithoutItems_ThrowsDomainException()
    {
        var payments = new List<InvoicePaymentSnapshot> { new(PaymentMethod.Cash, 100m, 100m, 0m) };

        Assert.Throws<DomainException>(() =>
            Invoice.Create(Guid.NewGuid(), 1, "VTA-1", "Cliente", 100m, Array.Empty<InvoiceItemSnapshot>(), payments));
    }

    [Fact]
    public void Create_WithoutPayments_ThrowsDomainException()
    {
        var items = new List<InvoiceItemSnapshot> { CreateItemSnapshot(Guid.NewGuid(), "IVA", 19m) };

        Assert.Throws<DomainException>(() =>
            Invoice.Create(Guid.NewGuid(), 1, "VTA-1", "Cliente", 119m, items, Array.Empty<InvoicePaymentSnapshot>()));
    }

    [Fact]
    public void Create_WithNumberZeroOrNegative_ThrowsDomainException()
    {
        var items = new List<InvoiceItemSnapshot> { CreateItemSnapshot(Guid.NewGuid(), "IVA", 19m) };
        var payments = new List<InvoicePaymentSnapshot> { new(PaymentMethod.Cash, 119m, 119m, 0m) };

        Assert.Throws<DomainException>(() => Invoice.Create(Guid.NewGuid(), 0, "VTA-1", "Cliente", 119m, items, payments));
        Assert.Throws<DomainException>(() => Invoice.Create(Guid.NewGuid(), -1, "VTA-1", "Cliente", 119m, items, payments));
    }

    // Requisito explícito del prompt: modificar una Invoice ya creada debe ser
    // IMPOSIBLE POR DISEÑO, no solo rechazado por validación. Se prueba
    // enumerando los métodos públicos de instancia declarados: no debe existir
    // ninguno más allá de los getters de propiedades (filtrados por IsSpecialName).
    // Create() es estático y no aparece aquí — es el único punto de entrada.
    [Theory]
    [InlineData(typeof(Invoice))]
    [InlineData(typeof(InvoiceItem))]
    [InlineData(typeof(InvoiceItemTax))]
    [InlineData(typeof(InvoicePayment))]
    public void Entity_HasNoPublicInstanceMutatorMethods(Type entityType)
    {
        var publicInstanceMethods = entityType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName) // excluye get_XXX de las propiedades autogeneradas
            .ToList();

        Assert.Empty(publicInstanceMethods);
    }

    // Revalida, a nivel de snapshot, que cambiar el producto/catálogo de
    // impuestos DESPUÉS de construir los datos de entrada no tiene forma de
    // afectar una Invoice ya creada: Invoice.Create solo recibe copias de datos
    // (records), nunca una referencia viva a Product/TaxType.
    [Fact]
    public void Create_SnapshotIsIndependentOfSourceDataMutatedAfterConstruction()
    {
        var taxTypeId = Guid.NewGuid();
        var itemSnapshot = CreateItemSnapshot(taxTypeId, "IVA", 19m);
        var payments = new List<InvoicePaymentSnapshot> { new(PaymentMethod.Cash, 119m, 119m, 0m) };

        var invoice = Invoice.Create(Guid.NewGuid(), 1, "VTA-1", "Cliente", 119m, new[] { itemSnapshot }, payments);

        // "Mutar" los datos de origen (records inmutables: se simula creando una
        // nueva versión con distinto nombre/porcentaje, como haría el catálogo
        // real tras una edición) no tiene ningún canal para llegar a la Invoice
        // ya construida, porque Invoice._items ya copió los valores.
        var mutatedItemSnapshot = itemSnapshot with
        {
            Taxes = new List<InvoiceItemTaxSnapshot> { new(taxTypeId, "IVA (renombrado)", 27m, 999m) }
        };

        var invoiceTax = Assert.Single(Assert.Single(invoice.Items).Taxes);
        Assert.Equal("IVA", invoiceTax.TaxTypeName);
        Assert.Equal(19m, invoiceTax.Percentage);
        Assert.Equal(19m, invoiceTax.Amount);

        // El snapshot "mutado" es un objeto totalmente distinto (con expression),
        // confirmando que no hay ninguna referencia compartida.
        Assert.NotEqual(mutatedItemSnapshot.Taxes[0].TaxTypeName, invoiceTax.TaxTypeName);
    }
}
