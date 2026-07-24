using Axon.Application.Inventory.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Taxes;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Inventory;

public class CreateProductCommandHandlerTaxesTests
{
    private static async Task<(CreateProductCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, Category Category, Unit Unit)> ArrangeAsync()
    {
        var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        await dbContext.SaveChangesAsync();

        var handler = new CreateProductCommandHandler(dbContext, new FakeUnitOfWork(dbContext));

        return (handler, dbContext, category, unit);
    }

    [Fact]
    public async Task Handle_WithNoTaxes_CreatesProductWithoutAnyTax()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var command = new CreateProductCommand(
            "SKU-001", "Producto sin impuestos", "", 1000m, 500m, 0,
            category.Id, unit.Id, Attributes: null, Taxes: null);

        var productId = await handler.Handle(command, CancellationToken.None);

        var product = await dbContext.Products.FindAsync(productId);
        Assert.Empty(product!.Taxes);
    }

    [Fact]
    public async Task Handle_WithOneTax_CreatesProductWithThatTax()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var iva = TaxType.Create("IVA", "IVA");
        dbContext.TaxTypes.Add(iva);
        await dbContext.SaveChangesAsync();

        var command = new CreateProductCommand(
            "SKU-002", "Producto con IVA", "", 1190m, 500m, 0,
            category.Id, unit.Id, Attributes: null,
            Taxes: new List<ProductTaxRequest> { new(iva.Id, 19m) });

        var productId = await handler.Handle(command, CancellationToken.None);

        var product = await dbContext.Products.FindAsync(productId);
        var tax = Assert.Single(product!.Taxes);
        Assert.Equal(iva.Id, tax.TaxTypeId);
        Assert.Equal(19m, tax.Percentage);
    }

    [Fact]
    public async Task Handle_WithMultipleSimultaneousTaxes_CreatesProductWithAllOfThem()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var iva = TaxType.Create("IVA", "IVA");
        var ica = TaxType.Create("ICA", "ICA");
        dbContext.TaxTypes.AddRange(iva, ica);
        await dbContext.SaveChangesAsync();

        var command = new CreateProductCommand(
            "SKU-003", "Producto con IVA e ICA", "", 1200m, 500m, 0,
            category.Id, unit.Id, Attributes: null,
            Taxes: new List<ProductTaxRequest> { new(iva.Id, 19m), new(ica.Id, 0.7m) });

        var productId = await handler.Handle(command, CancellationToken.None);

        var product = await dbContext.Products.FindAsync(productId);
        Assert.Equal(2, product!.Taxes.Count);
        Assert.Contains(product.Taxes, t => t.TaxTypeId == iva.Id && t.Percentage == 19m);
        Assert.Contains(product.Taxes, t => t.TaxTypeId == ica.Id && t.Percentage == 0.7m);
    }

    [Fact]
    public async Task Handle_WithNonExistentTaxType_ThrowsDomainException()
    {
        var (handler, _, category, unit) = await ArrangeAsync();

        var command = new CreateProductCommand(
            "SKU-004", "Producto con impuesto inexistente", "", 1000m, 500m, 0,
            category.Id, unit.Id, Attributes: null,
            Taxes: new List<ProductTaxRequest> { new(Guid.NewGuid(), 19m) });

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
    }
}
