using Axon.Application.Interfaces;
using Axon.Domain.Entities.Taxes;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Commands;

public class CreateTaxTypeCommandHandler : IRequestHandler<CreateTaxTypeCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTaxTypeCommandHandler(IApplicationDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateTaxTypeCommand request, CancellationToken cancellationToken)
    {
        var nameInUse = await _dbContext.TaxTypes.AnyAsync(
            t => EF.Functions.ILike(t.Name, request.Name), cancellationToken);

        if (nameInUse)
        {
            throw new DomainException($"Ya existe un impuesto con el nombre '{request.Name}'");
        }

        var taxType = TaxType.Create(request.Name, request.Code);

        _dbContext.TaxTypes.Add(taxType);
        await _unitOfWork.CommitAsync(cancellationToken);

        return taxType.Id;
    }
}
