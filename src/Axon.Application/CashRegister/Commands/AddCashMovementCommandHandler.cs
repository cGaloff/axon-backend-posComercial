using Axon.Application.Interfaces;
using Axon.Domain.Entities.CashRegister;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;

namespace Axon.Application.CashRegister.Commands;

public class AddCashMovementCommandHandler : IRequestHandler<AddCashMovementCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICashSessionRepository _cashSessionRepository;

    public AddCashMovementCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICashSessionRepository cashSessionRepository)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _cashSessionRepository = cashSessionRepository;
    }

    public async Task<Guid> Handle(AddCashMovementCommand request, CancellationToken cancellationToken)
    {
        var session = await _cashSessionRepository.GetByIdAsync(request.SessionId);

        if (session is null)
        {
            throw new DomainException("La sesión no existe");
        }

        var movement = CashMovement.Create(
            session.Id,
            request.Type,
            request.Amount,
            request.Description,
            request.CreatedBy,
            request.ReferenceId);

        // session.AddCashMovement ya valida internamente que Status == Open.
        session.AddCashMovement(request.Amount, request.Type);

        _dbContext.CashMovements.Add(movement);
        _cashSessionRepository.Update(session);

        await _unitOfWork.CommitAsync(cancellationToken);

        return movement.Id;
    }
}
