using Axon.Application.Common.Behaviors;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Auth.Commands;

// Cambio de la contraseña propia del usuario autenticado. Es la misma y única
// contraseña con la que entra al portal web y al POS.
public record ChangeOwnPasswordCommand(string CurrentPassword, string NewPassword) : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "User.ChangeOwnPassword";
    public Guid? AuditEntityId => null;
}
