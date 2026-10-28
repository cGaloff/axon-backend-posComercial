namespace Axon.Application.Tenants.Queries;

// SubscriptionExpiresAt/DaysRemaining/NextRenewalDate vienen null para los
// tenants sin vencimiento (grandfathered o aprovisionados sin fecha de corte):
// el frontend no debe mostrar contador para ellos.
public record SubscriptionStatusResult(
    bool IsActive,
    string Status,
    string Plan,
    string PlanName,
    DateTime? SubscriptionExpiresAt,
    int? DaysRemaining,
    int TrialDaysTotal,
    bool IsTrial,
    string? CurrentBillingCycle,
    decimal AmountPaid,
    DateTime? LastPaymentDate,
    DateTime? NextRenewalDate);
