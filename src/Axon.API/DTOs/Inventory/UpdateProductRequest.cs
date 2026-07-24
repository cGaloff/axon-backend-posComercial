namespace Axon.API.DTOs.Inventory;

public record UpdateProductRequest(
    string Name,
    string Description,
    decimal Price,
    decimal Cost,
    int MinStock,
    Guid CategoryId,
    Guid UnitId,
    Dictionary<string, string>? Attributes,
    List<ProductTaxRequestDto>? Taxes = null);
