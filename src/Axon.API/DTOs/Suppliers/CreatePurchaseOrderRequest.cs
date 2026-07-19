namespace Axon.API.DTOs.Suppliers;

public class CreatePurchaseOrderRequest
{
    public Guid SupplierId { get; set; }
    public List<PurchaseOrderItemDto> Items { get; set; } = new();
    public DateTime? ExpectedDate { get; set; }
    public string? Notes { get; set; }
}

public class PurchaseOrderItemDto
{
    public Guid ProductId { get; set; }
    public int QuantityOrdered { get; set; }
    public decimal UnitCost { get; set; }
}
