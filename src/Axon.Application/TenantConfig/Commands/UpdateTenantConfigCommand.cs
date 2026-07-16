using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.TenantConfig.Commands;

public record UpdateTenantConfigCommand(
    string BusinessName,
    string? Nit,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    string? LogoUrl,
    bool IsResponsableIva) : IRequest<MediatRUnit>;
