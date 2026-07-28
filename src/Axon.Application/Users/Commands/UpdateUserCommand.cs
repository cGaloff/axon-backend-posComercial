using Axon.Application.Common.Behaviors;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

public record UpdateUserCommand(
    Guid Id,
    string FullName,
    Guid RoleId) : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "User.Update";
    public Guid? AuditEntityId => Id;
}
