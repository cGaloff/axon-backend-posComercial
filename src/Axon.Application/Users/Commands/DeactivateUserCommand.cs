using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

public record DeactivateUserCommand(Guid Id) : IRequest<MediatRUnit>;
