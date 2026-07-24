using Axon.Application.Interfaces;
using Axon.Domain.Entities.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Reports.Queries;

public class GetInventorySummaryReportQueryHandler : IRequestHandler<GetInventorySummaryReportQuery, InventorySummaryReportDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetInventorySummaryReportQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InventorySummaryReportDto> Handle(GetInventorySummaryReportQuery request, CancellationToken cancellationToken)
    {
        var activeProducts = await _dbContext.Products
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);

        var totalProductsInStock = activeProducts.Count(p => p.Stock > 0);
        var totalInventoryValue = activeProducts.Sum(p => p.Stock * p.Cost);
        var lowStockProductsCount = activeProducts.Count(p => p.Stock <= p.MinStock);

        // Se agrupa en memoria (no via GroupBy traducido a SQL) para no depender de
        // cómo EF Core traduce GroupBy sobre una colección owned (Sale.Items).
        var completedSales = await _dbContext.Sales
            .Include(s => s.Items)
            .Where(s => s.Status == SaleStatus.Completed)
            .ToListAsync(cancellationToken);

        var topSellingProducts = completedSales
            .SelectMany(s => s.Items)
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new TopSellingProductDto(g.Key.ProductId, g.Key.ProductName, g.Sum(i => i.Quantity), g.Sum(i => i.Subtotal)))
            .OrderByDescending(t => t.QuantitySold)
            .Take(10)
            .ToList();

        // Sección de "acciones": alertas de stock bajo aún no atendidas (IsRead = false),
        // generadas por ProcessSaleCommandHandler/AdjustStockCommandHandler pero que hasta
        // ahora ningún reporte ni endpoint exponía.
        var pendingStockAlerts = await (
            from alert in _dbContext.StockAlerts
            join product in _dbContext.Products on alert.ProductId equals product.Id
            where !alert.IsRead
            orderby alert.CreatedAt descending
            select new PendingStockAlertDto(alert.ProductId, product.Name, alert.CurrentStock, alert.MinStock, alert.CreatedAt))
            .Take(20)
            .ToListAsync(cancellationToken);

        return new InventorySummaryReportDto(
            totalProductsInStock,
            totalInventoryValue,
            lowStockProductsCount,
            topSellingProducts,
            pendingStockAlerts);
    }
}
