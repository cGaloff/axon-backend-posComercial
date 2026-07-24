using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Invoicing;

// Snapshot congelado de una línea de venta, copiado de SaleItem al emitir la
// factura. Sin métodos de edición: solo lectura tras construirse.
public class InvoiceItem
{
    private readonly List<InvoiceItemTax> _taxes = new();

    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string ProductSku { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal Discount { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal SubtotalBase { get; private set; }

    public IReadOnlyList<InvoiceItemTax> Taxes => _taxes;

    private InvoiceItem()
    {
    }

    internal static InvoiceItem Create(
        Guid invoiceId,
        Guid productId,
        string productName,
        string productSku,
        decimal unitPrice,
        int quantity,
        decimal discount,
        decimal subtotal,
        decimal subtotalBase,
        IReadOnlyList<InvoiceItemTaxSnapshot> taxes)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainException("El nombre del producto es obligatorio.");
        }

        var id = Guid.NewGuid();

        var item = new InvoiceItem
        {
            Id = id,
            InvoiceId = invoiceId,
            ProductId = productId,
            ProductName = productName,
            ProductSku = productSku,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Discount = discount,
            Subtotal = subtotal,
            SubtotalBase = subtotalBase
        };

        item._taxes.AddRange(taxes.Select(t => InvoiceItemTax.Create(id, t.TaxTypeId, t.TaxTypeName, t.Percentage, t.Amount)));

        return item;
    }
}

// Entrada de datos (no una entidad) usada solo para construir InvoiceItem/InvoiceItemTax
// dentro de Invoice.Create: copia los valores ya calculados de SaleItem/SaleItemTax
// tal cual estaban en el momento de emitir la factura.
public record InvoiceItemTaxSnapshot(Guid TaxTypeId, string TaxTypeName, decimal Percentage, decimal Amount);

public record InvoiceItemSnapshot(
    Guid ProductId,
    string ProductName,
    string ProductSku,
    decimal UnitPrice,
    int Quantity,
    decimal Discount,
    decimal Subtotal,
    decimal SubtotalBase,
    IReadOnlyList<InvoiceItemTaxSnapshot> Taxes);
