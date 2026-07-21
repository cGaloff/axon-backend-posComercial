using MediatR;

namespace Axon.Application.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResult>;
