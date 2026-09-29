using Axon.Application.Interfaces;
using Axon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Axon.Infrastructure.MultiTenant;

public class TenantResolver : ITenantCacheInvalidator
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public TenantResolver(AppDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<TenantInfo?> ResolveAsync(string slug)
    {
        var cacheKey = $"tenant_{slug}";

        if (_cache.TryGetValue(cacheKey, out TenantInfo? cached))
        {
            return cached;
        }

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug);

        if (tenant is null)
        {
            return null;
        }

        var tenantInfo = new TenantInfo(tenant.Slug, tenant.SchemaName, tenant.IsActive, tenant.SubscriptionEndsAt);

        if (tenantInfo.IsActive)
        {
            _cache.Set(cacheKey, tenantInfo, CacheTtl);
        }

        return tenantInfo;
    }

    // Sin esto, renovar la suscripción no surtiría efecto hasta que expirara la
    // caché y el cliente seguiría bloqueado varios minutos después de pagar.
    public void Invalidate(string slug)
    {
        _cache.Remove($"tenant_{slug}");
    }
}
