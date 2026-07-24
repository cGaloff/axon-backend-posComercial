using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Sales;

// Snapshot de un impuesto aplicado a una línea de venta en el momento de la
// venta: nombre y porcentaje se copian del catálogo TaxType/ProductTax vigentes
// en ese instante. Si el catálogo o el producto cambian después, este registro
// NO se ve afectado (requisito de auditoría) — a diferencia de ProductTax, que
// sí refleja la configuración vigente del producto.
public class SaleItemTax
{
    public Guid Id { get; private set; }
    public Guid SaleItemId { get; private set; }
    public Guid TaxTypeId { get; private set; }
    public string TaxTypeName { get; private set; } = string.Empty;
    public decimal Percentage { get; private set; }
    public decimal Amount { get; private set; }

    private SaleItemTax()
    {
    }

    public static SaleItemTax Create(
        Guid saleItemId,
        Guid taxTypeId,
        string taxTypeName,
        decimal percentage,
        decimal amount)
    {
        if (string.IsNullOrWhiteSpace(taxTypeName))
        {
            throw new DomainException("El nombre del impuesto es obligatorio.");
        }

        if (percentage < 0)
        {
            throw new DomainException("El porcentaje de impuesto no puede ser negativo.");
        }

        if (amount < 0)
        {
            throw new DomainException("El monto de impuesto no puede ser negativo.");
        }

        return new SaleItemTax
        {
            Id = Guid.NewGuid(),
            SaleItemId = saleItemId,
            TaxTypeId = taxTypeId,
            TaxTypeName = taxTypeName,
            Percentage = percentage,
            Amount = amount
        };
    }
}
