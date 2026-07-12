using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Inventory;

public enum InventoryMovementType
{
    Purchase,
    Sale,
    ManualAdjustment,
    InitialStock,
    Return,
    Loss
}

public class InventoryMovement
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public InventoryMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public int StockBefore { get; private set; }
    public int StockAfter { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private InventoryMovement()
    {
    }

    public static InventoryMovement Create(
        Guid productId,
        Guid warehouseId,
        InventoryMovementType type,
        int quantity,
        int stockBefore,
        string reason,
        Guid createdBy)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("El producto es obligatorio.");
        }

        if (warehouseId == Guid.Empty)
        {
            throw new DomainException("La bodega es obligatoria.");
        }

        return new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            WarehouseId = warehouseId,
            Type = type,
            Quantity = quantity,
            StockBefore = stockBefore,
            StockAfter = stockBefore + quantity,
            Reason = reason,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
    }
}
