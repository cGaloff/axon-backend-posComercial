using Axon.Application.Interfaces;
using Axon.Domain.Entities.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Reports.Queries;

public class GetProfitReportQueryHandler : IRequestHandler<GetProfitReportQuery, ProfitReportDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetProfitReportQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProfitReportDto> Handle(GetProfitReportQuery request, CancellationToken cancellationToken)
    {
        var fromUtc = ColombiaTime.ToUtc(request.FromDate.Date);
        var toUtc = ColombiaTime.ToUtc(request.ToDate.Date.AddDays(1).AddTicks(-1));

        // Solo ventas completadas: las anuladas/devueltas no representan ingreso
        // ni costo real del período (igual que GetSalesSummaryReportQueryHandler).
        var sales = await _dbContext.Sales
            .Include(s => s.Items)
            .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt <= toUtc && s.Status == SaleStatus.Completed)
            .ToListAsync(cancellationToken);

        var items = sales.SelectMany(s => s.Items).ToList();

        // Ganancia real = ingreso SIN impuesto (SubtotalBase, que ya viene neto
        // de ambos descuentos aplicados) menos el costo unitario SNAPSHOTEADO al
        // momento de la venta (SaleItem.UnitCost) por la cantidad vendida. No se
        // usa Subtotal (que sí incluye impuesto) porque el impuesto cobrado se
        // traslada a la DIAN, no es ingreso real del negocio. Los ítems de ventas
        // anteriores a este campo (o de productos sin costo cargado) tienen
        // UnitCost = 0, así que su "ganancia" en el reporte es en realidad el
        // ingreso completo — no hay forma de recuperar ese dato retroactivamente.
        decimal Revenue(SaleItem item) => item.SubtotalBase;
        decimal Cost(SaleItem item) => item.UnitCost * item.Quantity;

        var totalRevenue = items.Sum(Revenue);
        var totalCost = items.Sum(Cost);
        var totalProfit = totalRevenue - totalCost;
        var marginPercentage = totalRevenue > 0 ? totalProfit / totalRevenue * 100 : 0m;

        var products = items
            .GroupBy(item => new { item.ProductId, item.ProductName })
            .Select(g =>
            {
                var revenue = g.Sum(Revenue);
                var cost = g.Sum(Cost);
                var profit = revenue - cost;

                return new ProductProfitDto(
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Sum(item => item.Quantity),
                    revenue,
                    cost,
                    profit,
                    revenue > 0 ? profit / revenue * 100 : 0m);
            })
            .OrderByDescending(p => p.Profit)
            .ToList();

        return new ProfitReportDto(totalRevenue, totalCost, totalProfit, marginPercentage, products);
    }
}
