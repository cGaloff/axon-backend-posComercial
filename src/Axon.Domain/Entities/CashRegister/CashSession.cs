using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.CashRegister;

public enum CashSessionStatus
{
    Open,
    Closed,
    ForceClosed
}

public class CashSession
{
    public Guid Id { get; private set; }
    public Guid CashRegisterId { get; private set; }
    public Guid OpenedBy { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public DateTime OpenedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public decimal InitialAmount { get; private set; }
    public decimal ExpectedAmount { get; private set; }
    public decimal? CountedAmount { get; private set; }
    public decimal? Difference { get; private set; }
    public CashSessionStatus Status { get; private set; }
    public string? Notes { get; private set; }

    private CashSession()
    {
    }

    public static CashSession Create(Guid cashRegisterId, Guid openedBy, decimal initialAmount)
    {
        if (initialAmount < 0)
        {
            throw new DomainException("El monto inicial no puede ser negativo.");
        }

        return new CashSession
        {
            Id = Guid.NewGuid(),
            CashRegisterId = cashRegisterId,
            OpenedBy = openedBy,
            OpenedAt = DateTime.UtcNow,
            InitialAmount = initialAmount,
            ExpectedAmount = initialAmount,
            Status = CashSessionStatus.Open
        };
    }

    public void AddCashMovement(decimal amount, CashMovementType type)
    {
        if (Status != CashSessionStatus.Open)
        {
            throw new DomainException("No se puede modificar una sesión cerrada");
        }

        switch (type)
        {
            case CashMovementType.ManualIncome:
            case CashMovementType.CashSale:
            case CashMovementType.CreditSale:
                ExpectedAmount += amount;
                break;
            case CashMovementType.Expense:
            case CashMovementType.SaleReturn:
                ExpectedAmount -= amount;
                break;
            default:
                // CardSale, TransferSale, OpeningAmount: se registran como
                // movimientos informativos, no modifican el monto físico esperado.
                break;
        }
    }

    public void Close(Guid closedBy, decimal countedAmount, string? notes = null)
    {
        if (Status != CashSessionStatus.Open)
        {
            throw new DomainException("La sesión ya está cerrada");
        }

        ClosedBy = closedBy;
        ClosedAt = DateTime.UtcNow;
        CountedAmount = countedAmount;
        Difference = countedAmount - ExpectedAmount;
        Status = CashSessionStatus.Closed;
        Notes = notes;
    }

    public void ForceClose(Guid closedBy, string reason)
    {
        if (Status != CashSessionStatus.Open)
        {
            throw new DomainException("La sesión ya está cerrada");
        }

        ClosedBy = closedBy;
        ClosedAt = DateTime.UtcNow;
        CountedAmount = ExpectedAmount;
        Difference = CountedAmount - ExpectedAmount;
        Status = CashSessionStatus.ForceClosed;
        Notes = reason;
    }
}
