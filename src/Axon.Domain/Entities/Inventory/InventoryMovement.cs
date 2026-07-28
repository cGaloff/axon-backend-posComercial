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

// Applied: se aplicó al stock de inmediato (todo movimiento salvo mermas por
// encima del umbral configurado). PendingApproval/Approved/Rejected: solo
// aplica a mermas (Loss) que superan TenantConfig.MermaApprovalThreshold — ver
// Matriz de Roles y Permisos v2, regla transversal B.
public enum InventoryMovementStatus
{
    Applied,
    PendingApproval,
    Approved,
    Rejected
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

    public InventoryMovementStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    private InventoryMovement()
    {
    }

    // requiresApproval=true crea el movimiento en PendingApproval SIN aplicar el
    // ajuste al stock todavía (StockAfter queda igual a StockBefore) — el stock
    // solo se toca cuando un Administrador lo aprueba (ver Approve()).
    public static InventoryMovement Create(
        Guid productId,
        Guid warehouseId,
        InventoryMovementType type,
        int quantity,
        int stockBefore,
        string reason,
        Guid createdBy,
        bool requiresApproval = false)
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
            StockAfter = requiresApproval ? stockBefore : stockBefore + quantity,
            Reason = reason,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            Status = requiresApproval ? InventoryMovementStatus.PendingApproval : InventoryMovementStatus.Applied
        };
    }

    // Aplica el ajuste de stock pendiente (el caller es responsable de llamar
    // product.AdjustStock(Quantity) con el mismo valor, ver ApproveInventoryMovementCommandHandler)
    // y deja el movimiento como Approved.
    public void Approve(Guid approvedBy)
    {
        if (Status != InventoryMovementStatus.PendingApproval)
        {
            throw new DomainException("Solo se pueden aprobar movimientos pendientes de aprobación.");
        }

        StockAfter = StockBefore + Quantity;
        Status = InventoryMovementStatus.Approved;
        ReviewedBy = approvedBy;
        ReviewedAt = DateTime.UtcNow;
    }

    public void Reject(Guid rejectedBy, string reason)
    {
        if (Status != InventoryMovementStatus.PendingApproval)
        {
            throw new DomainException("Solo se pueden rechazar movimientos pendientes de aprobación.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("El motivo de rechazo es obligatorio.");
        }

        Status = InventoryMovementStatus.Rejected;
        ReviewedBy = rejectedBy;
        ReviewedAt = DateTime.UtcNow;
        RejectionReason = reason;
    }
}
