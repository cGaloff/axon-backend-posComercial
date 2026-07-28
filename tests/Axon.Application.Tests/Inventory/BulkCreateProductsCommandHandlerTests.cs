using Axon.Application.Inventory.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Taxes;

namespace Axon.Application.Tests.Inventory;

public class BulkCreateProductsCommandHandlerTests
{
    private static async Task<(BulkCreateProductsCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, Category Category, Unit Unit)> ArrangeAsync()
    {
        var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        await dbContext.SaveChangesAsync();

        var handler = new BulkCreateProductsCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new CreateProductCommandValidator(), new FakeCurrentUserContext());

        return (handler, dbContext, category, unit);
    }

    private static CreateProductCommand Row(string sku, Guid categoryId, Guid unitId, decimal price = 1000m) =>
        new(sku, $"Producto {sku}", "", price, 500m, 0, categoryId, unitId, Attributes: null, Taxes: null);

    [Fact]
    public async Task Handle_WithAllValidRows_InsertsAllOfThemInASingleCommit()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var command = new BulkCreateProductsCommand(new List<CreateProductCommand>
        {
            Row("BULK-001", category.Id, unit.Id),
            Row("BULK-002", category.Id, unit.Id),
            Row("BULK-003", category.Id, unit.Id)
        });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(3, result.InsertedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Empty(result.Errors);
        Assert.Equal(3, dbContext.Products.Count());
    }

    // Solo se valida contra productos ACTIVOS (mismo criterio que CreateProduct
    // individual): un SKU de un producto ya desactivado puede reutilizarse.
    [Fact]
    public async Task Handle_WithSkuAlreadyUsedByActiveProduct_SkipsThatRowWithoutAbortingTheBatch()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var existing = Product.Create("BULK-EXISTS", "Ya existe", 1000m, 500m, 0, category.Id, unit.Id);
        dbContext.Products.Add(existing);
        await dbContext.SaveChangesAsync();

        var command = new BulkCreateProductsCommand(new List<CreateProductCommand>
        {
            Row("BULK-EXISTS", category.Id, unit.Id),
            Row("BULK-NEW", category.Id, unit.Id)
        });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains(result.Errors, e => e.Contains("BULK-EXISTS"));
    }

    // Dos filas nuevas con el mismo SKU DENTRO del mismo archivo: ninguna existe
    // todavía en la BD, así que solo comparar contra la BD no las detectaría —
    // deben compararse también entre sí.
    [Fact]
    public async Task Handle_WithDuplicateSkuWithinTheSameBatch_InsertsOnlyTheFirstOccurrence()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var command = new BulkCreateProductsCommand(new List<CreateProductCommand>
        {
            Row("BULK-DUP", category.Id, unit.Id),
            Row("BULK-DUP", category.Id, unit.Id)
        });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Single(dbContext.Products);
    }

    [Fact]
    public async Task Handle_WithNonExistentCategory_SkipsThatRowWithoutAbortingTheBatch()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var command = new BulkCreateProductsCommand(new List<CreateProductCommand>
        {
            Row("BULK-BADCAT", Guid.NewGuid(), unit.Id),
            Row("BULK-GOODCAT", category.Id, unit.Id)
        });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains(result.Errors, e => e.Contains("BULK-BADCAT") && e.Contains("categoría"));
    }

    [Fact]
    public async Task Handle_WithNonExistentUnit_SkipsThatRowWithoutAbortingTheBatch()
    {
        var (handler, _, category, _) = await ArrangeAsync();

        var command = new BulkCreateProductsCommand(new List<CreateProductCommand>
        {
            Row("BULK-BADUNIT", category.Id, Guid.NewGuid())
        });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains(result.Errors, e => e.Contains("BULK-BADUNIT") && e.Contains("unidad"));
    }

    [Fact]
    public async Task Handle_WithNonExistentOrInactiveTaxType_SkipsThatRowWithoutAbortingTheBatch()
    {
        var (handler, _, category, unit) = await ArrangeAsync();

        var badRow = Row("BULK-BADTAX", category.Id, unit.Id) with
        {
            Taxes = new List<ProductTaxRequest> { new(Guid.NewGuid(), 19m) }
        };

        var command = new BulkCreateProductsCommand(new List<CreateProductCommand> { badRow });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains(result.Errors, e => e.Contains("BULK-BADTAX") && e.Contains("impuesto"));
    }

    [Fact]
    public async Task Handle_WithValidTaxes_AppliesThemToTheCreatedProduct()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var iva = TaxType.Create(TaxCode.Iva, "IVA", "Impuesto sobre las ventas");
        dbContext.TaxTypes.Add(iva);
        await dbContext.SaveChangesAsync();

        var row = Row("BULK-TAX", category.Id, unit.Id) with
        {
            Taxes = new List<ProductTaxRequest> { new(iva.Id, 19m) }
        };

        var result = await handler.Handle(new BulkCreateProductsCommand(new List<CreateProductCommand> { row }), CancellationToken.None);

        Assert.Equal(1, result.InsertedCount);
        var product = dbContext.Products.Single(p => p.Sku == "BULK-TAX");
        var tax = Assert.Single(product.Taxes);
        Assert.Equal(iva.Id, tax.TaxTypeId);
    }

    // Catálogo fijo de impuestos colombianos: el IVA solo admite 19%, 10%, 5%
    // o exento (0%) — una fila con otro porcentaje se omite, no aborta el lote.
    [Fact]
    public async Task Handle_WithIvaAtADisallowedPercentage_SkipsThatRowWithoutAbortingTheBatch()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var iva = TaxType.Create(TaxCode.Iva, "IVA", "Impuesto sobre las ventas");
        dbContext.TaxTypes.Add(iva);
        await dbContext.SaveChangesAsync();

        var command = new BulkCreateProductsCommand(new List<CreateProductCommand>
        {
            Row("BULK-IVA-BAD", category.Id, unit.Id) with { Taxes = new List<ProductTaxRequest> { new(iva.Id, 12m) } },
            Row("BULK-IVA-GOOD", category.Id, unit.Id) with { Taxes = new List<ProductTaxRequest> { new(iva.Id, 19m) } }
        });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains(result.Errors, e => e.Contains("BULK-IVA-BAD") && e.Contains("IVA"));
    }

    // Una fila que rompe una regla de negocio básica (precio <= 0) se omite con
    // un mensaje de error, sin lanzar una excepción que aborte el resto del lote
    // ya validado.
    [Fact]
    public async Task Handle_WithRowFailingBusinessValidation_SkipsThatRowWithoutAbortingTheBatch()
    {
        var (handler, dbContext, category, unit) = await ArrangeAsync();

        var command = new BulkCreateProductsCommand(new List<CreateProductCommand>
        {
            Row("BULK-INVALIDPRICE", category.Id, unit.Id, price: 0m),
            Row("BULK-VALID", category.Id, unit.Id)
        });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains(result.Errors, e => e.Contains("BULK-INVALIDPRICE"));
        Assert.Single(dbContext.Products);
    }

    [Fact]
    public async Task Handle_WithInitialStockAndDefaultWarehouse_SetsProductStockAndRecordsMovement()
    {
        var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Warehouses.Add(Warehouse.Create("Tienda Principal", "Bodega principal", isDefault: true));
        await dbContext.SaveChangesAsync();

        var handler = new BulkCreateProductsCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new CreateProductCommandValidator(), new FakeCurrentUserContext());

        var row = Row("BULK-INITSTOCK", category.Id, unit.Id) with { InitialStock = 30 };
        var result = await handler.Handle(new BulkCreateProductsCommand(new List<CreateProductCommand> { row }), CancellationToken.None);

        Assert.Equal(1, result.InsertedCount);
        var product = dbContext.Products.Single(p => p.Sku == "BULK-INITSTOCK");
        Assert.Equal(30, product.Stock);
        Assert.Single(dbContext.InventoryMovements);
    }

    [Fact]
    public async Task Handle_WithInitialStockButNoDefaultWarehouse_ThrowsDomainException()
    {
        var (handler, _, category, unit) = await ArrangeAsync();

        var row = Row("BULK-INITSTOCK-NOWH", category.Id, unit.Id) with { InitialStock = 10 };

        await Assert.ThrowsAsync<Axon.Domain.Exceptions.DomainException>(
            () => handler.Handle(new BulkCreateProductsCommand(new List<CreateProductCommand> { row }), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithEmptyProductList_ThrowsValidationException()
    {
        var (handler, _, _, _) = await ArrangeAsync();

        var validator = new BulkCreateProductsCommandValidator();
        var command = new BulkCreateProductsCommand(new List<CreateProductCommand>());

        var validationResult = await validator.ValidateAsync(command);

        Assert.False(validationResult.IsValid);
    }
}
