using Axon.Application.Interfaces;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Sales.Commands;

public class ReturnSaleCommandHandler : IRequestHandler<ReturnSaleCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnSaleCommandHandler(IApplicationDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<MediatRUnit> Handle(ReturnSaleCommand request, CancellationToken cancellationToken)
    {
        var sale = await _dbContext.Sales
            .Include(s => s.Items)
            .SingleOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

        if (sale is null)
        {
            throw new DomainException("La venta no existe");
        }

        sale.MarkAsReturned(request.ReturnedBy);

        var saleReturn = SaleReturn.Create(sale.Id, request.Reason, request.ReturnedBy, sale.Total);
        _dbContext.SaleReturns.Add(saleReturn);

        var warehouse = await _dbContext.Warehouses.SingleOrDefaultAsync(w => w.IsDefault, cancellationToken);

        if (warehouse is null)
        {
            throw new DomainException("No hay una bodega por defecto configurada");
        }

        var productIds = sale.Items.Select(i => i.ProductId).ToList();

        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var movements = new List<InventoryMovement>();

        foreach (var item in sale.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                continue;
            }

            var stockBefore = product.Stock;
            product.AdjustStock(item.Quantity);

            movements.Add(InventoryMovement.Create(
                product.Id,
                warehouse.Id,
                InventoryMovementType.Return,
                item.Quantity,
                stockBefore,
                $"Devolución venta {sale.SaleNumber}",
                request.ReturnedBy));
        }

        _dbContext.InventoryMovements.AddRange(movements);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
