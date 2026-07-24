using Axon.Application.Interfaces;
using Axon.Domain.Entities.Suppliers;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Suppliers.Commands;

public class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSupplierCommandHandler(IApplicationDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var documentInUse = await _dbContext.Suppliers.AnyAsync(
            s => s.DocumentType == request.DocumentType && s.DocumentNumber == request.DocumentNumber,
            cancellationToken);

        if (documentInUse)
        {
            throw new DomainException($"Ya existe un proveedor con el documento '{request.DocumentType} {request.DocumentNumber}'");
        }

        var supplier = Supplier.Create(
            request.Name,
            request.DocumentType,
            request.DocumentNumber,
            request.ContactName,
            request.Phone,
            request.Email,
            request.Address,
            request.City);

        _dbContext.Suppliers.Add(supplier);
        await _unitOfWork.CommitAsync(cancellationToken);

        return supplier.Id;
    }
}
