using Axon.Domain.Entities;

namespace Axon.Domain.Interfaces;

public interface ITenantConfigRepository
{
    Task<TenantConfig?> GetAsync();
    Task AddAsync(TenantConfig config);
    void Update(TenantConfig config);
}
