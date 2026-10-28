using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Tenants.Queries;

public class GetSubscriptionStatusQueryHandler : IRequestHandler<GetSubscriptionStatusQuery, SubscriptionStatusResult>
{
    public const string StatusTrialing = "trialing";
    public const string StatusActive = "active";
    public const string StatusExpired = "expired";

    private const string DefaultPlanName = "Plan Todo Incluido PyME";

    private readonly IMasterDbContext _masterDbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;

    public GetSubscriptionStatusQueryHandler(
        IMasterDbContext masterDbContext,
        ITenantContext tenantContext,
        IConfiguration configuration)
    {
        _masterDbContext = masterDbContext;
        _tenantContext = tenantContext;
        _configuration = configuration;
    }

    public async Task<SubscriptionStatusResult> Handle(GetSubscriptionStatusQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _masterDbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Slug == _tenantContext.TenantSlug, cancellationToken);

        if (tenant is null)
        {
            throw new DomainException("Tenant no encontrado");
        }

        // Solo cuentan como pago las extensiones con monto: las manuales de
        // soporte (monto 0) extienden la fecha pero no son un cobro.
        var lastPayment = await _masterDbContext.SubscriptionExtensions
            .AsNoTracking()
            .Where(e => e.TenantId == tenant.Id && e.Amount > 0)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;

        // Redondeo hacia arriba: con 2 horas restantes todavía queda "1 día",
        // y llega a 0 solo cuando efectivamente venció.
        int? daysRemaining = tenant.SubscriptionExpiresAt.HasValue
            ? Math.Max(0, (int)Math.Ceiling((tenant.SubscriptionExpiresAt.Value - now).TotalDays))
            : null;

        var status = !tenant.IsActive || tenant.IsSubscriptionExpired(now)
            ? StatusExpired
            : tenant.IsTrial ? StatusTrialing : StatusActive;

        var trialDaysTotal = int.TryParse(_configuration["Subscription:TrialDays"], out var days) ? days : 5;
        var planName = _configuration["Subscription:PlanName"] ?? DefaultPlanName;

        return new SubscriptionStatusResult(
            IsActive: tenant.IsActive && !tenant.IsSubscriptionExpired(now),
            Status: status,
            Plan: tenant.Plan,
            PlanName: planName,
            SubscriptionExpiresAt: tenant.SubscriptionExpiresAt,
            DaysRemaining: daysRemaining,
            TrialDaysTotal: trialDaysTotal,
            IsTrial: tenant.IsTrial,
            CurrentBillingCycle: tenant.BillingCycle,
            AmountPaid: lastPayment?.Amount ?? 0m,
            LastPaymentDate: lastPayment?.CreatedAt,
            NextRenewalDate: tenant.SubscriptionExpiresAt);
    }
}
