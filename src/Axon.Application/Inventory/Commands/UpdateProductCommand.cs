using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public record UpdateProductCommand(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    decimal Cost,
    int MinStock,
    Guid CategoryId,
    Guid UnitId,
    Dictionary<string, string>? Attributes,
    List<ProductTaxRequest>? Taxes = null) : IRequest<MediatRUnit>;
