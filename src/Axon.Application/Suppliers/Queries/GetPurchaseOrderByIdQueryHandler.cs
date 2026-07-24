using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Suppliers.Queries;

public class GetPurchaseOrderByIdQueryHandler : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDetailsDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetPurchaseOrderByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PurchaseOrderDetailsDto> Handle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.PurchaseOrders
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == request.Id, cancellationToken);

        if (order is null)
        {
            throw new DomainException("La compra no existe");
        }

        var supplier = await _dbContext.Suppliers
            .SingleOrDefaultAsync(s => s.Id == order.SupplierId, cancellationToken);

        var items = order.Items
            .Select(i => new PurchaseOrderItemDetailsDto(
                i.Id,
                i.ProductId,
                i.ProductName,
                i.ProductSku,
                i.QuantityOrdered,
                i.QuantityReceived,
                i.UnitCost,
                i.Subtotal,
                i.TaxAmount,
                i.Total,
                i.Taxes.Select(t => new PurchaseOrderItemTaxDto(t.TaxTypeId, t.TaxTypeName, t.Percentage, t.Amount)).ToList()))
            .ToList();

        return new PurchaseOrderDetailsDto(
            order.Id,
            order.SupplierId,
            supplier?.Name ?? string.Empty,
            order.Status.ToString(),
            order.Notes,
            order.OrderDate,
            order.ExpectedDate,
            order.SupplierInvoiceNumber,
            order.SupplierInvoiceDate,
            order.SupplierDocumentTypeAtPurchase,
            order.TotalOrdered,
            items);
    }
}
