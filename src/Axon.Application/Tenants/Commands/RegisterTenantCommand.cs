using MediatR;

namespace Axon.Application.Tenants.Commands;

// Comando interno: la password ya llega hasheada. La reciben en texto plano
// RequestTenantRegistrationCommandHandler (auto-registro con verificación de
// email, que dispara este comando vía ConfirmTenantRegistrationCommandHandler)
// o ProvisionTenantCommandHandler (alta desde el backend de pagos).
//
// StartTrial = true asigna la prueba gratuita de Subscription:TrialDays e
// ignora SubscriptionExpiresAt. Con false se usa SubscriptionExpiresAt tal
// cual, y null deja al tenant sin vencimiento.
public record RegisterTenantCommand(
    string BusinessName,
    string Slug,
    string OwnerEmail,
    string OwnerPasswordHash,
    string Plan,
    bool StartTrial = true,
    DateTime? SubscriptionExpiresAt = null) : IRequest<RegisterTenantResult>;
