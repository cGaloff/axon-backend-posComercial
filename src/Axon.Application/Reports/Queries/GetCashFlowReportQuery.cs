using MediatR;

namespace Axon.Application.Reports.Queries;

public record GetCashFlowReportQuery(
    DateTime FromDate,
    DateTime ToDate,
    string GroupBy = "Day") : IRequest<CashFlowReportDto>;

public record CashFlowReportDto(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetBalance,
    List<CashFlowDataPointDto> Series);

public record CashFlowDataPointDto(string DateLabel, decimal Income, decimal Expense);
