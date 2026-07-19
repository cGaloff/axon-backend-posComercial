namespace Axon.Domain.Entities.Suppliers;

public class PurchaseReceipt
{
    private readonly List<PurchaseReceiptItem> _items = new();

    public Guid Id { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid ReceivedBy { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public string? Notes { get; private set; }
    public decimal TotalReceived { get; private set; }

    public IReadOnlyList<PurchaseReceiptItem> Items => _items;

    private PurchaseReceipt()
    {
    }

    public static PurchaseReceipt Create(Guid purchaseOrderId, Guid receivedBy, string? notes = null)
    {
        return new PurchaseReceipt
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = purchaseOrderId,
            ReceivedBy = receivedBy,
            ReceivedAt = DateTime.UtcNow,
            Notes = notes,
            TotalReceived = 0
        };
    }

    public void AddItem(PurchaseReceiptItem item)
    {
        _items.Add(item);
        TotalReceived += item.Subtotal;
    }
}
