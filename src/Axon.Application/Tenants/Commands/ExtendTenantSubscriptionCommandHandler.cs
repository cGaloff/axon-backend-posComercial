using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Tenants.Commands;

public class ExtendTenantSubscriptionCommandHandler : IRequestHandler<ExtendTenantSubscriptionCommand, MediatRUnit>
{
    private readonly IMasterDbContext _masterDbContext;

    public ExtendTenantSubscriptionCommandHandler(IMasterDbContext masterDbContext)
    {
        _masterDbContext = masterDbContext;
    }

    public async Task<MediatRUnit> Handle(ExtendTenantSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _masterDbContext.Tenants
            .SingleOrDefaultAsync(t => t.Slug == request.Slug, cancellationToken);

        if (tenant is null)
        {
            throw new DomainException("Tenant no encontrado");
        }

        tenant.ExtendSubscription(DateTime.UtcNow.AddDays(request.ExtensionDays));
        await _masterDbContext.SaveChangesAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
