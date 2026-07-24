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

        var pendingSale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var pendingItem = SaleItem.Create(pendingSale.Id, Guid.NewGuid(), "Producto", "SKU-002", unitPrice: 100m, quantity: 1);
        pendingSale.AddItem(pendingItem);
        pendingSale.AddPayment(SalePayment.Create(pendingSale.Id, PaymentMethod.Card, pendingSale.Total));

        dbContext.Sales.AddRange(invoicedSale, pendingSale);
        await dbContext.SaveChangesAsync();

        var issueHandler = new IssueInvoiceCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));
        await issueHandler.Handle(new IssueInvoiceCommand(invoicedSale.Id), CancellationToken.None);

        var handler = new GetSalesHistoryQueryHandler(dbContext);

        var result = await handler.Handle(new GetSalesHistoryQuery(null, null, null, null), CancellationToken.None);
        var items = result.Items.ToList();

        var invoicedDto = items.Single(s => s.Id == invoicedSale.Id);
        var pendingDto = items.Single(s => s.Id == pendingSale.Id);

        Assert.Equal(1, invoicedDto.InvoiceNumber);
        Assert.Null(pendingDto.InvoiceNumber);
    }
}
