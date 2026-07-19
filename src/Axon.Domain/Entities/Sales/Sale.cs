using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Sales;

public class Sale
{
    private readonly List<SaleItem> _items = new();

    public Guid Id { get; private set; }
    public string SaleNumber { get; private set; } = string.Empty;
    public Guid? CustomerId { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; private set; }
    public SaleStatus Status { get; private set; }
    public decimal Total { get; private set; }
    public decimal AmountPaid { get; private set; }
    public decimal Change { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public Guid CashRegisterId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? VoidedAt { get; private set; }
    public Guid? VoidedBy { get; private set; }
    public string? VoidReason { get; private set; }
    public DateTime? ReturnedAt { get; private set; }
    public Guid? ReturnedBy { get; private set; }

    public IReadOnlyList<SaleItem> Items => _items;

    private Sale()
    {
    }

    public static Sale Create(
        PaymentMethod method,
        Guid cashRegisterId,
        Guid createdBy,
        Guid? customerId = null,
        string? customerName = null,
        string? notes = null)
    {
        var randomSuffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var saleNumber = $"VTA-{DateTime.UtcNow:yyyyMMdd}-{randomSuffix}";

        var status = method is PaymentMethod.Card or PaymentMethod.Transfer
            ? SaleStatus.PendingPayment
            : SaleStatus.Completed;

        return new Sale
        {
            Id = Guid.NewGuid(),
            SaleNumber = saleNumber,
            CustomerId = customerId,
            CustomerName = customerName ?? string.Empty,
            PaymentMethod = method,
            Status = status,
            Total = 0,
            AmountPaid = 0,
            Change = 0,
            Notes = notes ?? string.Empty,
            CashRegisterId = cashRegisterId,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddItem(SaleItem item)
    {
        _items.Add(item);
        Total = _items.Sum(i => i.Subtotal);
    }

    public void SetAmountPaid(decimal amountPaid)
    {
        if (PaymentMethod != PaymentMethod.Cash)
        {
            throw new DomainException("Solo se puede registrar el monto pagado para ventas en efectivo.");
        }

        if (amountPaid < Total)
        {
            throw new DomainException("El monto pagado no puede ser menor al total de la venta.");
        }

        AmountPaid = amountPaid;
        Change = amountPaid - Total;
    }

    public void Complete()
    {
        if (Status != SaleStatus.PendingPayment)
        {
            throw new DomainException("Solo se pueden completar ventas pendientes de pago.");
        }

        Status = SaleStatus.Completed;
    }

    public void Void(Guid voidedBy, string reason)
    {
        if (Status == SaleStatus.Voided)
        {
            throw new DomainException("La venta ya fue anulada");
        }

        if (Status == SaleStatus.Returned)
        {
            throw new DomainException("No se puede anular una venta devuelta");
        }

        Status = SaleStatus.Voided;
        VoidedAt = DateTime.UtcNow;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }

    public void MarkAsReturned(Guid returnedBy)
    {
        if (Status != SaleStatus.Completed)
        {
            throw new DomainException("Solo se pueden devolver ventas completadas");
        }

        Status = SaleStatus.Returned;
        ReturnedAt = DateTime.UtcNow;
        ReturnedBy = returnedBy;
    }
}
