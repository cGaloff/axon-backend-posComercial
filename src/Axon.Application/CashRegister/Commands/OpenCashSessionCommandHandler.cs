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
    private readonly ICurrentUserContext _currentUserContext;

    public OpenCashSessionCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICashSessionRepository cashSessionRepository,
        ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _cashSessionRepository = cashSessionRepository;
        _currentUserContext = currentUserContext;
    }

    public async Task<OpenCashSessionResult> Handle(OpenCashSessionCommand request, CancellationToken cancellationToken)
    {
        // Quien abre la sesión en el sistema (para auditoría de movimientos) no
        // tiene que ser el mismo cajero asignado al turno — ver comentario en
        // OpenCashSessionCommand.
        var performedBy = _currentUserContext.UserId;

        var cashRegister = await _dbContext.CashRegisters
            .SingleOrDefaultAsync(c => c.Id == request.CashRegisterId, cancellationToken);

        if (cashRegister is null || !cashRegister.IsActive)
        {
            throw new DomainException("La caja no existe o está inactiva");
        }

        var cashier = await _dbContext.Users
            .Include(u => u.Role)
            .ThenInclude(r => r!.Permissions)
            .SingleOrDefaultAsync(u => u.Id == request.CashierId, cancellationToken);

        if (cashier is null || !cashier.IsActive)
        {
            throw new DomainException("El cajero asignado no existe o está inactivo");
        }

        if (!cashier.Role!.Permissions.Any(p => p.Key == "cash_register:write"))
        {
            throw new DomainException("El usuario asignado no tiene un rol habilitado para operar una caja");
        }

        var activeSession = await _cashSessionRepository.GetActiveSessionAsync(request.CashRegisterId);

        if (activeSession is not null)
        {
            throw new DomainException("Ya existe una sesión abierta para esta caja");
        }

        var session = CashSession.Create(request.CashRegisterId, request.CashierId, request.InitialAmount);

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
                performedBy);

            _dbContext.CashMovements.Add(openingMovement);
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        return new OpenCashSessionResult(session.Id, cashRegister.Name, cashier.Id, cashier.FullName, session.InitialAmount, session.OpenedAt);
    }
}
