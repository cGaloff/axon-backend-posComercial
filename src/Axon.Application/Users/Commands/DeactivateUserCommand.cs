using Axon.Application.Common.Behaviors;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

public record DeactivateUserCommand(Guid Id) : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "User.Deactivate";
    public Guid? AuditEntityId => Id;
}
