using Axon.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Application.Tenants.Commands;

public class UpdateTenantSubscriptionCommandHandler
    : IRequestHandler<UpdateTenantSubscriptionCommand, UpdateTenantSubscriptionResult?>
{
    private readonly IMasterDbContext _appDbContext;
    private readonly ITenantCacheInvalidator _tenantCacheInvalidator;
    private readonly ILogger<UpdateTenantSubscriptionCommandHandler> _logger;

    public UpdateTenantSubscriptionCommandHandler(
        IMasterDbContext appDbContext,
        ITenantCacheInvalidator tenantCacheInvalidator,
        ILogger<UpdateTenantSubscriptionCommandHandler> logger)
    {
        _appDbContext = appDbContext;
        _tenantCacheInvalidator = tenantCacheInvalidator;
        _logger = logger;
    }

    public async Task<UpdateTenantSubscriptionResult?> Handle(
        UpdateTenantSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        var tenant = await _appDbContext.Tenants
            .FirstOrDefaultAsync(t => t.Slug == request.Slug, cancellationToken);

        // null lo traduce el controlador a 404: un slug inexistente no es un
        // error de negocio del que haya que avisar como 400.
        if (tenant is null)
        {
            return null;
        }

        tenant.UpdateSubscription(request.SubscriptionEndsAt, request.Plan);

        await _appDbContext.SaveChangesAsync(cancellationToken);

        // El resolver cachea el tenant 5 minutos; sin esto una renovación no se
        // vería hasta que la caché expirara.
        _tenantCacheInvalidator.Invalidate(tenant.Slug);

        _logger.LogInformation(
            "Suscripción actualizada para el tenant '{Slug}': vence {SubscriptionEndsAt}, plan {Plan}.",
            tenant.Slug,
            tenant.SubscriptionEndsAt?.ToString("O") ?? "nunca",
            tenant.Plan);

        return new UpdateTenantSubscriptionResult(
            tenant.Id,
            tenant.Slug,
            tenant.BusinessName,
            tenant.Plan,
            tenant.IsActive,
            tenant.SubscriptionEndsAt);
    }
}
