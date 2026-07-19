using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

public class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;

    public DeactivateUserCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
    }

    public async Task<MediatRUnit> Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == _currentUserContext.UserId)
        {
            throw new DomainException("No podés desactivar tu propio usuario");
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
        {
            throw new DomainException("El usuario no existe");
        }

        if (!user.IsActive)
        {
            throw new DomainException("El usuario ya está desactivado");
        }

        user.Deactivate();

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
