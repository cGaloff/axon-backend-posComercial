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
            request.Attributes,
            request.TaxPercentage);

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

    [HttpGet("products/{id:guid}")]
    [RequirePermission("inventory:read")]
    public async Task<IActionResult> GetProductById(Guid id)
    {
        var result = await _mediator.Send(new GetProductByIdQuery(id));

        return Ok(ApiResponse<ProductDto>.Ok(result));
    }

    [HttpPut("products/{id:guid}")]
    [RequirePermission("inventory:write")]
    public async Task<IActionResult> UpdateProduct(Guid id, UpdateProductRequest request)
    {
        var command = new UpdateProductCommand(
            id,
            request.Name,
            request.Description,
            request.Price,
            request.Cost,
            request.MinStock,
            request.CategoryId,
            request.UnitId,
            request.Attributes,
            request.TaxPercentage);

        await _mediator.Send(command);

        return Ok(ApiResponse<string>.Ok("ok", "Producto actualizado exitosamente"));
    }

    [HttpDelete("products/{id:guid}")]
    [RequirePermission("inventory:write")]
    public async Task<IActionResult> DeactivateProduct(Guid id)
    {
        await _mediator.Send(new DeactivateProductCommand(id));

        return Ok(ApiResponse<string>.Ok("ok", "Producto desactivado exitosamente"));
    }

    [HttpGet("categories")]
    [RequirePermission("inventory:read")]
    public async Task<IActionResult> GetCategories([FromQuery] bool includeInactive = false)
    {
        var result = await _mediator.Send(new GetCategoriesQuery(includeInactive));

        return Ok(ApiResponse<List<CategoryDto>>.Ok(result));
    }

    [HttpPost("categories")]
    [RequirePermission("inventory:write")]
    public async Task<IActionResult> CreateCategory(CreateCategoryRequest request)
    {
        var id = await _mediator.Send(new CreateCategoryCommand(request.Name, request.Description));

        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.Ok(id, "Categoría creada exitosamente"));
    }

    [HttpGet("units")]
    [RequirePermission("inventory:read")]
    public async Task<IActionResult> GetUnits()
    {
        var result = await _mediator.Send(new GetUnitsQuery());

        return Ok(ApiResponse<List<UnitDto>>.Ok(result));
    }

    [HttpGet("warehouses")]
    [RequirePermission("inventory:read")]
    public async Task<IActionResult> GetWarehouses()
    {
        var result = await _mediator.Send(new GetWarehousesQuery());

        return Ok(ApiResponse<List<WarehouseDto>>.Ok(result));
    }

    [HttpGet("attribute-definitions")]
    [RequirePermission("inventory:read")]
    public async Task<IActionResult> GetAttributeDefinitions([FromQuery] Guid? categoryId)
    {
        var result = await _mediator.Send(new GetAttributeDefinitionsQuery(categoryId));

        return Ok(ApiResponse<List<AttributeDefinitionDto>>.Ok(result));
    }

    [HttpPost("attribute-definitions")]
    [RequirePermission("inventory:write")]
    public async Task<IActionResult> CreateAttributeDefinition(CreateAttributeDefinitionRequest request)
    {
        var command = new CreateAttributeDefinitionCommand(
            request.Key,
            request.Label,
            request.Type,
            request.Options,
            request.CategoryId,
            request.IsFilterable,
            request.SortOrder);

        var id = await _mediator.Send(command);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.Ok(id, "Atributo creado exitosamente"));
    }
}
