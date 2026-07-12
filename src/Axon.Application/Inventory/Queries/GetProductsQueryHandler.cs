using Axon.Application.Common.Models;
using Axon.Application.Interfaces;
using Axon.Application.Inventory.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Queries;

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetProductsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Products.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern) || EF.Functions.ILike(p.Sku, pattern));
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);
        }

        if (request.UnitId.HasValue)
        {
            query = query.Where(p => p.UnitId == request.UnitId.Value);
        }

        if (request.OnlyInStock == true)
        {
            query = query.Where(p => p.Stock > 0);
        }

        if (request.AttributeFilters is { Count: > 0 })
        {
            foreach (var (key, value) in request.AttributeFilters)
            {
                var filter = new Dictionary<string, string> { [key] = value };
                query = query.Where(p => EF.Functions.JsonContains(p.Attributes, filter));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await (
            from p in query
            join c in _dbContext.Categories on p.CategoryId equals c.Id
            join u in _dbContext.Units on p.UnitId equals u.Id
            orderby p.Name
            select new ProductDto(
                p.Id,
                p.Sku,
                p.Name,
                p.Description,
                p.Price,
                p.Cost,
                p.Stock,
                p.MinStock,
                c.Name,
                u.Name,
                u.Abbreviation,
                p.Attributes,
                p.Stock <= p.MinStock,
                p.IsActive))
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(totalCount, request.Page, request.PageSize, items);
    }
}
