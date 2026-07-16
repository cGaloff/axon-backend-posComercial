using MediatR;

namespace Axon.Application.Inventory.Queries;

public record GetUnitsQuery : IRequest<List<UnitDto>>;

public record UnitDto(Guid Id, string Name, string Abbreviation, bool IsActive);
