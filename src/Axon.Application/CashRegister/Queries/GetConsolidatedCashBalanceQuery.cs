using MediatR;

namespace Axon.Application.CashRegister.Queries;

// Balance en tiempo real de todas las cajas ACTIVAS del tenant (sesión abierta
// en este momento). Las cajas sin sesión abierta no entran en la consolidación
// — "balance de cada caja activa" en el sentido literal: no hay nada que
// consolidar de una caja que ya cerró (su dinero ya se contó y quedó fuera de
// circulación al cerrar la sesión).
public record GetConsolidatedCashBalanceQuery : IRequest<ConsolidatedCashBalanceDto>;

public record ConsolidatedCashBalanceDto(
    List<CashRegisterBalanceDto> Registers,
    decimal TotalExpectedAmount);

public record CashRegisterBalanceDto(
    Guid CashRegisterId,
    string CashRegisterName,
    Guid SessionId,
    DateTime OpenedAt,
    decimal InitialAmount,
    decimal TotalCashSales,
    decimal TotalCreditSales,
    decimal TotalCardSales,
    decimal TotalTransferSales,
    decimal TotalManualIncome,
    decimal TotalExpenses,
    decimal TotalReturns,
    decimal ExpectedAmount);
