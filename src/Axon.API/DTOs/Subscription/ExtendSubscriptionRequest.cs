namespace Axon.API.DTOs.Subscription;

public record ExtendSubscriptionRequest(
    string Slug,
    int? ExtensionDays = null,
    string? BillingCycle = null,
    decimal? Amount = null,
    string? TransactionReference = null);
