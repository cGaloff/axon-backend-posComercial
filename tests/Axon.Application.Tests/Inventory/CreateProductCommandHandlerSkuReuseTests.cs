using Axon.Application.Inventory.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Inventory;

// Bug reportado por frontend: "despues de eliminar un producto no se puede
// volver a crear uno con el mismo sku". DeactivateProduct es un soft-delete
// (el producto sigue existiendo con IsActive = false), pero la validacion de
// SKU duplicado comparaba contra TODOS los productos sin filtrar por
// IsActive, asi que el SKU de un producto ya "eliminado" quedaba bloqueado
// para siempre.
public class CreateProductCommandHandlerSkuReuseTests
{
    private static async Task<(CreateProductCommandHandler CreateHandler, DeactivateProductCommandHandler DeactivateHandler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, Category Category, Unit Unit)> ArrangeAsync()
    {
        var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        await dbContext.SaveChangesAsync();

        var unitOfWork = new FakeUnitOfWork(dbContext);
        var createHandler = new CreateProductCommandHandler(dbContext, unitOfWork);
        var deactivateHandler = new DeactivateProductCommandHandler(dbContext, unitOfWork);

        return (createHandler, deactivateHandler, dbContext, category, unit);
    }

    private static CreateProductCommand NewProductCommand(string sku, Guid categoryId, Guid unitId) =>
        new(sku, $"Producto {sku}", "", 1000m, 500m, 0, categoryId, unitId, Attributes: null, Taxes: null);

    [Fact]
    public async Task Handle_AfterDeactivatingProduct_AllowsCreatingNewProductWithSameSku()
    {
        var (createHandler, deactivateHandler, dbContext, category, unit) = await ArrangeAsync();

        var originalId = await createHandler.Handle(NewProductCommand("SKU-REUSE", category.Id, unit.Id), CancellationToken.None);
        await deactivateHandler.Handle(new DeactivateProductCommand(originalId), CancellationToken.None);

        var newId = await createHandler.Handle(NewProductCommand("SKU-REUSE", category.Id, unit.Id), CancellationToken.None);

        Assert.NotEqual(originalId, newId);
        var newProduct = await dbContext.Products.FindAsync(newId);
        Assert.NotNull(newProduct);
        Assert.True(newProduct!.IsActive);
    }

    [Fact]
    public async Task Handle_WithSkuAlreadyUsedByActiveProduct_ThrowsDomainException()
    {
        var (createHandler, _, _, category, unit) = await ArrangeAsync();

        await createHandler.Handle(NewProductCommand("SKU-ACTIVE", category.Id, unit.Id), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(
            () => createHandler.Handle(NewProductCommand("SKU-ACTIVE", category.Id, unit.Id), CancellationToken.None));
    }
}
