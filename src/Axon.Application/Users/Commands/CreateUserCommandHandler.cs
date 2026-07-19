using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Users.Commands;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public CreateUserCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var emailInUse = await _dbContext.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (emailInUse)
        {
            throw new DomainException("Ya existe un usuario con ese email");
        }

        var roleExists = await _dbContext.Roles.AnyAsync(r => r.Id == request.RoleId, cancellationToken);

        if (!roleExists)
        {
            throw new DomainException("El rol especificado no existe");
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(request.FullName, request.Email, passwordHash, request.RoleId);

        _dbContext.Users.Add(user);
        await _unitOfWork.CommitAsync(cancellationToken);

        return user.Id;
    }
}
