using Axon.Application.Common.Models;
using Axon.Application.Inventory.DTOs;
using MediatR;

namespace Axon.Application.Inventory.Queries;

public record GetProductsQuery(
    string? Search,
    Guid? CategoryId,
    Guid? UnitId,
    bool? OnlyInStock,
    Dictionary<string, string>? AttributeFilters,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<ProductDto>>;
