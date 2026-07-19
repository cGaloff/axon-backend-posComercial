namespace Axon.Domain.Entities.Suppliers;

public class PurchaseReceiptItem
{
    public Guid Id { get; private set; }
    public Guid PurchaseReceiptId { get; private set; }
    public Guid PurchaseOrderItemId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int QuantityReceived { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal Subtotal { get; private set; }

    private PurchaseReceiptItem()
    {
    }

    public static PurchaseReceiptItem Create(
        Guid purchaseReceiptId,
        Guid purchaseOrderItemId,
        Guid productId,
        string productName,
        int quantityReceived,
        decimal unitCost)
    {
        return new PurchaseReceiptItem
        {
            Id = Guid.NewGuid(),
            PurchaseReceiptId = purchaseReceiptId,
            PurchaseOrderItemId = purchaseOrderItemId,
            ProductId = productId,
            ProductName = productName,
            QuantityReceived = quantityReceived,
            UnitCost = unitCost,
            Subtotal = quantityReceived * unitCost
        };
    }
}
