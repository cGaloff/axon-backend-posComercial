using MediatR;

namespace Axon.Application.Reports.Queries;

public record GetInventorySummaryReportQuery : IRequest<InventorySummaryReportDto>;

public record InventorySummaryReportDto(
    int TotalProductsInStock,
    decimal TotalInventoryValue,
    int LowStockProductsCount,
    List<TopSellingProductDto> TopSellingProducts,
    List<PendingStockAlertDto> PendingStockAlerts);

public record TopSellingProductDto(Guid ProductId, string Name, int QuantitySold, decimal RevenueGenerated);

public record PendingStockAlertDto(
    Guid ProductId,
    string ProductName,
    int CurrentStock,
    int MinStock,
    DateTime CreatedAt);
