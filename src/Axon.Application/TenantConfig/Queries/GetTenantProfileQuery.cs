using MediatR;

namespace Axon.Application.TenantConfig.Queries;

public record GetTenantProfileQuery : IRequest<TenantProfileDto>;

// Mezcla dos orígenes: los datos comerciales salen de tenant_config (schema
// del tenant) y Id/Slug/CreatedAt de la base maestra.
public record TenantProfileDto(
    Guid Id,
    string BusinessName,
    string? LegalName,
    string? Nit,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string TaxRegime,
    string? Email,
    string Slug,
    string? OwnerName,
    DateTime CreatedAt);
