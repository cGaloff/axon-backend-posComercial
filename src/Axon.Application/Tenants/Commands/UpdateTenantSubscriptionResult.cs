namespace Axon.Application.Tenants.Commands;

public record UpdateTenantSubscriptionResult(
    Guid TenantId,
    string Slug,
    string BusinessName,
    string Plan,
    bool IsActive,
    DateTime? SubscriptionEndsAt);
