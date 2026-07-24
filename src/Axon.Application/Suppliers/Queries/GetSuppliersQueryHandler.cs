using Axon.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Suppliers.Queries;

public class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, List<SupplierDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetSuppliersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Suppliers.AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search}%";
            query = query.Where(s => EF.Functions.ILike(s.Name, pattern));
        }

        var suppliers = await query.OrderBy(s => s.Name).ToListAsync(cancellationToken);
        var supplierIds = suppliers.Select(s => s.Id).ToList();

        var productCounts = await _dbContext.ProductSuppliers
            .Where(ps => supplierIds.Contains(ps.SupplierId))
            .GroupBy(ps => ps.SupplierId)
            .Select(g => new { SupplierId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Count, cancellationToken);

        var totalReceived = await _dbContext.PurchaseReceipts
            .Join(_dbContext.PurchaseOrders, r => r.PurchaseOrderId, o => o.Id, (r, o) => new { r.TotalReceived, o.SupplierId })
            .Where(x => supplierIds.Contains(x.SupplierId))
            .GroupBy(x => x.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(x => x.TotalReceived) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Total, cancellationToken);

        var totalPaid = await _dbContext.SupplierPayments
            .Where(p => supplierIds.Contains(p.SupplierId))
            .GroupBy(p => p.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Total, cancellationToken);

        return suppliers.Select(s => new SupplierDto(
            s.Id,
            s.Name,
            s.DocumentType,
            s.DocumentNumber,
            s.ContactName,
            s.Phone,
            s.Email,
            s.Address,
            s.City,
            s.IsActive,
            productCounts.GetValueOrDefault(s.Id),
            totalReceived.GetValueOrDefault(s.Id) - totalPaid.GetValueOrDefault(s.Id)))
            .ToList();
    }
}
