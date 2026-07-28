using Axon.Application.Common.Behaviors;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

public record ReactivateUserCommand(Guid Id) : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "User.Reactivate";
    public Guid? AuditEntityId => Id;
}
