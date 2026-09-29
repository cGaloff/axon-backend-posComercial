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

    // Fecha (UTC) en la que caduca la suscripción. null = sin vencimiento, que
    // es como quedan los tenants creados antes de que existiera este campo.
    public DateTime? SubscriptionEndsAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private Tenant()
    {
    }

    public static Tenant Create(string slug, string businessName, string plan, DateTime? subscriptionEndsAt = null)
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
            SubscriptionEndsAt = NormalizeToUtc(subscriptionEndsAt),
            CreatedAt = DateTime.UtcNow
        };
    }

    // La landing llama a esto al convertir una prueba gratis en un plan pagado
    // y en cada renovación. Pasar null deja al tenant sin vencimiento.
    public void UpdateSubscription(DateTime? subscriptionEndsAt, string? plan = null)
    {
        if (plan is not null)
        {
            if (string.IsNullOrWhiteSpace(plan))
            {
                throw new DomainException("Plan is required.");
            }

            Plan = plan;
        }

        SubscriptionEndsAt = NormalizeToUtc(subscriptionEndsAt);
    }

    public bool IsSubscriptionExpired(DateTime utcNow)
    {
        return SubscriptionEndsAt is not null && utcNow > SubscriptionEndsAt.Value;
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
