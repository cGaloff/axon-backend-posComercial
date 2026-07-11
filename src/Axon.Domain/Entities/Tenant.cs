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

    private Tenant()
    {
    }

    public static Tenant Create(string slug, string businessName, string plan)
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
            CreatedAt = DateTime.UtcNow
        };
    }
}
