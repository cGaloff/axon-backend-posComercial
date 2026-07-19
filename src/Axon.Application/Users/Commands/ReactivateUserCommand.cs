using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

public record ReactivateUserCommand(Guid Id) : IRequest<MediatRUnit>;
