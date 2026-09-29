using MediatR;

namespace Axon.Application.Tenants.Commands;

// La landing lo envía al convertir una prueba gratis en plan pagado y en cada
// renovación. SubscriptionEndsAt null deja al tenant sin vencimiento.
public record UpdateTenantSubscriptionCommand(
    string Slug,
    DateTime? SubscriptionEndsAt,
    string? Plan = null) : IRequest<UpdateTenantSubscriptionResult?>;
