using MediatR;

namespace Axon.Application.CashRegister.Queries;

public record GetCashSessionSummaryQuery(Guid SessionId) : IRequest<CashSessionSummaryDto>;

public record CashSessionSummaryDto(
    Guid SessionId,
    string CashRegisterName,
    string Status,
    decimal InitialAmount,
    decimal TotalCashSales,
    decimal TotalCreditSales,
    decimal TotalCardSales,
    decimal TotalTransferSales,
    decimal TotalManualIncome,
    decimal TotalExpenses,
    decimal TotalReturns,
    decimal ExpectedAmount,
    decimal? CountedAmount,
    decimal? Difference,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    List<CashMovementDto> Movements);

public record CashMovementDto(
    Guid Id,
    string Type,
    decimal Amount,
    string Description,
    DateTime CreatedAt,
    Guid? ReferenceId);
