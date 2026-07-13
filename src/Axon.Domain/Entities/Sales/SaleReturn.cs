using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Sales;

public class SaleReturn
{
    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid ReturnedBy { get; private set; }
    public DateTime ReturnedAt { get; private set; }
    public decimal Total { get; private set; }

    private SaleReturn()
    {
    }

    public static SaleReturn Create(Guid saleId, string reason, Guid returnedBy, decimal total)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("El motivo de la devolución es obligatorio.");
        }

        return new SaleReturn
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            Reason = reason,
            ReturnedBy = returnedBy,
            ReturnedAt = DateTime.UtcNow,
            Total = total
        };
    }
}
