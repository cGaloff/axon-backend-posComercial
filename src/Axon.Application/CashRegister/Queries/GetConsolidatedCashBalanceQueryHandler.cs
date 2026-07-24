using Axon.Application.Interfaces;
using Axon.Domain.Entities.CashRegister;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.CashRegister.Queries;

public class GetConsolidatedCashBalanceQueryHandler : IRequestHandler<GetConsolidatedCashBalanceQuery, ConsolidatedCashBalanceDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetConsolidatedCashBalanceQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ConsolidatedCashBalanceDto> Handle(GetConsolidatedCashBalanceQuery request, CancellationToken cancellationToken)
    {
        // El aislamiento entre tenants es automático: _dbContext.CashSessions ya
        // apunta al schema del tenant resuelto para este request (search_path vía
        // TenantSchemaInterceptor), así que esta consulta nunca ve cajas de otro
        // tenant — no existe una columna TenantId que filtrar aquí a propósito.
        var activeSessions = await _dbContext.CashSessions
            .Where(s => s.Status == CashSessionStatus.Open)
            .ToListAsync(cancellationToken);

        if (activeSessions.Count == 0)
        {
            return new ConsolidatedCashBalanceDto(new List<CashRegisterBalanceDto>(), 0m);
        }

        var registerIds = activeSessions.Select(s => s.CashRegisterId).ToList();

        var registerNames = await _dbContext.CashRegisters
            .Where(r => registerIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        var sessionIds = activeSessions.Select(s => s.Id).ToList();

        var movements = await _dbContext.CashMovements
            .Where(m => sessionIds.Contains(m.CashSessionId))
            .ToListAsync(cancellationToken);

        var movementsBySession = movements.ToLookup(m => m.CashSessionId);

        var registerBalances = activeSessions
            .Select(session =>
            {
                var sessionMovements = movementsBySession[session.Id];

                decimal SumByType(CashMovementType type) =>
                    sessionMovements.Where(m => m.Type == type).Sum(m => m.Amount);

                return new CashRegisterBalanceDto(
                    session.CashRegisterId,
                    registerNames.GetValueOrDefault(session.CashRegisterId, string.Empty),
                    session.Id,
                    session.OpenedAt,
                    session.InitialAmount,
                    SumByType(CashMovementType.CashSale),
                    SumByType(CashMovementType.CreditSale),
                    SumByType(CashMovementType.CardSale),
                    SumByType(CashMovementType.TransferSale),
                    SumByType(CashMovementType.ManualIncome),
                    SumByType(CashMovementType.Expense),
                    SumByType(CashMovementType.SaleReturn),
                    // ExpectedAmount ya lo mantiene CashSession.AddCashMovement de forma
                    // incremental (misma fuente de verdad que el resumen de una sola
                    // sesión) — no se recalcula distinto aquí, solo se consolida.
                    session.ExpectedAmount);
            })
            .OrderBy(r => r.CashRegisterName)
            .ToList();

        var totalExpectedAmount = registerBalances.Sum(r => r.ExpectedAmount);

        return new ConsolidatedCashBalanceDto(registerBalances, totalExpectedAmount);
    }
}
