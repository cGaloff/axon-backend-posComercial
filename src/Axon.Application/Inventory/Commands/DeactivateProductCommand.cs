using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public record DeactivateProductCommand(Guid Id) : IRequest<MediatRUnit>;
