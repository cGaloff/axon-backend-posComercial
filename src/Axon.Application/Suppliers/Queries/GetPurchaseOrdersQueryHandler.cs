using Axon.Application.Common.Models;
using Axon.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Suppliers.Queries;

public class GetPurchaseOrdersQueryHandler : IRequestHandler<GetPurchaseOrdersQuery, PagedResult<PurchaseOrderDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetPurchaseOrdersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<PurchaseOrderDto>> Handle(GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.PurchaseOrders.Include(o => o.Items).AsQueryable();

        if (request.SupplierId.HasValue)
        {
            query = query.Where(o => o.SupplierId == request.SupplierId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(o => o.Status == request.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();

        var supplierNames = await _dbContext.Suppliers
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var items = orders.Select(o => new PurchaseOrderDto(
            o.Id,
            supplierNames.GetValueOrDefault(o.SupplierId, string.Empty),
            o.Status.ToString(),
            o.TotalOrdered,
            o.OrderDate,
            o.ExpectedDate,
            o.Items.Count,
            o.Items.Count(i => i.PendingQuantity > 0)))
            .ToList();

        return new PagedResult<PurchaseOrderDto>(totalCount, request.Page, request.PageSize, items);
    }
}
