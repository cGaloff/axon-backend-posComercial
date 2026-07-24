using System.Reflection;
using Axon.Application.Invoicing.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Invoicing;
using Axon.Domain.Entities.Sales;

namespace Axon.Application.Tests.Invoicing;

public class GetInvoicesQueryHandlerTests
{
    private static Invoice CreateInvoice(Guid saleId, long number, DateTime issuedAtUtc)
    {
        var items = new List<InvoiceItemSnapshot>
        {
            new(Guid.NewGuid(), "Producto", "SKU-001", 100m, 1, 0m, 100m, 100m, new List<InvoiceItemTaxSnapshot>())
        };
        var payments = new List<InvoicePaymentSnapshot> { new(PaymentMethod.Cash, 100m, 100m, 0m) };

        var invoice = Invoice.Create(saleId, number, $"VTA-{number}", "Cliente", 100m, items, payments);

        typeof(Invoice).GetProperty(nameof(Invoice.IssuedAt), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(invoice, issuedAtUtc);

        return invoice;
    }

    // Filtro por rango de fecha Y HORA (no solo fecha): dos facturas emitidas el
    // mismo día calendario en Colombia, una en la mañana y otra en la noche;
    // filtrar solo por la mañana debe excluir la de la noche.
    [Fact]
    public async Task Handle_FiltersByDateAndTimeRange_NotJustDate()
    {
        await using var dbContext = TestDbContextFactory.Create();

        // 24/07/2026 08:00 Colombia (UTC-5) = 13:00 UTC.
        var morningInvoice = CreateInvoice(Guid.NewGuid(), 1, new DateTime(2026, 7, 24, 13, 0, 0, DateTimeKind.Utc));

        // 24/07/2026 20:00 Colombia (UTC-5) = 01:00 UTC del 25/07/2026.
        var nightInvoice = CreateInvoice(Guid.NewGuid(), 2, new DateTime(2026, 7, 25, 1, 0, 0, DateTimeKind.Utc));

        dbContext.Invoices.AddRange(morningInvoice, nightInvoice);
        await dbContext.SaveChangesAsync();

        var handler = new GetInvoicesQueryHandler(dbContext);

        // Rango: 24/07/2026 de 00:00 a 12:00 hora Colombia — solo debe incluir la
        // factura de la mañana, aunque ambas caen en el mismo día calendario.
        var query = new GetInvoicesQuery(
            From: new DateTime(2026, 7, 24, 0, 0, 0),
            To: new DateTime(2026, 7, 24, 12, 0, 0));

        var result = await handler.Handle(query, CancellationToken.None);

        var onlyInvoice = Assert.Single(result.Items);
        Assert.Equal(morningInvoice.Id, onlyInvoice.Id);
    }

    [Fact]
    public async Task Handle_WithoutDateFilters_ReturnsAllInvoicesOrderedByNumberDescending()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var invoice1 = CreateInvoice(Guid.NewGuid(), 1, DateTime.UtcNow.AddDays(-1));
        var invoice2 = CreateInvoice(Guid.NewGuid(), 2, DateTime.UtcNow);

        dbContext.Invoices.AddRange(invoice1, invoice2);
        await dbContext.SaveChangesAsync();

        var handler = new GetInvoicesQueryHandler(dbContext);

        var result = await handler.Handle(new GetInvoicesQuery(From: null, To: null), CancellationToken.None);
        var items = result.Items.ToList();

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, items[0].Number);
        Assert.Equal(1, items[1].Number);
    }
}
