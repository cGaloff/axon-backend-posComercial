using MediatR;

namespace Axon.Application.Users.Commands;

public record CreateUserCommand(
    string FullName,
    string Email,
    string Password,
    Guid RoleId) : IRequest<Guid>;
