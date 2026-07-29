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
    public CustomerDocumentType? CustomerDocumentType { get; private set; }
    public string CustomerDocumentNumber { get; private set; } = string.Empty;
    public SaleStatus Status { get; private set; }
    public decimal Total { get; private set; }

    // Monto ya convertido (si se ingresó como % se guarda el monto resultante,
    // no el porcentaje) del descuento aplicado a TODA la venta, repartido
    // internamente entre los ítems como SaleItem.GeneralDiscountShare. Este
    // campo es solo para mostrarlo en la factura como una línea propia
    // ("Descuento general"), separada de los descuentos manuales por producto
    // — el cálculo real (impuestos, total) ya vive en cada SaleItem.
    public decimal GeneralDiscountAmount { get; private set; }

    // Null si el descuento general se ingresó como monto fijo (ej. un bono de
    // $10.000) — en ese caso la factura solo muestra el monto. Si se ingresó
    // como % (ej. "10% a toda la venta"), este campo guarda ESE porcentaje tal
    // cual lo dio el cajero, solo para mostrarlo junto al monto en la factura
    // ("Descuento general (10%)") — no participa en ningún cálculo, el monto
    // ya convertido en GeneralDiscountAmount es el que se usa siempre.
    public decimal? GeneralDiscountPercentage { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public Guid CashRegisterId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? VoidedAt { get; private set; }
    public Guid? VoidedBy { get; private set; }
    public string? VoidReason { get; private set; }
    public DateTime? ReturnedAt { get; private set; }
    public Guid? ReturnedBy { get; private set; }

    // Quién PIDIÓ la anulación/devolución es VoidedBy/ReturnedBy (la sesión activa,
    // p. ej. un Cajero); AuthorizedBy es el Administrador/Propietario cuyo PIN la
    // habilitó, cuando quien la pide no tiene el permiso por sí mismo. Null si quien
    // la pidió ya tenía el permiso directamente (no hizo falta autorización de nadie
    // más) — Matriz de Roles y Permisos v2, rol Cajero.
    public Guid? AuthorizedBy { get; private set; }

    public IReadOnlyList<SaleItem> Items => _items;
    public IReadOnlyList<SalePayment> Payments => _payments;

    private Sale()
    {
    }

    // Consumidor Final es el nombre que se muestra para una venta sin cliente
    // identificado (cliente ocasional) — se aplica aquí, no en el PDF/frontend,
    // para que el historial de ventas y la factura muestren siempre el mismo
    // valor. El documento (CC/NIT), en cambio, no tiene default: si el cliente
    // no lo da, queda vacío a propósito.
    public const string DefaultCustomerName = "Consumidor Final";

    public static Sale Create(
        Guid cashRegisterId,
        Guid createdBy,
        Guid? customerId = null,
        string? customerName = null,
        string? notes = null,
        CustomerDocumentType? customerDocumentType = null,
        string? customerDocumentNumber = null)
    {
        var randomSuffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var saleNumber = $"VTA-{DateTime.UtcNow:yyyyMMdd}-{randomSuffix}";

        return new Sale
        {
            Id = Guid.NewGuid(),
            SaleNumber = saleNumber,
            CustomerId = customerId,
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? DefaultCustomerName : customerName,
            CustomerDocumentType = customerDocumentType,
            CustomerDocumentNumber = customerDocumentNumber ?? string.Empty,
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

    // Debe llamarse después de agregar todos los ítems, una vez que ya se
    // repartió (y aplicó) el descuento general entre sus SaleItem.GeneralDiscountShare
    // — este método solo guarda el monto (y, si vino de ahí, el % original) para
    // mostrarlo en la factura, no recalcula nada (el Total ya quedó correcto vía
    // AddItem). percentage es null cuando el descuento general se dio como monto
    // fijo (no hay % que mostrar).
    public void SetGeneralDiscountAmount(decimal amount, decimal? percentage = null)
    {
        if (amount < 0)
        {
            throw new DomainException("El descuento general no puede ser negativo.");
        }

        if (percentage is < 0 or > 100)
        {
            throw new DomainException("El porcentaje del descuento general debe estar entre 0 y 100.");
        }

        GeneralDiscountAmount = amount;
        GeneralDiscountPercentage = percentage;
    }

    // Venta presencial: Tarjeta/Transferencia se cobran en el momento (datáfono propio
    // del negocio o transferencia verificada a simple vista por el cajero), igual que
    // Efectivo y Crédito — no hay pasarela de pago externa integrada que confirme el
    // cobro de forma asíncrona, así que ningún método de pago deja la venta pendiente.
    // PendingPayment/ConfirmSalePaymentCommand quedan disponibles para una futura
    // integración real con una pasarela de pagos.
    public void AddPayment(SalePayment payment)
    {
        _payments.Add(payment);
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

    public void Void(Guid voidedBy, string reason, Guid? authorizedBy = null)
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
        AuthorizedBy = authorizedBy;
    }

    public void MarkAsReturned(Guid returnedBy, Guid? authorizedBy = null)
    {
        if (Status != SaleStatus.Completed)
        {
            throw new DomainException("Solo se pueden devolver ventas completadas");
        }

        Status = SaleStatus.Returned;
        ReturnedAt = DateTime.UtcNow;
        ReturnedBy = returnedBy;
        AuthorizedBy = authorizedBy;
    }
}
