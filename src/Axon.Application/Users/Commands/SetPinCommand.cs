using Axon.Application.Common.Behaviors;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

// Auto-servicio: cada usuario configura SU PROPIO PIN (no un admin asignándoselo
// a otro) — usa ICurrentUserContext.UserId, no recibe un Id de usuario objetivo.
public record SetPinCommand(string Pin) : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "User.SetPin";
    public Guid? AuditEntityId => null;
}
