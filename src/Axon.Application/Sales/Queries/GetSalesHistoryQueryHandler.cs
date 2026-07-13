using Axon.Application.Common.Models;
using Axon.Application.Interfaces;
using Axon.Application.Sales.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Sales.Queries;

public class GetSalesHistoryQueryHandler : IRequestHandler<GetSalesHistoryQuery, PagedResult<SaleDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetSalesHistoryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<SaleDto>> Handle(GetSalesHistoryQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Sales.AsQueryable();

        if (request.From.HasValue)
        {
            query = query.Where(s => s.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(s => s.CreatedAt <= request.To.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.Status == request.Status.Value);
        }

        if (request.CustomerId.HasValue)
        {
            query = query.Where(s => s.CustomerId == request.CustomerId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new SaleDto(
                s.Id,
                s.SaleNumber,
                s.CustomerName,
                s.PaymentMethod.ToString(),
                s.Status.ToString(),
                s.Total,
                s.CreatedAt,
                s.Items.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<SaleDto>(totalCount, request.Page, request.PageSize, items);
    }
}
