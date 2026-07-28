using Axon.Application.Inventory.Commands;
using Axon.Application.Inventory.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Exceptions;
using TenantConfigEntity = Axon.Domain.Entities.TenantConfig;

namespace Axon.Application.Tests.Inventory;

public class InventoryMovementApprovalTests
{
    private static async Task<(
        Axon.Infrastructure.Persistence.TenantDbContext DbContext,
        Product Product,
        InventoryMovement PendingMovement,
        FakeCurrentUserContext CurrentUser)> ArrangeWithPendingMovementAsync()
    {
        var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUserContext();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Tienda Principal", "Bodega principal", isDefault: true);
        var product = Product.Create("SKU-APPROVAL", "Producto con merma pendiente", 1000m, 5000m, minStock: 5, category.Id, unit.Id);
        product.AdjustStock(100);

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Warehouses.Add(warehouse);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var config = TenantConfigEntity.Create("Negocio de prueba");
        config.SetMermaApprovalThreshold(50000m);

        var adjustHandler = new AdjustStockCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), currentUser, new FakeTenantConfigRepository(config));

        // Costo 5.000 x cantidad 20 = 100.000, por encima del umbral -> queda pendiente.
        await adjustHandler.Handle(
            new AdjustStockCommand(product.Id, -20, InventoryMovementType.Loss, "Producto dañado"),
            CancellationToken.None);

        var pendingMovement = dbContext.InventoryMovements.Single();

        return (dbContext, product, pendingMovement, currentUser);
    }

    [Fact]
    public async Task Approve_OnPendingMovement_AppliesStockAdjustmentAndMarksApproved()
    {
        var (dbContext, product, pendingMovement, currentUser) = await ArrangeWithPendingMovementAsync();

        var handler = new ApproveInventoryMovementCommandHandler(dbContext, new FakeUnitOfWork(dbContext), currentUser);

        await handler.Handle(new ApproveInventoryMovementCommand(pendingMovement.Id), CancellationToken.None);

        var updatedProduct = await dbContext.Products.FindAsync(product.Id);
        Assert.Equal(80, updatedProduct!.Stock);

        var updatedMovement = await dbContext.InventoryMovements.FindAsync(pendingMovement.Id);
        Assert.Equal(InventoryMovementStatus.Approved, updatedMovement!.Status);
        Assert.Equal(currentUser.UserId, updatedMovement.ReviewedBy);
    }

    [Fact]
    public async Task Reject_OnPendingMovement_LeavesStockUntouchedAndMarksRejected()
    {
        var (dbContext, product, pendingMovement, currentUser) = await ArrangeWithPendingMovementAsync();

        var handler = new RejectInventoryMovementCommandHandler(dbContext, new FakeUnitOfWork(dbContext), currentUser);

        await handler.Handle(new RejectInventoryMovementCommand(pendingMovement.Id, "No corresponde a una merma real"), CancellationToken.None);

        var updatedProduct = await dbContext.Products.FindAsync(product.Id);
        Assert.Equal(100, updatedProduct!.Stock);

        var updatedMovement = await dbContext.InventoryMovements.FindAsync(pendingMovement.Id);
        Assert.Equal(InventoryMovementStatus.Rejected, updatedMovement!.Status);
        Assert.Equal("No corresponde a una merma real", updatedMovement.RejectionReason);
    }

    [Fact]
    public async Task Approve_OnAlreadyApprovedMovement_ThrowsDomainException()
    {
        var (dbContext, _, pendingMovement, currentUser) = await ArrangeWithPendingMovementAsync();

        var handler = new ApproveInventoryMovementCommandHandler(dbContext, new FakeUnitOfWork(dbContext), currentUser);
        await handler.Handle(new ApproveInventoryMovementCommand(pendingMovement.Id), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ApproveInventoryMovementCommand(pendingMovement.Id), CancellationToken.None));
    }

    [Fact]
    public async Task GetPendingInventoryMovements_ReturnsOnlyPendingOnesWithProductInfo()
    {
        var (dbContext, product, pendingMovement, _) = await ArrangeWithPendingMovementAsync();

        var handler = new GetPendingInventoryMovementsQueryHandler(dbContext);

        var result = await handler.Handle(new GetPendingInventoryMovementsQuery(), CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(pendingMovement.Id, dto.Id);
        Assert.Equal(product.Name, dto.ProductName);
        Assert.Equal(100000m, dto.EstimatedValue);
    }

    [Fact]
    public async Task SetMermaApprovalThreshold_PersistsNewValue()
    {
        var dbContext = TestDbContextFactory.Create();
        var config = TenantConfigEntity.Create("Negocio de prueba");
        var repository = new FakeTenantConfigRepository(config);

        var handler = new SetMermaApprovalThresholdCommandHandler(repository, new FakeUnitOfWork(dbContext));

        await handler.Handle(new SetMermaApprovalThresholdCommand(75000m), CancellationToken.None);

        var updated = await repository.GetAsync();
        Assert.Equal(75000m, updated!.MermaApprovalThreshold);
    }
}
