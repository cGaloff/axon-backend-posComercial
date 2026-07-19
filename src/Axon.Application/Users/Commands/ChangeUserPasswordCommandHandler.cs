using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

public class ChangeUserPasswordCommandHandler : IRequestHandler<ChangeUserPasswordCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public ChangeUserPasswordCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<MediatRUnit> Handle(ChangeUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
        {
            throw new DomainException("El usuario no existe");
        }

        var newHash = _passwordHasher.Hash(request.NewPassword);
        user.ChangePassword(newHash);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
