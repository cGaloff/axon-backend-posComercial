using Axon.Application.Common.Behaviors;
using MediatR;

namespace Axon.Application.Users.Commands;

public record CreateUserCommand(
    string FullName,
    string Email,
    string Password,
    Guid RoleId) : IRequest<Guid>, IAuditableRequest
{
    public string AuditAction => "User.Create";
    public Guid? AuditEntityId => null;
}
