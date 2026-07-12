namespace Axon.Domain.Entities.Inventory;

public class StockAlert
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public int CurrentStock { get; private set; }
    public int MinStock { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private StockAlert()
    {
    }

    public static StockAlert Create(Guid productId, Guid warehouseId, int currentStock, int minStock)
    {
        return new StockAlert
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            WarehouseId = warehouseId,
            CurrentStock = currentStock,
            MinStock = minStock,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
    }
}
