using MediatR;

namespace Axon.Application.Inventory.Commands;

public record CreateTaxTypeCommand(string Name, string? Code) : IRequest<Guid>;
