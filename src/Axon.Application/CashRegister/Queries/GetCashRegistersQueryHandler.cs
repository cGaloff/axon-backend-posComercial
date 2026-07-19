using Axon.Application.Interfaces;
using Axon.Domain.Entities.CashRegister;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.CashRegister.Queries;

public class GetCashRegistersQueryHandler : IRequestHandler<GetCashRegistersQuery, List<CashRegisterDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetCashRegistersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CashRegisterDto>> Handle(GetCashRegistersQuery request, CancellationToken cancellationToken)
    {
        var registers = await _dbContext.CashRegisters.ToListAsync(cancellationToken);
        var registerIds = registers.Select(r => r.Id).ToList();

        var activeSessions = await _dbContext.CashSessions
            .Where(s => registerIds.Contains(s.CashRegisterId) && s.Status == CashSessionStatus.Open)
            .ToListAsync(cancellationToken);

        var activeByRegister = activeSessions.ToDictionary(s => s.CashRegisterId, s => s.Id);

        return registers
            .Select(r => new CashRegisterDto(
                r.Id,
                r.Name,
                r.Description,
                r.IsDefault,
                r.IsActive,
                activeByRegister.ContainsKey(r.Id),
                activeByRegister.TryGetValue(r.Id, out var sessionId) ? sessionId : null))
            .ToList();
    }
}
