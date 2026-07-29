using Axon.Application.Invoicing.Commands;
using Axon.Application.Sales.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Sales;
using TenantConfigEntity = Axon.Domain.Entities.TenantConfig;

namespace Axon.Application.Tests.Sales;

public class GetSalesHistoryQueryHandlerTests
{
    // Desde el historial de ventas debe poder verse si una venta ya tiene
    // factura (y su número), para poder ir a descargar su PDF por separado
    // (GET /api/sales/{id}/invoice) — null si todavía no se ha facturado.
    [Fact]
    public async Task Handle_IncludesInvoiceNumberOnlyForInvoicedSales()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var config = TenantConfigEntity.Create("Negocio de prueba");

        var invoicedSale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var invoicedItem = SaleItem.Create(invoicedSale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 100m, quantity: 1);
        invoicedSale.AddItem(invoicedItem);
        invoicedSale.AddPayment(SalePayment.Create(invoicedSale.Id, PaymentMethod.Cash, invoicedSale.Total));

        // No se llama a IssueInvoiceCommandHandler para esta venta: se prueba el caso
        // "todavía no facturada", sin importar el método de pago (todos completan de
        // inmediato — ver Sale.AddPayment).
        var uninvoicedSale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var uninvoicedItem = SaleItem.Create(uninvoicedSale.Id, Guid.NewGuid(), "Producto", "SKU-002", unitPrice: 100m, quantity: 1);
        uninvoicedSale.AddItem(uninvoicedItem);
        uninvoicedSale.AddPayment(SalePayment.Create(uninvoicedSale.Id, PaymentMethod.Cash, uninvoicedSale.Total));

        dbContext.Sales.AddRange(invoicedSale, uninvoicedSale);
        await dbContext.SaveChangesAsync();

        var issueHandler = new IssueInvoiceCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));
        await issueHandler.Handle(new IssueInvoiceCommand(invoicedSale.Id), CancellationToken.None);

        var handler = new GetSalesHistoryQueryHandler(dbContext);

        var result = await handler.Handle(new GetSalesHistoryQuery(null, null, null, null), CancellationToken.None);
        var items = result.Items.ToList();

        var invoicedDto = items.Single(s => s.Id == invoicedSale.Id);
        var uninvoicedDto = items.Single(s => s.Id == uninvoicedSale.Id);

        Assert.Equal(1, invoicedDto.InvoiceNumber);
        Assert.Null(uninvoicedDto.InvoiceNumber);
    }

    // Reportado por negocio: el historial de ventas debe mostrar la
    // identificación del cliente (CC/NIT) en vez de un hueco en blanco. Sin
    // documento, el campo queda vacío (no tiene default, a diferencia del
    // nombre, que sí cae en "Consumidor Final").
    [Fact]
    public async Task Handle_ExposesCustomerDocumentTypeAndNumber()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var identifiedSale = Sale.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            customerName: "Juan Pérez",
            customerDocumentType: CustomerDocumentType.Cc,
            customerDocumentNumber: "1002003004");
        var identifiedItem = SaleItem.Create(identifiedSale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 100m, quantity: 1);
        identifiedSale.AddItem(identifiedItem);
        identifiedSale.AddPayment(SalePayment.Create(identifiedSale.Id, PaymentMethod.Cash, identifiedSale.Total));

        var anonymousSale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var anonymousItem = SaleItem.Create(anonymousSale.Id, Guid.NewGuid(), "Producto", "SKU-002", unitPrice: 100m, quantity: 1);
        anonymousSale.AddItem(anonymousItem);
        anonymousSale.AddPayment(SalePayment.Create(anonymousSale.Id, PaymentMethod.Cash, anonymousSale.Total));

        dbContext.Sales.AddRange(identifiedSale, anonymousSale);
        await dbContext.SaveChangesAsync();

        var handler = new GetSalesHistoryQueryHandler(dbContext);

        var result = await handler.Handle(new GetSalesHistoryQuery(null, null, null, null), CancellationToken.None);
        var items = result.Items.ToList();

        var identifiedDto = items.Single(s => s.Id == identifiedSale.Id);
        var anonymousDto = items.Single(s => s.Id == anonymousSale.Id);

        Assert.Equal(CustomerDocumentType.Cc, identifiedDto.CustomerDocumentType);
        Assert.Equal("1002003004", identifiedDto.CustomerDocumentNumber);

        Assert.Null(anonymousDto.CustomerDocumentType);
        Assert.Equal(string.Empty, anonymousDto.CustomerDocumentNumber);
        Assert.Equal("Consumidor Final", anonymousDto.CustomerName);
    }

    // Bug reportado por frontend: un filtro de un solo día (from == to, ambos a
    // medianoche, como envía un selector de fecha simple) no mostraba ninguna
    // venta, porque el rango resultante medianoche-a-medianoche cubre 0
    // segundos. Además, From/To representan el día calendario en hora Colombia,
    // no UTC (ver ColombiaTime.ToUtc).
    [Fact]
    public async Task Handle_WithSingleDayFilter_IncludesSalesFromThatDayInColombiaTime()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 1000m, quantity: 1);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total));

        // 24/07/2026 10:00 hora Colombia (UTC-5) == 24/07/2026 15:00 UTC.
        typeof(Sale).GetProperty(nameof(Sale.CreatedAt), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .SetValue(sale, new DateTime(2026, 7, 24, 15, 0, 0, DateTimeKind.Utc));

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new GetSalesHistoryQueryHandler(dbContext);

        // El caller filtra "el día 24/07/2026" enviando la misma fecha en from y
        // to, como hace un selector de un solo día.
        var query = new GetSalesHistoryQuery(
            From: new DateTime(2026, 7, 24, 0, 0, 0),
            To: new DateTime(2026, 7, 24, 0, 0, 0),
            Status: null,
            CustomerId: null);

        var result = await handler.Handle(query, CancellationToken.None);

        var dto = Assert.Single(result.Items);
        Assert.Equal(sale.Id, dto.Id);
    }
}
