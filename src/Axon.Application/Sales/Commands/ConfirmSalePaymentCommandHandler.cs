using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Sales.Commands;

public class ConfirmSalePaymentCommandHandler : IRequestHandler<ConfirmSalePaymentCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmSalePaymentCommandHandler(IApplicationDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<MediatRUnit> Handle(ConfirmSalePaymentCommand request, CancellationToken cancellationToken)
    {
        var sale = await _dbContext.Sales.SingleOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

        if (sale is null)
        {
            throw new DomainException("La venta no existe");
        }

        sale.Complete();

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
