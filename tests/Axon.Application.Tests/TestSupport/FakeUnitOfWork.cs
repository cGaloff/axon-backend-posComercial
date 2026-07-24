using Axon.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Tests.TestSupport;

public class FakeUnitOfWork : IUnitOfWork
{
    private readonly DbContext _dbContext;

    public FakeUnitOfWork(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> CommitAsync(CancellationToken ct = default) => _dbContext.SaveChangesAsync(ct);

    public void Rollback()
    {
    }
}
