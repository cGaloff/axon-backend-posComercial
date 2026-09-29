using Axon.Application.Interfaces;
using Axon.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Tenants.Services;

public class SubscriptionExpirationSweeper : ISubscriptionExpirationSweeper
{
    private readonly IMasterDbContext _masterDbContext;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public SubscriptionExpirationSweeper(
        IMasterDbContext masterDbContext,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _masterDbContext = masterDbContext;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        var reminderThreshold = int.TryParse(_configuration["Subscription:ReminderDaysBeforeExpiry"], out var threshold)
            ? threshold
            : 5;

        var today = DateTime.UtcNow.Date;

        // Los tenants con SubscriptionExpiresAt == null están "grandfathered"
        // (existían antes de este sistema, o se gestionan a mano) — nunca
        // deben tocarse acá.
        var activeTenants = await _masterDbContext.Tenants
            .Where(t => t.IsActive && t.SubscriptionExpiresAt != null)
            .ToListAsync(cancellationToken);

        foreach (var tenant in activeTenants)
        {
            var daysRemaining = (tenant.SubscriptionExpiresAt!.Value.Date - today).Days;

            // Los tenants aprovisionados antes de existir OwnerEmail no tienen a
            // quién avisar: se suspenden igual, pero sin correo.
            var hasOwnerEmail = !string.IsNullOrWhiteSpace(tenant.OwnerEmail);

            if (daysRemaining <= 0)
            {
                tenant.Suspend();

                if (hasOwnerEmail)
                {
                    await _emailService.SendTrialExpiredAsync(tenant.OwnerEmail!, tenant.BusinessName);
                }

                continue;
            }

            // El guard por fecha (no por contador) hace que mandar el
            // recordatorio sea idempotente sin importar cuántas veces corra
            // el barrido en el mismo día.
            if (hasOwnerEmail &&
                daysRemaining <= reminderThreshold &&
                (tenant.LastTrialReminderSentAt is null || tenant.LastTrialReminderSentAt.Value.Date < today))
            {
                await _emailService.SendTrialReminderAsync(tenant.OwnerEmail!, tenant.BusinessName, daysRemaining);
                tenant.MarkTrialReminderSent();
            }
        }

        await _masterDbContext.SaveChangesAsync(cancellationToken);
    }
}
