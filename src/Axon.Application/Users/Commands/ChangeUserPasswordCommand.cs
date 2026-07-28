using Axon.Application.Common.Behaviors;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

public record ChangeUserPasswordCommand(Guid Id, string NewPassword) : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "User.ResetPassword";
    public Guid? AuditEntityId => Id;
}
