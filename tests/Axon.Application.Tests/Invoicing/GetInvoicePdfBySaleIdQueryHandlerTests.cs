using Axon.Application.Invoicing.Commands;
using Axon.Application.Invoicing.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;
using TenantConfigEntity = Axon.Domain.Entities.TenantConfig;

namespace Axon.Application.Tests.Invoicing;

public class GetInvoicePdfBySaleIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenSaleHasNoInvoice_Throws()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var config = TenantConfigEntity.Create("Negocio de prueba");

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 100m, quantity: 1);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Card, sale.Total));

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new GetInvoicePdfBySaleIdQueryHandler(dbContext, new FakePdfService(), new FakeTenantConfigRepository(config));

        // Venta con pago por tarjeta aún pendiente de confirmación: nunca se
        // emitió factura, así que ver "la factura de esa venta" debe rechazarse.
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new GetInvoicePdfBySaleIdQuery(sale.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSaleHasInvoice_ReturnsPdf()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var config = TenantConfigEntity.Create("Negocio de prueba");

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 100m, quantity: 1);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total));

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var issueHandler = new IssueInvoiceCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));
        await issueHandler.Handle(new IssueInvoiceCommand(sale.Id), CancellationToken.None);

        var handler = new GetInvoicePdfBySaleIdQueryHandler(dbContext, new FakePdfService(), new FakeTenantConfigRepository(config));

        var pdf = await handler.Handle(new GetInvoicePdfBySaleIdQuery(sale.Id), CancellationToken.None);

        Assert.NotNull(pdf);
    }
}
