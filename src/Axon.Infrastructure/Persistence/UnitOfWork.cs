using Axon.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Axon.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly TenantDbContext _context;

    public UnitOfWork(TenantDbContext context)
    {
        _context = context;
    }

    public Task<int> CommitAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }

    public void Rollback()
    {
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
