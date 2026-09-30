namespace Axon.Infrastructure.MultiTenant;

public record TenantInfo(string Slug, string SchemaName, bool IsActive, DateTime? SubscriptionExpiresAt)
{
    public bool IsSubscriptionExpired(DateTime utcNow)
    {
        return SubscriptionExpiresAt is not null && utcNow > SubscriptionExpiresAt.Value;
    }
}
