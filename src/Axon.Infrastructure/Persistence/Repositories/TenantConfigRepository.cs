using Axon.Domain.Entities;
using Axon.Domain.Interfaces;
using Axon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Axon.Infrastructure.Persistence.Repositories;

public class TenantConfigRepository : ITenantConfigRepository
{
    private readonly TenantDbContext _dbContext;

    public TenantConfigRepository(TenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<TenantConfig?> GetAsync()
    {
        return _dbContext.TenantConfigs.FirstOrDefaultAsync();
    }

    public Task AddAsync(TenantConfig config)
    {
        _dbContext.TenantConfigs.Add(config);
        return Task.CompletedTask;
    }

    public void Update(TenantConfig config)
    {
        _dbContext.TenantConfigs.Update(config);
    }
}
