using Axon.Application.Interfaces;
using Axon.Domain.Entities.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Reports.Queries;

public class GetSalesSummaryReportQueryHandler : IRequestHandler<GetSalesSummaryReportQuery, SalesSummaryReportDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetSalesSummaryReportQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SalesSummaryReportDto> Handle(GetSalesSummaryReportQuery request, CancellationToken cancellationToken)
    {
        // Npgsql exige Kind=Utc para comparar contra columnas timestamptz; el binder
        // de ASP.NET Core entrega las fechas del query string con Kind=Unspecified.
        var fromDate = DateTime.SpecifyKind(request.FromDate, DateTimeKind.Utc);
        var toDate = DateTime.SpecifyKind(request.ToDate, DateTimeKind.Utc);

        // Solo ventas completadas cuentan como ingreso real: las anuladas/devueltas
        // no representan dinero efectivamente ganado en el período.
        var sales = await _dbContext.Sales
            .Where(s => s.CreatedAt >= fromDate && s.CreatedAt <= toDate && s.Status == SaleStatus.Completed)
            .ToListAsync(cancellationToken);

        var totalTransactions = sales.Count;
        var totalRevenue = sales.Sum(s => s.Total);
        var averageTicket = totalTransactions > 0 ? totalRevenue / totalTransactions : 0m;

        var revenueByPaymentMethod = sales
            .GroupBy(s => s.PaymentMethod.ToString())
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Total));

        var salesByHour = sales
            .GroupBy(s => ColombiaTime.ToLocal(s.CreatedAt).Hour)
            .Select(g => new HourlySalesDto(g.Key, g.Sum(s => s.Total), g.Count()))
            .OrderBy(h => h.Hour)
            .ToList();

        return new SalesSummaryReportDto(totalTransactions, totalRevenue, averageTicket, revenueByPaymentMethod, salesByHour);
    }
}
