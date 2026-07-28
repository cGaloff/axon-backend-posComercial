using Axon.Application.Invoicing.Commands;
using Axon.Application.Sales.Commands;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Sales;
using CashRegisterEntity = Axon.Domain.Entities.CashRegister.CashRegister;
using CashSessionEntity = Axon.Domain.Entities.CashRegister.CashSession;
using TenantConfigEntity = Axon.Domain.Entities.TenantConfig;

namespace Axon.Application.Tests.Sales;

// Deuda técnica reportada por frontend: no se generaba factura cuando el pago
// era Tarjeta, Transferencia, Crédito o una división de pago que incluyera
// alguno de esos métodos. Causa raíz: Sale.AddPayment dejaba la venta en
// PendingPayment para Tarjeta/Transferencia, esperando una confirmación
// externa (ConfirmSalePaymentCommand vía webhook) que nunca llega porque no
// hay ninguna pasarela de pago real integrada — en una venta presencial el
// cobro con datáfono propio o transferencia ya está confirmado en el momento.
public class ProcessSaleCommandHandlerInvoicingTests
{
    private static async Task<(ProcessSaleCommandHandler Handler, Axon.Infrastructure.Persistence.TenantDbContext DbContext, Product Product, CashRegisterEntity CashRegister)> ArrangeAsync()
    {
        var dbContext = TestDbContextFactory.Create();

        var category = Category.Create("Categoria de prueba", "");
        var unit = Unit.Create("Unidad", "und");
        var warehouse = Warehouse.Create("Bodega principal", "Bodega por defecto", isDefault: true);
        var cashRegister = CashRegisterEntity.Create("Caja principal", "Caja por defecto", isDefault: true);

        var product = Product.Create("SKU-INVOICE", "Producto facturable", 10000m, 5000m, 0, category.Id, unit.Id);
        product.AdjustStock(10);

        var userId = Guid.NewGuid();
        var cashSession = CashSessionEntity.Create(cashRegister.Id, userId, initialAmount: 0m);
        var config = TenantConfigEntity.Create("Negocio de prueba");

        dbContext.Categories.Add(category);
        dbContext.Units.Add(unit);
        dbContext.Warehouses.Add(warehouse);
        dbContext.CashRegisters.Add(cashRegister);
        dbContext.Products.Add(product);
        dbContext.CashSessions.Add(cashSession);
        await dbContext.SaveChangesAsync();

        var issueInvoiceHandler = new IssueInvoiceCommandHandler(
            dbContext, new FakeUnitOfWork(dbContext), new FakePdfService(), new FakeTenantConfigRepository(config));

        var handler = new ProcessSaleCommandHandler(
            dbContext,
            new FakeUnitOfWork(dbContext),
            new FakeCashSessionRepository(dbContext),
            new FakeCurrentUserContext { UserId = userId },
            new FakeEmailService(),
            new FakeMediator(issueInvoiceHandler));

        return (handler, dbContext, product, cashRegister);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash)]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.Transfer)]
    [InlineData(PaymentMethod.Credit)]
    public async Task Handle_WithAnyPaymentMethod_CompletesSaleAndIssuesInvoiceImmediately(PaymentMethod method)
    {
        var (handler, dbContext, product, cashRegister) = await ArrangeAsync();

        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 1) },
            Payments: new List<SalePaymentRequest> { new(method, 10000m, method == PaymentMethod.Cash ? 10000m : null) },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(SaleStatus.Completed, result.Status);
        Assert.NotNull(result.InvoiceNumber);
        Assert.NotNull(result.PdfReceipt);
        Assert.Single(dbContext.Invoices);
    }

    // División de pago que incluye Tarjeta: antes quedaba PendingPayment y sin
    // factura; ahora completa y factura de inmediato, igual que un pago único.
    [Fact]
    public async Task Handle_WithSplitPaymentIncludingCard_CompletesSaleAndIssuesInvoiceImmediately()
    {
        var (handler, dbContext, product, cashRegister) = await ArrangeAsync();

        var command = new ProcessSaleCommand(
            Items: new List<SaleItemRequest> { new(product.Id, Quantity: 1) },
            Payments: new List<SalePaymentRequest>
            {
                new(PaymentMethod.Cash, 4000m, 4000m),
                new(PaymentMethod.Card, 6000m)
            },
            CashRegisterId: cashRegister.Id,
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(SaleStatus.Completed, result.Status);
        Assert.NotNull(result.InvoiceNumber);
        Assert.Single(dbContext.Invoices);
    }
}
