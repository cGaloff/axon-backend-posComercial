using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Tenants.Commands;

public record ExtendTenantSubscriptionCommand(string Slug, int ExtensionDays) : IRequest<MediatRUnit>;
