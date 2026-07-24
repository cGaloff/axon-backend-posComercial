using Axon.Application.Common.Models;
using Axon.Application.Inventory.DTOs;
using MediatR;

namespace Axon.Application.Inventory.Queries;

// OnlyInStock: true = solo disponibles (Stock > 0), false = solo agotados
// (Stock <= 0), null = sin filtrar por stock.
public record GetProductsQuery(
    string? Search,
    Guid? CategoryId,
    Guid? UnitId,
    bool? OnlyInStock,
    decimal? MinPrice,
    decimal? MaxPrice,
    Dictionary<string, string>? AttributeFilters,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<ProductDto>>;
