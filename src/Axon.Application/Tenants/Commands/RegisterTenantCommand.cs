using MediatR;

namespace Axon.Application.Tenants.Commands;

public record RegisterTenantCommand(
    string BusinessName,
    string Slug,
    string OwnerEmail,
    string OwnerPassword,
    string Plan) : IRequest<RegisterTenantResult>;
