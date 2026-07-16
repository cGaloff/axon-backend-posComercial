using Axon.API.Common;
using Axon.API.DTOs.Inventory;
using Axon.API.Filters;
using Axon.Application.Common.Models;
using Axon.Application.Inventory.Commands;
using Axon.Application.Inventory.DTOs;
using Axon.Application.Inventory.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private static readonly HashSet<string> KnownProductQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "search", "categoryId", "unitId", "onlyInStock", "page", "pageSize"
    };

    private readonly IMediator _mediator;

    public InventoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("products")]
    [RequirePermission("inventory:read")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? unitId,
        [FromQuery] bool? onlyInStock,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var attributeFilters = Request.Query
            .Where(q => !KnownProductQueryKeys.Contains(q.Key))
            .ToDictionary(q => q.Key, q => q.Value.ToString());

        var query = new GetProductsQuery(
            search,
            categoryId,
            unitId,
            onlyInStock,
            attributeFilters.Count > 0 ? attributeFilters : null,
            page,
            pageSize);

        var result = await _mediator.Send(query);

        return Ok(ApiResponse<PagedResult<ProductDto>>.Ok(result));
    }

    [HttpPost("products")]
    [RequirePermission("inventory:write")]
    public async Task<IActionResult> CreateProduct(CreateProductRequest request)
    {
        var command = new CreateProductCommand(
            request.Sku,
            request.Name,
            request.Description,
            request.Price,
            request.Cost,
            request.MinStock,
            request.CategoryId,
            request.UnitId,
            request.Attributes);

        var id = await _mediator.Send(command);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.Ok(id, "Producto creado exitosamente"));
    }

    [HttpPost("products/{id:guid}/adjust-stock")]
    [RequirePermission("inventory:write")]
    public async Task<IActionResult> AdjustStock(Guid id, AdjustStockRequest request)
    {
        var command = new AdjustStockCommand(id, request.Quantity, request.Type, request.Reason);

        await _mediator.Send(command);

        return Ok(ApiResponse<string>.Ok("ok", "Stock ajustado exitosamente"));
    }
}
