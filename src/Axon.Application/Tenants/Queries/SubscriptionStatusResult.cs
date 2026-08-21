namespace Axon.Application.Tenants.Queries;

public record SubscriptionStatusResult(bool IsActive, string Plan, DateTime? SubscriptionExpiresAt, int? DaysRemaining);
