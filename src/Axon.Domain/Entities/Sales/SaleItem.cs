using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Sales;

public class SaleItem
{
    private readonly List<SaleItemTax> _taxes = new();

    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string ProductSku { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal Discount { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal SubtotalBase { get; private set; }

    public IReadOnlyList<SaleItemTax> Taxes => _taxes;

    public decimal TotalTaxAmount => _taxes.Sum(t => t.Amount);

    private SaleItem()
    {
    }

    // appliedTaxes es el snapshot de impuestos vigentes al momento de la venta
    // (TaxTypeId + nombre + porcentaje, tomados de ProductTax/TaxType en ese
    // instante). Todos los impuestos se calculan sobre la MISMA base gravable
    // (no son compuestos entre sí) — decisión de diseño documentada en el
    // resumen del prompt 3, consistente con cómo IVA e ICA conviven hoy en
    // Colombia (ambos aplican sobre el valor de la venta, no uno sobre el otro).
    public static SaleItem Create(
        Guid saleId,
        Guid productId,
        string productName,
        string productSku,
        decimal unitPrice,
        int quantity,
        decimal discount = 0,
        IEnumerable<(Guid TaxTypeId, string TaxTypeName, decimal Percentage)>? appliedTaxes = null)
    {
        if (quantity <= 0)
        {
            throw new DomainException("La cantidad debe ser mayor a cero.");
        }

        if (unitPrice <= 0)
        {
            throw new DomainException("El precio unitario debe ser mayor a cero.");
        }

        if (discount < 0)
        {
            throw new DomainException("El descuento no puede ser negativo.");
        }

        var grossSubtotal = unitPrice * quantity;

        if (discount >= grossSubtotal)
        {
            throw new DomainException("El descuento no puede ser mayor al subtotal");
        }

        var taxes = (appliedTaxes ?? Enumerable.Empty<(Guid, string, decimal)>()).ToList();
        var totalTaxRate = taxes.Sum(t => t.Percentage);

        // Subtotal incluye todos los impuestos aplicados (precio final que paga el
        // cliente); SubtotalBase es la base gravable obtenida al "desquitar" la suma
        // de tasas del subtotal con descuento aplicado.
        var subtotal = grossSubtotal - discount;
        var subtotalBase = subtotal / (1 + totalTaxRate / 100);

        var id = Guid.NewGuid();

        var taxSnapshots = taxes
            .Select(t => SaleItemTax.Create(id, t.TaxTypeId, t.TaxTypeName, t.Percentage, subtotalBase * t.Percentage / 100))
            .ToList();

        var item = new SaleItem
        {
            Id = id,
            SaleId = saleId,
            ProductId = productId,
            ProductName = productName,
            ProductSku = productSku,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Discount = discount,
            Subtotal = subtotal,
            SubtotalBase = subtotalBase
        };

        item._taxes.AddRange(taxSnapshots);

        return item;
    }
}
