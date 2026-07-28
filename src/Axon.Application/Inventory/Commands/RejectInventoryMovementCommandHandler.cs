using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public class RejectInventoryMovementCommandHandler : IRequestHandler<RejectInventoryMovementCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;

    public RejectInventoryMovementCommandHandler(
        IApplicationDbContext dbContext, IUnitOfWork unitOfWork, ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
    }

    public async Task<MediatRUnit> Handle(RejectInventoryMovementCommand request, CancellationToken cancellationToken)
    {
        var movement = await _dbContext.InventoryMovements
            .SingleOrDefaultAsync(m => m.Id == request.MovementId, cancellationToken);

        if (movement is null)
        {
            throw new DomainException("El movimiento de inventario no existe");
        }

        // No toca el stock: la merma reportada nunca llegó a aplicarse.
        movement.Reject(_currentUserContext.UserId, request.Reason);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
