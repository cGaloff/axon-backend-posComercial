using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Sales.Commands;

public class VoidSaleCommandHandler : IRequestHandler<VoidSaleCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPasswordHasher _passwordHasher;

    public VoidSaleCommandHandler(
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

    public async Task<MediatRUnit> Handle(VoidSaleCommand request, CancellationToken cancellationToken)
    {
        var authorizedBy = await SupervisorAuthorization.ResolveAsync(
            _dbContext, _passwordHasher, _currentUserContext, "sales:void", request.SupervisorPin, cancellationToken);

        var sale = await _dbContext.Sales.SingleOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

        if (sale is null)
        {
            throw new DomainException("La venta no existe");
        }

        sale.Void(_currentUserContext.UserId, request.Reason, authorizedBy);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
