namespace Axon.API.DTOs.Inventory;

public record CreateProductRequest(
    string Sku,
    string Name,
    string Description,
    decimal Price,
    decimal Cost,
    int MinStock,
    Guid CategoryId,
    Guid UnitId,
    Dictionary<string, string>? Attributes,
    List<ProductTaxRequestDto>? Taxes = null,
    int InitialStock = 0);

public record ProductTaxRequestDto(Guid TaxTypeId, decimal Percentage);
