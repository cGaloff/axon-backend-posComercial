namespace Axon.Domain.Entities.Suppliers;

public class ProductSupplier
{
    public Guid ProductId { get; private set; }
    public Guid SupplierId { get; private set; }
    public decimal LastPurchasePrice { get; private set; }
    public bool IsPreferred { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ProductSupplier()
    {
    }

    public ProductSupplier(Guid productId, Guid supplierId, decimal lastPurchasePrice, bool isPreferred = false)
    {
        ProductId = productId;
        SupplierId = supplierId;
        LastPurchasePrice = lastPurchasePrice;
        IsPreferred = isPreferred;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePrice(decimal newPrice)
    {
        LastPurchasePrice = newPrice;
        UpdatedAt = DateTime.UtcNow;
    }
}
