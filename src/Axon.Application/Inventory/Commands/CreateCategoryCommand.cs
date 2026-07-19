using MediatR;

namespace Axon.Application.Inventory.Commands;

public record CreateCategoryCommand(string Name, string Description) : IRequest<Guid>;
