using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Suppliers;

public enum PurchaseOrderStatus
{
    Pending,
    PartiallyReceived,
    Received,
    Cancelled
}

public class PurchaseOrder
{
    private readonly List<PurchaseOrderItem> _items = new();

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime OrderDate { get; private set; }
    public DateTime? ExpectedDate { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<PurchaseOrderItem> Items => _items;

    public decimal TotalOrdered => Items.Sum(i => i.Subtotal);

    private PurchaseOrder()
    {
    }

    public static PurchaseOrder Create(
        Guid supplierId,
        Guid createdBy,
        DateTime? expectedDate = null,
        string? notes = null)
    {
        return new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            Status = PurchaseOrderStatus.Pending,
            Notes = notes,
            OrderDate = DateTime.UtcNow,
            ExpectedDate = expectedDate,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddItem(PurchaseOrderItem item)
    {
        if (Status != PurchaseOrderStatus.Pending)
        {
            throw new DomainException("Solo se pueden agregar ítems a órdenes pendientes");
        }

        _items.Add(item);
    }

    public void Cancel(string reason)
    {
        if (Status == PurchaseOrderStatus.Received)
        {
            throw new DomainException("No se puede cancelar una orden ya recibida");
        }

        Status = PurchaseOrderStatus.Cancelled;
        Notes = string.IsNullOrWhiteSpace(Notes) ? $"Cancelada: {reason}" : $"{Notes} | Cancelada: {reason}";
    }

    public void UpdateStatus()
    {
        if (_items.All(i => i.QuantityReceived == i.QuantityOrdered))
        {
            Status = PurchaseOrderStatus.Received;
        }
        else if (_items.Any(i => i.QuantityReceived > 0))
        {
            Status = PurchaseOrderStatus.PartiallyReceived;
        }
        else
        {
            Status = PurchaseOrderStatus.Pending;
        }
    }
}
