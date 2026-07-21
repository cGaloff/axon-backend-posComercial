using Axon.Application.Common.Models;
using Axon.Application.Interfaces;
using Axon.Domain.Entities.CashRegister;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.CashRegister.Queries;

public class GetCashSessionsHistoryQueryHandler : IRequestHandler<GetCashSessionsHistoryQuery, PagedResult<CashSessionSummaryDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetCashSessionsHistoryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<CashSessionSummaryDto>> Handle(GetCashSessionsHistoryQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.CashSessions.AsQueryable();

        if (request.FromDate.HasValue)
        {
            query = query.Where(s => s.OpenedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(s => s.OpenedAt <= request.ToDate.Value);
        }

        if (request.CashRegisterId.HasValue)
        {
            query = query.Where(s => s.CashRegisterId == request.CashRegisterId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.Status == request.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var sessions = await query
            .OrderByDescending(s => s.OpenedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var registerIds = sessions.Select(s => s.CashRegisterId).Distinct().ToList();

        var registerNames = await _dbContext.CashRegisters
            .Where(r => registerIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        var movements = await _dbContext.CashMovements
            .Where(m => sessionIds.Contains(m.CashSessionId))
            .ToListAsync(cancellationToken);

        var movementsBySession = movements.ToLookup(m => m.CashSessionId);

        static decimal SumByType(IEnumerable<CashMovement> movements, CashMovementType type) =>
            movements.Where(m => m.Type == type).Sum(m => m.Amount);

        var items = sessions.Select(s =>
        {
            var sessionMovements = movementsBySession[s.Id].ToList();

            var movementDtos = sessionMovements
                .OrderBy(m => m.CreatedAt)
                .Select(m => new CashMovementDto(m.Id, m.Type.ToString(), m.Amount, m.Description, m.CreatedAt, m.ReferenceId))
                .ToList();

            return new CashSessionSummaryDto(
                s.Id,
                registerNames.GetValueOrDefault(s.CashRegisterId, string.Empty),
                s.Status.ToString(),
                s.InitialAmount,
                SumByType(sessionMovements, CashMovementType.CashSale),
                SumByType(sessionMovements, CashMovementType.CreditSale),
                SumByType(sessionMovements, CashMovementType.CardSale),
                SumByType(sessionMovements, CashMovementType.TransferSale),
                SumByType(sessionMovements, CashMovementType.ManualIncome),
                SumByType(sessionMovements, CashMovementType.Expense),
                SumByType(sessionMovements, CashMovementType.SaleReturn),
                s.ExpectedAmount,
                s.CountedAmount,
                s.Difference,
                s.OpenedAt,
                s.ClosedAt,
                movementDtos);
        }).ToList();

        return new PagedResult<CashSessionSummaryDto>(totalCount, request.Page, request.PageSize, items);
    }
}
