namespace Axon.Infrastructure.MultiTenant;

public record TenantInfo(string Slug, string SchemaName, bool IsActive, DateTime? SubscriptionEndsAt)
{
    public bool IsSubscriptionExpired(DateTime utcNow)
    {
        return SubscriptionEndsAt is not null && utcNow > SubscriptionEndsAt.Value;
    }
}
