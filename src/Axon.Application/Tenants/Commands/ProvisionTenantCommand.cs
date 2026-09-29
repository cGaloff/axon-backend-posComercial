using MediatR;

namespace Axon.Application.Tenants.Commands;

// Alta directa de una empresa desde el backend de pagos (protegida por
// X-Provisioning-Secret). A diferencia del auto-registro no pide verificar el
// email ni asigna prueba gratis: el tenant nace al instante y vence en
// SubscriptionEndsAt; null = sin vencimiento (compras normales, donde el
// corte lo marca el pago).
public record ProvisionTenantCommand(
    string BusinessName,
    string Slug,
    string OwnerEmail,
    string OwnerPassword,
    string Plan,
    DateTime? SubscriptionEndsAt = null) : IRequest<RegisterTenantResult>;
