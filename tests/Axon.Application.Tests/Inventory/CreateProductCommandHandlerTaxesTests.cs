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

        var handler = new CreateProductCommandHandler(dbContext, new FakeUnitOfWork(dbContext), new FakeCurrentUserContext());

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

        var iva = TaxType.Create(TaxCode.Iva, "IVA", "Impuesto sobre las ventas");
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

        var iva = TaxType.Create(TaxCode.Iva, "IVA", "Impuesto sobre las ventas");
        var ica = TaxType.Create(TaxCode.Ica, "ICA", "Impuesto de timbre departamental");
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

    // Diagnóstico del reporte "el stock mínimo siempre llega en 0 sin importar lo que
    // se escriba": este test prueba el handler con MinStock=10 explícito y confirma que
    // persiste tal cual — si esto pasa en verde, el problema no está en el backend (la
    // llamada real desde el frontend debe estar enviando 0 en el payload).
    [Fact]
    public async Task Handle_WithNonZeroMinStock_PersistsTheExactValueSent()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var command = new CreateProductCommand(
            "SKU-MINSTOCK", "Producto con stock mínimo", "", 1000m, 500m, MinStock: 10,
            category.Id, unit.Id, Attributes: null, Taxes: null);

        var productId = await handler.Handle(command, CancellationToken.None);

        var product = await dbContext.Products.FindAsync(productId);
        Assert.Equal(10, product!.MinStock);
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

    // Catálogo fijo de impuestos colombianos: el IVA solo admite 19%, 10%, 5%
    // o exento (0%) — no un porcentaje libre como el resto de los impuestos.
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(19)]
    public async Task Handle_WithIvaAtAnAllowedPercentage_Succeeds(decimal percentage)
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var iva = TaxType.Create(TaxCode.Iva, "IVA", "Impuesto sobre las ventas");
        dbContext.TaxTypes.Add(iva);
        await dbContext.SaveChangesAsync();

        var command = new CreateProductCommand(
            $"SKU-IVA-{percentage}", "Producto con IVA", "", 1000m, 500m, 0,
            category.Id, unit.Id, Attributes: null,
            Taxes: new List<ProductTaxRequest> { new(iva.Id, percentage) });

        var productId = await handler.Handle(command, CancellationToken.None);

        var product = await dbContext.Products.FindAsync(productId);
        var tax = Assert.Single(product!.Taxes);
        Assert.Equal(percentage, tax.Percentage);
    }

    [Fact]
    public async Task Handle_WithIvaAtADisallowedPercentage_ThrowsDomainException()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var iva = TaxType.Create(TaxCode.Iva, "IVA", "Impuesto sobre las ventas");
        dbContext.TaxTypes.Add(iva);
        await dbContext.SaveChangesAsync();

        var command = new CreateProductCommand(
            "SKU-IVA-BAD", "Producto con IVA inválido", "", 1000m, 500m, 0,
            category.Id, unit.Id, Attributes: null,
            Taxes: new List<ProductTaxRequest> { new(iva.Id, 12m) });

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("IVA", ex.Message);
    }

    // Los impuestos que no son IVA (p. ej. ICA) admiten cualquier porcentaje,
    // igual que antes de fijar el catálogo.
    [Fact]
    public async Task Handle_WithNonIvaTaxAtAnyPercentage_Succeeds()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var ica = TaxType.Create(TaxCode.Ica, "ICA", "Impuesto de timbre departamental");
        dbContext.TaxTypes.Add(ica);
        await dbContext.SaveChangesAsync();

        var command = new CreateProductCommand(
            "SKU-ICA-FREE", "Producto con ICA", "", 1000m, 500m, 0,
            category.Id, unit.Id, Attributes: null,
            Taxes: new List<ProductTaxRequest> { new(ica.Id, 0.7m) });

        var productId = await handler.Handle(command, CancellationToken.None);

        var product = await dbContext.Products.FindAsync(productId);
        var tax = Assert.Single(product!.Taxes);
        Assert.Equal(0.7m, tax.Percentage);
    }
}
