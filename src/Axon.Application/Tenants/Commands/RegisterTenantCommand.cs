using MediatR;

namespace Axon.Application.Tenants.Commands;

// Comando interno: la password ya llega hasheada porque, desde que existe
// verificación de email, quien la recibe en texto plano es
// RequestTenantRegistrationCommandHandler (paso 1), no este comando (paso 2,
// disparado por ConfirmTenantRegistrationCommandHandler tras validar el
// código). Ver PendingTenantRegistration.
public record RegisterTenantCommand(
    string BusinessName,
    string Slug,
    string OwnerEmail,
    string OwnerPasswordHash,
    string Plan) : IRequest<RegisterTenantResult>;
