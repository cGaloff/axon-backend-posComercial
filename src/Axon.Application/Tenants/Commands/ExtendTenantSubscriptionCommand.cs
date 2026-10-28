using MediatR;

namespace Axon.Application.Tenants.Commands;

// ExtensionDays es opcional si viene BillingCycle (se deriva: 30/90/180/365).
// BillingCycle, Amount y TransactionReference son opcionales para que soporte
// pueda seguir extendiendo a mano solo con slug + días.
public record ExtendTenantSubscriptionCommand(
    string Slug,
    int? ExtensionDays,
    string? BillingCycle = null,
    decimal? Amount = null,
    string? TransactionReference = null) : IRequest<ExtendTenantSubscriptionResult>;

// AlreadyProcessed = true cuando la referencia ya se había aplicado (reintento
// del webhook): no se suman días de nuevo y se devuelve la fecha vigente.
public record ExtendTenantSubscriptionResult(
    string Slug,
    DateTime SubscriptionExpiresAt,
    int DaysAdded,
    bool AlreadyProcessed);
