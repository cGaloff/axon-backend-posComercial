namespace Axon.API.DTOs.Tenants;

// SubscriptionEndsAt admite null explícito para dejar al tenant sin vencimiento.
public record UpdateTenantSubscriptionRequest(
    DateTime? SubscriptionEndsAt,
    string? Plan = null);
