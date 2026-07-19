using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Suppliers;

public class SupplierPayment
{
    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime PaidAt { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty;
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public Guid CreatedBy { get; private set; }

    private SupplierPayment()
    {
    }

    public static SupplierPayment Create(
        Guid supplierId,
        decimal amount,
        string paymentMethod,
        Guid createdBy,
        string? reference = null,
        string? notes = null)
    {
        if (amount <= 0)
        {
            throw new DomainException("El monto del pago debe ser mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            throw new DomainException("El método de pago es obligatorio.");
        }

        return new SupplierPayment
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            Amount = amount,
            PaidAt = DateTime.UtcNow,
            PaymentMethod = paymentMethod,
            Reference = reference,
            Notes = notes,
            CreatedBy = createdBy
        };
    }
}
