using Axon.Application.Interfaces;
using Axon.Domain.Entities.CashRegister;
using Axon.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.CashRegister.Queries;

public class GetCashSessionSummaryQueryHandler : IRequestHandler<GetCashSessionSummaryQuery, CashSessionSummaryDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetCashSessionSummaryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CashSessionSummaryDto> Handle(GetCashSessionSummaryQuery request, CancellationToken cancellationToken)
    {
        var session = await _dbContext.CashSessions
            .SingleOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
        {
            throw new DomainException("La sesión no existe");
        }

        var cashRegister = await _dbContext.CashRegisters
            .SingleOrDefaultAsync(c => c.Id == session.CashRegisterId, cancellationToken);

        var movements = await _dbContext.CashMovements
            .Where(m => m.CashSessionId == session.Id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        decimal SumByType(CashMovementType type) => movements.Where(m => m.Type == type).Sum(m => m.Amount);

        var movementDtos = movements
            .Select(m => new CashMovementDto(m.Id, m.Type.ToString(), m.Amount, m.Description, m.CreatedAt, m.ReferenceId))
            .ToList();

        return new CashSessionSummaryDto(
            session.Id,
            cashRegister?.Name ?? string.Empty,
            session.Status.ToString(),
            session.InitialAmount,
            SumByType(CashMovementType.CashSale),
            SumByType(CashMovementType.CreditSale),
            SumByType(CashMovementType.CardSale),
            SumByType(CashMovementType.TransferSale),
            SumByType(CashMovementType.ManualIncome),
            SumByType(CashMovementType.Expense),
            SumByType(CashMovementType.SaleReturn),
            session.ExpectedAmount,
            session.CountedAmount,
            session.Difference,
            session.OpenedAt,
            session.ClosedAt,
            movementDtos);
    }
}
