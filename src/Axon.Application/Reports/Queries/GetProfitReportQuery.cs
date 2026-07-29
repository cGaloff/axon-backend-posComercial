using MediatR;

namespace Axon.Application.Reports.Queries;

public record GetProfitReportQuery(DateTime FromDate, DateTime ToDate) : IRequest<ProfitReportDto>;

public record ProfitReportDto(
    decimal TotalRevenue,
    decimal TotalCost,
    decimal TotalProfit,
    decimal MarginPercentage,
    List<ProductProfitDto> Products);

public record ProductProfitDto(
    Guid ProductId,
    string ProductName,
    int QuantitySold,
    decimal Revenue,
    decimal Cost,
    decimal Profit,
    decimal MarginPercentage);
