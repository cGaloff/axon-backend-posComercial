using MediatR;

namespace Axon.Application.CashRegister.Commands;

public record OpenCashSessionCommand(
    Guid CashRegisterId,
    Guid OpenedBy,
    decimal InitialAmount) : IRequest<OpenCashSessionResult>;

public record OpenCashSessionResult(
    Guid SessionId,
    string CashRegisterName,
    decimal InitialAmount,
    DateTime OpenedAt);
