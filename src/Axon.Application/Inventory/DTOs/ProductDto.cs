using System.Text.Json;
using Axon.Domain.Entities.Taxes;

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

// Code es el valor del enum fijo (TaxCode, ver GetTaxTypesQuery) para que el
// frontend pueda identificar/interpretar el impuesto (p. ej. saber que es IVA
// para aplicar la restricción de porcentajes fijos al editarlo).
public record ProductTaxDto(Guid TaxTypeId, TaxCode Code, string TaxTypeName, decimal Percentage);
