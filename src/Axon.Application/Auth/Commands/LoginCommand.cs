using MediatR;

namespace Axon.Application.Auth.Commands;

public record LoginCommand(string Email, string Password, string TenantSlug) : IRequest<LoginResult>;
