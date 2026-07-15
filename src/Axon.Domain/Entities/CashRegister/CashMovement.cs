using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.CashRegister;

public enum CashMovementType
{
    CashSale,
    CreditSale,
    CardSale,
    TransferSale,
    ManualIncome,
    Expense,
    OpeningAmount,
    SaleReturn
}

public class CashMovement
{
    public Guid Id { get; private set; }
    public Guid CashSessionId { get; private set; }
    public CashMovementType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid? ReferenceId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private CashMovement()
    {
    }

    public static CashMovement Create(
        Guid cashSessionId,
        CashMovementType type,
        decimal amount,
        string description,
        Guid createdBy,
        Guid? referenceId = null)
    {
        if (amount <= 0)
        {
            throw new DomainException("El monto debe ser mayor a cero.");
        }

        return new CashMovement
        {
            Id = Guid.NewGuid(),
            CashSessionId = cashSessionId,
            Type = type,
            Amount = amount,
            Description = description,
            ReferenceId = referenceId,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
    }
}
