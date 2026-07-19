using MediatR;

namespace Axon.Application.CashRegister.Queries;

public record GetCashRegistersQuery() : IRequest<List<CashRegisterDto>>;

public record CashRegisterDto(
    Guid Id,
    string Name,
    string Description,
    bool IsDefault,
    bool IsActive,
    bool HasActiveSession,
    Guid? ActiveSessionId);
