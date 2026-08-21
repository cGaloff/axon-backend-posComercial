using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Tenants.Commands;

public class ConfirmTenantRegistrationCommandHandler : IRequestHandler<ConfirmTenantRegistrationCommand, RegisterTenantResult>
{
    private readonly IMasterDbContext _masterDbContext;
    private readonly IMediator _mediator;

    public ConfirmTenantRegistrationCommandHandler(IMasterDbContext masterDbContext, IMediator mediator)
    {
        _masterDbContext = masterDbContext;
        _mediator = mediator;
    }

    public async Task<RegisterTenantResult> Handle(ConfirmTenantRegistrationCommand request, CancellationToken cancellationToken)
    {
        var pending = await _masterDbContext.PendingTenantRegistrations
            .SingleOrDefaultAsync(p => p.Id == request.PendingRegistrationId, cancellationToken);

        if (pending is null || !pending.IsActive)
        {
            throw new DomainException("El código no es válido o expiró. Iniciá el registro nuevamente.");
        }

        var codeHash = PendingTenantRegistration.HashVerificationCode(request.Code);

        if (codeHash != pending.CodeHash)
        {
            pending.RegisterFailedAttempt();
            await _masterDbContext.SaveChangesAsync(cancellationToken);

            var remainingAttempts = PendingTenantRegistration.MaxCodeAttempts - pending.FailedAttempts;

            throw new DomainException(remainingAttempts > 0
                ? $"Código incorrecto. Te quedan {remainingAttempts} intentos."
                : "Superaste el máximo de intentos. Iniciá el registro nuevamente.");
        }

        // Si esto lanza (falla real de aprovisionamiento del schema, no del
        // código), la excepción se propaga SIN marcar la solicitud como
        // consumida: el código ya validado sigue sirviendo para reintentar.
        var result = await _mediator.Send(
            new RegisterTenantCommand(pending.BusinessName, pending.Slug, pending.OwnerEmail, pending.OwnerPasswordHash, pending.Plan),
            cancellationToken);

        pending.MarkConsumed();
        await _masterDbContext.SaveChangesAsync(cancellationToken);

        return result;
    }
}
