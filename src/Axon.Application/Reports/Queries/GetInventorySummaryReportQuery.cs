using MediatR;

namespace Axon.Application.Reports.Queries;

public record GetInventorySummaryReportQuery : IRequest<InventorySummaryReportDto>;

public record InventorySummaryReportDto(
    int TotalProductsInStock,
    decimal TotalInventoryValue,
    int LowStockProductsCount,
    List<TopSellingProductDto> TopSellingProducts);

public record TopSellingProductDto(Guid ProductId, string Name, int QuantitySold, decimal RevenueGenerated);
