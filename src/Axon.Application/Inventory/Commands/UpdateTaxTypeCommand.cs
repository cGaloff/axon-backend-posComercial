using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public record UpdateTaxTypeCommand(Guid Id, string Name, string? Code) : IRequest<MediatRUnit>;
