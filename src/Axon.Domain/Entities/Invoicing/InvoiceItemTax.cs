using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Invoicing;

// Snapshot congelado de un impuesto de línea, copiado de SaleItemTax al emitir
// la factura. No tiene ningún método más allá de Create(): es inmutable por
// diseño, no solo por convención (ver InvoiceTests.Invoice_HasNoPublicMutatorMethods).
public class InvoiceItemTax
{
    public Guid Id { get; private set; }
    public Guid InvoiceItemId { get; private set; }
    public Guid TaxTypeId { get; private set; }
    public string TaxTypeName { get; private set; } = string.Empty;
    public decimal Percentage { get; private set; }
    public decimal Amount { get; private set; }

    private InvoiceItemTax()
    {
    }

    internal static InvoiceItemTax Create(
        Guid invoiceItemId,
        Guid taxTypeId,
        string taxTypeName,
        decimal percentage,
        decimal amount)
    {
        if (string.IsNullOrWhiteSpace(taxTypeName))
        {
            throw new DomainException("El nombre del impuesto es obligatorio.");
        }

        return new InvoiceItemTax
        {
            Id = Guid.NewGuid(),
            InvoiceItemId = invoiceItemId,
            TaxTypeId = taxTypeId,
            TaxTypeName = taxTypeName,
            Percentage = percentage,
            Amount = amount
        };
    }
}
