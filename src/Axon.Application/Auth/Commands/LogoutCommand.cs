using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Auth.Commands;

public record LogoutCommand(string RefreshToken) : IRequest<MediatRUnit>;
