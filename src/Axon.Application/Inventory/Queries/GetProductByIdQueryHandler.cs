using Axon.Application.Interfaces;
using Axon.Application.Inventory.DTOs;
using Axon.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Queries;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetProductByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductDto> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await (
            from p in _dbContext.Products
            join c in _dbContext.Categories on p.CategoryId equals c.Id
            join u in _dbContext.Units on p.UnitId equals u.Id
            where p.Id == request.Id
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
            .SingleOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            throw new DomainException("Producto no encontrado");
        }

        if (!product.IsActive)
        {
            throw new DomainException("Producto inactivo");
        }

        if (product.Taxes.Count > 0)
        {
            var taxTypeIds = product.Taxes.Select(t => t.TaxTypeId).ToList();

            var taxTypeNames = await _dbContext.TaxTypes
                .Where(t => taxTypeIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

            product = product with
            {
                Taxes = product.Taxes
                    .Select(t => t with { TaxTypeName = taxTypeNames.GetValueOrDefault(t.TaxTypeId, string.Empty) })
                    .ToList()
            };
        }

        return product;
    }
}
