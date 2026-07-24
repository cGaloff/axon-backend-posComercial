using MediatR;

namespace Axon.Application.Inventory.Queries;

public record GetTaxTypesQuery(bool IncludeInactive = false) : IRequest<List<TaxTypeDto>>;

public record TaxTypeDto(Guid Id, string Name, string? Code, bool IsActive);
