using MediatR;

namespace Axon.Application.Inventory.Queries;

public record GetWarehousesQuery : IRequest<List<WarehouseDto>>;

public record WarehouseDto(Guid Id, string Name, string Description, bool IsDefault, bool IsActive);
