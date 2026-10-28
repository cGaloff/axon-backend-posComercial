using Axon.Application.Common.Behaviors;
using MediatR;

namespace Axon.Application.TenantConfig.Commands;

public record UpdateTenantProfileCommand(
    string BusinessName,
    string? LegalName,
    string? Nit,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string TaxRegime) : IRequest<UpdateTenantProfileResult>, IAuditableRequest
{
    public string AuditAction => "TenantProfile.Update";
    public Guid? AuditEntityId => null;
}

public record UpdateTenantProfileResult(
    string BusinessName,
    string? LegalName,
    string? Nit,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string TaxRegime);
