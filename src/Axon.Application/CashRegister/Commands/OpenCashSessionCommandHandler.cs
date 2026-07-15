using Axon.Application.Interfaces;
using Axon.Domain.Entities.CashRegister;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.CashRegister.Commands;

public class OpenCashSessionCommandHandler : IRequestHandler<OpenCashSessionCommand, OpenCashSessionResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICashSessionRepository _cashSessionRepository;

    public OpenCashSessionCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICashSessionRepository cashSessionRepository)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _cashSessionRepository = cashSessionRepository;
    }

    public async Task<OpenCashSessionResult> Handle(OpenCashSessionCommand request, CancellationToken cancellationToken)
    {
        var cashRegister = await _dbContext.CashRegisters
            .SingleOrDefaultAsync(c => c.Id == request.CashRegisterId, cancellationToken);

        if (cashRegister is null || !cashRegister.IsActive)
        {
            throw new DomainException("La caja no existe o está inactiva");
        }

        var activeSession = await _cashSessionRepository.GetActiveSessionAsync(request.CashRegisterId);

        if (activeSession is not null)
        {
            throw new DomainException("Ya existe una sesión abierta para esta caja");
        }

        var session = CashSession.Create(request.CashRegisterId, request.OpenedBy, request.InitialAmount);

        await _cashSessionRepository.AddAsync(session);

        // Solo se registra el movimiento de apertura si hay algo que registrar:
        // CashMovement.Create exige amount > 0, y una apertura en 0 es válida
        // (InitialAmount solo exige >= 0), así que en ese caso no hay movimiento que crear.
        if (request.InitialAmount > 0)
        {
            var openingMovement = CashMovement.Create(
                session.Id,
                CashMovementType.OpeningAmount,
                request.InitialAmount,
                "Apertura de caja",
                request.OpenedBy);

            _dbContext.CashMovements.Add(openingMovement);
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        return new OpenCashSessionResult(session.Id, cashRegister.Name, session.InitialAmount, session.OpenedAt);
    }
}
