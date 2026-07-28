using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

public class SetPinCommandHandler : IRequestHandler<SetPinCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPasswordHasher _passwordHasher;

    public SetPinCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<MediatRUnit> Handle(SetPinCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == _currentUserContext.UserId, cancellationToken);

        if (user is null)
        {
            throw new DomainException("El usuario no existe");
        }

        var pinHash = _passwordHasher.Hash(request.Pin);
        user.SetPin(pinHash);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
