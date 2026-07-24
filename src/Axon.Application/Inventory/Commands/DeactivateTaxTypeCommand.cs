using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public record DeactivateTaxTypeCommand(Guid Id) : IRequest<MediatRUnit>;
