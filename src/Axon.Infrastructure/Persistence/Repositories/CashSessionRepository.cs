using Axon.Domain.Entities.CashRegister;
using Axon.Domain.Interfaces;
using Axon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Axon.Infrastructure.Persistence.Repositories;

public class CashSessionRepository : ICashSessionRepository
{
    private readonly TenantDbContext _dbContext;

    public CashSessionRepository(TenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<CashSession?> GetActiveSessionAsync(Guid cashRegisterId)
    {
        return _dbContext.CashSessions
            .SingleOrDefaultAsync(s => s.CashRegisterId == cashRegisterId && s.Status == CashSessionStatus.Open);
    }

    public Task<CashSession?> GetByIdAsync(Guid id)
    {
        return _dbContext.CashSessions.SingleOrDefaultAsync(s => s.Id == id);
    }

    public Task AddAsync(CashSession session)
    {
        _dbContext.CashSessions.Add(session);
        return Task.CompletedTask;
    }

    public void Update(CashSession session)
    {
        _dbContext.CashSessions.Update(session);
    }
}
