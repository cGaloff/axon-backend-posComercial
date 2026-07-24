using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Sales;

// Una de N formas de pago que cubren el total de una venta. AmountTendered/Change
// solo aplican a pagos en efectivo (el resto no tiene concepto de "vuelto").
// Es consultable/snapshoteable igual que SaleItem/SaleItemTax: la factura (prompt
// futuro) podrá listar el desglose de pagos de una venta ya cerrada.
public class SalePayment
{
    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public decimal? AmountTendered { get; private set; }
    public decimal? Change { get; private set; }

    private SalePayment()
    {
    }

    public static SalePayment Create(
        Guid saleId,
        PaymentMethod method,
        decimal amount,
        decimal? amountTendered = null)
    {
        if (amount <= 0)
        {
            throw new DomainException("El monto del pago debe ser mayor a cero.");
        }

        decimal? change = null;

        if (method == PaymentMethod.Cash)
        {
            // Si no se indica lo entregado, se asume pago exacto (sin vuelto).
            var tendered = amountTendered ?? amount;

            if (tendered < amount)
            {
                throw new DomainException("El monto entregado no puede ser menor al monto del pago en efectivo.");
            }

            change = tendered - amount;
            amountTendered = tendered;
        }
        else if (amountTendered is not null)
        {
            throw new DomainException("El monto entregado solo aplica a pagos en efectivo.");
        }

        return new SalePayment
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            Method = method,
            Amount = amount,
            AmountTendered = amountTendered,
            Change = change
        };
    }
}
