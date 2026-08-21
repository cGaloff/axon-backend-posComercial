using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Auth.Commands;

public record ResetPasswordCommand(string Token, string NewPassword, string TenantSlug) : IRequest<MediatRUnit>;
