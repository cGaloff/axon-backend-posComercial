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
    private readonly ITenantConfigRepository _tenantConfigRepository;

    public AdjustStockCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        ITenantConfigRepository tenantConfigRepository)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _tenantConfigRepository = tenantConfigRepository;
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

        // Mermas (Loss) cuyo valor supere el umbral configurado quedan pendientes
        // de aprobación de un Administrador: el stock NO se toca todavía (Matriz
        // de Roles y Permisos v2, regla transversal B). Cualquier otro tipo de
        // ajuste se aplica de inmediato, igual que antes.
        var requiresApproval = false;

        if (request.Type == InventoryMovementType.Loss)
        {
            var config = await _tenantConfigRepository.GetAsync()
                ?? throw new DomainException("Configuración del tenant no encontrada");

            var mermaValue = Math.Abs(request.Quantity) * product.Cost;
            requiresApproval = mermaValue > config.MermaApprovalThreshold;
        }

        if (!requiresApproval)
        {
            product.AdjustStock(request.Quantity);
        }

        var movement = InventoryMovement.Create(
            product.Id,
            warehouse.Id,
            request.Type,
            request.Quantity,
            stockBefore,
            request.Reason,
            _currentUserContext.UserId,
            requiresApproval);

        _dbContext.InventoryMovements.Add(movement);

        if (!requiresApproval && product.Stock <= product.MinStock)
        {
            var alert = StockAlert.Create(product.Id, warehouse.Id, product.Stock, product.MinStock);
            _dbContext.StockAlerts.Add(alert);
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
