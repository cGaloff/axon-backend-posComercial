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
        if (!string.IsNullOrWhiteSpace(request.Nit))
        {
            var nitInUse = await _dbContext.Suppliers.AnyAsync(s => s.Nit == request.Nit, cancellationToken);

            if (nitInUse)
            {
                throw new DomainException("Ya existe un proveedor con ese NIT");
            }
        }

        var supplier = Supplier.Create(request.Name);

        supplier.Update(
            request.Name,
            request.Nit,
            request.ContactName,
            request.Phone,
            request.Email,
            request.Address,
            request.City,
            request.PaymentTermDays);

        _dbContext.Suppliers.Add(supplier);
        await _unitOfWork.CommitAsync(cancellationToken);

        return supplier.Id;
    }
}
