using MediatR;

namespace Axon.Application.Inventory.Commands;

public record ProductTaxRequest(Guid TaxTypeId, decimal Percentage);

public record CreateProductCommand(
    string Sku,
    string Name,
    string Description,
    decimal Price,
    decimal Cost,
    int MinStock,
    Guid CategoryId,
    Guid UnitId,
    Dictionary<string, string>? Attributes,
    List<ProductTaxRequest>? Taxes = null) : IRequest<Guid>;
