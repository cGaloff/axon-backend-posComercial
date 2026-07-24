using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Invoicing;

// Snapshot congelado de un pago, copiado de SalePayment al emitir la factura.
// Sin métodos de edición: solo lectura tras construirse (ver Invoice.Create).
public class InvoicePayment
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public decimal? AmountTendered { get; private set; }
    public decimal? Change { get; private set; }

    private InvoicePayment()
    {
    }

    internal static InvoicePayment Create(
        Guid invoiceId,
        PaymentMethod method,
        decimal amount,
        decimal? amountTendered,
        decimal? change)
    {
        if (amount <= 0)
        {
            throw new DomainException("El monto del pago debe ser mayor a cero.");
        }

        return new InvoicePayment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            Method = method,
            Amount = amount,
            AmountTendered = amountTendered,
            Change = change
        };
    }
}
