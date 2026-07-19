using Axon.Application.Interfaces;
using Axon.Domain.Entities.Suppliers;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Suppliers.Commands;

public class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;

    public CreatePurchaseOrderCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
    }

    public async Task<Guid> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers.SingleOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);

        if (supplier is null)
        {
            throw new DomainException("El proveedor no existe");
        }

        if (!supplier.IsActive)
        {
            throw new DomainException("El proveedor está inactivo");
        }

        var productIds = request.Items.Select(i => i.ProductId).ToList();

        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var productsById = products.ToDictionary(p => p.Id);

        var missingProductId = productIds.FirstOrDefault(id => !productsById.ContainsKey(id));
        if (missingProductId != Guid.Empty)
        {
            throw new DomainException($"El producto '{missingProductId}' no existe");
        }

        var order = PurchaseOrder.Create(
            request.SupplierId,
            _currentUserContext.UserId,
            request.ExpectedDate,
            request.Notes);

        foreach (var itemRequest in request.Items)
        {
            var product = productsById[itemRequest.ProductId];

            var orderItem = PurchaseOrderItem.Create(
                order.Id,
                product.Id,
                product.Name,
                product.Sku,
                itemRequest.QuantityOrdered,
                itemRequest.UnitCost);

            order.AddItem(orderItem);
        }

        _dbContext.PurchaseOrders.Add(order);
        await _unitOfWork.CommitAsync(cancellationToken);

        return order.Id;
    }
}
