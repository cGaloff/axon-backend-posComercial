using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Suppliers.Queries;

public class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, SupplierDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetSupplierByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SupplierDto> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers.SingleOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (supplier is null)
        {
            throw new DomainException("El proveedor no existe");
        }

        var productCount = await _dbContext.ProductSuppliers.CountAsync(ps => ps.SupplierId == supplier.Id, cancellationToken);

        var totalReceived = await _dbContext.PurchaseReceipts
            .Join(_dbContext.PurchaseOrders, r => r.PurchaseOrderId, o => o.Id, (r, o) => new { r.TotalReceived, o.SupplierId })
            .Where(x => x.SupplierId == supplier.Id)
            .SumAsync(x => x.TotalReceived, cancellationToken);

        var totalPaid = await _dbContext.SupplierPayments
            .Where(p => p.SupplierId == supplier.Id)
            .SumAsync(p => p.Amount, cancellationToken);

        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.DocumentType,
            supplier.DocumentNumber,
            supplier.ContactName,
            supplier.Phone,
            supplier.Email,
            supplier.Address,
            supplier.City,
            supplier.IsActive,
            productCount,
            totalReceived - totalPaid);
    }
}
