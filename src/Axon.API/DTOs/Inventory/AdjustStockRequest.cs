using Axon.Domain.Entities.Inventory;

namespace Axon.API.DTOs.Inventory;

public record AdjustStockRequest(int Quantity, InventoryMovementType Type, string Reason);
