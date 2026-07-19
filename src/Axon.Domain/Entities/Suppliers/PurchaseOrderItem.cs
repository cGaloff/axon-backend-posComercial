using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Suppliers;

public class PurchaseOrderItem
{
    public Guid Id { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string ProductSku { get; private set; } = string.Empty;
    public int QuantityOrdered { get; private set; }
    public int QuantityReceived { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal Subtotal { get; private set; }

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
        decimal unitCost)
    {
        if (quantityOrdered <= 0)
        {
            throw new DomainException("La cantidad ordenada debe ser mayor a cero.");
        }

        if (unitCost <= 0)
        {
            throw new DomainException("El costo unitario debe ser mayor a cero.");
        }

        return new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = purchaseOrderId,
            ProductId = productId,
            ProductName = productName,
            ProductSku = productSku,
            QuantityOrdered = quantityOrdered,
            QuantityReceived = 0,
            UnitCost = unitCost,
            Subtotal = quantityOrdered * unitCost
        };
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
