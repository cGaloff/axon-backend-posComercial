using Axon.Application.Interfaces;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;

    public AdjustStockCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
    }

    public async Task<MediatRUnit> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.SingleOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            throw new DomainException("El producto no existe");
        }

        var warehouse = await _dbContext.Warehouses.SingleOrDefaultAsync(w => w.IsDefault, cancellationToken);

        if (warehouse is null)
        {
            throw new DomainException("No hay una bodega por defecto configurada");
        }

        var stockBefore = product.Stock;

        product.AdjustStock(request.Quantity);

        var movement = InventoryMovement.Create(
            product.Id,
            warehouse.Id,
            request.Type,
            request.Quantity,
            stockBefore,
            request.Reason,
            _currentUserContext.UserId);

        _dbContext.InventoryMovements.Add(movement);

        if (product.Stock <= product.MinStock)
        {
            var alert = StockAlert.Create(product.Id, warehouse.Id, product.Stock, product.MinStock);
            _dbContext.StockAlerts.Add(alert);
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
