using Axon.Domain.Entities.CashRegister;
using MediatR;

namespace Axon.Application.CashRegister.Commands;

public record AddCashMovementCommand(
    Guid SessionId,
    CashMovementType Type,
    decimal Amount,
    string Description,
    Guid? ReferenceId = null) : IRequest<Guid>;
