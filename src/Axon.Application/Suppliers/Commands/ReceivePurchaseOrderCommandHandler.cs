using Axon.Application.Interfaces;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Suppliers;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Suppliers.Commands;

public class ReceivePurchaseOrderCommandHandler : IRequestHandler<ReceivePurchaseOrderCommand, ReceivePurchaseOrderResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;

    public ReceivePurchaseOrderCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
    }

    public async Task<ReceivePurchaseOrderResult> Handle(ReceivePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.PurchaseOrders
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == request.PurchaseOrderId, cancellationToken);

        if (order is null)
        {
            throw new DomainException("La orden de compra no existe");
        }

        if (order.Status == PurchaseOrderStatus.Cancelled)
        {
            throw new DomainException("No se puede recibir una orden cancelada");
        }

        var warehouse = await _dbContext.Warehouses.SingleOrDefaultAsync(w => w.IsDefault, cancellationToken);

        if (warehouse is null)
        {
            throw new DomainException("No hay una bodega por defecto configurada");
        }

        var currentUserId = _currentUserContext.UserId;
        var receipt = PurchaseReceipt.Create(order.Id, currentUserId, request.Notes);

        foreach (var itemRequest in request.Items)
        {
            var orderItem = order.Items.SingleOrDefault(i => i.Id == itemRequest.PurchaseOrderItemId);

            if (orderItem is null)
            {
                throw new DomainException($"El ítem '{itemRequest.PurchaseOrderItemId}' no pertenece a esta orden");
            }

            orderItem.RegisterReception(itemRequest.QuantityReceived);

            var product = await _dbContext.Products.SingleOrDefaultAsync(p => p.Id == orderItem.ProductId, cancellationToken);

            if (product is null)
            {
                throw new DomainException($"El producto '{orderItem.ProductId}' no existe");
            }

            // Costo Promedio Ponderado (CPP): se calcula aquí, no en la entidad, porque
            // depende del stock actual del producto en el momento de la recepción.
            var stockActual = product.Stock;
            var costoActual = product.Cost;
            var stockNuevo = itemRequest.QuantityReceived;
            var costoNuevo = orderItem.UnitCost;

            var nuevoCPP = stockActual + stockNuevo == 0
                ? costoNuevo
                : (stockActual * costoActual + stockNuevo * costoNuevo) / (stockActual + stockNuevo);

            var stockBefore = product.Stock;

            product.AdjustStock(itemRequest.QuantityReceived);
            product.UpdateAverageCost(nuevoCPP);

            var receiptItem = PurchaseReceiptItem.Create(
                receipt.Id,
                orderItem.Id,
                orderItem.ProductId,
                orderItem.ProductName,
                itemRequest.QuantityReceived,
                orderItem.UnitCost);

            receipt.AddItem(receiptItem);

            var movement = InventoryMovement.Create(
                product.Id,
                warehouse.Id,
                InventoryMovementType.Purchase,
                itemRequest.QuantityReceived,
                stockBefore,
                $"Recepción OC #{order.Id}",
                currentUserId);

            _dbContext.InventoryMovements.Add(movement);

            var productSupplier = await _dbContext.ProductSuppliers
                .SingleOrDefaultAsync(
                    ps => ps.ProductId == product.Id && ps.SupplierId == order.SupplierId,
                    cancellationToken);

            if (productSupplier is null)
            {
                productSupplier = new ProductSupplier(product.Id, order.SupplierId, orderItem.UnitCost);
                _dbContext.ProductSuppliers.Add(productSupplier);
            }
            else
            {
                productSupplier.UpdatePrice(orderItem.UnitCost);
            }
        }

        order.UpdateStatus();

        _dbContext.PurchaseReceipts.Add(receipt);

        await _unitOfWork.CommitAsync(cancellationToken);

        return new ReceivePurchaseOrderResult(receipt.Id, receipt.TotalReceived, order.Status.ToString());
    }
}
