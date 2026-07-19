using MediatR;

namespace Axon.Application.Inventory.Queries;

public record GetCategoriesQuery(bool IncludeInactive = false) : IRequest<List<CategoryDto>>;

public record CategoryDto(Guid Id, string Name, string Description, bool IsActive);
