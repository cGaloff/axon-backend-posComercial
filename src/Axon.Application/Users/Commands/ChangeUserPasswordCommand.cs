using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

public record ChangeUserPasswordCommand(Guid Id, string NewPassword) : IRequest<MediatRUnit>;
