using Axon.Application.Inventory.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;

namespace Axon.Application.Tests.Inventory;

public class GetProductsQueryHandlerTests
{
    private static async Task<(Axon.Infrastructure.Persistence.TenantDbContext DbContext, Category Category, Unit Unit)> ArrangeCatalogAsync()
    {
        var dbContext = TestDbContextFactory.Create();
        var category = Category.Create("Herramientas", "");
        var unit = Unit.Create("Unidad", "und");

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        await dbContext.SaveChangesAsync();

        return (dbContext, category, unit);
    }

    private static GetProductsQuery EmptyQuery() =>
        new(Search: null, CategoryId: null, UnitId: null, OnlyInStock: null, MinPrice: null, MaxPrice: null, AttributeFilters: null);

    // Nota: el filtro "Search" (preexistente, no modificado en este prompt) usa
    // EF.Functions.ILike, una función específica de Npgsql que el proveedor
    // InMemory no puede traducir (lanza InvalidOperationException al querer
    // ejecutarse). No es un defecto de la implementación — ILike funciona
    // correctamente contra Postgres real, ya cubierto por
    // scripts/test-inventory-crud.ps1 — es una limitación de este arnés de
    // pruebas InMemory. Por eso los tests de este archivo combinan
    // categoría/precio/stock (los filtros nuevos o corregidos en este prompt)
    // en vez de "Search", que no es verificable bajo InMemory.

    [Fact]
    public async Task Handle_FilterByCategory_ReturnsOnlyProductsInThatCategory()
    {
        var (dbContext, category, unit) = await ArrangeCatalogAsync();
        var otherCategory = Category.Create("Ferretería", "");
        dbContext.Categories.Add(otherCategory);

        var inCategory = Product.Create("SKU-1", "Producto A", 1000m, 500m, 0, category.Id, unit.Id);
        var inOtherCategory = Product.Create("SKU-2", "Producto B", 1000m, 500m, 0, otherCategory.Id, unit.Id);
        dbContext.Products.AddRange(inCategory, inOtherCategory);
        await dbContext.SaveChangesAsync();

        var handler = new GetProductsQueryHandler(dbContext);

        var result = await handler.Handle(EmptyQuery() with { CategoryId = category.Id }, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(inCategory.Id, result.Items.Single().Id);
    }

    [Fact]
    public async Task Handle_FilterByPriceRange_ReturnsOnlyProductsWithinRange()
    {
        var (dbContext, category, unit) = await ArrangeCatalogAsync();

        var cheap = Product.Create("SKU-CHEAP", "Producto barato", 5000m, 2000m, 0, category.Id, unit.Id);
        var mid = Product.Create("SKU-MID", "Producto medio", 50000m, 20000m, 0, category.Id, unit.Id);
        var expensive = Product.Create("SKU-EXP", "Producto caro", 500000m, 200000m, 0, category.Id, unit.Id);
        dbContext.Products.AddRange(cheap, mid, expensive);
        await dbContext.SaveChangesAsync();

        var handler = new GetProductsQueryHandler(dbContext);

        var result = await handler.Handle(EmptyQuery() with { MinPrice = 10000m, MaxPrice = 100000m }, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(mid.Id, result.Items.Single().Id);
    }

    [Fact]
    public async Task Handle_FilterByStockAvailable_ReturnsOnlyProductsWithStock()
    {
        var (dbContext, category, unit) = await ArrangeCatalogAsync();

        var inStock = Product.Create("SKU-STOCK", "Con stock", 1000m, 500m, 0, category.Id, unit.Id);
        inStock.AdjustStock(10);
        var outOfStock = Product.Create("SKU-NOSTOCK", "Agotado", 1000m, 500m, 0, category.Id, unit.Id);
        dbContext.Products.AddRange(inStock, outOfStock);
        await dbContext.SaveChangesAsync();

        var handler = new GetProductsQueryHandler(dbContext);

        var available = await handler.Handle(EmptyQuery() with { OnlyInStock = true }, CancellationToken.None);
        Assert.Single(available.Items);
        Assert.Equal(inStock.Id, available.Items.Single().Id);

        var soldOut = await handler.Handle(EmptyQuery() with { OnlyInStock = false }, CancellationToken.None);
        Assert.Single(soldOut.Items);
        Assert.Equal(outOfStock.Id, soldOut.Items.Single().Id);
    }

    [Fact]
    public async Task Handle_WithMultipleFiltersCombined_AppliesAllOfThemTogether()
    {
        var (dbContext, category, unit) = await ArrangeCatalogAsync();
        var otherCategory = Category.Create("Ferretería", "");
        dbContext.Categories.Add(otherCategory);

        // Coincide con TODOS los filtros combinados (categoría + rango de precio + stock disponible).
        var match = Product.Create("SKU-MATCH", "Producto que coincide", 30000m, 15000m, 0, category.Id, unit.Id);
        match.AdjustStock(5);

        // Coincide con precio y stock, pero es de otra categoría.
        var wrongCategory = Product.Create("SKU-CAT", "Producto de otra categoria", 30000m, 15000m, 0, otherCategory.Id, unit.Id);
        wrongCategory.AdjustStock(5);

        // Coincide con categoría y stock, pero el precio está fuera de rango.
        var wrongPrice = Product.Create("SKU-PRICE", "Producto fuera de rango", 900000m, 500000m, 0, category.Id, unit.Id);
        wrongPrice.AdjustStock(5);

        // Coincide con categoría y precio, pero está agotado.
        var wrongStock = Product.Create("SKU-STOCK", "Producto agotado", 30000m, 15000m, 0, category.Id, unit.Id);

        dbContext.Products.AddRange(match, wrongCategory, wrongPrice, wrongStock);
        await dbContext.SaveChangesAsync();

        var handler = new GetProductsQueryHandler(dbContext);

        var result = await handler.Handle(
            EmptyQuery() with
            {
                CategoryId = category.Id,
                MinPrice = 10000m,
                MaxPrice = 100000m,
                OnlyInStock = true
            },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(match.Id, result.Items.Single().Id);
    }
}
