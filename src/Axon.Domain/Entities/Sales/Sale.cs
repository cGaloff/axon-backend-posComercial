using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Sales;

public class Sale
{
    // Tolerancia de redondeo entre la suma de pagos y el total de la venta: 1
    // centavo. Las columnas de dinero son decimal(12,2) (2 decimales); al dividir
    // un total entre N pagos, cada monto puede redondearse independientemente a 2
    // decimales (p. ej. en el cliente/POS), lo que puede dejar una diferencia de
    // hasta un centavo por operación de redondeo. Con 1 centavo se cubre ese caso
    // común sin ocultar un descuadre real (que sería de varios centavos o más).
    public const decimal PaymentTolerance = 0.01m;

    private readonly List<SaleItem> _items = new();
    private readonly List<SalePayment> _payments = new();

    public Guid Id { get; private set; }
    public string SaleNumber { get; private set; } = string.Empty;
    public Guid? CustomerId { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public SaleStatus Status { get; private set; }
    public decimal Total { get; private set; }
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
    public IReadOnlyList<SalePayment> Payments => _payments;

    private Sale()
    {
    }

    public static Sale Create(
        Guid cashRegisterId,
        Guid createdBy,
        Guid? customerId = null,
        string? customerName = null,
        string? notes = null)
    {
        var randomSuffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var saleNumber = $"VTA-{DateTime.UtcNow:yyyyMMdd}-{randomSuffix}";

        return new Sale
        {
            Id = Guid.NewGuid(),
            SaleNumber = saleNumber,
            CustomerId = customerId,
            CustomerName = customerName ?? string.Empty,
            // Sin pagos todavía, se asume Completed; AddPayment recalcula según los
            // métodos de pago que efectivamente se agreguen (Card/Transfer => Pending).
            Status = SaleStatus.Completed,
            Total = 0,
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

    // Tarjeta/transferencia requieren confirmación externa (ver ConfirmSalePaymentCommand
    // y PaymentWebhookController); si CUALQUIER pago de la venta usa uno de esos
    // métodos, la venta entera queda pendiente hasta que se confirme. Se recalcula
    // en cada llamada para no depender del orden en que se agregan los pagos.
    public void AddPayment(SalePayment payment)
    {
        _payments.Add(payment);

        if (Status is SaleStatus.Completed or SaleStatus.PendingPayment)
        {
            Status = _payments.Any(p => p.Method is PaymentMethod.Card or PaymentMethod.Transfer)
                ? SaleStatus.PendingPayment
                : SaleStatus.Completed;
        }
    }

    // Debe llamarse después de agregar todos los ítems y todos los pagos: valida
    // que la suma de los pagos cubra el total dentro de la tolerancia de redondeo.
    public void EnsurePaymentsMatchTotal()
    {
        var paid = _payments.Sum(p => p.Amount);

        if (Math.Abs(paid - Total) > PaymentTolerance)
        {
            throw new DomainException(
                $"La suma de los pagos ({paid}) no coincide con el total de la venta ({Total}).");
        }
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
