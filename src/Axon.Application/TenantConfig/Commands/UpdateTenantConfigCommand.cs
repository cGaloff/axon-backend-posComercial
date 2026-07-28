using Axon.Application.Common.Behaviors;
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
    bool IsResponsableIva) : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "TenantConfig.Update";
    public Guid? AuditEntityId => null;
}
