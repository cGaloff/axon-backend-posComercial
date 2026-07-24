using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Invoicing;

// Entrada de datos (no una entidad) usada solo para construir InvoicePayment
// dentro de Invoice.Create: copia los valores ya calculados de SalePayment tal
// cual estaban en el momento de emitir la factura.
public record InvoicePaymentSnapshot(PaymentMethod Method, decimal Amount, decimal? AmountTendered, decimal? Change);

// Registro auditable interno de una venta ya completada (no numeración legal
// tipo DIAN). Es inmutable por diseño: Create() es el ÚNICO punto de entrada,
// construye Items/Taxes/Payments de una sola vez a partir de un snapshot ya
// calculado, y la clase no expone ningún otro método público — no hay forma de
// alterar una Invoice después de creada, ni siquiera internamente (no existe el
// método), a diferencia de validar-y-rechazar una edición.
public class Invoice
{
    private readonly List<InvoiceItem> _items = new();
    private readonly List<InvoicePayment> _payments = new();

    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public long Number { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public string SaleNumber { get; private set; } = string.Empty;
    public string CustomerName { get; private set; } = string.Empty;
    public decimal Total { get; private set; }

    public IReadOnlyList<InvoiceItem> Items => _items;
    public IReadOnlyList<InvoicePayment> Payments => _payments;

    private Invoice()
    {
    }

    public static Invoice Create(
        Guid saleId,
        long number,
        string saleNumber,
        string customerName,
        decimal total,
        IReadOnlyList<InvoiceItemSnapshot> items,
        IReadOnlyList<InvoicePaymentSnapshot> payments)
    {
        if (saleId == Guid.Empty)
        {
            throw new DomainException("La venta de origen es obligatoria.");
        }

        if (number <= 0)
        {
            throw new DomainException("El número de factura debe ser mayor a cero.");
        }

        if (items.Count == 0)
        {
            throw new DomainException("La factura debe tener al menos un ítem.");
        }

        if (payments.Count == 0)
        {
            throw new DomainException("La factura debe tener al menos un pago.");
        }

        var invoiceId = Guid.NewGuid();

        var invoice = new Invoice
        {
            Id = invoiceId,
            SaleId = saleId,
            Number = number,
            IssuedAt = DateTime.UtcNow,
            SaleNumber = saleNumber,
            CustomerName = customerName ?? string.Empty,
            Total = total
        };

        invoice._items.AddRange(items.Select(i => InvoiceItem.Create(
            invoiceId,
            i.ProductId,
            i.ProductName,
            i.ProductSku,
            i.UnitPrice,
            i.Quantity,
            i.Discount,
            i.Subtotal,
            i.SubtotalBase,
            i.Taxes)));

        invoice._payments.AddRange(payments.Select(p => InvoicePayment.Create(
            invoiceId,
            p.Method,
            p.Amount,
            p.AmountTendered,
            p.Change)));

        return invoice;
    }
}
