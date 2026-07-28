using Axon.Application.Interfaces;
using Axon.Domain.Entities.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Queries;

public class GetPendingInventoryMovementsQueryHandler : IRequestHandler<GetPendingInventoryMovementsQuery, List<PendingInventoryMovementDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetPendingInventoryMovementsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<PendingInventoryMovementDto>> Handle(GetPendingInventoryMovementsQuery request, CancellationToken cancellationToken)
    {
        var movements = await _dbContext.InventoryMovements
            .Where(m => m.Status == InventoryMovementStatus.PendingApproval)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var productIds = movements.Select(m => m.ProductId).Distinct().ToList();

        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return movements
            .Select(m =>
            {
                var product = products.GetValueOrDefault(m.ProductId);

                return new PendingInventoryMovementDto(
                    m.Id,
                    m.ProductId,
                    product?.Name ?? string.Empty,
                    product?.Sku ?? string.Empty,
                    m.Quantity,
                    Math.Abs(m.Quantity) * (product?.Cost ?? 0m),
                    m.Reason,
                    m.CreatedBy,
                    m.CreatedAt);
            })
            .ToList();
    }
}
