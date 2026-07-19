namespace Axon.API.DTOs.Suppliers;

public class ReceivePurchaseOrderRequest
{
    public List<ReceiptItemDto> Items { get; set; } = new();
    public string? Notes { get; set; }
}

public class ReceiptItemDto
{
    public Guid PurchaseOrderItemId { get; set; }
    public int QuantityReceived { get; set; }
}
