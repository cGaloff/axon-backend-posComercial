using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public class UpdateTaxTypeCommandHandler : IRequestHandler<UpdateTaxTypeCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTaxTypeCommandHandler(IApplicationDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<MediatRUnit> Handle(UpdateTaxTypeCommand request, CancellationToken cancellationToken)
    {
        var taxType = await _dbContext.TaxTypes.SingleOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (taxType is null)
        {
            throw new DomainException("Impuesto no encontrado");
        }

        taxType.Update(request.Name, request.Code);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
