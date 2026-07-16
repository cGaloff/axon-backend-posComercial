using MediatR;

namespace Axon.Application.CashRegister.Commands;

public record OpenCashSessionCommand(
    Guid CashRegisterId,
    decimal InitialAmount) : IRequest<OpenCashSessionResult>;

public record OpenCashSessionResult(
    Guid SessionId,
    string CashRegisterName,
    decimal InitialAmount,
    DateTime OpenedAt);
