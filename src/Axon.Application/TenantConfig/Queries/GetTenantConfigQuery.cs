using MediatR;

namespace Axon.Application.TenantConfig.Queries;

public record GetTenantConfigQuery : IRequest<TenantConfigDto>;

public record TenantConfigDto(
    Guid Id,
    string BusinessName,
    string? Nit,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    string? LogoUrl,
    bool IsResponsableIva);
