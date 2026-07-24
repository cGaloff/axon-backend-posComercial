using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Suppliers;

// Snapshot de un impuesto aplicado a una línea de compra, tomado de
// ProductTax/TaxType vigentes al momento de crear la orden (mismo patrón que
// Sales.SaleItemTax). Si el catálogo cambia después, esta fila no se ve afectada.
public class PurchaseOrderItemTax
{
    public Guid Id { get; private set; }
    public Guid PurchaseOrderItemId { get; private set; }
    public Guid TaxTypeId { get; private set; }
    public string TaxTypeName { get; private set; } = string.Empty;
    public decimal Percentage { get; private set; }
    public decimal Amount { get; private set; }

    private PurchaseOrderItemTax()
    {
    }

    internal static PurchaseOrderItemTax Create(
        Guid purchaseOrderItemId,
        Guid taxTypeId,
        string taxTypeName,
        decimal percentage,
        decimal amount)
    {
        if (string.IsNullOrWhiteSpace(taxTypeName))
        {
            throw new DomainException("El nombre del impuesto es obligatorio.");
        }

        return new PurchaseOrderItemTax
        {
            Id = Guid.NewGuid(),
            PurchaseOrderItemId = purchaseOrderItemId,
            TaxTypeId = taxTypeId,
            TaxTypeName = taxTypeName,
            Percentage = percentage,
            Amount = amount
        };
    }
}
