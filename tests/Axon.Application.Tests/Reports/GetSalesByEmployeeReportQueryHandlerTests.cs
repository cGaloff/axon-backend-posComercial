using System.Reflection;
using Axon.Application.Reports.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Axon.Domain.Entities.Sales;

namespace Axon.Application.Tests.Reports;

public class GetSalesByEmployeeReportQueryHandlerTests
{
    private static void SetCreatedAt(Sale sale, DateTime createdAtUtc)
    {
        typeof(Sale).GetProperty(nameof(Sale.CreatedAt), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(sale, createdAtUtc);
    }

    private static Sale CreateCompletedSale(Guid createdBy, decimal amount, DateTime createdAtUtc)
    {
        var sale = Sale.Create(Guid.NewGuid(), createdBy);
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: amount, quantity: 1);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total));
        SetCreatedAt(sale, createdAtUtc);
        return sale;
    }

    [Fact]
    public async Task Handle_WithSalesFromMultipleEmployees_GroupsAndSumsCorrectly()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var cashier1 = User.Create("Ana Cajera", "ana@test.com", "hash", Guid.NewGuid());
        var cashier2 = User.Create("Beto Cajero", "beto@test.com", "hash", Guid.NewGuid());
        dbContext.Users.AddRange(cashier1, cashier2);

        // 24/07/2026 10:00 Colombia = 15:00 UTC (dentro del rango consultado).
        var withinRange = new DateTime(2026, 7, 24, 15, 0, 0, DateTimeKind.Utc);

        var sale1 = CreateCompletedSale(cashier1.Id, 1000m, withinRange);
        var sale2 = CreateCompletedSale(cashier1.Id, 2000m, withinRange);
        var sale3 = CreateCompletedSale(cashier2.Id, 500m, withinRange);

        dbContext.Sales.AddRange(sale1, sale2, sale3);
        await dbContext.SaveChangesAsync();

        var handler = new GetSalesByEmployeeReportQueryHandler(dbContext);

        var query = new GetSalesByEmployeeReportQuery(
            FromDate: new DateTime(2026, 7, 24, 0, 0, 0),
            ToDate: new DateTime(2026, 7, 24, 23, 59, 59));

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.Employees.Count);

        var anaSummary = result.Employees.Single(e => e.UserId == cashier1.Id);
        Assert.Equal("Ana Cajera", anaSummary.UserName);
        Assert.Equal(2, anaSummary.TotalTransactions);
        Assert.Equal(3000m, anaSummary.TotalRevenue);

        var betoSummary = result.Employees.Single(e => e.UserId == cashier2.Id);
        Assert.Equal("Beto Cajero", betoSummary.UserName);
        Assert.Equal(1, betoSummary.TotalTransactions);
        Assert.Equal(500m, betoSummary.TotalRevenue);
    }

    [Fact]
    public async Task Handle_WithNoSalesInRange_ReturnsEmptyListWithoutThrowing()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var handler = new GetSalesByEmployeeReportQueryHandler(dbContext);

        var query = new GetSalesByEmployeeReportQuery(
            FromDate: new DateTime(2026, 1, 1, 0, 0, 0),
            ToDate: new DateTime(2026, 1, 2, 0, 0, 0));

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Empty(result.Employees);
    }
}
