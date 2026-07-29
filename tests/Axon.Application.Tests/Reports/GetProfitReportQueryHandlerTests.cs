using Axon.Application.Reports.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Entities.Taxes;

namespace Axon.Application.Tests.Reports;

public class GetProfitReportQueryHandlerTests
{
    private static readonly GetProfitReportQuery DefaultRangeQuery = new(
        FromDate: new DateTime(2026, 7, 1, 0, 0, 0),
        ToDate: new DateTime(2026, 7, 31, 0, 0, 0));

    // Caso de negocio explícito: la ganancia real de una línea debe ser el
    // ingreso SIN impuesto (SubtotalBase, ya neto del descuento) menos el costo
    // snapshoteado — no el precio de lista, ni el subtotal con impuesto incluido.
    [Fact]
    public async Task Handle_ComputesTotalsAndPerProductBreakdown_NetOfTaxAndDiscount()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var iva = TaxType.Create(TaxCode.Iva, "IVA", "Impuesto sobre las ventas");

        // iPhone: 130.000 - 11.000 de descuento = 119.000 con IVA 19% -> base
        // gravable 100.000; costo 60.000 -> ganancia 40.000 (40%).
        var iphone = SaleItem.Create(
            sale.Id, Guid.NewGuid(), "iPhone", "SKU-IPHONE",
            unitPrice: 130000m, quantity: 1, discount: 11000m, unitCost: 60000m,
            appliedTaxes: new[] { (iva.Id, "IVA", 19m) });
        sale.AddItem(iphone);

        // Motorola: 50.000 sin descuento ni impuesto; costo 20.000 -> ganancia
        // 30.000 (60%).
        var motorola = SaleItem.Create(
            sale.Id, Guid.NewGuid(), "Motorola", "SKU-MOTOROLA",
            unitPrice: 50000m, quantity: 1, unitCost: 20000m);
        sale.AddItem(motorola);

        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total, amountTendered: sale.Total));

        SetCreatedAt(sale, new DateTime(2026, 7, 15, 15, 0, 0, DateTimeKind.Utc));

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new GetProfitReportQueryHandler(dbContext);

        var result = await handler.Handle(DefaultRangeQuery, CancellationToken.None);

        Assert.Equal(150000m, result.TotalRevenue);
        Assert.Equal(80000m, result.TotalCost);
        Assert.Equal(70000m, result.TotalProfit);
        Assert.Equal(46.67m, Math.Round(result.MarginPercentage, 2));

        Assert.Equal(2, result.Products.Count);

        // Orden descendente por ganancia: iPhone (40.000) antes que Motorola (30.000).
        Assert.Equal("iPhone", result.Products[0].ProductName);
        Assert.Equal(100000m, result.Products[0].Revenue);
        Assert.Equal(60000m, result.Products[0].Cost);
        Assert.Equal(40000m, result.Products[0].Profit);
        Assert.Equal(40m, result.Products[0].MarginPercentage);

        Assert.Equal("Motorola", result.Products[1].ProductName);
        Assert.Equal(50000m, result.Products[1].Revenue);
        Assert.Equal(20000m, result.Products[1].Cost);
        Assert.Equal(30000m, result.Products[1].Profit);
        Assert.Equal(60m, result.Products[1].MarginPercentage);
    }

    // Responde directamente la pregunta de negocio: si el iPhone tiene
    // descuento, su ganancia real debe ser menor que sin descuento.
    [Fact]
    public async Task Handle_WithDiscountedItem_ProducesLowerProfitThanWithoutDiscount()
    {
        await using var dbContextWithDiscount = TestDbContextFactory.Create();
        await using var dbContextWithoutDiscount = TestDbContextFactory.Create();

        var saleWithDiscount = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var discountedItem = SaleItem.Create(
            saleWithDiscount.Id, Guid.NewGuid(), "iPhone", "SKU-IPHONE",
            unitPrice: 1000000m, quantity: 1, discount: 50000m, unitCost: 700000m);
        saleWithDiscount.AddItem(discountedItem);
        saleWithDiscount.AddPayment(SalePayment.Create(saleWithDiscount.Id, PaymentMethod.Cash, saleWithDiscount.Total, amountTendered: saleWithDiscount.Total));
        SetCreatedAt(saleWithDiscount, new DateTime(2026, 7, 15, 15, 0, 0, DateTimeKind.Utc));
        dbContextWithDiscount.Sales.Add(saleWithDiscount);
        await dbContextWithDiscount.SaveChangesAsync();

        var saleWithoutDiscount = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var fullPriceItem = SaleItem.Create(
            saleWithoutDiscount.Id, Guid.NewGuid(), "iPhone", "SKU-IPHONE",
            unitPrice: 1000000m, quantity: 1, unitCost: 700000m);
        saleWithoutDiscount.AddItem(fullPriceItem);
        saleWithoutDiscount.AddPayment(SalePayment.Create(saleWithoutDiscount.Id, PaymentMethod.Cash, saleWithoutDiscount.Total, amountTendered: saleWithoutDiscount.Total));
        SetCreatedAt(saleWithoutDiscount, new DateTime(2026, 7, 15, 15, 0, 0, DateTimeKind.Utc));
        dbContextWithoutDiscount.Sales.Add(saleWithoutDiscount);
        await dbContextWithoutDiscount.SaveChangesAsync();

        var resultWithDiscount = await new GetProfitReportQueryHandler(dbContextWithDiscount).Handle(DefaultRangeQuery, CancellationToken.None);
        var resultWithoutDiscount = await new GetProfitReportQueryHandler(dbContextWithoutDiscount).Handle(DefaultRangeQuery, CancellationToken.None);

        Assert.Equal(250000m, resultWithDiscount.TotalProfit);
        Assert.Equal(300000m, resultWithoutDiscount.TotalProfit);
        Assert.True(resultWithDiscount.TotalProfit < resultWithoutDiscount.TotalProfit);
    }

    [Fact]
    public async Task Handle_ExcludesVoidedSales()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 1000m, quantity: 1, unitCost: 400m);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total, amountTendered: sale.Total));
        sale.Void(Guid.NewGuid(), "Cliente se arrepintió");

        SetCreatedAt(sale, new DateTime(2026, 7, 15, 15, 0, 0, DateTimeKind.Utc));

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new GetProfitReportQueryHandler(dbContext);

        var result = await handler.Handle(DefaultRangeQuery, CancellationToken.None);

        Assert.Equal(0m, result.TotalRevenue);
        Assert.Empty(result.Products);
    }

    private static void SetCreatedAt(Sale sale, DateTime createdAtUtc)
    {
        typeof(Sale).GetProperty(nameof(Sale.CreatedAt), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .SetValue(sale, createdAtUtc);
    }
}
