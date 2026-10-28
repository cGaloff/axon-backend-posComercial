using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Tenants.Commands;

public class ExtendTenantSubscriptionCommandHandler : IRequestHandler<ExtendTenantSubscriptionCommand, ExtendTenantSubscriptionResult>
{
    private readonly IMasterDbContext _masterDbContext;
    private readonly ITenantCacheInvalidator _tenantCacheInvalidator;

    public ExtendTenantSubscriptionCommandHandler(
        IMasterDbContext masterDbContext,
        ITenantCacheInvalidator tenantCacheInvalidator)
    {
        _masterDbContext = masterDbContext;
        _tenantCacheInvalidator = tenantCacheInvalidator;
    }

    public async Task<ExtendTenantSubscriptionResult> Handle(ExtendTenantSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _masterDbContext.Tenants
            .SingleOrDefaultAsync(t => t.Slug == request.Slug, cancellationToken);

        if (tenant is null)
        {
            throw new DomainException("Tenant no encontrado");
        }

        var reference = string.IsNullOrWhiteSpace(request.TransactionReference)
            ? null
            : request.TransactionReference.Trim();

        if (reference is not null)
        {
            var alreadyProcessed = await _masterDbContext.SubscriptionExtensions
                .AnyAsync(e => e.TransactionReference == reference, cancellationToken);

            if (alreadyProcessed)
            {
                return new ExtendTenantSubscriptionResult(
                    tenant.Slug, tenant.SubscriptionExpiresAt ?? DateTime.UtcNow, 0, AlreadyProcessed: true);
            }
        }

        var billingCycle = request.BillingCycle?.Trim().ToLowerInvariant();
        var extensionDays = request.ExtensionDays ?? BillingCycles.ExtensionDaysFor(billingCycle!);

        var previousExpiresAt = tenant.SubscriptionExpiresAt;
        var newExpiresAt = tenant.ExtendSubscriptionByDays(extensionDays, billingCycle, DateTime.UtcNow);

        _masterDbContext.SubscriptionExtensions.Add(SubscriptionExtension.Create(
            tenant.Id,
            billingCycle,
            extensionDays,
            request.Amount ?? 0m,
            reference,
            previousExpiresAt,
            newExpiresAt));

        await _masterDbContext.SaveChangesAsync(cancellationToken);

        // El resolver cachea el tenant 5 minutos con su fecha de vencimiento:
        // sin esto el cliente seguiría viendo "Suscripción vencida" después de
        // pagar hasta que expirara la caché.
        _tenantCacheInvalidator.Invalidate(tenant.Slug);

        return new ExtendTenantSubscriptionResult(tenant.Slug, newExpiresAt, extensionDays, AlreadyProcessed: false);
    }
}
