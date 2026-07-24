using Axon.Application.Invoicing.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Entities.Taxes;
using Axon.Domain.Exceptions;
using TenantConfigEntity = Axon.Domain.Entities.TenantConfig;

namespace Axon.Application.Tests.Invoicing;

public class IssueInvoiceCommandHandlerTests
{
    private static Sale CreateCompletedSaleWithTax(Guid taxTypeId, string taxTypeName)
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(
            sale.Id, Guid.NewGuid(), "Producto de prueba", "SKU-001",
            unitPrice: 119m, quantity: 1,
            appliedTaxes: new[] { (taxTypeId, taxTypeName, 19m) });
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total));
        return sale;
    }

    [Fact]
    public async Task Handle_ForCompletedSale_CreatesInvoiceWithNumberOneAndFullSnapshot()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var config = TenantConfigEntity.Create("Negocio de prueba");
        var taxTypeId = Guid.NewGuid();

        var sale = CreateCompletedSaleWithTax(taxTypeId, "IVA");
        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new IssueInvoiceCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var result = await handler.Handle(new IssueInvoiceCommand(sale.Id), CancellationToken.None);

        Assert.Equal(1, result.Number);

        var invoice = await dbContext.Invoices.FindAsync(result.InvoiceId);
        Assert.NotNull(invoice);
        Assert.Equal(sale.Id, invoice!.SaleId);
        Assert.Equal(sale.Total, invoice.Total);

        var item = Assert.Single(invoice.Items);
        Assert.Equal("Producto de prueba", item.ProductName);
        var tax = Assert.Single(item.Taxes);
        Assert.Equal("IVA", tax.TaxTypeName);

        var payment = Assert.Single(invoice.Payments);
        Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal(sale.Total, payment.Amount);
    }

    [Fact]
    public async Task Handle_CalledTwiceForSameSale_IsIdempotent()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var config = TenantConfigEntity.Create("Negocio de prueba");

        var sale = CreateCompletedSaleWithTax(Guid.NewGuid(), "IVA");
        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new IssueInvoiceCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var firstResult = await handler.Handle(new IssueInvoiceCommand(sale.Id), CancellationToken.None);
        var secondResult = await handler.Handle(new IssueInvoiceCommand(sale.Id), CancellationToken.None);

        Assert.Equal(firstResult.InvoiceId, secondResult.InvoiceId);
        Assert.Equal(firstResult.Number, secondResult.Number);
        Assert.Single(dbContext.Invoices);
    }

    [Fact]
    public async Task Handle_SecondSaleInSameTenant_GetsIncrementingNumber()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var config = TenantConfigEntity.Create("Negocio de prueba");

        var sale1 = CreateCompletedSaleWithTax(Guid.NewGuid(), "IVA");
        var sale2 = CreateCompletedSaleWithTax(Guid.NewGuid(), "IVA");
        dbContext.Sales.AddRange(sale1, sale2);
        await dbContext.SaveChangesAsync();

        var handler = new IssueInvoiceCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var result1 = await handler.Handle(new IssueInvoiceCommand(sale1.Id), CancellationToken.None);
        var result2 = await handler.Handle(new IssueInvoiceCommand(sale2.Id), CancellationToken.None);

        Assert.Equal(1, result1.Number);
        Assert.Equal(2, result2.Number);
    }

    // Proxy de "no colisiona entre tenants": cada TestDbContextFactory.Create()
    // es una base de datos completamente independiente (equivalente, para este
    // propósito, a un schema de tenant distinto en Postgres). En producción la
    // garantía real es aún más fuerte: cada tenant tiene su PROPIA secuencia
    // invoice_number_seq en su PROPIO schema — objetos de Postgres separados que
    // no pueden colisionar entre sí por construcción (ver tenant_schema_template.sql).
    [Fact]
    public async Task Handle_TwoIndependentTenantContexts_BothStartNumberingAtOne()
    {
        await using var dbContextTenantA = TestDbContextFactory.Create();
        await using var dbContextTenantB = TestDbContextFactory.Create();
        var config = TenantConfigEntity.Create("Negocio de prueba");

        var saleA = CreateCompletedSaleWithTax(Guid.NewGuid(), "IVA");
        dbContextTenantA.Sales.Add(saleA);
        await dbContextTenantA.SaveChangesAsync();

        var saleB = CreateCompletedSaleWithTax(Guid.NewGuid(), "IVA");
        dbContextTenantB.Sales.Add(saleB);
        await dbContextTenantB.SaveChangesAsync();

        var handlerA = new IssueInvoiceCommandHandler(
            dbContextTenantA, new FakeUnitOfWork(dbContextTenantA), new FakePdfService(), new FakeTenantConfigRepository(config));
        var handlerB = new IssueInvoiceCommandHandler(
            dbContextTenantB, new FakeUnitOfWork(dbContextTenantB), new FakePdfService(), new FakeTenantConfigRepository(config));

        var resultA = await handlerA.Handle(new IssueInvoiceCommand(saleA.Id), CancellationToken.None);
        var resultB = await handlerB.Handle(new IssueInvoiceCommand(saleB.Id), CancellationToken.None);

        Assert.Equal(1, resultA.Number);
        Assert.Equal(1, resultB.Number);
    }

    [Fact]
    public async Task Handle_ForPendingPaymentSale_Throws()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var config = TenantConfigEntity.Create("Negocio de prueba");

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 100m, quantity: 1);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Card, sale.Total));

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new IssueInvoiceCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new IssueInvoiceCommand(sale.Id), CancellationToken.None));
    }

    // Snapshot de impuestos y pagos en la factura no cambia si el producto o el
    // catálogo de impuestos se modifican después de emitida.
    [Fact]
    public async Task Handle_InvoiceSnapshotUnaffectedByLaterTaxTypeChanges()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var config = TenantConfigEntity.Create("Negocio de prueba");

        var taxType = TaxType.Create("IVA", "IVA");
        dbContext.TaxTypes.Add(taxType);

        var sale = CreateCompletedSaleWithTax(taxType.Id, "IVA");
        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var handler = new IssueInvoiceCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var result = await handler.Handle(new IssueInvoiceCommand(sale.Id), CancellationToken.None);

        // El catálogo cambia DESPUÉS de emitida la factura.
        taxType.Update("IVA (renombrado)", "IVA2");

        var invoice = await dbContext.Invoices.FindAsync(result.InvoiceId);
        var tax = Assert.Single(Assert.Single(invoice!.Items).Taxes);

        Assert.Equal("IVA", tax.TaxTypeName);
        Assert.Equal(19m, tax.Percentage);
    }
}
