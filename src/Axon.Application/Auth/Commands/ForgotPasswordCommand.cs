using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Auth.Commands;

public record ForgotPasswordCommand(string Email, string TenantSlug) : IRequest<MediatRUnit>;
