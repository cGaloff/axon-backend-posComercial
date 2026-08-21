using MediatR;

namespace Axon.Application.Tenants.Commands;

public record ConfirmTenantRegistrationCommand(Guid PendingRegistrationId, string Code) : IRequest<RegisterTenantResult>;
