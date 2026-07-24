using Axon.Application.Interfaces;
using Axon.Domain.Entities.CashRegister;
using Axon.Domain.Interfaces;

namespace Axon.Application.Tests.TestSupport;

public class FakeCashSessionRepository : ICashSessionRepository
{
    private readonly IApplicationDbContext _dbContext;

    public FakeCashSessionRepository(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<CashSession?> GetActiveSessionAsync(Guid cashRegisterId)
    {
        var session = _dbContext.CashSessions
            .FirstOrDefault(s => s.CashRegisterId == cashRegisterId && s.Status == CashSessionStatus.Open);

        return Task.FromResult(session);
    }

    public Task<CashSession?> GetByIdAsync(Guid id)
    {
        var session = _dbContext.CashSessions.FirstOrDefault(s => s.Id == id);

        return Task.FromResult(session);
    }

    public Task AddAsync(CashSession session)
    {
        _dbContext.CashSessions.Add(session);
        return Task.CompletedTask;
    }

    public void Update(CashSession session)
    {
    }
}
