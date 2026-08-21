using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Tenants.Queries;

public class GetSubscriptionStatusQueryHandler : IRequestHandler<GetSubscriptionStatusQuery, SubscriptionStatusResult>
{
    private readonly IMasterDbContext _masterDbContext;
    private readonly ITenantContext _tenantContext;

    public GetSubscriptionStatusQueryHandler(IMasterDbContext masterDbContext, ITenantContext tenantContext)
    {
        _masterDbContext = masterDbContext;
        _tenantContext = tenantContext;
    }

    public async Task<SubscriptionStatusResult> Handle(GetSubscriptionStatusQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _masterDbContext.Tenants
            .SingleOrDefaultAsync(t => t.Slug == _tenantContext.TenantSlug, cancellationToken);

        if (tenant is null)
        {
            throw new DomainException("Tenant no encontrado");
        }

        int? daysRemaining = tenant.SubscriptionExpiresAt.HasValue
            ? (tenant.SubscriptionExpiresAt.Value.Date - DateTime.UtcNow.Date).Days
            : null;

        return new SubscriptionStatusResult(tenant.IsActive, tenant.Plan, tenant.SubscriptionExpiresAt, daysRemaining);
    }
}
