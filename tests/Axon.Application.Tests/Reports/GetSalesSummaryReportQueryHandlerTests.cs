using System.Reflection;
using Axon.Application.Reports.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Sales;

namespace Axon.Application.Tests.Reports;

public class GetSalesSummaryReportQueryHandlerTests
{
    // Bug 4 (reporte de ventas incompleto): Sale.CreatedAt se guarda en UTC, pero
    // FromDate/ToDate representan los límites del día calendario en hora Colombia
    // (UTC-5). El código anterior comparaba esos límites directamente contra
    // CreatedAt sin convertir, así que una venta hecha tarde en la noche en Colombia
    // (que ya cae en el día UTC siguiente) quedaba fuera del reporte de "hoy".
    // Este test falla en rojo contra el código anterior (la venta no aparece) y pasa
    // en verde con la conversión de zona horaria aplicada al filtro.
    [Fact]
    public async Task Handle_IncludesSaleMadeLateAtNightInColombiaButAlreadyNextDayInUtc()
    {
        await using var dbContext = TestDbContextFactory.Create();

        // 24/07/2026 23:30 hora Colombia (UTC-5) == 25/07/2026 04:30 UTC.
        var saleCreatedAtUtc = new DateTime(2026, 7, 25, 4, 30, 0, DateTimeKind.Utc);

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var saleItem = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 1000m, quantity: 1);
        sale.AddItem(saleItem);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, 1000m));

        SetCreatedAt(sale, saleCreatedAtUtc);

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new GetSalesSummaryReportQueryHandler(dbContext);

        // El caller pide "el día 24/07/2026 completo, hora Colombia".
        var query = new GetSalesSummaryReportQuery(
            FromDate: new DateTime(2026, 7, 24, 0, 0, 0),
            ToDate: new DateTime(2026, 7, 24, 23, 59, 59));

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(1, result.TotalTransactions);
        Assert.Equal(1000m, result.TotalRevenue);
    }

    private static void SetCreatedAt(Sale sale, DateTime createdAtUtc)
    {
        typeof(Sale).GetProperty(nameof(Sale.CreatedAt), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(sale, createdAtUtc);
    }
}
