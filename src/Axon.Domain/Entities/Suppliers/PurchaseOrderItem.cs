using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Suppliers;

public class PurchaseOrderItem
{
    private readonly List<PurchaseOrderItemTax> _taxes = new();

    public Guid Id { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string ProductSku { get; private set; } = string.Empty;
    public int QuantityOrdered { get; private set; }
    public int QuantityReceived { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal TaxAmount { get; private set; }

    // Lo que realmente se le debe al proveedor por esta línea (base + impuestos).
    // A diferencia de SaleItem (precio ya incluye impuesto, se "desquita" la
    // base), en compras el costo se cotiza SIN impuesto y el impuesto se suma
    // encima — convención opuesta pero igual de válida para cada lado del negocio.
    public decimal Total => Subtotal + TaxAmount;

    public IReadOnlyList<PurchaseOrderItemTax> Taxes => _taxes;

    public int PendingQuantity => QuantityOrdered - QuantityReceived;
    public bool IsFullyReceived => QuantityReceived >= QuantityOrdered;

    private PurchaseOrderItem()
    {
    }

    public static PurchaseOrderItem Create(
        Guid purchaseOrderId,
        Guid productId,
        string productName,
        string productSku,
        int quantityOrdered,
        decimal unitCost,
        IEnumerable<(Guid TaxTypeId, string TaxTypeName, decimal Percentage)>? appliedTaxes = null)
    {
        if (quantityOrdered <= 0)
        {
            throw new DomainException("La cantidad ordenada debe ser mayor a cero.");
        }

        if (unitCost <= 0)
        {
            throw new DomainException("El costo unitario debe ser mayor a cero.");
        }

        var subtotal = quantityOrdered * unitCost;
        var taxes = (appliedTaxes ?? Enumerable.Empty<(Guid, string, decimal)>()).ToList();

        var id = Guid.NewGuid();

        var taxSnapshots = taxes
            .Select(t => PurchaseOrderItemTax.Create(id, t.TaxTypeId, t.TaxTypeName, t.Percentage, subtotal * t.Percentage / 100))
            .ToList();

        var item = new PurchaseOrderItem
        {
            Id = id,
            PurchaseOrderId = purchaseOrderId,
            ProductId = productId,
            ProductName = productName,
            ProductSku = productSku,
            QuantityOrdered = quantityOrdered,
            QuantityReceived = 0,
            UnitCost = unitCost,
            Subtotal = subtotal,
            TaxAmount = taxSnapshots.Sum(t => t.Amount)
        };

        item._taxes.AddRange(taxSnapshots);

        return item;
    }

    public void RegisterReception(int quantity)
    {
        if (quantity > PendingQuantity)
        {
            throw new DomainException(
                $"Cantidad a recibir ({quantity}) supera la pendiente ({PendingQuantity})");
        }

        QuantityReceived += quantity;
    }
}
