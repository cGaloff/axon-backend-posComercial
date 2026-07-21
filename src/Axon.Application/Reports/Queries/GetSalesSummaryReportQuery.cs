using MediatR;

namespace Axon.Application.Reports.Queries;

public record GetSalesSummaryReportQuery(DateTime FromDate, DateTime ToDate) : IRequest<SalesSummaryReportDto>;

public record SalesSummaryReportDto(
    int TotalTransactions,
    decimal TotalRevenue,
    decimal AverageTicket,
    Dictionary<string, decimal> RevenueByPaymentMethod,
    List<HourlySalesDto> SalesByHour);

public record HourlySalesDto(int Hour, decimal Revenue, int TransactionCount);
