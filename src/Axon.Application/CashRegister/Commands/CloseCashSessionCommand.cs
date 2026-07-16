using MediatR;

namespace Axon.Application.CashRegister.Commands;

public record CloseCashSessionCommand(
    Guid SessionId,
    decimal CountedAmount,
    string? Notes,
    bool ForceClose = false) : IRequest<CloseCashSessionResult>;

public record CloseCashSessionResult(
    Guid SessionId,
    decimal ExpectedAmount,
    decimal CountedAmount,
    decimal Difference,
    string Status,
    DateTime ClosedAt);
