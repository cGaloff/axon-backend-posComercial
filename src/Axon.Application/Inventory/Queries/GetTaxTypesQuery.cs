using Axon.Domain.Entities.Taxes;
using MediatR;

namespace Axon.Application.Inventory.Queries;

public record GetTaxTypesQuery(bool IncludeInactive = false) : IRequest<List<TaxTypeDto>>;

// Code es el valor del enum fijo (TaxCode) que el frontend debe usar para
// identificar el tipo de impuesto; Id sigue siendo la llave foránea real que
// se envía en ProductTaxRequest al crear/editar un producto.
public record TaxTypeDto(Guid Id, TaxCode Code, string Name, string Description, bool IsActive);
