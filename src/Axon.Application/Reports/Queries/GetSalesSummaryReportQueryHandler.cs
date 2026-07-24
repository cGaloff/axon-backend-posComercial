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
        // FromDate/ToDate llegan como límites del día calendario en hora Colombia, pero
        // Sale.CreatedAt se guarda en UTC. Se convierten antes de filtrar para que las
        // ventas hechas de noche (ya en el día UTC siguiente) no desaparezcan del reporte.
        var fromUtc = ColombiaTime.ToUtc(request.FromDate);
        var toUtc = ColombiaTime.ToUtc(request.ToDate);

        // Solo ventas completadas cuentan como ingreso real: las anuladas/devueltas
        // no representan dinero efectivamente ganado en el período.
        var sales = await _dbContext.Sales
            .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt <= toUtc && s.Status == SaleStatus.Completed)
            .ToListAsync(cancellationToken);

        var totalTransactions = sales.Count;
        var totalRevenue = sales.Sum(s => s.Total);
        var averageTicket = totalTransactions > 0 ? totalRevenue / totalTransactions : 0m;

        // Con pagos divididos, cada método contribuye por el monto que efectivamente
        // cubrió (no el total de la venta completa), así una venta mitad efectivo /
        // mitad tarjeta reparte correctamente entre ambos buckets.
        var revenueByPaymentMethod = sales
            .SelectMany(s => s.Payments)
            .GroupBy(p => p.Method.ToString())
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var salesByHour = sales
            .GroupBy(s => ColombiaTime.ToLocal(s.CreatedAt).Hour)
            .Select(g => new HourlySalesDto(g.Key, g.Sum(s => s.Total), g.Count()))
            .OrderBy(h => h.Hour)
            .ToList();

        return new SalesSummaryReportDto(totalTransactions, totalRevenue, averageTicket, revenueByPaymentMethod, salesByHour);
    }
}
