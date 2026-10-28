using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities;

public class Tenant
{
    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string SchemaName { get; private set; } = string.Empty;
    public string BusinessName { get; private set; } = string.Empty;
    public string Plan { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? OwnerEmail { get; private set; }

    // Fecha (UTC) en la que caduca la suscripción. null = sin vencimiento: así
    // quedan los tenants anteriores a este campo y las compras aprovisionadas
    // por el backend de pagos, donde el corte lo marca el pago y no el POS.
    public DateTime? SubscriptionExpiresAt { get; private set; }
    public DateTime? LastTrialReminderSentAt { get; private set; }

    // true mientras el tenant corre la prueba gratuita del auto-registro; pasa a
    // false con la primera renovación o pago. Solo informativo: el corte lo
    // sigue decidiendo SubscriptionExpiresAt.
    public bool IsTrial { get; private set; }

    // Último ciclo pagado (ver BillingCycles). null = nunca renovó por este
    // backend (prueba, grandfathered o aprovisionado desde pagos).
    public string? BillingCycle { get; private set; }

    private Tenant()
    {
    }

    public static Tenant Create(
        string slug,
        string businessName,
        string plan,
        string? ownerEmail = null,
        DateTime? subscriptionExpiresAt = null,
        bool isTrial = false)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainException("Slug is required.");
        }

        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new DomainException("Business name is required.");
        }

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            BusinessName = businessName,
            Plan = plan,
            SchemaName = $"tenant_{Guid.NewGuid().ToString("N")[..8]}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            OwnerEmail = ownerEmail,
            SubscriptionExpiresAt = NormalizeToUtc(subscriptionExpiresAt),
            IsTrial = isTrial
        };
    }

    public void Suspend()
    {
        IsActive = false;
    }

    // Reactiva un tenant bloqueado por vencimiento (o lo renueva antes de
    // vencer) y limpia el contador de recordatorios: el próximo ciclo de 7
    // días debe poder volver a avisar desde cero.
    public void ExtendSubscription(DateTime newExpiresAt)
    {
        SubscriptionExpiresAt = NormalizeToUtc(newExpiresAt);
        IsActive = true;
        IsTrial = false;
        LastTrialReminderSentAt = null;
    }

    // Renovación por pago: los días se suman a lo que le quedaba si aún no
    // vencía (pagar antes de tiempo no hace perder días); si ya venció, o no
    // tenía vencimiento, se cuentan desde ahora.
    public DateTime ExtendSubscriptionByDays(int extensionDays, string? billingCycle, DateTime utcNow)
    {
        if (extensionDays <= 0)
        {
            throw new DomainException("Los días de extensión deben ser mayores a cero.");
        }

        var baseDate = SubscriptionExpiresAt is not null && SubscriptionExpiresAt.Value > utcNow
            ? SubscriptionExpiresAt.Value
            : utcNow;

        ExtendSubscription(baseDate.AddDays(extensionDays));

        if (billingCycle is not null)
        {
            BillingCycle = billingCycle;
        }

        return SubscriptionExpiresAt!.Value;
    }

    // El nombre comercial vive también en tenant_config (schema del tenant); se
    // replica acá para que el login muestre el nombre vigente.
    public void Rename(string businessName)
    {
        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new DomainException("Business name is required.");
        }

        BusinessName = businessName;
    }

    // El backend de pagos llama a esto al convertir una prueba gratis en un
    // plan pagado y en cada renovación. Pasar null deja al tenant sin
    // vencimiento. Una fecha pasada corta el acceso de inmediato (impago).
    public void UpdateSubscription(DateTime? subscriptionExpiresAt, string? plan = null)
    {
        if (plan is not null)
        {
            if (string.IsNullOrWhiteSpace(plan))
            {
                throw new DomainException("Plan is required.");
            }

            Plan = plan;
        }

        SubscriptionExpiresAt = NormalizeToUtc(subscriptionExpiresAt);
        IsTrial = false;
        LastTrialReminderSentAt = null;

        // El barrido de vencimientos suspende (IsActive = false) a los tenants
        // vencidos; una renovación tiene que devolverles el acceso.
        if (!IsSubscriptionExpired(DateTime.UtcNow))
        {
            IsActive = true;
        }
    }

    public void MarkTrialReminderSent()
    {
        LastTrialReminderSentAt = DateTime.UtcNow;
    }

    public bool IsSubscriptionExpired(DateTime utcNow)
    {
        return SubscriptionExpiresAt is not null && utcNow > SubscriptionExpiresAt.Value;
    }

    // El JSON puede llegar con offset o sin Kind; se guarda siempre en UTC para
    // que la comparación contra DateTime.UtcNow no dependa de la zona horaria.
    // Público para que los validadores comparen con el mismo criterio.
    public static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
