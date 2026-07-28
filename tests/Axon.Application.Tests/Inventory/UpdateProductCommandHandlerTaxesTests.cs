using Axon.Application.Inventory.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Taxes;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Inventory;

// Catálogo fijo de impuestos colombianos: el IVA solo admite 19%, 10%, 5% o
// exento (0%), igual que en CreateProductCommandHandler — ver
// ProductTaxNormalization, compartido por ambos handlers.
public class UpdateProductCommandHandlerTaxesTests
{
    private static async Task<(UpdateProductCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, Product Product, TaxType Iva)> ArrangeAsync()
    {
        var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var product = Product.Create("SKU-UPD-TAX", "Producto de prueba", 1000m, 500m, 0, category.Id, unit.Id);
        var iva = TaxType.Create(TaxCode.Iva, "IVA", "Impuesto sobre las ventas");

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Products.Add(product);
        dbContext.TaxTypes.Add(iva);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateProductCommandHandler(dbContext, new FakeUnitOfWork(dbContext));

        return (handler, dbContext, product, iva);
    }

    [Fact]
    public async Task Handle_WithIvaAtAnAllowedPercentage_Succeeds()
    {
        var (handler, dbContext, product, iva) = await ArrangeAsync();

        var command = new UpdateProductCommand(
            product.Id, product.Name, product.Description, product.Price, product.Cost, product.MinStock,
            product.CategoryId, product.UnitId, Attributes: null,
            Taxes: new List<ProductTaxRequest> { new(iva.Id, 10m) });

        await handler.Handle(command, CancellationToken.None);

        var updated = await dbContext.Products.FindAsync(product.Id);
        var tax = Assert.Single(updated!.Taxes);
        Assert.Equal(10m, tax.Percentage);
    }

    [Fact]
    public async Task Handle_WithIvaAtADisallowedPercentage_ThrowsDomainException()
    {
        var (handler, _, product, iva) = await ArrangeAsync();

        var command = new UpdateProductCommand(
            product.Id, product.Name, product.Description, product.Price, product.Cost, product.MinStock,
            product.CategoryId, product.UnitId, Attributes: null,
            Taxes: new List<ProductTaxRequest> { new(iva.Id, 12m) });

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("IVA", ex.Message);
    }
}
