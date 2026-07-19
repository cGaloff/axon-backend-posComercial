using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

public record UpdateUserCommand(
    Guid Id,
    string FullName,
    Guid RoleId) : IRequest<MediatRUnit>;
