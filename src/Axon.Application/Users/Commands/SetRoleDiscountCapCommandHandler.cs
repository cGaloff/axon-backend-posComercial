using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

public class SetRoleDiscountCapCommandHandler : IRequestHandler<SetRoleDiscountCapCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public SetRoleDiscountCapCommandHandler(IApplicationDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<MediatRUnit> Handle(SetRoleDiscountCapCommand request, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.SingleOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role is null)
        {
            throw new DomainException("El rol no existe");
        }

        role.SetMaxDiscountPercentage(request.MaxDiscountPercentage);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
