using System.Text.Json;

namespace Axon.Application.Inventory.DTOs;

public record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal Price,
    decimal Cost,
    int Stock,
    int MinStock,
    string CategoryName,
    string UnitName,
    string UnitAbbreviation,
    Dictionary<string, JsonElement> Attributes,
    bool IsLowStock,
    bool IsActive,
    List<ProductTaxDto> Taxes);

public record ProductTaxDto(Guid TaxTypeId, string TaxTypeName, decimal Percentage);
