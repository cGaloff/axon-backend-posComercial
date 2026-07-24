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
        else if (request.OnlyInStock == false)
        {
            query = query.Where(p => p.Stock <= 0);
        }

        if (request.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= request.MinPrice.Value);
        }

        if (request.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= request.MaxPrice.Value);
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
                p.IsActive,
                p.Taxes.Select(t => new ProductTaxDto(t.TaxTypeId, string.Empty, t.Percentage)).ToList()))
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var itemsWithTaxNames = await ResolveTaxTypeNamesAsync(items, cancellationToken);

        return new PagedResult<ProductDto>(totalCount, request.Page, request.PageSize, itemsWithTaxNames);
    }

    private async Task<List<ProductDto>> ResolveTaxTypeNamesAsync(List<ProductDto> products, CancellationToken cancellationToken)
    {
        var taxTypeIds = products.SelectMany(p => p.Taxes).Select(t => t.TaxTypeId).Distinct().ToList();

        if (taxTypeIds.Count == 0)
        {
            return products;
        }

        var taxTypeNames = await _dbContext.TaxTypes
            .Where(t => taxTypeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        return products
            .Select(p => p with
            {
                Taxes = p.Taxes
                    .Select(t => t with { TaxTypeName = taxTypeNames.GetValueOrDefault(t.TaxTypeId, string.Empty) })
                    .ToList()
            })
            .ToList();
    }
}
