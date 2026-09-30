using Axon.Domain.Interfaces;
using MediatR;

namespace Axon.Application.Tenants.Commands;

public class ProvisionTenantCommandHandler : IRequestHandler<ProvisionTenantCommand, RegisterTenantResult>
{
    private readonly IMediator _mediator;
    private readonly IPasswordHasher _passwordHasher;

    public ProvisionTenantCommandHandler(IMediator mediator, IPasswordHasher passwordHasher)
    {
        _mediator = mediator;
        _passwordHasher = passwordHasher;
    }

    public Task<RegisterTenantResult> Handle(ProvisionTenantCommand request, CancellationToken cancellationToken)
    {
        return _mediator.Send(
            new RegisterTenantCommand(
                request.BusinessName,
                request.Slug,
                request.OwnerEmail,
                _passwordHasher.Hash(request.OwnerPassword),
                request.Plan,
                StartTrial: false,
                SubscriptionExpiresAt: request.SubscriptionEndsAt),
            cancellationToken);
    }
}
