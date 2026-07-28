using MediatR;

namespace Axon.Application.CashRegister.Commands;

// CashierId es el empleado que operará ese turno (deuda técnica: antes la
// apertura asumía que quien hace la petición ES el cajero — no permitía que un
// Administrador/supervisor abriera la caja asignándole el turno a otro
// empleado). Puede ser distinto de quien efectivamente llama al endpoint.
public record OpenCashSessionCommand(
    Guid CashRegisterId,
    Guid CashierId,
    decimal InitialAmount) : IRequest<OpenCashSessionResult>;

public record OpenCashSessionResult(
    Guid SessionId,
    string CashRegisterName,
    Guid CashierId,
    string CashierName,
    decimal InitialAmount,
    DateTime OpenedAt);
