using Axon.Domain.Interfaces;
using TenantConfigEntity = Axon.Domain.Entities.TenantConfig;

namespace Axon.Application.Tests.TestSupport;

public class FakeTenantConfigRepository : ITenantConfigRepository
{
    private TenantConfigEntity? _config;

    public FakeTenantConfigRepository(TenantConfigEntity? config = null)
    {
        _config = config;
    }

    public Task<TenantConfigEntity?> GetAsync() => Task.FromResult(_config);

    public Task AddAsync(TenantConfigEntity config)
    {
        _config = config;
        return Task.CompletedTask;
    }

    public void Update(TenantConfigEntity config)
    {
        _config = config;
    }
}
