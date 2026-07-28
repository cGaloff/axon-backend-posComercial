using Axon.Application.Interfaces;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public class ApproveInventoryMovementCommandHandler : IRequestHandler<ApproveInventoryMovementCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;

    public ApproveInventoryMovementCommandHandler(
        IApplicationDbContext dbContext, IUnitOfWork unitOfWork, ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
    }

    public async Task<MediatRUnit> Handle(ApproveInventoryMovementCommand request, CancellationToken cancellationToken)
    {
        var movement = await _dbContext.InventoryMovements
            .SingleOrDefaultAsync(m => m.Id == request.MovementId, cancellationToken);

        if (movement is null)
        {
            throw new DomainException("El movimiento de inventario no existe");
        }

        var product = await _dbContext.Products.SingleOrDefaultAsync(p => p.Id == movement.ProductId, cancellationToken);

        if (product is null)
        {
            throw new DomainException("El producto no existe");
        }

        // Recién ahora, al aprobar, se aplica el ajuste que quedó pendiente.
        product.AdjustStock(movement.Quantity);
        movement.Approve(_currentUserContext.UserId);

        if (product.Stock <= product.MinStock)
        {
            var alert = StockAlert.Create(product.Id, movement.WarehouseId, product.Stock, product.MinStock);
            _dbContext.StockAlerts.Add(alert);
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
