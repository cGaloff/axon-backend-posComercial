using MediatR;

namespace Axon.Application.Tenants.Commands;

public record RequestTenantRegistrationCommand(
    string BusinessName,
    string Slug,
    string OwnerEmail,
    string OwnerPassword,
    string Plan) : IRequest<RequestTenantRegistrationResult>;
